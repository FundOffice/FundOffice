using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FMO.Models;
using FMO.Shared;
using FMO.TPL;
using FMO.Utilities;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace FMO;

public partial class DividendFlowViewModel : FlowViewModel, IChangeableEntityViewModel//, IFileSetter
{
    public static DividendType[] Types = [DividendType.PerUnitDividend, DividendType.TargetNetValue, DividendType.SpecifiedAmount];
    public static DividendMethod[] Methods = [DividendMethod.Cash, DividendMethod.Reinvestment, DividendMethod.Manual];


    /// <summary>
    /// 方案
    /// ①按每单位红利分红：按照产品分红基准日的单位净值进行扣减，例如基准日单位净值为1.2513，可以选择每单位红利为 0.1，分红后单位净值为 1.1513。
    ///本模式下【可分单位红利上限】将按净值保留位数截位计算（例如: 公式计算出的可分单位红利上限=1.12345，单位净值保留位数=4 位，则【可分单位红利上限】=1.1234) ，
    ///以保证分红总金额不超过基金可供分配利润，如需将基金可供分配利润全部分配，可选择“按指定金额分红"或“按单位净值归目标净值分红”。
    ///②按指定金额分红：指定本次分红的总金额，系统会根据权益进行计算本次分红应该扣减的净值。
    ///③按单位净值归目标净值分配：系统自动计算本次分红金额，使分红结果尽可能为设定的目标净值。 
    /// </summary>
    public ChangeableViewModel<DividendFlow, DividendType> Type { get; }

    public ChangeableViewModel<DividendFlow, decimal?> Target { get; set; }


    public ChangeableViewModel<DividendFlow, DividendMethod> Method { get; }




    public ChangeableViewModel<DividendFlow, DateTime?> DividendReferenceDate { get; }
    public ChangeableViewModel<DividendFlow, DateTime?> RecordDate { get; }
    public ChangeableViewModel<DividendFlow, DateTime?> ExDividendDate { get; }
    public ChangeableViewModel<DividendFlow, DateTime?> CashPaymentDate { get; }


    /// <summary>
    /// 分红公告
    /// </summary>  
    public DualFileViewModel Announcement { get; }
    // public SimpleFile SealedAnnouncement { get; }




    [SetsRequiredMembers]
    public DividendFlowViewModel(DividendFlow flow) : base(flow)
    {
        Type = new()
        {
            InitFunc = x => x.Type,
            UpdateFunc = (x, y) => x.Type = y,
            ClearFunc = x => x.Type = DividendType.PerUnitDividend,
        };
        Type.Init(flow);

        Target = new()
        {
            Label = "目标值",
            InitFunc = x => x.Target == 0 ? null : x.Target,
            UpdateFunc = (x, y) => x.Target = y ?? 0,
            ClearFunc = x => x.Type = 0,
        };
        Target.Init(flow);

        Method = new()
        {
            Label = "分红方式",
            InitFunc = x => x.Method,
            UpdateFunc = (x, y) => x.Method = y,
            ClearFunc = x => x.Method = DividendMethod.Cash,
        };
        Method.Init(flow);

        DividendReferenceDate = new()
        {
            Label = "分红基准日",
            InitFunc = x => x.DividendReferenceDate == default ? null : new DateTime(x.DividendReferenceDate, default),
            UpdateFunc = (x, y) => x.DividendReferenceDate = y is null ? default : DateOnly.FromDateTime(y.Value),
            ClearFunc = x => x.DividendReferenceDate = default,
            DisplayFunc = x => x?.ToString("yyyy-MM-dd")
        };
        DividendReferenceDate.Init(flow);


        RecordDate = new()
        {
            Label = "权益登记日",
            InitFunc = x => x.RecordDate == default ? null : new DateTime(x.RecordDate, default),
            UpdateFunc = (x, y) => x.RecordDate = y is null ? default : DateOnly.FromDateTime(y.Value),
            ClearFunc = x => x.RecordDate = default,
            DisplayFunc = x => x?.ToString("yyyy-MM-dd")
        };
        RecordDate.Init(flow);


        ExDividendDate = new()
        {
            Label = "除息日",
            InitFunc = x => x.ExDividendDate == default ? null : new DateTime(x.ExDividendDate, default),
            UpdateFunc = (x, y) => x.ExDividendDate = y is null ? default : DateOnly.FromDateTime(y.Value),
            ClearFunc = x => x.ExDividendDate = default,
            DisplayFunc = x => x?.ToString("yyyy-MM-dd")
        };
        ExDividendDate.Init(flow);


        CashPaymentDate = new()
        {
            Label = "现金红利发放日",
            InitFunc = x => x.CashPaymentDate == default ? null : new DateTime(x.CashPaymentDate, default),
            UpdateFunc = (x, y) => x.CashPaymentDate = y is null ? default : DateOnly.FromDateTime(y.Value),
            ClearFunc = x => x.CashPaymentDate = default,
            DisplayFunc = x => x?.ToString("yyyy-MM-dd")
        };
        CashPaymentDate.Init(flow);


        Announcement = new() { Label = "分红公告", Filter = "文档|*.docx;*.doc;*.pdf" };
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


    [RelayCommand]
    public void Delete(IPropertyModifier unit)
    {
        if (unit is IEntityModifier<DividendFlow> entity)
        {
            using var db = DbHelper.Base();
            var v = db.GetCollection<FundFlow>().FindById(FlowId) as DividendFlow;

            if (v is not null)
            {
                entity.RemoveValue(v);
                entity.Init(v);
                db.GetCollection<FundFlow>().Update(v);

                WeakReferenceMessenger.Default.Send(v);
            }
        }
    }


    [RelayCommand]
    public void Reset(IPropertyModifier unit)
    {
        unit.Reset();
    }



    [RelayCommand]
    public void Modify(IPropertyModifier unit)
    {
        if (unit is IEntityModifier<DividendFlow> property)
        {
            using var db = DbHelper.Base();
            var v = db.GetCollection<FundFlow>().FindById(FlowId) as DividendFlow;

            property.UpdateEntity(v!);
            db.GetCollection<FundFlow>().Update(v!);
        }
        unit.Apply();
    }


    [RelayCommand]
    public void Save()
    {
        var ps = GetType().GetProperties();
        foreach (var p in ps)
        {
            if (p.PropertyType.IsAssignableTo(typeof(IPropertyModifier)) && p.GetValue(this) is IPropertyModifier v && v.IsValueChanged)
                Modify(v);
        }
        IsReadOnly = true;
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

                var data = new
                {
                    ManagerName = manager.Name,
                    FundName = fund.Name,
                    FundCode = fund.Code,
                    FundTrustee = fund.Trustee,
                    ModeTarget = Type.OldValue switch { DividendType.PerUnitDividend => "每单位红利", DividendType.TargetNetValue => "分红后净值", DividendType.SpecifiedAmount => "分红总金额", _ => "" },
                    TargetValue = Target.OldValue,
                    DividendReferenceDate = $"{DividendReferenceDate.OldValue ?? DateTime.Today:yyyy年MM月dd日}",
                    RecordDate = $"{RecordDate.OldValue ?? DateTime.Today:yyyy年MM月dd日}",
                    ExDividendDate = $"{ExDividendDate.OldValue ?? DateTime.Today:yyyy年MM月dd日}",
                    CashPaymentDate = $"{CashPaymentDate.OldValue ?? DateTime.Today:yyyy年MM月dd日}",
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
            if (DividendReferenceDate.NewValue is null)
                DividendReferenceDate.NewValue = Date;
            if (RecordDate.NewValue is null)
                RecordDate.NewValue = Date;
            if (ExDividendDate.NewValue is null)
                ExDividendDate.NewValue = Date;
            if (CashPaymentDate.NewValue is null)
                CashPaymentDate.NewValue = Date.Value.AddDays(1);
        }
    }

}