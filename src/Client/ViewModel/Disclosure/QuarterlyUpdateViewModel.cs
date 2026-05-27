using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FMO.Disclosure;

using FMO.Models;
using FMO.Shared;
using FMO.TPL;
using FMO.Utilities;
using LiteDB;
using MoT;
using System.Collections.ObjectModel;
using System.IO;

namespace FMO;

public partial class QuarterlyUpdateViewModel : ObservableObject
{
    private readonly QuarterlyUpdate report;

    public QuarterlyUpdateViewModel(QuarterlyUpdate report, IEnumerable<DisclosureWorkflow> workflows, IEnumerable<DisclosureInstance> runs)
    {
        Id = report.Id;
        FundId = report.FundId;
        FundName = report.FundName;
        FundCode = report.FundCode;
        Type = report.Type;
        PeriodEnd = report.ReportDate;
        Investor = new(report.Investor);
        Operation = new(report.Operation);
        this.report = report;


        var data = from workflow in workflows
                       // 左连接：以 workflow 为主体，匹配对应的实例
                   join instance in runs on workflow.Id equals instance.WorkflowId into instanceGroup
                   from instance in instanceGroup.DefaultIfEmpty()
                       // 构建 ViewModel
                   select new DisclosureRunViewModel(report, workflow, instance);

        Runs = new(data);



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

    public long Id { get; }
    public int FundId { get; }

    public DisclosureType Type { get; }
    public string Title => $"{PeriodEnd:yy} {PeriodEnd.Month switch { < 4 => "Q1", < 7 => "Q2", < 10 => "Q3", _ => "Q4" }}";

    public string? FundName { get; set; }
    public string FundCode { get; }

    public string? DisplayName => Fund.GetDefaultShortName(FundName);

    public DateOnly PeriodEnd { get; }

    public SimpleFileViewModel Investor { get; }

    public SimpleFileViewModel Operation { get; }



    public ObservableCollection<DisclosureRunViewModel> Runs { get; }





    [RelayCommand]
    public async Task UploadOperation()
    {
        throw new NotImplementedException();
        // 获取 账号
        //using var db = DbHelper.Base();
        //var acc = db.GetCollection<AmacReportAccount>().FindOne(x => x.Id == "pmg");

        //if (acc is null || string.IsNullOrWhiteSpace(acc.Name) || string.IsNullOrWhiteSpace(acc.Password) || string.IsNullOrWhiteSpace(acc.Key))
        //{
        //    HandyControl.Controls.Growl.Info("请先在[平台]中设置信批账号");
        //    return;
        //}

        //var manager = db.GetCollection<Manager>().Query().First();

        //// 检查是否有上传记录
        //var result = db.GetCollection<AmacProcessResult>().FindById(Id);
        //if (result is null)
        //{
        //    result = await AmacDirectReporter.UploadReport(report, acc);
        //    if (result?.UploadCode != 0)
        //    {
        //        HandyControl.Controls.Growl.Info($"上传文件失败:{result?.UploadError}");
        //        return;
        //    }

        //    HandyControl.Controls.Growl.Info($"上传报告成功，请等待校验结果");
        //    //await Task.Delay(20 * 1000);
        //}
        //else HandyControl.Controls.Growl.Info("存在上传记录，继续查询结果");

        //OperationResult.Status = AmacDirectResultViewModel.State.Upload;
        //OperationResult.IsSuccess = result.UploadCode == 0;

        //await AmacDirectReporter.QueryResult(result, acc);
        //OperationResult.Status = AmacDirectResultViewModel.State.Verify;
        //OperationResult.IsSuccess = result.ValidateCode == 0;

        //// 重新上传
        //if (result.ValidateCode == 99)
        //{
        //    result = await AmacDirectReporter.UploadReport(report, acc);
        //    if (result?.UploadCode != 0)
        //    {
        //        HandyControl.Controls.Growl.Info($"上传文件失败:{result?.UploadError}");
        //        return;
        //    }

        //    HandyControl.Controls.Growl.Info($"上传报告成功，请等待校验结果");
        //    //await Task.Delay(20 * 1000);

        //    OperationResult.Status = AmacDirectResultViewModel.State.Upload;
        //    OperationResult.IsSuccess = result.UploadCode == 0;

        //    await AmacDirectReporter.QueryResult(result, acc);
        //    OperationResult.Status = AmacDirectResultViewModel.State.Verify;
        //    OperationResult.IsSuccess = result.ValidateCode == 0;
        //}

        //if (result.ValidateCode == 0 || result.ValidateCode == 10) // 已完成
        //{
        //    result.SubmitCode = 0;
        //    OperationResult.Status = AmacDirectResultViewModel.State.Submit;
        //    OperationResult.IsSuccess = true;
        //    db.GetCollection<AmacProcessResult>().Update(result);
        //    return;
        //}

        //if (result.ResultInfo?.Count > 0)
        //    HandyControl.Controls.Growl.Info($"{result.ResultInfo[0].Message}");
        //else
        //    HandyControl.Controls.Growl.Info($"校验异常");

        //if (result.ValidateCode != 0)
        //{
        //    Growl.Warning($"{FundName} 季度更新存在警告或错误，请手动检查后提交");
        //    return;
        //}

        //await AmacDirectReporter.Submit(result, manager.Name, acc);

        //if (result.SubmitError?.Contains("handle参数错误或已失效") ?? false)
        //{
        //    db.GetCollection<AmacProcessResult>().Delete(Id);
        //    OperationResult.Status = AmacDirectResultViewModel.State.None;
        //}

        //HandyControl.Controls.Growl.Info($"报告提交, Code:{result.SubmitCode},{result.SubmitError}");
    }

    [RelayCommand]
    public async Task SubmitOperation()
    {
        throw new NotImplementedException();
        //using var db = DbHelper.Base();
        //var acc = db.GetCollection<AmacReportAccount>().FindOne(x => x.Id == "pmg");

        //if (acc is null || string.IsNullOrWhiteSpace(acc.Name) || string.IsNullOrWhiteSpace(acc.Password) || string.IsNullOrWhiteSpace(acc.Key))
        //{
        //    HandyControl.Controls.Growl.Info("请先在[平台]中设置信批账号");
        //    return;
        //}

        //var result = db.GetCollection<AmacProcessResult>().FindById(Id);
        //var manager = db.GetCollection<Manager>().Query().First();

        //if (MessageBox.Show($"季度更新存在警告或错误", "是否强制提交", button: System.Windows.MessageBoxButton.YesNo) == System.Windows.MessageBoxResult.Yes)
        //{
        //    //await Task.Delay(5000);
        //    await AmacDirectReporter.Submit(result, manager.Name, acc);
        //    HandyControl.Controls.Growl.Info($"报告提交, Code:{result.SubmitCode},{result.SubmitError}");

        //    if (result.SubmitCode == 0)
        //    {
        //        OperationResult.Status = AmacDirectResultViewModel.State.Submit;
        //        OperationResult.IsSuccess = true;
        //    }
        //}
    }


    [RelayCommand]
    public async Task GenerateInvestorSheet()
    {
        try
        {
            var path = @"ambers_investor.xlsx";

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

            Tpl.GenerateByPredefined(outp, path, obj);

            // 保存
            var r = db.GetCollection<QuarterlyUpdate>(nameof(IDisclosureNotice)).FindById(Id);
            r.Investor = new SimpleFile { File = FileMeta.Create(outp) };
            db.GetCollection<IDisclosureNotice>().Update(r);
            Investor.Meta = r.Investor.File;
            File.Delete(outp);

            old?.Delete();
            //PackDiscloseSheets(data);
        }
        catch (Exception e)
        {
            Logg.Error(e);
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
