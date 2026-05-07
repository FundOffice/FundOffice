using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FMO.Disclosure;
using FMO.Models;
using FMO.Utilities;
using HandyControl.Controls;
using LiteDB;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Data;

namespace FMO;

/// <summary>
/// DisclosurePage.xaml 的交互逻辑
/// </summary>
public partial class DisclosurePage : UserControl
{
    public DisclosurePage()
    {
        InitializeComponent();
    }
}


public partial class DisclosurePageViewModel : ObservableObject
{
    public DisclosurePageViewModel()
    {
        using var db = DbHelper.Base();
        var begin = db.GetCollection<Fund>().Find(x => x.SetupDate != default).Min(x => x.SetupDate).Year;
        var end = DateTime.Now.Year;
        Years = Enumerable.Range(begin, end - begin + 1).Reverse().ToArray();

        Months = Enumerable.Range(1, 12).Reverse().ToArray();

        debouncer = new Debouncer(() => Update());

        clearDateMap = db.GetCollection<Fund>().Query().Select(x => new { x.Code, x.ClearDate, x.Status }).ToArray().ToDictionary(x => x.Code!, x => x.Status == FundStatus.Normal ? DateOnly.MaxValue : x.ClearDate);
        MonthlySource.Filter += (s, e) => e.Accepted = Filter(e.Item as PeriodicReportViewModel);
        QuarterlySource.Filter += (s, e) => e.Accepted = Filter(e.Item as PeriodicReportViewModel);
        SemiAnnualSource.Filter += (s, e) => e.Accepted = Filter(e.Item as PeriodicReportViewModel);
        AnnualSource.Filter += (s, e) => e.Accepted = Filter(e.Item as PeriodicReportViewModel);
    }

    private bool Filter(PeriodicReportViewModel? v)
    {
        return !FilterClearedFund || (v is not null && clearDateMap.TryGetValue(v.Code!, out var date) && date > v.PeriodEnd);
    }

    public int[] Years { get; set; }

    public Dictionary<string, DateOnly> clearDateMap { get; set; }

    public FundReportType[] Types { get; } = [FundReportType.MonthlyReport, FundReportType.QuarterlyReport, FundReportType.QuarterlyUpdate, FundReportType.AnnualReport];


    [ObservableProperty]
    public partial FundReportType SelectedType { get; set; }

    [ObservableProperty]
    public partial int[] Months { get; set; }

    [ObservableProperty]
    public partial int? SelectedYear { get; set; }


    [ObservableProperty]
    public partial int? SelectedMonth { get; set; }

    [ObservableProperty]
    public partial bool FilterClearedFund { get; set; } = true;

    public CollectionViewSource MonthlySource { get; } = new();

    public CollectionViewSource QuarterlySource { get; } = new();


    public CollectionViewSource SemiAnnualSource { get; } = new();

    public CollectionViewSource AnnualSource { get; } = new();


    public CollectionViewSource QuarterlyUpdateSource { get; } = new();


    public CollectionViewSource TemporaryNoticeSource { get; } = new();


    [ObservableProperty]
    public partial ObservableCollection<TemporaryNoticeViewModel> TemporaryNotices { get; set; }


    private Debouncer debouncer;

    partial void OnSelectedYearChanged(int? value)
    {
        if (value is null) Months = [];
        else if (value == DateTime.Now.Year)
            Months = Enumerable.Range(1, DateTime.Now.Month).Reverse().ToArray();
        else
            Months = Enumerable.Range(1, 12).Reverse().ToArray();

        SelectedMonth = Months.FirstOrDefault();
        debouncer.Invoke();
    }

    partial void OnSelectedMonthChanged(int? value)
    {
        debouncer.Invoke();
    }

    partial void OnSelectedTypeChanged(FundReportType value) => debouncer.Invoke();

    partial void OnFilterClearedFundChanged(bool oldValue, bool newValue)
    {
        MonthlySource.View.Refresh();
        QuarterlySource.View.Refresh();
        SemiAnnualSource.View.Refresh();
        AnnualSource.View.Refresh();
    }

    private void Update()
    {
        var pe = new DateOnly(SelectedYear!.Value, SelectedMonth!.Value, 1).AddMonths(1).AddDays(-1);

        using var db = DbHelper.Base();

        List<IDisclosureNotice> disclosureNotices = db.GetCollection<IDisclosureNotice>().Query().Where("ReportDate.DayNumber=@0", pe.DayNumber).ToList();
        var reports = disclosureNotices.OfType<PeriodicalDisclosureNotice>().ToList();

        var updates = disclosureNotices.OfType<QuarterlyUpdate>().ToList();

        // 其它报告
        var otherNotice = db.GetCollection<IDisclosureNotice>().Query().Where(x => x.PublishDate.Year == SelectedYear.Value && x.PublishDate.Month == SelectedMonth.Value).
            ToList().Where(x => x is not PeriodicalDisclosureNotice && x is not QuarterlyUpdate).ToList();

        var noticeIds = reports.Select(x => x.Id).Concat(updates.Select(x => x.Id)).Concat(otherNotice.Select(x => x.Id)).ToArray();

        var workflows = DisclosureService.GetWorkflows().Where(x => x.IsEnabled && !string.IsNullOrWhiteSpace(x.Channel)).ToArray().ToLookup(x => x.Type);

        var run = db.GetCollection<DisclosureInstance>().Query().Where(Query.In(nameof(DisclosureInstance.NoticeId), noticeIds.Select(x => new BsonValue(x)))).ToArray().ToLookup(x => x.NoticeId);



        if (SelectedMonth % 3 == 0)
        {
            // 补全没有的季度更新
            var lack = db.GetCollection<Fund>().Find(x => x.Status == FundStatus.Normal || x.ClearDate > pe).ToList().ExceptBy(updates.Select(x => x.FundId), x => x.Id);
            foreach (var item in lack)
            {
                var v = new QuarterlyUpdate
                {
                    FundId = item.Id,
                    FundCode = item.Code!,
                    FundName = item.Name,
                    Name = $"{item.Name}_季度更新_{pe}",
                    ReportDate = pe
                };
                updates.Add(v);
                db.GetCollection<IDisclosureNotice>().Insert(v);
            }
        }

        var vm = reports.Select(x => new PeriodicReportViewModel(x, workflows[x.Type], run[x.Id])).ToArray();

        App.Current.Dispatcher.InvokeAsync(() =>
        {
            MonthlySource.Source = vm.Where(x => x.Type == DisclosureType.Monthly);
            QuarterlySource.Source = vm.Where(x => x.Type == DisclosureType.Quarterly);
            SemiAnnualSource.Source = vm.Where(x => x.Type == DisclosureType.SemiAnnually);
            AnnualSource.Source = vm.Where(x => x.Type == DisclosureType.Annually);
            QuarterlyUpdateSource.Source = updates.Select(x => new QuarterlyUpdateViewModel(x, workflows[x.Type], run[x.Id]));


            TemporaryNotices = [.. otherNotice.OfType<IFundDisclosureNotice>().Select(x => TemporaryNoticeViewModel.Create(x, workflows[x.Type], run[x.Id]))];
            TemporaryNoticeSource.Source = TemporaryNotices;
        });


    }

    private void UpdateTemporary()
    {
        if (SelectedYear is null || SelectedMonth is null) return;

        using var db = DbHelper.Base();

        // 其它报告
        var otherNotice = db.GetCollection<IDisclosureNotice>().Query().Where(x => x.PublishDate.Year == SelectedYear.Value && x.PublishDate.Month == SelectedMonth.Value).
            ToList().Where(x => x is not PeriodicalDisclosureNotice && x is not QuarterlyUpdate).ToList();




        var workflows = DisclosureService.GetWorkflows().Where(x => x.IsEnabled && !string.IsNullOrWhiteSpace(x.Channel)).ToArray().ToLookup(x => x.Type);

        var run = db.GetCollection<DisclosureInstance>().Query().Where(Query.In(nameof(DisclosureInstance.NoticeId), otherNotice.Select(x => new BsonValue(x.Id)))).ToArray().ToLookup(x => x.NoticeId);


        App.Current.Dispatcher.InvokeAsync(() =>
        {
            TemporaryNotices = [.. otherNotice.OfType<IFundDisclosureNotice>().Select(x => TemporaryNoticeViewModel.Create(x, workflows[x.Type], run[x.Id]))];

        });

    }



    [RelayCommand]
    public void Configure()
    {
        var wnd = new ConfigureDisclosureWorkflowWindow();
        wnd.Owner = App.Current.MainWindow;
        wnd.ShowDialog();

        debouncer.Invoke();
    }


    [RelayCommand]
    public async Task GenerateTemporaryOpen()
    {
        var pe = new DateOnly(SelectedYear!.Value, SelectedMonth!.Value, 1);

        using var db = DbHelper.Base();
        var funds = db.GetCollection<Fund>().Query().Where(x => x.Status == FundStatus.Normal || x.ClearDate >= pe).ToArray();

        var dc = new AddTemporaryOpenWindowViewModel(funds)
        {
            OpenDate = DateTime.Now,
        };
        var wnd = new AddTemporaryOpenWindow();
        wnd.DataContext = dc;
        wnd.Owner = App.Current.MainWindow;
        if (wnd.ShowDialog() != true || dc.SelectedFund is null) return;

        TemporaryOpenNotice notice = new()
        {
            FundId = dc.SelectedFund.Id,
            FundCode = dc.SelectedFund.Code ?? "",
            FundName = dc.SelectedFund.Name,
            AllowPurchase = dc.AllowBuy,
            AllowRedemption = dc.AllowSell,
            OpenDay = DateOnly.FromDateTime(dc.OpenDate),

            PublishDate = DateOnly.FromDateTime(dc.PublishTime),
            PublishTime = TimeOnly.FromDateTime(dc.PublishTime)
        };

        DataTracker.OnNewNotice(notice);

        await Task.Delay(500);
        var workflows = DisclosureService.GetWorkflows().Where(x => x.IsEnabled && x.Type == notice.Type);

        var run = db.GetCollection<DisclosureInstance>().Query().Where(x => x.NoticeId == notice.Id).ToArray();

        await App.Current.Dispatcher.InvokeAsync(() => TemporaryNotices.Add(TemporaryNoticeViewModel.Create(notice, workflows, run)));
    }



    [RelayCommand]
    public async Task GenerateHugeRedemptionNotice()
    {
        var pe = new DateOnly(SelectedYear!.Value, SelectedMonth!.Value, 1);

        using var db = DbHelper.Base();
        var funds = db.GetCollection<Fund>().Query().Where(x => x.Status == FundStatus.Normal || x.ClearDate >= pe).ToArray();

        var dc = new AddHugeRedemptionNoticeWindowViewModel(funds)
        {
            OpenDate = DateTime.Now,
        };
        var wnd = new AddHugeRedemptionNoticeWindow();
        wnd.DataContext = dc;
        wnd.Owner = App.Current.MainWindow;
        if (wnd.ShowDialog() != true || dc.SelectedFund is null) return;

        HugeRedemptionNotice notice = new()
        {
            FundId = dc.SelectedFund.Id,
            FundCode = dc.SelectedFund.Code ?? "",
            FundName = dc.SelectedFund.Name,
            OpenDay = DateOnly.FromDateTime(dc.OpenDate),
            RealRatio = (dc.RealRatio ?? 0) /100,
            DefinedRatio = (dc.DefinedRatio ?? 0) /100,
            PublishDate = DateOnly.FromDateTime(dc.PublishTime),
            PublishTime = TimeOnly.FromDateTime(dc.PublishTime)
        };

        DataTracker.OnNewNotice(notice);

        await Task.Delay(1000);
        var workflows = DisclosureService.GetWorkflows().Where(x => x.IsEnabled && x.Type == notice.Type);

        var run = db.GetCollection<DisclosureInstance>().Query().Where(x => x.NoticeId == notice.Id).ToArray();

        await App.Current.Dispatcher.InvokeAsync(() => TemporaryNotices.Add(TemporaryNoticeViewModel.Create(notice, workflows, run)));
    }













    [RelayCommand]
    public void DeleteTemporyNotice(TemporaryNoticeViewModel notice)
    {
        DisclosureService.RemoveNotice(notice.Id);

        TemporaryNotices.Remove(notice);
    }


    [RelayCommand]
    public void Delete(object obj)
    {
        switch (obj)
        {
            case TemporaryFundNoticeViewModel v:
                if (MessageBox.Show(new HandyControl.Data.MessageBoxInfo
                {
                    Caption = "是否删除临时报告",
                    Message = $" {v.Name}",
                    Button = System.Windows.MessageBoxButton.YesNo
                }) == System.Windows.MessageBoxResult.No)
                    return;

                DisclosureService.RemoveNotice(v.Id);
                UpdateTemporary();
                break;

            default:
                break;
        }
    }
}

