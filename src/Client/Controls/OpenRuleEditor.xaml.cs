using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FMO.Models;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Data;

namespace FMO;

/// <summary>
/// OpenRuleEditor.xaml 的交互逻辑
/// </summary>
public partial class OpenRuleEditor : Window
{
    public OpenRuleEditor()
    {
        InitializeComponent();
    }
}



public partial class OpenRuleViewModel : ObservableObject
{
    static string[] weekstr = ["一", "二", "三", "四", "五"];

    /// <summary>
    /// 选择季
    /// Year 否则1-4
    /// 其它 忽略
    /// </summary>
    public QuarterInfo[] Quarters { get; }

    /// <summary>
    /// 选择月
    /// Year 否则1-12
    /// QuarterFlag 则1-3
    /// Month Week 忽略
    /// </summary>
    public MonthInfo[] Months { get; }

    public CollectionViewSource MonthSource { get; } = new();

    /// <summary>
    /// 选择周
    /// Year 否则1-54
    /// QuarterFlag 则1-14
    /// Month 1-5
    /// Week 忽略
    /// </summary>
    public WeekInfo[] Weeks { get; set; }
    public CollectionViewSource WeekSource { get; } = new();


    /// <summary>
    /// 选择周
    /// Year 否则1-54
    /// QuarterFlag 则1-14
    /// Month 1-5
    /// Week 忽略
    /// </summary>
    public DateInfo[] Days { get; set; }
    public CollectionViewSource DaySource { get; } = new();


    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowMonthList))]
    [NotifyPropertyChangedFor(nameof(ShowQuarterList))]
    [NotifyPropertyChangedFor(nameof(ShowDayList))]
    [NotifyPropertyChangedFor(nameof(ShowPostpone))]
    [NotifyPropertyChangedFor(nameof(AllowDayOrder))]
    public partial FundOpenType SelectedType { get; set; }

    public static FundOpenType[] Types { get; } = [FundOpenType.Closed, FundOpenType.Daily, FundOpenType.Weekly, FundOpenType.Monthly, FundOpenType.Quarterly, FundOpenType.Yearly,];


    public bool AllowDayOrder => SelectedType != FundOpenType.Weekly || TradeOrNatrual;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AllowDayOrder))]
    [NotifyPropertyChangedFor(nameof(ShowPostpone))]
    public partial bool TradeOrNatrual { get; set; }

    [ObservableProperty]
    public partial bool WeekDescend { get; set; }

    [ObservableProperty]
    public partial bool DayDescend { get; set; }


    [ObservableProperty]
    public partial bool Postpone { get; set; }




    public bool ShowQuarterList => SelectedType switch { FundOpenType.Yearly => true, _ => false };

    public bool ShowMonthList => SelectedType switch { FundOpenType.Yearly or FundOpenType.Quarterly => true, _ => false };

    [ObservableProperty]
    public partial bool ShowWeekList { get; set; } //=> SelectedType switch { FundOpenType.Yearly or FundOpenType.Quarterly => true, FundOpenType.Monthly => TradeOrNatrual ? false : true, _ => false };

    public bool ShowDayList => SelectedType switch { FundOpenType.Daily or FundOpenType.Closed => false, _ => true };

    public bool ShowPostpone => !TradeOrNatrual;

    public string? Statement => Rule.ToString();

    public OpenRule Rule => Build();




    public OpenRuleViewModel()
    {
        Quarters = Enumerable.Range(1, 4).Select(x => new QuarterInfo(x)).ToArray();
        foreach (var item in Quarters)
            item.PropertyChanged += QuarterChanged;


        Months = Enumerable.Range(1, 12).Select(x => new MonthInfo(x)).ToArray();
        MonthSource.Source = Months;
        MonthSource.Filter += MonthSource_Filter;

        foreach (var item in Months)
            item.PropertyChanged += MonthChanged;

        Weeks = [new WeekInfo { Value = -1, Name = "倒序" }, .. Enumerable.Range(1, 54).Select(x => new WeekInfo(x))];

        foreach (var item in Weeks)
            item.PropertyChanged += WeekChanged;
        WeekSource.Source = Weeks;
        WeekSource.Filter += WeekSource_Filter;


        Days = [new DateInfo { Value = -1, Name = "倒序" }, new DateInfo { Value = -1, Name = "交易日" }, new DateInfo { Value = -1, Name = "顺延" }, .. Enumerable.Range(1, 366).Select(x => new DateInfo(x))];
        foreach (var item in Days)
            item.PropertyChanged += DayChanged;
        DaySource.Source = Days;
        DaySource.Filter += DaySource_Filter;


        SelectedYear = DateTime.Today.Year;
    }

    private void MonthSource_Filter(object sender, FilterEventArgs e)
    {
        if (SelectedType == FundOpenType.Quarterly || Quarters.Any(x => x.IsSelected))
            e.Accepted = (e.Item as MonthInfo)!.Value <= 3;
        else if (SelectedType == FundOpenType.Yearly)
            e.Accepted = true;
    }

    private void DaySource_Filter(object sender, FilterEventArgs e)
    {
        if (SelectedType == FundOpenType.Weekly || (Weeks.Any(x => x.IsSelected && x.Value != -1)))
            e.Accepted = (e.Item as DateInfo)!.Value <= 5;
        else if (SelectedType == FundOpenType.Monthly || Months.Any(x => x.IsSelected))
            e.Accepted = (e.Item as DateInfo)!.Value <= 31;
        else if (SelectedType == FundOpenType.Quarterly || Quarters.Any(x => x.IsSelected))
            e.Accepted = (e.Item as DateInfo)!.Value <= 92;
        else if (SelectedType == FundOpenType.Yearly)
            e.Accepted = true;

        if (!e.Accepted) (e.Item as DateInfo)!.IsSelected = false;
    }

    private void WeekSource_Filter(object sender, FilterEventArgs e)
    {
        if (SelectedType == FundOpenType.Monthly || Months.Any(x => x.IsSelected))
            e.Accepted = (e.Item as WeekInfo)!.Value <= 6;
        else if (SelectedType == FundOpenType.Quarterly || Quarters.Any(x => x.IsSelected))
            e.Accepted = (e.Item as WeekInfo)!.Value <= 26;
        else if (SelectedType == FundOpenType.Yearly)
            e.Accepted = true;
    }

    private void QuarterChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        UpdateWeekable();

        MonthSource.View.Refresh();
        WeekSource.View.Refresh();
        DaySource.View.Refresh();


        UpdateRule();
    }

    private void MonthChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        UpdateWeekable();
        WeekSource.View.Refresh();
        DaySource.View.Refresh();

        UpdateRule();
    }

    private void WeekChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        DaySource.View.Refresh();

        UpdateDayName();
        UpdateRule();
    }

    private void DayChanged(object? sender, PropertyChangedEventArgs e)
    {
        UpdateRule();
    }




    public OpenRule Build()
    {
        return new OpenRule
        {
            Type = SelectedType,
            Quarters = Quarters.Where(x => x.IsSelected).Select(x => x.Value).ToArray(),
            Months = Months.Where(x => x.IsSelected).Select(x => x.Value).ToArray(),
            Weeks = Weeks.Skip(1).Where(x => x.IsSelected).Select(x => x.Value).ToArray(),
            WeekOrder = WeekDescend ? SequenceOrder.Descend : SequenceOrder.Ascend,
            Dates = Days.Skip(3).Where(x => x.IsSelected).Select(x => x.Value).ToArray(),
            DayOrder = DayDescend ? SequenceOrder.Descend : SequenceOrder.Ascend,
            TradeOrNatural = TradeOrNatrual,
            Postpone = Postpone,
        };
    }

    public void Init(OpenRule rule)
    {
        SelectedType = rule.Type;
        Postpone = rule.Postpone;
        TradeOrNatrual = rule.TradeOrNatural;
        DayDescend = rule.DayOrder == SequenceOrder.Descend;
        WeekDescend = rule.WeekOrder == SequenceOrder.Descend;
        if (rule.Quarters is not null)
            foreach (var item in Quarters.Where(x => rule.Quarters.Contains(x.Value)))
                item.IsSelected = true;

        if (rule.Months is not null)
            foreach (var item in Months.Where(x => rule.Months.Contains(x.Value)))
                item.IsSelected = true;

        if (rule.Weeks is not null)
            foreach (var item in Weeks.Where(x => rule.Weeks.Contains(x.Value)))
                item.IsSelected = true;

        if (rule.Dates is not null)
            foreach (var item in Days.Where(x => rule.Dates.Contains(x.Value)))
                item.IsSelected = true;
    }


    partial void OnTradeOrNatrualChanged(bool value)
    {
        if (!value && SelectedType == FundOpenType.Weekly)
            DayDescend = false;


        if (value && SelectedType == FundOpenType.Monthly)
            foreach (var w in Weeks)
                w.IsSelected = false;

        if (value)
            Postpone = false;

        UpdateDayName();
        UpdateWeekable();
    }

    partial void OnSelectedTypeChanged(FundOpenType value)
    {
        UpdateWeekable();

        MonthSource.View.Refresh();
        WeekSource.View.Refresh();
        DaySource.View.Refresh();

        UpdateRule();
        UpdateDayName();
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        switch (e.PropertyName)
        {
            case nameof(SelectedType):
            case nameof(WeekDescend):
            case nameof(DayDescend):
            case nameof(TradeOrNatrual):
            case nameof(Postpone):
                UpdateRule();
                break;

            default:
                break;
        }
    }


    public int[] Years { get; set; } = Enumerable.Range(DateTime.Today.Year - 10, 11).ToArray();

    [ObservableProperty]
    public partial int SelectedYear { get; set; }


    [ObservableProperty]
    public partial DayIsOpenViewModel[]? Data { get; set; }

    private void UpdateWeekable()
    {
        if ((SelectedType == FundOpenType.Monthly || Months.Any(x => x.IsSelected) || SelectedType == FundOpenType.Quarterly || Quarters.Any(x => x.IsSelected) || SelectedType == FundOpenType.Yearly) && TradeOrNatrual)
        {
            ShowWeekList = false;
            foreach (var w in Weeks)
                w.IsSelected = false;
        }
        else ShowWeekList = true;
    }

    public void UpdateDayName()
    {
        if (!TradeOrNatrual && (SelectedType == FundOpenType.Weekly || Weeks.Any(x => x.IsSelected)))
        {
            for (int i = 3; i < 8; i++)
                Days[i].Name = "周" + weekstr[i - 3];

        }
        else
        {
            for (int i = 3; i < 8; i++)
                Days[i].Name = $"第{i - 2}日";
        }
    }


    public void UpdateRule()
    {
        OnPropertyChanged(nameof(Statement));
        ConfirmCommand.NotifyCanExecuteChanged();

        var r = Rule.Apply(SelectedYear);

        for (int i = 0; i < Data!.Length; i++)
        {
            Data[i].IsOpen = r[i].Type == OpenType.Fixed;
        }
        // Data = Rule.Apply(SelectedYear).Select(x => new DayIsOpenViewModel { Date = x.Date, IsHoliday = x.Flag.HasFlag(DayFlag.Holiday), IsOpen = x.Type == OpenType.Fixed, IsWeekEnd = x.Flag.HasFlag(DayFlag.Weekend) }).ToArray();
    }


    partial void OnSelectedYearChanged(int value)
    {
        var days = FMO.Models.Days.DayInfosByYear(value);


        Data = days.Select(x => new DayIsOpenViewModel { Date = x.Date, IsWeekEnd = x.Flag.HasFlag(DayFlag.Weekend), IsHoliday = x.Flag.HasFlag(DayFlag.Holiday) }).ToArray();


        var r = Rule.Apply(SelectedYear);

        for (int i = 0; i < Data!.Length; i++)
        {
            Data[i].IsOpen = r[i].Type == OpenType.Fixed;
        }
    }


    public bool CanConfirm => Rule?.IsValid() ?? false;

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    public void Confirm(Window wnd)
    {
        wnd.DialogResult = true;
        wnd.Close();
    }

    public partial class QuarterInfo : ObservableObject
    {
        public required string Name { get; set; }

        public int Value { get; set; }


        [ObservableProperty]
        public partial bool IsSelected { get; set; }

        [SetsRequiredMembers]
        public QuarterInfo(int i)
        {
            Value = i;
            Name = i switch { 1 => "一季度", 2 => "二季度", 3 => "三季度", 4 => "四季度", _ => "Error" };
        }
    }

    public partial class MonthInfo : ObservableObject
    {
        static string[] array = ["一", "二", "三", "四", "五", "六", "七", "八", "九", "十", "十一", "十二"];

        [SetsRequiredMembers]
        public MonthInfo(int month)
        {
            Value = month;
            Name = $"{array[month - 1]}月";
        }


        public required string Name { get; set; }

        public int Value { get; set; }

        public DateTime Start { get; set; }

        public DateTime End { get; set; }

        [ObservableProperty]
        public partial bool IsSelected { get; set; }


    }



    public partial class WeekInfo : ObservableObject
    {

        [SetsRequiredMembers]
        public WeekInfo(int w)
        {
            Value = w;
            Name = $"第{w}周";
        }

        public WeekInfo() { }


        public required string Name { get; set; }

        public int Value { get; set; }

        [ObservableProperty]
        public partial bool IsSelected { get; set; }
    }


    public partial class DateInfo : ObservableObject
    {

        [SetsRequiredMembers]
        public DateInfo(int day)
        {
            Value = day;
            Name = $"第{day}日";
        }

        public DateInfo() { }


        public required string Name { get; set; }

        public int Value { get; set; }

        [ObservableProperty]
        public partial bool IsSelected { get; set; }
    }
}









public partial class DayIsOpenViewModel : ObservableObject, IDate
{
    public DateOnly Date { get; set; }


    public bool IsWeekEnd { get; set; }

    public bool IsHoliday { get; set; }

    [ObservableProperty]
    public partial bool IsOpen { get; set; }
}

public partial class DayOfWeekInfo : ObservableObject
{
    static string[] array = ["一", "二", "三", "四", "五"];

    [SetsRequiredMembers]
    public DayOfWeekInfo(DayOfWeek value)
    {
        Value = value;
        Name = array[(int)value - 1];
    }

    public required string Name { get; set; }

    public DayOfWeek Value { get; set; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}