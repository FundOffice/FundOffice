using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FMO.Disclosure;
using FMO.Models;
using FMO.Shared;
using FMO.TPL;
using FMO.Utilities;
using LiteDB;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Windows;

namespace FMO;


[ForceNull(nameof(DateRange.Begin))]
[ForceNull(nameof(DateRange.End))]
public partial class DateRangeViewModel : IViewModel<DateRange?, DateRangeViewModel>
{
   
     
}


public partial class SetupFlowViewModel : FlowViewModel
{



    public ModifiableViewModel<DateRange?, DateRangeViewModel> RaisingPeriod { get; set; }




    public ModifiableViewModel<decimal?> InitialAsset { get; }


    public string? Capital => InitialAsset.NewValue is null ? null : NumberHelper.NumberToChinese(InitialAsset.NewValue.Value);


    /// <summary>
    /// 实缴出资
    /// </summary>
    public SimpleFileViewModel PaidInCapitalProof { get; }

    /// <summary>
    /// 成立公告
    /// </summary>  
    public DualFileViewModel EstablishmentAnnouncement { get; }






    [SetsRequiredMembers]
    public SetupFlowViewModel(SetupFlow flow) : base(flow)
    {
        if (flow.RasingPeriod?.Begin == DateOnly.MinValue || flow.RasingPeriod?.End == DateOnly.MinValue)
            flow.RasingPeriod = null;

        RaisingPeriod = new ModifiableViewModel<DateRange?, DateRangeViewModel>
        {
            NewValue = new(flow.RasingPeriod),
            OldValue = flow.RasingPeriod,
            FallbackValue = null
        };
        RaisingPeriod.Changed += e => Update(x => x.RasingPeriod = e.NewValue);



        InitialAsset = new ModifiableViewModel<decimal?>
        {
            NewValue = flow.InitialAsset,
            OldValue = flow.InitialAsset,
            FallbackValue = null
        };
        InitialAsset.Changed += e =>
        {
            Update(x => x.InitialAsset = e.NewValue ?? 0);
            OnPropertyChanged(nameof(Capital));
        };


        //募集规模为0时，检查ta
        if (flow.InitialAsset == 0)
        {
            using var db = DbHelper.Base();
            var ta = db.GetCollection<TransferRecord>().Find(x => x.FundId == FundId && x.Type == TransferRecordType.Subscription).ToArray();
            if (ta.Length > 0)
                InitialAsset.NewValue = ta.Sum(x => x.ConfirmedNetAmount);
        }

        PaidInCapitalProof = new(flow.PaidInCapitalProof) { Filter = "文档|*.docx;*.doc;*.pdf" };
        PaidInCapitalProof.FileChanged += f => SaveFileChanged(new { PaidInCapitalProof = f });


        EstablishmentAnnouncement = new(flow.EstablishmentAnnouncement) { Filter = "文档|*.docx;*.doc;*.pdf" };
        EstablishmentAnnouncement.FileChanged += f => SaveFileChanged(new { EstablishmentAnnouncement = f });


        Initialized = true;
    }

    private void Update(Action<SetupFlow> upd)
    {
        using var db = DbHelper.Base();
        var flow = db.GetCollection<FundFlow>().FindById(FlowId);
        if (flow is SetupFlow f)
        {
            upd(f);
            db.GetCollection<FundFlow>().Update(f);
        }
    }

    protected override void CanLockOverride(ref bool ok, List<string> err)
    {
        if (RaisingPeriod.OldValue is null || !IsDateRangeValid(RaisingPeriod.OldValue))
        {
            ok = false;
            err.Add("未设置募集期或不合法，间隔不能超过180天");
        }
        if (InitialAsset.NewValue is null)
        {
            ok = false;
            err.Add("募集金额不能为空");
        }

    }

    private bool IsDateRangeValid(DateRange range)
    {
        if (range.Begin == default) return false;
        if (range.End == default) return false;
        if (range.End.DayNumber - range.Begin.DayNumber is > 180 or < 1) return false;

        return true;
    }






    [RelayCommand]
    public void GenerateFile(DualFileMetaViewModel v)
    {
        if (v == EstablishmentAnnouncement)
        {
            string path = Path.GetTempFileName();
            try
            {
                using var db = DbHelper.Base();
                var manager = db.GetCollection<Manager>().FindOne(x => x.IsMaster);
                var fund = db.GetCollection<Fund>().FindById(FundId);

                var data = new Dictionary<string, object?>
                    {
                        {"Manager", manager.Name },
                        {"Name", fund.Name },
                        {"Date", $"{Date?? DateTime.Today:yyyy年MM月dd日}" },
                        {"Amount", InitialAsset.OldValue },
                        {"Capital", Capital },
                        { "Share", InitialAsset.OldValue }
                    };

                if (Tpl.GenerateByPredefined(path, "产品成立公告.docx", data))
                    v.Normal.Meta = FileMeta.Create(path, @$"{fund.Name}_产品成立公告.docx");
                else HandyControl.Controls.Growl.Error($"生成【产品成立公告】失败，请查看Log，检查模板是否存在");
            }
            catch { }
            File.Delete(path);
        }

    }


    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.PropertyName == nameof(IsReadOnly) && IsReadOnly && Date is not null && Date.Value != default(DateTime) && InitialAsset.OldValue is not null && Capital?.Length > 2) //锁定了
        {
            // 检查是否存在公告
            using var db = DbHelper.Base();
            var old = db.GetCollection<IDisclosureNotice>().Query().Where(Query.EQ(nameof(IFundDisclosureNotice.FundId), FundId)).Where(x => x.Type == DisclosureType.FundSetup).FirstOrDefault();

            if (old is null && HandyControl.Controls.MessageBox.Show("是否创建成立公告，并发布", "提示", MessageBoxButton.YesNo) == MessageBoxResult.No)
                return;
            else if (old is not null && HandyControl.Controls.MessageBox.Show("是否更新成立公告，并发布", "提示", MessageBoxButton.YesNo) == MessageBoxResult.No)
                return;

            var fund = db.GetCollection<Fund>().FindById(FundId);
            FundSetupNotice notice = new FundSetupNotice
            {
                FundCode = fund.Code!,
                FundName = fund.Name,
                FundId = fund.Id,
                SetupDay = DateOnly.FromDateTime(Date.Value),
                PublishDate = DateOnly.FromDateTime(Date.Value),
                PublishTime = TimeOnly.FromDateTime(DateTime.Now),
            };

            var manager = db.GetCollection<Manager>().FindOne(x => x.IsMaster);
            notice.MakeWord("产品成立公告.docx", new
            {
                Manager = manager.Name,
                Name = fund.Name,
                Date = $"{Date ?? DateTime.Today:yyyy年MM月dd日}",
                Amount = InitialAsset.OldValue,
                Capital = Capital,
                Share = InitialAsset.OldValue
            });

            DisclosureService.RegisterNotice(notice);
        }
    }


    //[RelayCommand]
    //public void ChooseFile(SimpleFile<SetupFlow> file)
    //{
    //    var fd = new OpenFileDialog();
    //    fd.Filter = file.Filter;
    //    if (fd.ShowDialog() != true)
    //        return;

    //    SetFile(file, fd.FileName);
    //}


    //public void SetFile(ISimpleFile? file, string path)
    //{
    //    if (file is SimpleFile<SetupFlow> ff)
    //    {
    //        ff.File = new FileInfo(path);

    //        using var db = DbHelper.Base();
    //        var flow = db.GetCollection<FundFlow>().FindById(FlowId) as SetupFlow;
    //        if (flow is SetupFlow f)
    //        {
    //            ff.SetProperty(flow, ff.Build());
    //            db.GetCollection<FundFlow>().Update(flow);
    //        }
    //    }
    //}




    //[RelayCommand]
    //public void Clear(SimpleFile<SetupFlow> file)
    //{
    //    if (file is null) return;

    //    var r = HandyControl.Controls.MessageBox.Show("是否删除文件", "提示", MessageBoxButton.YesNoCancel);
    //    if (r == MessageBoxResult.Cancel) return;

    //    if (r == MessageBoxResult.Yes) file.File?.Delete();

    //    using var db = DbHelper.Base();
    //    var flow = db.GetCollection<FundFlow>().FindById(FlowId) as SetupFlow;
    //    file.SetProperty(flow!, null);
    //    db.GetCollection<FundFlow>().Update(flow!);
    //    file.File = null;
    //}
}