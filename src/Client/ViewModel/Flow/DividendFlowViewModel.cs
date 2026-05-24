using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FMO.Disclosure;
using FMO.Models;
using FMO.Shared;
using FMO.TPL;
using FMO.Utilities;
using LiteDB;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Windows;

namespace FMO;


[EntityModifiable(typeof(DividendFlow))]
public partial class DividendFlowViewModel : FlowViewModel
{
    public static DividendType[] Types = [DividendType.PerUnitDividend, DividendType.TargetNetValue, DividendType.SpecifiedAmount];
    public static DividendMethod[] Methods = [DividendMethod.Cash, DividendMethod.Reinvestment, DividendMethod.Manual];
    private readonly DividendFlow _flow;


    /// <summary>
    /// 方案
    /// ①按每单位红利分红：按照产品分红基准日的单位净值进行扣减，例如基准日单位净值为1.2513，可以选择每单位红利为 0.1，分红后单位净值为 1.1513。
    ///本模式下【可分单位红利上限】将按净值保留位数截位计算（例如: 公式计算出的可分单位红利上限=1.12345，单位净值保留位数=4 位，则【可分单位红利上限】=1.1234) ，
    ///以保证分红总金额不超过基金可供分配利润，如需将基金可供分配利润全部分配，可选择“按指定金额分红"或“按单位净值归目标净值分红”。
    ///②按指定金额分红：指定本次分红的总金额，系统会根据权益进行计算本次分红应该扣减的净值。
    ///③按单位净值归目标净值分配：系统自动计算本次分红金额，使分红结果尽可能为设定的目标净值。 
    /// </summary>
    //public ModifiableViewModel<DividendType?> Type { get; }

    public ModifiableViewModel<decimal?> Target { get; set; } = null!;


    //public ModifiableViewModel<DividendMethod?> Method { get; }



    [ObservableProperty]
    public partial ModifiableViewModel<DateOnly?> DividendReferenceDate { get; set; } = null!;

    [ObservableProperty]
    public partial ModifiableViewModel<DateOnly?> RecordDate { get; set; } = null!;

    [ObservableProperty]
    public partial ModifiableViewModel<DateOnly?> ExDividendDate { get; set; } = null!;

    [ObservableProperty]
    public partial ModifiableViewModel<DateOnly?> CashPaymentDate { get; set; } = null!;


    /// <summary>
    /// 分红公告
    /// </summary>  
    public DualFileViewModel Announcement { get; }
    // public SimpleFile SealedAnnouncement { get; }




    [SetsRequiredMembers]
    public DividendFlowViewModel(DividendFlow flow) : base(flow)
    {
        _flow = flow;

        FillBy(flow);



        Announcement = new(flow.Announcement) { Label = "分红公告", Filter = "文档|*.docx;*.doc;*.pdf", };
        Announcement.Normal.SpecificFileName = Announcement.SpecificFileName;
        Announcement.Another.SpecificFileName = Announcement.SpecificFileName;
        Announcement.FileChanged += f => SaveFileChanged(new { Announcement = f });

        //Announcement = new()
        //{
        //    Label = "分红公告",
        //    SaveFolder = FundHelper.GetFolder(FundId, "Announcement"),
        //    SetProperty = (x, y) => { if (x is DividendFlow f) f.Announcement = y; },
        //    GetProperty = x => x switch { DividendFlow f => f.Announcement, _ => null },
        //    Filter = "文档|*.docx;*.doc;*.pdf"
        //};
        //Announcement.Init(flow);



        //SealedAnnouncement = new()
        //{
        //    Label = "分红公告",
        //    SaveFolder = FundHelper.GetFolder(FundId, "Announcement"),
        //    SetProperty = (x, y) => { if (x is DividendFlow f) f.SealedAnnouncement = y; },
        //    GetProperty = x => x switch { DividendFlow f => f.SealedAnnouncement, _ => null },
        //    Filter = "文档|*.pdf"
        //};
        //SealedAnnouncement.Init(flow);

        Initialized = true;
    }


    public partial void OnEntityChanged()
    {
        using var db = DbHelper.Base();
        db.GetCollection<FundFlow>().Update(_flow);
    }

    protected override void CanLockOverride(ref bool ok, List<string> err)
    {
        if (Target.OldValue is not > 0)
        {
            ok = false;
            err.Add("未设置分红目标");
        }
        if (DividendReferenceDate.OldValue == default)
        {
            ok = false;
            err.Add("未设置分红基准日");
        }
        if (RecordDate.OldValue == default)
        {
            ok = false;
            err.Add("未设置权益登记日");
        }
        if (ExDividendDate.OldValue == default)
        {
            ok = false;
            err.Add("未设置除息日");
        }
        if (CashPaymentDate.OldValue == default)
        {
            ok = false;
            err.Add("现金红利发放日");
        }

    }


    [RelayCommand]
    public void GenerateFile(DualFileViewModel v)
    {
        if (v == Announcement)
        {
            try
            {
                using var temp = new TempFile();
                using var db = DbHelper.Base();
                var manager = db.GetCollection<Manager>().FindOne(x => x.IsMaster);
                var fund = db.GetCollection<Fund>().FindById(FundId);

                var anndate = Date is null ? DateTime.Today : (DateTime.Today < Date ? DateTime.Today : Date);

                var today = DateOnly.FromDateTime(DateTime.Today);

                var data = new
                {
                    ManagerName = manager.Name,
                    FundName = fund.Name,
                    FundCode = fund.Code,
                    FundTrustee = fund.Trustee,
                    ModeTarget = Type.OldValue switch { DividendType.PerUnitDividend => "每单位红利", DividendType.TargetNetValue => "分红后净值", DividendType.SpecifiedAmount => "分红总金额", _ => "" },
                    TargetValue = Target.OldValue,
                    DividendReferenceDate = $"{DividendReferenceDate.OldValue ?? today:yyyy年MM月dd日}",
                    RecordDate = $"{RecordDate.OldValue ?? today:yyyy年MM月dd日}",
                    ExDividendDate = $"{ExDividendDate.OldValue ?? today:yyyy年MM月dd日}",
                    CashPaymentDate = $"{CashPaymentDate.OldValue ?? today:yyyy年MM月dd日}",
                    AnnouncementDate = $"{anndate:yyyy年MM月dd日}",
                    Mail = manager.Email
                };

                if (Tpl.GenerateByPredefined(temp.FilePath, "产品分红公告.docx", data))
                    v.Normal.Meta = FileMeta.Create(temp.FilePath, @$"{fund.Name}_产品分红公告.docx");
                else
                    HandyControl.Controls.Growl.Error($"生成{v.Label}失败，请查看Log，检查模板是否存在");
            }
            catch { }
        }

    }


    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.PropertyName == nameof(Date) && Date is not null && Initialized)
        {
            var date = DateOnly.FromDateTime(Date.Value);

            if (DividendReferenceDate.NewValue is null)
                DividendReferenceDate.NewValue = date;
            if (RecordDate.NewValue is null)
                RecordDate.NewValue = date;
            if (ExDividendDate.NewValue is null)
                ExDividendDate.NewValue = date;
            if (CashPaymentDate.NewValue is null)
                CashPaymentDate.NewValue = date.AddDays(1);
        }

        if (e.PropertyName == nameof(IsReadOnly) && IsReadOnly && Date is not null && Date.Value != default(DateTime) && Target.OldValue is not null) //锁定了
        {
            // 检查是否存在公告
            using var db = DbHelper.Base();
            var old = db.GetCollection<IDisclosureNotice>().Query().Where(Query.EQ(nameof(IFundDisclosureNotice.FundId), FundId)).Where(x => x.Type == DisclosureType.FundSetup).FirstOrDefault();

            if (old is null && HandyControl.Controls.MessageBox.Show("是否创建分红公告，并发布", "提示", MessageBoxButton.YesNo) == MessageBoxResult.No)
                return;
            else if (old is not null && HandyControl.Controls.MessageBox.Show("是否更新分红公告，并发布", "提示", MessageBoxButton.YesNo) == MessageBoxResult.No)
                return;

            var fund = db.GetCollection<Fund>().FindById(FundId);
            var anndate = DateTime.Today < Date ? DateTime.Today : Date;

            var today = DateOnly.FromDateTime(DateTime.Today);

            FundDivdendNotice notice = new()
            {
                FundCode = fund.Code!,
                FundName = fund.Name,
                FundId = fund.Id,
                DividendDay = DateOnly.FromDateTime(Date.Value),
                PublishDate = DateOnly.FromDateTime(anndate.Value),
                PublishTime = TimeOnly.FromDateTime(DateTime.Now),
                DividendType = Type.NewValue,
                Target = Target.OldValue.Value,
                DividendReferenceDate = DividendReferenceDate.OldValue ?? default,
                RecordDate = RecordDate.OldValue ?? default,
                ExDividendDate = ExDividendDate.OldValue ?? default,
                CashPaymentDate = CashPaymentDate.OldValue ?? default,
            };

            var manager = db.GetCollection<Manager>().FindOne(x => x.IsMaster);

            if (Announcement.Another.Exists)
                notice.Pdf = new SimpleFile(Announcement.Another.Meta);

            if (Announcement.Normal.Exists)
                notice.Word = new SimpleFile(Announcement.Normal.Meta);
            else
            {
                notice.MakeWord("产品分红公告.docx", new
                {
                    ManagerName = manager.Name,
                    FundName = fund.Name,
                    FundCode = fund.Code,
                    FundTrustee = fund.Trustee,
                    ModeTarget = Type.OldValue switch { DividendType.PerUnitDividend => "每单位红利", DividendType.TargetNetValue => "分红后净值", DividendType.SpecifiedAmount => "分红总金额", _ => "" },
                    TargetValue = Target.OldValue,
                    DividendReferenceDate = $"{DividendReferenceDate.OldValue ?? today:yyyy年MM月dd日}",
                    RecordDate = $"{RecordDate.OldValue ?? today:yyyy年MM月dd日}",
                    ExDividendDate = $"{ExDividendDate.OldValue ?? today:yyyy年MM月dd日}",
                    CashPaymentDate = $"{CashPaymentDate.OldValue ?? today:yyyy年MM月dd日}",
                    AnnouncementDate = $"{anndate:yyyy年MM月dd日}",
                    Mail = manager.Email
                });
            }


            DisclosureService.RegisterNotice(notice);
        }

    }

}