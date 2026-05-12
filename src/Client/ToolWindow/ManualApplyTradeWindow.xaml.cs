using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FMO.ESigning;
using FMO.Models;
using FMO.Utilities;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace FMO;

/// <summary>
/// ManualApplyTradeWindow.xaml 的交互逻辑
/// </summary>
public partial class ManualApplyTradeWindow : Window
{
    public ManualApplyTradeWindow()
    {
        InitializeComponent();

        DataContext = new ManualApplyTradeWindowViewModel();
    }
}

/// <summary>
/// 向电签平台发出交易订单
/// </summary>
public partial class ManualApplyTradeWindowViewModel : ObservableObject
{
    public ManualApplyTradeWindowViewModel()
    {
        Signings = SigningGalley.Platforms.ToArray();

        using var db = DbHelper.Base();
        Investors = db.GetCollection<Investor>().FindAll().ToArray();
        Funds = db.GetCollection<Fund>().FindAll().Where(x => x.Status <= FundStatus.StartLiquidation).ToArray();
        IsBuy = true;

        FundSource.Source = Funds;
        FundSource.Filter += (s, e) => e.Accepted = string.IsNullOrWhiteSpace(SearchFundKey) || SearchFundKey == SelectedFund?.Name ? true : e.Item switch { Fund f => f.Name.Contains(SearchFundKey), _ => true };

        InvestorSource.Source = Investors;
        InvestorSource.Filter += (s, e) =>
        {
            e.Accepted = (IsBuy || _currentHolding.ContainsKey((e.Item as Investor)!.Id)) && (string.IsNullOrWhiteSpace(SearchInvestorKey) || SearchInvestorKey == SelectedInvestor?.Name ? true : e.Item switch { Investor f => f.Name.Contains(SearchInvestorKey), _ => true });
        };

        if (Signings.Length == 1) SelectedSigner = Signings[0];
    }




    public ManualApplyTradeWindowViewModel(int fundId, int investorId, decimal share)
    {
        Signings = SigningGalley.Platforms.ToArray();

        using var db = DbHelper.Base();
        SelectedInvestor = db.GetCollection<Investor>().FindById(investorId);
        //Investors = [SelectedInvestor];
        SelectedFund = db.GetCollection<Fund>().FindById(fundId);
        //Funds = [SelectedFund];
        IsBuy = true;

        HoldingShare = share;

        var dv = db.GetDailyCollection(SelectedFund?.Id ?? 0).Query().OrderByDescending(x => x.Date).FirstOrDefault();
        if (dv?.NetValue > 0)
        {
            HoldingDay = dv.Date;
            HoldingValue = dv.NetValue * share;
        }

    }

    [ObservableProperty]
    public partial bool IsBuy { get; set; }

    public bool IsBuySealed { get; init; }

    [ObservableProperty]
    public partial string? ShareClass { get; set; }

    [ObservableProperty]
    public partial string? SearchFundKey { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial Fund? SelectedFund { get; set; }


    [ObservableProperty]
    public partial ISigning? SelectedSigner { get; set; }

    [ObservableProperty]
    public partial ISigning[] Signings { get; set; }

    public Fund[]? Funds { get; set; }

    public CollectionViewSource FundSource { get; } = new();


    public CollectionViewSource InvestorSource { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial string? SearchInvestorKey { get; set; }


    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial Investor? SelectedInvestor { get; set; }


    public Investor[]? Investors { get; set; }

    private Dictionary<int, decimal> _currentHolding = [];


    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial TransferOrderType? OrderType { get; set; }



    [ObservableProperty]
    public partial TransferOrderType[] OrderTypes { get; set; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial decimal? Number { get; set; }


    [ObservableProperty]
    public partial decimal? HoldingShare { get; set; }

    [ObservableProperty]
    public partial decimal? HoldingValue { get; set; }


    [ObservableProperty]
    public partial DateOnly? HoldingDay { get; set; }

    [ObservableProperty]
    public partial DateTime? Date { get; set; }

    private DateTime[] FixedOpenDates { get; set; }

    /// <summary>
    /// 不可更换投资人
    /// </summary>
    public bool IsInvestorSealed => SelectedInvestor is not null && Investors is null;

    public bool IsFundSealed => SelectedFund is not null && Funds is null;


    public bool CanConfirm => SelectedFund is not null && SelectedInvestor is not null && OrderType is not null && Number > 0;


    partial void OnSearchFundKeyChanged(string? value)
    {
        if (!IsFundSealed)
            FundSource.View.Refresh();
    }
    partial void OnSearchInvestorKeyChanged(string? value)
    {
        if (!IsInvestorSealed)
            InvestorSource.View.Refresh();
    }
    partial void OnIsBuyChanged(bool value)
    {
        OrderTypes = value ? [TransferOrderType.FirstTrade, TransferOrderType.Buy] : [TransferOrderType.Share, TransferOrderType.Amount, TransferOrderType.RemainAmout];


        if (!IsInvestorSealed) OrderType = null;
    }
    partial void OnSelectedFundChanged(Fund? value)
    {
        FixedOpenDates = [];

        if (value is null) return;

        // 检查电签平台
        if (Signings.Length == 1)
            SelectedSigner = Signings[0];

        if (SelectedSigner is null) return;

        // 加载固定开放日
        using var db = DbHelper.Base();
        var element = db.GetCollection<FundElements>().FindById(value.Id);
        if(element is not null)
        {
            var sheets = element.FundOpenRule.Value?.Apply(DateTime.Now.Year);
            FixedOpenDates = sheets?.Where(x => x.Type is OpenType.Postpone or OpenType.Fixed).Select(x => new DateTime(x.Date, default)).ToArray() ?? [];
        }


        UpdateInvestorList(true);
    }


    private async Task UpdateOpenDays(ISigning signer, int fundId)
    {
        var days = await signer.QueryAvaliableOpenDay(fundId, null, IsBuy ? OpenFlag.Buy : OpenFlag.Sell);


    }



    partial void OnOrderTypeChanged(TransferOrderType? value) => UpdateInvestorList();


    partial void OnSelectedInvestorChanged(Investor? value)
    {
        if (IsBuy) return;

        if (!_currentHolding.TryGetValue(value?.Id ?? -1, out var share)) return;

        HoldingShare = share;

        using var db = DbHelper.Base();
        var dv = db.GetDailyCollection(SelectedFund?.Id ?? 0).Query().OrderByDescending(x => x.Date).FirstOrDefault();
        if (dv?.NetValue > 0)
        {
            HoldingDay = dv.Date;
            HoldingValue = dv.NetValue * share;
        }
    }

    partial void OnOrderTypeChanged(TransferOrderType? oldValue, TransferOrderType? newValue)
    {
        if (oldValue is TransferOrderType.Amount or TransferOrderType.RemainAmout && newValue is TransferOrderType.Amount or TransferOrderType.RemainAmout)
            return;

        if (oldValue is TransferOrderType.FirstTrade or TransferOrderType.Buy && newValue is TransferOrderType.FirstTrade or TransferOrderType.Buy)
            return;

        Number = null;
    }


    private void UpdateInvestorList(bool updateHolder = false)
    {
        if (IsInvestorSealed) return;

        if (updateHolder)
        {
            using var db = DbHelper.Base();
            _currentHolding = SelectedFund is null ? [] : db.GetCollection<TransferRecord>().Query().Where(x => x.FundId == SelectedFund.Id).ToArray().GroupBy(x => x.InvestorId).Select(x => new KeyValuePair<int, decimal>(x.Key, x.Sum(y => y.ShareChange()))).Where(x => x.Value > 0).ToDictionary();
        }


        InvestorSource.View.Refresh();
    }


    [RelayCommand]
    public void RefreshDate(DatePicker picker)
    {
        DateTime stTime = DateTime.Now.AddDays(-DateTime.Now.Day + 1);
        picker.DisplayDateStart = stTime;
        picker.DisplayDateEnd = DateTime.Now.AddMonths(1);

        var begin = DateOnly.FromDateTime(stTime).DayNumber;

        var dates = Enumerable.Range(0, 62).Select(x => DateOnly.FromDayNumber(begin + x)).Select(x => new DateTime(x, default));


        //var days = Days.DayInfosByYear(DateTime.Now.Year).Where(x => !x.Flag.HasFlag(DayFlag.Trade)).
        //    Select(x => new DateTime(x.Date, default)).Where(x => x > picker.DisplayDateStart && x < picker.DisplayDateEnd);

        picker.BlackoutDates.AddDatesInPast();



        foreach (var item in ConvertToCalendarDateRanges(dates.Except(FixedOpenDates)))
            picker.BlackoutDates.Add(item);
         
    }

    /// <summary>
    /// 将一组日期转换为 CalendarDateRange 集合，自动合并连续日期
    /// </summary>
    /// <param name="dates">日期集合</param>
    /// <returns>CalendarDateRange 列表</returns>
    public static List<CalendarDateRange> ConvertToCalendarDateRanges(IEnumerable<DateTime> dates)
    {
        var result = new List<CalendarDateRange>();

        // 空校验
        if (dates == null || !dates.Any())
            return result;

        // 1. 排序 + 去重
        var orderedDates = dates.Distinct().OrderBy(d => d.Date).ToList();

        // 2. 初始化起始日期
        DateTime start = orderedDates[0];
        DateTime end = orderedDates[0];

        // 3. 遍历合并连续日期
        for (int i = 1; i < orderedDates.Count; i++)
        {
            var current = orderedDates[i];
            // 判断是否连续（相差1天）
            if (current.Date == end.Date.AddDays(1))
            {
                end = current;
            }
            else
            {
                // 不连续，添加当前范围
                result.Add(new CalendarDateRange(start, end));
                start = current;
                end = current;
            }
        }

        // 4. 添加最后一个范围
        result.Add(new CalendarDateRange(start, end));

        return result;
    }

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    public void Confirm()
    {

    }

    [RelayCommand]
    public void RedeemAll()
    {
        OrderType = TransferOrderType.Share;
        Number = HoldingShare;
    }

}