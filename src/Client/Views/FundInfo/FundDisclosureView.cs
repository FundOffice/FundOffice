using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FMO.AMAC.Direct;
using FMO.Logging;
using FMO.Models;
using FMO.Shared;
using FMO.TPL;
using FMO.Utilities;
using HandyControl.Controls;
using LiteDB;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Controls;
using System.Windows.Data;

namespace FMO;

/// <summary>
/// FundAnnouncementView.xaml 的交互逻辑
/// </summary>
public partial class FundDisclosureView : UserControl
{
    public FundDisclosureView()
    {
        InitializeComponent();
    }
}


public partial class FundDisclosureViewModel : ObservableObject, IRecipient<FundPeriodicReport>
{
    public FundDisclosureViewModel(int fid)
    {
        FundId = fid;

        // 获取公告列表
        using var db = DbHelper.Base();
        var data = db.GetCollection<FundAnnouncement>().Find(x => x.FundId == fid).ToArray();

        Announcements = [.. data.Select(x => new AnnouncementViewModel(x))];

        PeriodicDisclosure = [.. db.GetCollection<FundPeriodicReport>().Find(x => x.FundId == fid).Select(x => new FundPeriodicReportViewModel(x))];

        var qu = db.GetCollection<FundQuarterlyUpdate>().Find(x => x.FundId == fid).ToArray();
        var dic = db.GetCollection<AmacProcessResult>().Query().Where(Query.In("_id", qu.Select(x => new BsonValue(x.Id)))).ToArray().ToDictionary(x => x.Id, x => x);
        QuarterlyDisclosure = [.. qu.Select(x => new FundQuarterlyUpdateViewModel(x, dic.TryGetValue(x.Id, out var v)? v: null))];


        //if (PeriodicDisclosure.Count == 0) PeriodicDisclosure = [new FundPeriodicReport { FundId = FundId, Type = PeriodicReportType.MonthlyReport }, new FundQuarterlyUpdate { FundId = FundId }];


        Monthly.Source = PeriodicDisclosure;
        Monthly.Filter += (s, e) => e.Accepted = e.Item switch { FundPeriodicReportViewModel r => r.Type == FundReportType.MonthlyReport, _ => false };

        Quarterly.Source = PeriodicDisclosure;
        Quarterly.Filter += (s, e) => e.Accepted = e.Item switch { FundPeriodicReportViewModel r => r.Type == FundReportType.QuarterlyReport, _ => false };

        SemiAnnually.Source = PeriodicDisclosure;
        SemiAnnually.Filter += (s, e) => e.Accepted = e.Item switch { FundPeriodicReportViewModel r => r.Type == FundReportType.SemiAnnualReport, _ => false };

        Annually.Source = PeriodicDisclosure;
        Annually.Filter += (s, e) => e.Accepted = e.Item switch { FundPeriodicReportViewModel r => r.Type == FundReportType.AnnualReport, _ => false };

        QuarterlyUpdate.Source = QuarterlyDisclosure;
    }

    public int FundId { get; }


    public ObservableCollection<AnnouncementViewModel> Announcements { get; init; }

    public CollectionViewSource Monthly { get; } = new();
    public CollectionViewSource Quarterly { get; } = new();
    public CollectionViewSource SemiAnnually { get; } = new();
    public CollectionViewSource Annually { get; } = new();
    public CollectionViewSource QuarterlyUpdate { get; } = new();


    public ObservableCollection<FundPeriodicReportViewModel> PeriodicDisclosure { get; }

    public ObservableCollection<FundQuarterlyUpdateViewModel> QuarterlyDisclosure { get; }

    [RelayCommand]
    public void AddAnnouncement()
    {
        using var db = DbHelper.Base();
        var obj = new FundAnnouncement { FundId = FundId };
        db.GetCollection<FundAnnouncement>().Insert(obj);

        Announcements?.Add(new(obj));
    }

    public void Receive(FundPeriodicReport message)
    {
    }
}





public partial class FundPeriodicReportViewModel : ObservableObject
{
    private readonly FundPeriodicReport report;

    public FundPeriodicReportViewModel(FundPeriodicReport report)
    {
        Id = report.Id;
        Code = report.FundCode;
        Type = report.Type;
        PeriodEnd = report.PeriodEnd;
        Word = new(report.Word);
        Excel = new(report.Excel);
        Pdf = new(report.Pdf);
        Xbrl = new(report.Xbrl);
        Sealed = new(report.Sealed);
        this.report = report;


        Word.FileChanged += f => UpdateFile(new { Word = f });
        Excel.FileChanged += f => UpdateFile(new { Excel = f });
        Pdf.FileChanged += f => UpdateFile(new { Pdf = f });
        Xbrl.FileChanged += f => UpdateFile(new { Xbrl = f });
        Sealed.FileChanged += f => UpdateFile(new { Sealed = f });
    }

    private void UpdateFile<T>(T v)
    {
        if (Id == 0) return;
        using var db = DbHelper.Base();
        report.UpdateFrom(v!);
        db.GetCollection<FundPeriodicReport>().UpdateMany(BsonMapper.Global.ToDocument(v).ToString(), $"_id={Id}");
    }



    public int Id { get; }
    public string? Code { get; }
    public FundReportType Type { get; }

    public string Title => Type switch
    {
        FundReportType.QuarterlyReport => $"{PeriodEnd:yy} {PeriodEnd.Month switch { < 4 => "Q1", < 7 => "Q2", < 10 => "Q3", _ => "Q4" }}",
        FundReportType.SemiAnnualReport => $"{PeriodEnd:yy} {PeriodEnd.Month switch { < 7 => "上半年", _ => "下半年" }}",
        FundReportType.AnnualReport => $"{PeriodEnd:yy}",
        _ => $"{PeriodEnd:yy/MM}",
    };


    public string? FundName { get; set; }

    public DateOnly PeriodEnd { get; }

    public SimpleFileViewModel Word { get; }

    public SimpleFileViewModel Excel { get; }

    public SimpleFileViewModel Xbrl { get; }

    public SimpleFileViewModel Pdf { get; }


    public SimpleFileViewModel Sealed { get; }


    [RelayCommand]
    public async Task Upload()
    {
        // 获取 账号
        using var db = DbHelper.Base();
        var acc = db.GetCollection<AmacReportAccount>().FindOne(x => x.Id == "pof");

        if (acc is null || string.IsNullOrWhiteSpace(acc.Name) || string.IsNullOrWhiteSpace(acc.Password) || string.IsNullOrWhiteSpace(acc.Key))
        {
            HandyControl.Controls.Growl.Info("请先在[平台]中设置信批账号");
            return;
        }

        var manager = db.GetCollection<Manager>().Query().First();

        var result = await DirectReporter.UploadReport(report, acc);

        if (result.UploadCode != 0)
        {
            HandyControl.Controls.Growl.Info($"上传文件失败:{result.UploadError}");
            return;
        }

        HandyControl.Controls.Growl.Info($"上传报告成功，请等待校验结果");
        await Task.Delay(20 * 1000);

        await DirectReporter.QueryResult(result, acc);

        if (result.ResultInfo?.Count > 0)
            HandyControl.Controls.Growl.Info($"{result.ResultInfo[0].Message}");
        else
            HandyControl.Controls.Growl.Info($"校验异常");

        //if (result.ValidateCode == 0)
        //{
        //    await Task.Delay(2000);
        //    await DirectReporter.Submit(result, manager.Name, acc);
        //    HandyControl.Controls.Growl.Info($"报告提交:{result.SubmitError}");
        //}
    }
}

public partial class FundQuarterlyUpdateViewModel : ObservableObject
{
    private readonly FundQuarterlyUpdate report;

    public FundQuarterlyUpdateViewModel(FundQuarterlyUpdate report, AmacProcessResult result)
    {
        Id = report.Id;
        FundId = report.FundId;
        Type = report.Type;
        PeriodEnd = report.PeriodEnd;
        Investor = new(report.Investor);
        Operation = new(report.Operation);
        this.report = report;

        OperationResult = new(result);

        Investor.FileChanged += f =>
        {
            using var db = DbHelper.Base();
            db.GetCollection<FundQuarterlyUpdate>().UpdateMany(BsonMapper.Global.ToDocument(new { Investor = f }).ToString(), $"_id={Id}");
            //var i = db.GetCollection<FundQuarterlyUpdate>().FindById(Id);
            //i.Investor = f;
            //db.GetCollection<FundQuarterlyUpdate>().Update(i);
        };

        Operation.FileChanged += f =>
        {
            using var db = DbHelper.Base();
            db.GetCollection<FundQuarterlyUpdate>().UpdateMany(BsonMapper.Global.ToDocument(new { Operation = f }).ToString(), $"_id={Id}");
            //var i = db.GetCollection<FundQuarterlyUpdate>().FindById(Id);
            //i.Operation = f;
            //db.GetCollection<FundQuarterlyUpdate>().Update(i);
        };

    }

    public int Id { get; }
    public int FundId { get; }

    public FundReportType Type { get; }
    public string Title => $"{PeriodEnd:yy} {PeriodEnd.Month switch { < 4 => "Q1", < 7 => "Q2", < 10 => "Q3", _ => "Q4" }}";

    public string? FundName { get; set; }

    public DateOnly PeriodEnd { get; }

    public SimpleFileViewModel Investor { get; }

    public SimpleFileViewModel Operation { get; }

    public AmacDirectResultViewModel OperationResult { get; }

    [RelayCommand]
    public async Task UploadOperation()
    {
        // 获取 账号
        using var db = DbHelper.Base();
        var acc = db.GetCollection<AmacReportAccount>().FindOne(x => x.Id == "pmg");

        if (acc is null || string.IsNullOrWhiteSpace(acc.Name) || string.IsNullOrWhiteSpace(acc.Password) || string.IsNullOrWhiteSpace(acc.Key))
        {
            HandyControl.Controls.Growl.Info("请先在[平台]中设置信批账号");
            return;
        }

        var manager = db.GetCollection<Manager>().Query().First();

        // 检查是否有上传记录
        var result = db.GetCollection<AmacProcessResult>().FindById(Id);
        if (result is null)
        {
            result = await DirectReporter.UploadReport(report, acc);
            if (result.UploadCode != 0)
            {
                HandyControl.Controls.Growl.Info($"上传文件失败:{result.UploadError}");
                return;
            }

            HandyControl.Controls.Growl.Info($"上传报告成功，请等待校验结果");
            //await Task.Delay(20 * 1000);
        }
        else HandyControl.Controls.Growl.Info("存在上传记录，继续查询结果");

        OperationResult.Status = AmacDirectResultViewModel.State.Upload;
        OperationResult.IsSuccess = result.UploadCode == 0;

        await DirectReporter.QueryResult(result, acc);
        OperationResult.Status = AmacDirectResultViewModel.State.Verify;
        OperationResult.IsSuccess = result.ValidateCode == 0;

        // 重新上传
        if (result.ValidateCode == 99)
        {
            result = await DirectReporter.UploadReport(report, acc);
            if (result.UploadCode != 0)
            {
                HandyControl.Controls.Growl.Info($"上传文件失败:{result.UploadError}");
                return;
            }

            HandyControl.Controls.Growl.Info($"上传报告成功，请等待校验结果");
            //await Task.Delay(20 * 1000);

            OperationResult.Status = AmacDirectResultViewModel.State.Upload;
            OperationResult.IsSuccess = result.UploadCode == 0;

            await DirectReporter.QueryResult(result, acc);
            OperationResult.Status = AmacDirectResultViewModel.State.Verify;
            OperationResult.IsSuccess = result.ValidateCode == 0;
        }

        if (result.ValidateCode == 0 || result.ValidateCode == 10) // 已完成
        {
            result.SubmitCode = 0;
            OperationResult.Status = AmacDirectResultViewModel.State.Submit;
            OperationResult.IsSuccess = true;
            db.GetCollection<AmacProcessResult>().Update(result);
            return;
        }

        if (result.ResultInfo?.Count > 0)
            HandyControl.Controls.Growl.Info($"{result.ResultInfo[0].Message}");
        else
            HandyControl.Controls.Growl.Info($"校验异常");

        if (result.ValidateCode != 0)
        {
            Growl.Warning($"{FundName} 季度更新存在警告或错误，请手动检查后提交");
            return;
        }

        await DirectReporter.Submit(result, manager.Name, acc);

        if (result.SubmitError?.Contains("handle参数错误或已失效") ?? false)
        {
            db.GetCollection<AmacProcessResult>().Delete(Id);
            OperationResult.Status = AmacDirectResultViewModel.State.None;
        }

        HandyControl.Controls.Growl.Info($"报告提交, Code:{result.SubmitCode},{result.SubmitError}");
    }

    [RelayCommand]
    public async Task SubmitOperation()
    {
        using var db = DbHelper.Base();
        var acc = db.GetCollection<AmacReportAccount>().FindOne(x => x.Id == "pmg");

        if (acc is null || string.IsNullOrWhiteSpace(acc.Name) || string.IsNullOrWhiteSpace(acc.Password) || string.IsNullOrWhiteSpace(acc.Key))
        {
            HandyControl.Controls.Growl.Info("请先在[平台]中设置信批账号");
            return;
        }

        var result = db.GetCollection<AmacProcessResult>().FindById(Id);
        var manager = db.GetCollection<Manager>().Query().First();

        if (MessageBox.Show($"季度更新存在警告或错误", "是否强制提交", button: System.Windows.MessageBoxButton.YesNo) == System.Windows.MessageBoxResult.Yes)
        {
            //await Task.Delay(5000);
            await DirectReporter.Submit(result, manager.Name, acc);
            HandyControl.Controls.Growl.Info($"报告提交, Code:{result.SubmitCode},{result.SubmitError}");

            if (result.SubmitCode == 0)
            {
                OperationResult.Status = AmacDirectResultViewModel.State.Submit;
                OperationResult.IsSuccess = true;
            }
        }
    }


    [RelayCommand]
    public async Task GenerateInvestorSheet()
    {
        try
        {
            var path = @$"files\tpl\ambers_investor.xlsx";

            if (!File.Exists(path))
            {
                WeakReferenceMessenger.Default.Send(new ToastMessage(LogLevel.Warning, "投资人表模板不存在，无法生成"));
                return;
            }

            var old = Investor.Meta;
            using var db = DbHelper.Base();
            var ta = db.GetCollection<TransferRecord>().Find(x => x.FundId == FundId && x.ConfirmedDate < PeriodEnd).ToArray();

            // 排除已全部赎回的
            var groupd = ta.GroupBy(x => x.InvestorId).Select(x => (id: x.Key, share: x.Sum(y => y.ShareChange()), saler: x.First().Agency)).Where(x => x.share > 0).ToDictionary(x => x.id, x => x);
            var ids = groupd.Keys.Select(x => new BsonValue(x));
            var data = db.GetCollection<Investor>().Find(Query.In("_id", new BsonArray(ids))).ToList();
            var manager = db.GetCollection<Manager>().FindOne(x => x.IsMaster).Name;

            // 数据校验
            var nv = db.GetDailyCollection(FundId).Find(x => x.Date <= PeriodEnd).LastOrDefault();
            if (nv is null || nv.Share != groupd.Sum(x => x.Value.share))
            {
                HandyControl.Controls.Growl.Warning($"{FundName} 的基金份额异常，生成的投资者信息表可能不正确！！");
                return;
            }

            // 写入
            var outp = @$"temp\investor_{Id}.xlsx";

            var obj = new
            {
                i = data.Select(x => new
                {
                    Type = x.Type.ToAmacString(),
                    Name = x.Name,
                    IDType = x.Identity!.Type.ToAmacString(),
                    IDType2 = x.Identity?.Other,
                    ID = x.Identity?.Id,
                    Share = (groupd[x.Id].share / 10000).ToString(),
                    Saler = groupd[x.Id].saler?.Contains("直销") ?? true ? manager : groupd[x.Id].saler
                })
            };

            Tpl.Generate(outp, path, obj);

            // 保存
            var r = db.GetCollection<FundQuarterlyUpdate>().FindById(Id);
            r.Investor = new SimpleFile { File = FileMeta.Create(outp) };
            db.GetCollection<FundQuarterlyUpdate>().Update(r);
            Investor.Meta = r.Investor.File;
            File.Delete(outp);

            old?.Delete();
            //PackDiscloseSheets(data);
        }
        catch (Exception e)
        {
            LogEx.Error(e);
            HandyControl.Controls.Growl.Warning("生成投资者信息表出错");
        }
    }


    /// <summary>
    /// 打包风险揭示书
    /// </summary>
    private void PackDiscloseSheets(List<Investor> data)
    {
        using var db = DbHelper.Base();
        var orders = db.GetCollection<TransferOrder>().Find(x => x.FundId == FundId && x.Date < PeriodEnd).OrderByDescending(x => x.Date).ToArray();

        var ids = data.Select(x => x.Id).ToList();

        var d = orders.Where(x => x.RiskDiscloure?.File is not null).GroupBy(x => x.InvestorId).
            Where(x => ids.Contains(x.Key)).Select(x => x.First()).Select(x => (x.InvestorId, File: x.RiskDiscloure!.File!)).ToList();

        if (d.Count != data.Count)
            HandyControl.Controls.Growl.Warning("风险揭示书数量不全");

        ZipSplitter.CreateSplitZip(d.Select(x => x.File).ToArray(), "temp", $"{FundName}_风险揭示书", 20 * 1024 * 1024);

    }

}




public partial class AnnouncementViewModel : EditableControlViewModelBase<FundAnnouncement>
{
    public AnnouncementViewModel(FundAnnouncement obj)
    {
        Id = obj.Id;
        FundId = obj.FundId;

        Title = new()
        {
            InitFunc = x => x.Title,
            UpdateFunc = (x, y) => x.Title = y,
            ClearFunc = x => x.Title = null
        };
        Title.Init(obj);

        Date = new()
        {
            InitFunc = x => x.Date == default ? null : new DateTime(x.Date, default),
            UpdateFunc = (x, y) => x.Date = y is null ? default : DateOnly.FromDateTime(y.Value),
            ClearFunc = x => x.Date = default,
            DisplayFunc = x => x?.ToString("yyyy-MM-dd")
        };
        Date.Init(obj);

        File = new(obj.File);
        File.FileChanged += f =>
        {
            if (Id == 0) return; // 新建时不保存
            using var db = DbHelper.Base();
            db.GetCollection<FundAnnouncement>().UpdateMany(BsonMapper.Global.ToDocument(new { File = f }).ToString(), $"_id={Id}");
        };
    }

    public DualFileViewModel File { get; }


    public int FundId { get; }


    public ChangeableViewModel<FundAnnouncement, string?> Title { get; }

    public ChangeableViewModel<FundAnnouncement, DateTime?> Date { get; }


    protected override FundAnnouncement InitNewEntity() => new FundAnnouncement { FundId = FundId };
}

public partial class AmacDirectResultViewModel : ObservableObject
{
    public AmacDirectResultViewModel(AmacProcessResult? result)
    {
        if (result is null)
            return;

        if (result.SubmitCode == 0)
        {
            Status = State.Submit;
            IsSuccess = true;
        }
        else if (result.SubmitCode > 0)
        {
            Status = State.Submit;
            IsSuccess = false;
        }
        else if (result.ValidateCode == 10)
        {
            Status = State.Submit;
            IsSuccess = true;
        }
        else if (result.ValidateCode == 0)
        {
            Status = State.Verify;
            IsSuccess = true;
        }
        else if (result.ValidateCode > 0)
        {
            Status = State.Verify;
            IsSuccess = false;
        }
        else if (result.UploadCode == 0)
        {
            Status = State.Upload;
            IsSuccess = true;
        }
        else if (result.UploadCode > 0)
        {
            Status = State.Upload;
            IsSuccess = false;
        }
        else
        {
            Status = State.None;
            IsSuccess = false;
        }
    }

    public enum State
    {
        None = 0,
        Upload = 1,
        Verify = 2,
        Submit = 3,
    }

    [ObservableProperty]
    public partial State Status { get; set; }


    [ObservableProperty]
    public partial bool IsSuccess { get; set; }

    [ObservableProperty]
    public partial int Code { get; set; }



    [ObservableProperty]
    public partial IList<ValidationInfo>? Validations { get; set; }





}