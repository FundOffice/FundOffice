using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FMO.Logging;
using FMO.Models;
using FMO.TPL;
using FMO.Trustee;
using FMO.Utilities;
using Microsoft.Win32;
using System.IO;
using System.Runtime.Loader;
using System.Windows.Controls;

namespace FMO;

/// <summary>
/// FundTAView.xaml 的交互逻辑
/// </summary>
public partial class FundTAView : UserControl
{
    public FundTAView()
    {
        InitializeComponent();
    }
}



public partial class FundTAViewModel : ObservableObject
{
    public FundTAViewModel(int fundId)
    {

        FundId = fundId;

        using var db = DbHelper.Base();
        Fund fund = db.GetCollection<Fund>().FindById(fundId);
        FundName = fund.Name;

        IsCleared = fund.Status > FundStatus.StartLiquidation;
        var ta = db.GetCollection<TransferRecord>().Find(x => x.FundId == fundId).ToList();
        //var ts = db.GetCollection<FundShareRecord>().Find(x => x.FundId == fundId).OrderBy(x => x.Date).ToList();
        Records = ta;

        // customeid = 0
        foreach (var item in ta.Where(x => x.InvestorId == 0))
            item.InvestorId = item.InvestorIdentity.GetHashCode();

        // 对齐开放日净值
        var ds = db.GetDailyCollection(fundId).FindAll().OrderByDescending(x => x.Date);
        //foreach (var item in ta)
        //{
        //    var d = ds.FirstOrDefault(x => x.Date < item.ConfirmedDate);
        //}

        var daily = ds.FirstOrDefault(x => x.NetValue > 0);
        var nv = daily?.NetValue ?? 0;
        NetValueDate = daily?.Date;
        Daily = daily;

        if (ta.Count > 0)
        {
            // 按投资人分
            var cur = ta.GroupBy(x => x.InvestorId).Select(x => new { Id = x.Key, Name = x.First().InvestorName, Record = x, Share = x.Sum(y => y.ShareChange()) }).OrderByDescending(x => x.Share);
            InvestorCount = cur.Count(x => x.Share > 0);
            CurrentTotalShare = cur.Sum(x => x.Share);
            CurrentShareDate = ta.Max(x => x.ConfirmedDate);
            CurrentShares = cur.Select(x => new InvestorShareViewModel
            {
                Id = x.Id,
                Name = x.Name,
                Share = x.Share,
                Asset = x.Share * nv,
                Deposit = x.Record.Where(x => x.Type switch { TransferRecordType.Subscription or TransferRecordType.Purchase or TransferRecordType.MoveIn => true, _ => false }).Sum(x => x.ConfirmedNetAmount),
                Withdraw = x.Record.Where(x => x.Type switch { TransferRecordType.Redemption or TransferRecordType.Redemption or TransferRecordType.MoveOut or TransferRecordType.Distribution => true, _ => false }).Sum(x => x.ConfirmedNetAmount),
                Proportion = x.Share == 0 ? null : x.Share / CurrentTotalShare
                //Profit = x.Record.Sum(y => y.AmountChange()) + x.Share * nv
            }).ToList();
            CurrentTotalProfit = CurrentShares.Sum(x => x.Profit);
        }

        if (DataTracker.FundTips.Any(x => x.FundId == fundId && x.Type == TipType.FundShareNotPair))
            CurrentShareNotPair = true;

        // 认购时份额
        var subscription = ta.Where(x => x.Type == TransferRecordType.Subscription);
        InitialShares = new(subscription.Select(x => new InvestorShareViewModel { Id = x.Id, Name = x.InvestorName, Share = x.ConfirmedShare }));
        InitialTotalShare = subscription.Sum(x => x.ConfirmedShare);
    }

    public int FundId { get; }

    public IList<TransferRecord> Records { get; set; }

    public string FundName { get; }

    /// <summary>
    /// 清盘
    /// </summary>
    [ObservableProperty]
    public partial bool IsCleared { get; set; }

    [ObservableProperty]
    public partial int InvestorCount { get; set; }

    [ObservableProperty]
    public partial decimal CurrentTotalShare { get; set; }


    [ObservableProperty]
    public partial decimal CurrentTotalProfit { get; set; }

    [ObservableProperty]
    public partial DateOnly CurrentShareDate { get; set; }

    [ObservableProperty]
    public partial List<InvestorShareViewModel>? CurrentShares { get; set; }

    [ObservableProperty]
    public partial InvestorShareViewModel? SelectedInvestor { get; set; }

    [ObservableProperty]
    public partial bool CurrentShareNotPair { get; set; }

    [ObservableProperty]
    public partial DateOnly? NetValueDate { get; set; }

    public DailyValue? Daily { get; set; }


    [ObservableProperty]
    public partial List<InvestorShareViewModel>? InitialShares { get; set; }


    [ObservableProperty]
    public partial decimal InitialTotalShare { get; set; }


    [ObservableProperty]
    public partial bool InvestorDetailIsOpen { get; set; }


    [ObservableProperty]
    public partial bool InvestorViewIsOpen { get; set; }

    [ObservableProperty]
    public partial CustomerViewModel? InvestorViewModel { get; set; }


    [ObservableProperty]
    public partial IEnumerable<TransferRecord>? InvestorDetail { get; set; }

    [ObservableProperty]
    public partial bool IsTrusteeApiAvaliable { get; set; }


    partial void OnSelectedInvestorChanged(InvestorShareViewModel? value)
    {
        InvestorDetail = Records.Where(x => x.InvestorId == value?.Id);
    }


    [RelayCommand]
    public void ShowInvestorDetail(InvestorShareViewModel v)
    {
        InvestorDetailIsOpen = true;
        InvestorDetail = Records.Where(x => x.InvestorId == v.Id);
    }


    [RelayCommand]
    public void ViewInvestor(InvestorShareViewModel v)
    {
        InvestorViewIsOpen = true;
        InvestorViewModel = new CustomerViewModel(v.Id);
    }

    [RelayCommand]
    public void Export()
    {
        if (CurrentShares is null || Daily is null) return;

        using var db = DbHelper.Base();
        // 排除不存在的模板
        var tpl = db.GetCollection<TemplateInfo>().FindById("DF3CEE4F-EF22-E7F7-8238-6CFEE4605326");
        if (tpl is null || !Directory.Exists(@$"files\tpl\{tpl.Id}"))
        {
            WeakReferenceMessenger.Default.Send(new ToastMessage(LogLevel.Warning, "模板不存在"));
            return;
        }

        AssemblyLoadContext context = new AssemblyLoadContext(Guid.NewGuid().ToString(), true);
        try
        {
            string assemblyPath = Path.GetFullPath(Path.Combine(@$"files\tpl\{tpl.Id}", tpl.Entry));
            using var fs = new FileStream(assemblyPath, FileMode.Open);
            var assembly = context.LoadFromStream(fs);
            var type = assembly.GetType(tpl.Type);
            if (type is null)
                return;
            var obj = Activator.CreateInstance(type) as IExporter;

            var data = obj?.Generate((FundId, DateOnly.FromDateTime(DateTime.Now)));
            if (data?.Data is null)
            {
                WeakReferenceMessenger.Default.Send(new ToastMessage(LogLevel.Warning, "导出失败，未能成功生成数据。"));
                return;
            }

            var fd = new SaveFileDialog
            {
                FileName = data.FileName,
                Filter = data.Filter,
                Title = $"导出 {tpl.Name}"
            };

            var filePath = Path.Combine(@$"files\tpl\{tpl.Id}", "default.xlsx");
            if (fd.ShowDialog() == true)
                try { Tpl.Generate(fd.FileName!, filePath, data.Data); } catch { }
        }
        catch (Exception e)
        {
            LogEx.Error(e);
            WeakReferenceMessenger.Default.Send(new ToastMessage(LogLevel.Warning, "导出失败，请查看Log"));
        }
        finally
        {
            context.Unload();
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

    }


    [RelayCommand]
    public void GoToTA()
    {
        WeakReferenceMessenger.Default.Send(new OpenPageMessage("TA"));
        WeakReferenceMessenger.Default.Send(new PageTAMessage(2, FundName));
    }

    [RelayCommand]
    public async Task UpdateTAByApi()
    {
        // check
        var api = TrusteeGallay.Find(FundId);
        if (!(api?.IsValid ?? false))
        {
            WeakReferenceMessenger.Default.Send(new ToastMessage(LogLevel.Warning, "API 不可用"));
            return;
        }
        try
        {

            using var db = DbHelper.Base();
            var fund = db.GetCollection<Fund>().FindById(FundId);

            var result = await api.QueryTransferRequests(fund.SetupDate.AddMonths(-3), DateOnly.FromDateTime(DateTime.Now), fund.Code);
            if (result.Code == ReturnCode.Success && result.Data?.Count > 0)
                DataTracker.OnBatchTransferRequest(result.Data);

            var records = await api.QueryTransferRecords(fund.SetupDate.AddMonths(-3), DateOnly.FromDateTime(DateTime.Now), fund.Code);
            if (records.Code == ReturnCode.Success && records.Data?.Count > 0)
                DataTracker.OnBatchTransferRecord(records.Data);
        }
        catch (Exception e)
        {
            LogEx.Error(e);
            WeakReferenceMessenger.Default.Send(new ToastMessage(LogLevel.Warning, "更新失败"));
        }
    }
}

public class InvestorShareViewModel
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public decimal Share { get; set; }

    /// <summary>
    /// 市值
    /// </summary>
    public decimal Asset { get; set; }


    public decimal Deposit { get; set; }

    public decimal Withdraw { get; set; }

    /// <summary>
    /// 当前份额占比
    /// </summary>
    public decimal? Proportion { get; set; }

    public decimal Profit => Asset + Withdraw - Deposit;


}
