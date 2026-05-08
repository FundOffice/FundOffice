using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FMO.Disclosure;
using FMO.Models;
using FMO.Shared;
using FMO.Utilities;
using LiteDB;
using System.Collections.ObjectModel;

namespace FMO;

public partial class PeriodicReportViewModel : ObservableObject
{
    private readonly PeriodicalDisclosureNotice report;
    public PeriodicReportViewModel(PeriodicalDisclosureNotice report, IEnumerable<DisclosureWorkflow> workflows, IEnumerable<DisclosureInstance> runs)
    {
        Id = report.Id;
        Code = report.FundCode;
        Type = report.Type;
        PeriodEnd = report.ReportDate;
        FundName = report.FundName;
        Word = new(report.Word);
        Excel = new(report.Excel);
        Pdf = new(report.Pdf);
        Xbrl = new(report.Xbrl);
        Sealed = new(report.Sealed);
        this.report = report;

        var data = from workflow in workflows
                   where workflow.ForAllFunds || workflow.TargetFunds.Contains(report.FundId)
                   // 左连接：以 workflow 为主体，匹配对应的实例
                   join instance in runs on workflow.Id equals instance.WorkflowId into instanceGroup
                   from instance in instanceGroup.DefaultIfEmpty()
                       // 构建 ViewModel
                   select new DisclosureRunViewModel(report, workflow, instance);

        Runs = new(data);

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
        db.GetCollection<IDisclosureNotice>().UpdateMany(BsonMapper.Global.ToDocument(v).ToString(), $"_id={Id}");
    }



    public long Id { get; }
    public string? Code { get; }
    public DisclosureType Type { get; }

    public string Title => Type switch
    {
        DisclosureType.Quarterly => $"{PeriodEnd:yy} {PeriodEnd.Month switch { < 4 => "Q1", < 7 => "Q2", < 10 => "Q3", _ => "Q4" }}",
        DisclosureType.SemiAnnually => $"{PeriodEnd:yy} {PeriodEnd.Month switch { < 7 => "上半年", _ => "下半年" }}",
        DisclosureType.Annually => $"{PeriodEnd:yy}",
        _ => $"{PeriodEnd:yy/MM}",
    };


    public string? FundName { get; set; }

    public string? DisplayName => Fund.GetDefaultShortName(FundName);

    public DateOnly PeriodEnd { get; }

    public SimpleFileViewModel Word { get; }

    public SimpleFileViewModel Excel { get; }

    public SimpleFileViewModel Xbrl { get; }

    public SimpleFileViewModel Pdf { get; }


    public SimpleFileViewModel Sealed { get; }

    public ObservableCollection<DisclosureRunViewModel> Runs { get; }


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

        //var result = await DirectReporter.UploadReport(report, acc);

        //if (result.UploadCode != 0)
        //{
        //    HandyControl.Controls.Growl.Info($"上传文件失败:{result.UploadError}");
        //    return;
        //}

        //HandyControl.Controls.Growl.Info($"上传报告成功，请等待校验结果");
        //await Task.Delay(20 * 1000);

        //await DirectReporter.QueryResult(result, acc);

        //if (result.ResultInfo?.Count > 0)
        //    HandyControl.Controls.Growl.Info($"{result.ResultInfo[0].Message}");
        //else
        //    HandyControl.Controls.Growl.Info($"校验异常");

        //if (result.ValidateCode == 0)
        //{
        //    await Task.Delay(2000);
        //    await DirectReporter.Submit(result, manager.Name, acc);
        //    HandyControl.Controls.Growl.Info($"报告提交:{result.SubmitError}");
        //}
    }
}
 