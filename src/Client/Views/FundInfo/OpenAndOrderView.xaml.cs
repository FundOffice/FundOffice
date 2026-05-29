using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FMO.Models;
using FMO.Trustee;
using FMO.Utilities;
using System.Windows.Controls;

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
    public partial ShareOpenInfo? SelectedShare { get; set; }

    [ObservableProperty]
    public partial IEnumerable<OpenDayViewModel> SelectedShareOpenDays { get; private set; }

    [ObservableProperty]
    public partial IDate? SelectedOpenDay { get; set; }

    /// <summary>
    /// 有托管API
    /// </summary>
    [ObservableProperty]
    public partial bool HasAPI { get; set; }
    #endregion



    public OpenAndOrderViewModel(int fundId, ShareClass[] shares, TemporarilyOpenInfo?[] current)
    {

        HasMultipleShare = shares.Length > 1;

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


        for (int i = 0; i < shares.Length; i++)
        {
            var sc = shares[i];
            var temp = current?.Length > i ? current[i] : current?.Length == 1 ? current[0] : new();

            var data = openDays.Where(x => x.ShareId == sc.Id);

            info[i] = new ShareOpenInfo(sc, OpenDayViewModel.Create(fundId, today, end, data, temp ?? new()), sc.Requirement ?? "");
        }
        ShareOpenInfos = info;



        SelectedShare = ShareOpenInfos.FirstOrDefault();
        SelectedShareOpenDays = ShareOpenInfos[0].DateInfo;
        //        Logg.Information($"{FundName} 无法获取托管接口，尝试手动计算");


    }


    [RelayCommand]
    public void RefreshOpenDay()
    {
        //await api.QueryOpenDays(new DateOnly(today.Year, today.Month, 1), new DateOnly(today.Year, today.Month + 2, 1).AddDays(-1), sc.Code!);
    }
}

public record ShareOpenInfo(ShareClass Share, OpenDayViewModel[] DateInfo, string Requirement);

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
            AllowPushRedemption = x.OpenRedemption != OpenType.None,
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
                    AllowPushPurchase = false,
                    AllowPushRedemption = false
                });
            }
        }

        list.Sort((x, y) => Comparer<DateOnly>.Default.Compare(x.Date, y.Date));
        // 补空
        for (int i = 0; i < (int)minDate.DayOfWeek; i++)
        {
            result.Insert(0, null!);
        }


        return result.ToArray();
    }
}