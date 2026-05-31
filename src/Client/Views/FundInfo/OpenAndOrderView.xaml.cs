using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FMO.Disclosure;
using FMO.ESigning;
using FMO.Models;
using FMO.Trustee;
using FMO.Utilities;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows.Controls;
using System.Windows.Data;
using Utilities;

namespace FMO;

/// <summary>
/// OpenAndOrderView.xaml 的交互逻辑
/// </summary>
public partial class OpenAndOrderView : UserControl
{
    public OpenAndOrderView()
    {
        InitializeComponent();
    }
}


public partial class OpenAndOrderViewModel : ObservableObject
{
    #region 开放

    [ObservableProperty]
    public partial bool HasMultipleShare { get; set; }


    public string[] DayOfWeekList { get; } = ["日", "一", "二", "三", "四", "五", "六"];

    /// <summary>
    /// 开放日
    /// </summary>
    [ObservableProperty]
    public partial ShareOpenInfo[] ShareOpenInfos { get; set; }


    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentShareAllowSetTemporaryPurchase))]
    [NotifyPropertyChangedFor(nameof(CurrentShareAllowSetTemporaryRedemption))]
    public partial ShareOpenInfo? SelectedShare { get; set; }

    public bool CurrentShareAllowSetTemporaryPurchase => SelectedShare?.TemporarilyOpenInfo.IsAllowed is true && SelectedShare?.TemporarilyOpenInfo.AllowPurchase is true;

    public bool CurrentShareAllowSetTemporaryRedemption => SelectedShare?.TemporarilyOpenInfo.IsAllowed is true && SelectedShare?.TemporarilyOpenInfo.AllowRedemption is true;

    public bool NoOpenDay => !SelectedShareOpenDays.Any(x => x?.AllowPush is true);


    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoOpenDay))]
    public partial IEnumerable<OpenDayViewModel> SelectedShareOpenDays { get; private set; }

    [ObservableProperty]
    public partial OpenDayViewModel? SelectedOpenDay { get; set; }

    /// <summary>
    /// 电签平台中可用的日期
    /// </summary>
    [ObservableProperty]
    public partial DateOnly[] AvalialbeDaysInESigning { get; set; } = [];


    [ObservableProperty]
    public partial bool NeedCreateTemporaryInESigning { get; set; }


    [ObservableProperty]
    public partial bool CreateTemporaryWithPurchase { get; set; }

    [ObservableProperty]
    public partial bool CreateTemporaryWithRedemption { get; set; }

    /// <summary>
    /// 托管的固定开放日，电签平台不能用
    /// </summary>
    [ObservableProperty]
    public partial bool FixDayCannotUse { get; set; }

    [ObservableProperty]
    public partial bool CanPushOrder { get; set; }

    /// <summary>
    /// 有托管API
    /// </summary>
    [ObservableProperty]
    public partial bool HasAPI { get; set; }

    public int FundId { get; }
    public string FundName { get; }
    public ShareClass[] Shares { get; }



    [ObservableProperty]
    public partial ISigning? SelectedSigner { get; set; }

    [ObservableProperty]
    public partial ISigning[] Signings { get; set; }



    public CollectionViewSource InvestorSource { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial string? SearchInvestorKey { get; set; }


    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial Investor? SelectedInvestor { get; set; }


    public Investor[]? Investors { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial decimal? Number { get; set; }


    [ObservableProperty]
    public partial decimal? HoldingShare { get; set; }

    [ObservableProperty]
    public partial decimal? HoldingValue { get; set; }


    [ObservableProperty]
    public partial DateOnly? HoldingDay { get; set; }

    public bool CanConfirm => SelectedInvestor is not null && OrderType is not null && Number > 0;


    private Dictionary<(int, string?), decimal> _currentHolding = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OrderTypesView))]
    public partial TransferOrderType[] OrderTypes { get; set; } = [];


    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial TransferOrderType? OrderType { get; set; }

    public ICollectionView? OrderTypesView
    {
        get
        {

            field = CollectionViewSource.GetDefaultView(OrderTypes);
            // 添加分组描述：propertyName 传 null 表示将整个 item 传给转换器
            field.GroupDescriptions.Add(
                new PropertyGroupDescription(null, new OrderTypeGroupConverter()));

            // ⚠️ 分组后默认会丢失原始顺序，如需保持顺序可添加排序描述
            // _orderTypesView.SortDescriptions.Add(new SortDescription("", ListSortDirection.Ascending));

            return field;
        }
    }
    public class OrderTypeGroupConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TransferOrderType orderType)
            {
                return orderType switch
                {
                    < TransferOrderType.Share => "认申购",
                    _ => "赎回"
                };
            }
            return "未分组";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
    #endregion



    public OpenAndOrderViewModel(int fundId, string fundName, ShareClass[] shares, TemporarilyOpenInfo?[] tempInfo)
    {
        HasMultipleShare = shares.Length > 1;
        Signings = SigningGalley.Platforms.ToArray();
        FundId = fundId;
        FundName = fundName;
        Shares = shares;

        if (shares.Length != tempInfo.Length && tempInfo.Length > 1)
            throw new InvalidDataException("临时开放规则与份额不匹配");

        var info = new ShareOpenInfo[shares.Length];
        if (TrusteeGallay.Find(fundId) is ITrustee api && api.IsValid)
        {
            HasAPI = true;
        }
        var today = DateOnly.FromDateTime(DateTime.Today);
        var end = Days.NextTradingDay(today, 7);

        // 获取从今天往后7个工作日
        using var db = DbHelper.Base();
        var openDays = db.GetCollection<FundOpenDay>().Find(x => x.FundId == fundId && x.Date.DayNumber >= today.DayNumber && x.Date.DayNumber <= end.DayNumber).ToArray();

        _currentHolding = db.GetCollection<TransferRecord>().Query().Where(x => x.FundId == FundId).ToArray().GroupBy(x => (x.InvestorId, x.ShareClass)).Select(x => new KeyValuePair<(int, string?), decimal>(x.Key, x.Sum(y => y.ShareChange()))).Where(x => x.Value > 0).ToDictionary();

        Investors = db.GetCollection<Investor>().FindAll().ToArray();
        InvestorSource.Source = Investors;
        InvestorSource.Filter += (s, e) =>
        {
            e.Accepted = (OrderType == TransferOrderType.FirstTrade || _currentHolding.ContainsKey(((e.Item as Investor)!.Id, SelectedShare?.Share?.Name))) &&
            (string.IsNullOrWhiteSpace(SearchInvestorKey) || SearchInvestorKey == SelectedInvestor?.Name ? true : e.Item switch { Investor f => f.Name.Contains(SearchInvestorKey), _ => true });
        };

        for (int i = 0; i < shares.Length; i++)
        {
            var sc = shares[i];
            var temp = tempInfo?.Length > i ? tempInfo[i] : tempInfo?.Length == 1 ? tempInfo[0] : new();

            var data = openDays.Where(x => x.ShareId == sc.Id);

            info[i] = new ShareOpenInfo(sc, OpenDayViewModel.Create(fundId, today, end, data, temp ?? new()), sc.Requirement ?? "", temp ?? new());
        }
        ShareOpenInfos = info;



        SelectedShare = ShareOpenInfos.FirstOrDefault();
        SelectedShareOpenDays = ShareOpenInfos[0].DateInfo;

        if (Signings.Length == 1)
            SelectedSigner = Signings[0];
    }

    partial void OnSearchInvestorKeyChanged(string? value)
    {
        InvestorSource.View.Refresh();
    }

    partial void OnSelectedInvestorChanged(Investor? value)
    {
        if (OrderType is not TransferOrderType.Amount and not TransferOrderType.Share and not TransferOrderType.RemainAmout) return;

        if (!_currentHolding.TryGetValue((value?.Id ?? -1, SelectedShare?.Share?.Name), out var share)) return;

        HoldingShare = share;

        using var db = DbHelper.Base();
        var dv = db.GetDailyCollection(FundId).Query().OrderByDescending(x => x.Date).FirstOrDefault();
        if (dv?.NetValue > 0)
        {
            HoldingDay = dv.Date;
            HoldingValue = dv.NetValue * share;
        }
    }

    partial void OnOrderTypeChanged(TransferOrderType? oldValue, TransferOrderType? newValue)
    {
        int buysell(TransferOrderType? v) => v switch { TransferOrderType.FirstTrade or TransferOrderType.Buy => 1, null => 4, _ => 2 };

        if (oldValue is TransferOrderType.FirstTrade ^ newValue is TransferOrderType.FirstTrade)
            UpdateInvestorList();

        if (0 != (buysell(oldValue) ^ buysell(newValue)))
            UpdateESignerOpenDay();
        Number = null;
    }


    private void UpdateESignerOpenDay()
    {
        if (SelectedSigner is ISigning s && SelectedShare is ShareOpenInfo sc && OrderType is TransferOrderType type)
        {
            CanPushOrder = false;
            Task.Run(async () =>
            {
                var dates = await s.QueryAvaliableOpenDayAsync(FundName, sc.Share.Code, type switch
                {
                    TransferOrderType.FirstTrade or TransferOrderType.Buy => OpenTradeType.Purchase,
                    _ => OpenTradeType.Redemption
                });

                AvalialbeDaysInESigning = dates.Successed ? dates.Data : [];
                CheckOpenDayForPush();
            });
        }
    }

    private void CheckOpenDayForPush()
    {
        if (OrderType is TransferOrderType.FirstTrade or TransferOrderType.Buy)
        {
            FixDayCannotUse = SelectedOpenDay?.IsPurchaseFixedOpen is true && AvalialbeDaysInESigning?.Contains(SelectedOpenDay.Date) is not true;

            NeedCreateTemporaryInESigning = SelectedOpenDay?.IsPurchaseFixedOpen is not true &&
                SelectedShare?.TemporarilyOpenInfo.IsAllowed is true &&
                SelectedShare?.TemporarilyOpenInfo.AllowPurchase is true &&
                !AvalialbeDaysInESigning.Contains(SelectedOpenDay?.Date ?? default);

            if (NeedCreateTemporaryInESigning)
            {
                CreateTemporaryWithPurchase = true;
                CreateTemporaryWithRedemption = false;
            }
            CanPushOrder = !FixDayCannotUse && !NeedCreateTemporaryInESigning;
        }
        else if (OrderType is TransferOrderType.Amount or TransferOrderType.Share or TransferOrderType.RemainAmout)
        {
            FixDayCannotUse = SelectedOpenDay?.IsRedemptionFixedOpen is true && AvalialbeDaysInESigning?.Contains(SelectedOpenDay.Date) is not true;

            NeedCreateTemporaryInESigning = SelectedOpenDay?.IsRedemptionFixedOpen is not true &&
                SelectedShare?.TemporarilyOpenInfo.IsAllowed is true &&
                SelectedShare?.TemporarilyOpenInfo.AllowRedemption is true &&
                !AvalialbeDaysInESigning.Contains(SelectedOpenDay?.Date ?? default);

            if (NeedCreateTemporaryInESigning)
            {
                CreateTemporaryWithPurchase = false;
                CreateTemporaryWithRedemption = true;
            }
            CanPushOrder = !FixDayCannotUse && !NeedCreateTemporaryInESigning;
        }
        else CanPushOrder = false;
    }



    private void UpdateInvestorList(bool updateHolder = false)
    {
        InvestorSource.View.Refresh();
    }

    partial void OnSelectedOpenDayChanged(OpenDayViewModel? value)
    {
        TransferOrderType[] a = value?.AllowPushPurchase is true || SelectedShare?.TemporarilyOpenInfo.AllowPurchase is true ? [TransferOrderType.FirstTrade, TransferOrderType.Buy] : [];
        TransferOrderType[] b = value?.AllowPushRedemption is true || SelectedShare?.TemporarilyOpenInfo.AllowRedemption is true ? [TransferOrderType.Share, TransferOrderType.Amount, TransferOrderType.RemainAmout] : [];

        OrderTypes = a.Union(b).ToArray();
        CheckOpenDayForPush();
    }

    partial void OnSelectedShareChanged(ShareOpenInfo? value)
    {
        var old = SelectedOpenDay;
        var oldt = OrderType;
        SelectedShareOpenDays = value?.DateInfo ?? [];
        SelectedOpenDay = SelectedShareOpenDays.FirstOrDefault(x => x is not null && x.Date == old?.Date);
        OrderType = oldt;
        UpdateInvestorList();
        UpdateESignerOpenDay();
    }


    partial void OnSelectedSignerChanged(ISigning? value)
    {
        if (value is null) return;

        UpdateESignerOpenDay();
    }

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    public async Task Confirm()
    {
        SigningOrder order = new()
        {
            FundId = FundId,
            InvestorId = SelectedInvestor!.Id,
            OpenDate = SelectedOpenDay!.Date,
            Number = Number!.Value,
            OrderType = OrderType!.Value
        };

        await SelectedSigner!.PushOrder(order);



        SelectedOpenDay = null;
        SelectedInvestor = null;
        Number = null;
        OrderType = null;

    }

    [RelayCommand]
    public void RedeemAll()
    {
        OrderType = TransferOrderType.Share;
        Number = HoldingShare;
    }


    [RelayCommand]
    public async Task RefreshOpenDay()
    {
        if (TrusteeGallay.Find(FundId) is ITrustee api && api.IsValid)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var end = Days.NextTradingDay(today, 7);
            var data = await api.QueryOpenDays(today, end);

            if (data.Code == ReturnCode.Success && data.Data is not null)
            {
                using var db = DbHelper.Base();
                db.GetCollection<FundOpenDay>().Upsert(data.Data);

                for (int i = 0; i < Shares.Length; i++)
                {
                    var sc = ShareOpenInfos[i];

                    var dd = data.Data.Where(x => x.ShareId == sc.Share.Id);

                    ShareOpenInfos[i] = sc with { DateInfo = OpenDayViewModel.Create(FundId, today, end, dd, sc.TemporarilyOpenInfo) };
                }
            }
        }
    }


    [RelayCommand]
    public async Task CreateTemporaryOpen()
    {
        if (!CreateTemporaryWithPurchase && !CreateTemporaryWithRedemption)
        {
            Toast.Warning("至少选择一个开放的交易类型");
            return;
        }

        if (SelectedSigner is null || SelectedShare is null || SelectedOpenDay is null)
        {
            Toast.Warning("前置条件不成立");
            return;
        }

        var type = (CreateTemporaryWithPurchase ? OpenTradeType.Purchase : OpenTradeType.None) | (CreateTemporaryWithRedemption ? OpenTradeType.Redemption : OpenTradeType.None);

        var r = await SelectedSigner.CreateTemporaryOpenDay(FundName, SelectedShare.Share.Code, SelectedOpenDay.Date, type, false);
        if (!r.Successed)
        {
            Toast.Warning("临时开放日创建失败");
            return;
        }
        UpdateESignerOpenDay();

        // 创建公告
        TemporaryOpenNotice notice = new()
        {
            FundId = FundId,
            FundCode = SelectedShare.Share.Code!,
            FundName = FundName,
            AllowPurchase = CreateTemporaryWithPurchase,
            AllowRedemption = CreateTemporaryWithRedemption,
            OpenDay = SelectedOpenDay.Date,
            PublishDate = DateOnly.FromDateTime(DateTime.Now),
            PublishTime = TimeOnly.FromDateTime(DateTime.Now)
        };
        DisclosureService.RegisterNotice(notice);
    }
}

public record ShareOpenInfo(ShareClass Share, OpenDayViewModel[] DateInfo, string Requirement, TemporarilyOpenInfo TemporarilyOpenInfo);

public partial class OpenDayViewModel : ObservableObject, IDate
{
    public int FundId { get; set; }

    public DateOnly Date { get; set; }

    public bool AllowPush => AllowPushPurchase || AllowPushRedemption;

    /// <summary>
    /// 允许设置临开
    /// </summary> 
    public bool AllowSetTemporaryOpen => AllowSetTemporaryPurchase || AllowSetTemporaryRedemption;

    [ObservableProperty]
    public partial bool AllowSetTemporaryPurchase { get; private set; }


    [ObservableProperty]
    public partial bool AllowSetTemporaryRedemption { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AllowPush))]
    public partial bool AllowPushPurchase { get; private set; }


    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AllowPush))]
    public partial bool AllowPushRedemption { get; private set; }


    [ObservableProperty]
    public partial bool IsPurchaseFixedOpen { get; private set; }


    [ObservableProperty]
    public partial bool IsRedemptionFixedOpen { get; private set; }



    [ObservableProperty]
    public partial bool Selectable { get; set; }

    [RelayCommand]
    public void PushOrder(string buy)
    {
        var wnd = new ManualApplyTradeWindow();
        wnd.Owner = App.Current.MainWindow;
        wnd.DataContext = new ManualApplyTradeWindowViewModel(FundId, buy == "True", Date);

        wnd.ShowDialog();
    }

    public static OpenDayViewModel[] Create(int fundId, DateOnly minDate, DateOnly maxDate, IEnumerable<FundOpenDay>? openDays, TemporarilyOpenInfo temp)
    {
        if (openDays == null)
            return [];

        var list = openDays.ToList();

        // 构建现有日期集合，用于快速查重
        var existingDates = list.Select(x => x.Date).ToHashSet();

        // 获取全局日期范围

        var dayi = Days.DayInfosBetween(minDate, maxDate).ToDictionary(x => x.Date, x => x);


        var today = DateOnly.FromDateTime(DateTime.Today);
        var result = new List<OpenDayViewModel>(list.Select(x => new OpenDayViewModel
        {
            FundId = fundId,
            Date = x.Date,
            Selectable = x.Date >= today,
            AllowSetTemporaryPurchase = temp.IsAllowed && temp.AllowPurchase && x.OpenPurchase == OpenType.None,
            AllowSetTemporaryRedemption = temp.IsAllowed && temp.AllowRedemption && x.OpenRedemption == OpenType.None,

            AllowPushPurchase = x.OpenPurchase != OpenType.None,
            IsPurchaseFixedOpen = x.OpenPurchase is OpenType.Fixed or OpenType.Postpone,
            AllowPushRedemption = x.OpenRedemption != OpenType.None,
            IsRedemptionFixedOpen = x.OpenRedemption is OpenType.Fixed or OpenType.Postpone
        })); // 先加入原数据

        // 遍历日期范围，补全缺失日期
        for (var date = minDate; date <= maxDate; date = date.AddDays(1))
        {
            if (!existingDates.Contains(date))
            {
                var trade = dayi[date].Flag.HasFlag(DayFlag.Trade);
                result.Add(new OpenDayViewModel
                {
                    FundId = fundId,
                    Date = date,
                    Selectable = date >= today && trade,
                    AllowSetTemporaryPurchase = temp.IsAllowed && temp.AllowPurchase && trade,
                    AllowSetTemporaryRedemption = temp.IsAllowed && temp.AllowRedemption && trade,
                });
            }
        }

        result.Sort((x, y) => Comparer<DateOnly>.Default.Compare(x.Date, y.Date));
        // 补空
        for (int i = 0; i < (int)minDate.DayOfWeek; i++)
        {
            result.Insert(0, null!);
        }


        return result.ToArray();
    }
}