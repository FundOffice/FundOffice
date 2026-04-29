using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FMO.AMAC.Direct;
using FMO.Disclosure;
using FMO.ESigning.MeiShi;
using FMO.Logging;
using FMO.Models;
using FMO.Utilities;
using LiteDB;
using System.IO;
using System.IO.Compression;
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

    private Debouncer debouncer;

    partial void OnSelectedYearChanged(int? value)
    {
        if (value is null) Months = [];
        else if (value == DateTime.Now.Year)
            Months = Enumerable.Range(1, DateTime.Now.Month - 1).Reverse().ToArray();
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


        var noticeIds = reports.Select(x => x.Id).Concat(updates.Select(x => x.Id)).ToArray();

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
            QuarterlyUpdateSource.Source = updates.Select(x => new QuarterlyUpdateViewModel(x, workflows[x.Type], run[x.Id], db.GetCollection<AmacProcessResult>().FindById(x.Id)));
        });

    }


  


    //[RelayCommand]
    //public async Task UploadQuarterlyReportToMeiShi()
    //{
    //    MeiShiAssit assit = new();

    //    foreach (var item in QuarterlySource.View)
    //    {
    //        if (item is FundPeriodicReportViewModel v)
    //        {
    //            string[] quarters = ["一", "二", "三", "四"];
    //            string q = quarters[(v.PeriodEnd.Month - 1) / 3];
    //            if (v.Pdf?.Meta is not null)
    //            {
    //                File.Copy(@$"files\hardlink\{v.Pdf.Meta.Id}", @$"temp\{v.FundName}-{SelectedYear}年{q}季度报告.pdf", true);
    //                try
    //                {
    //                    bool r = await assit.UploadDisclosureFile(v.FundName!, v.Code!, "", DateTime.Now, $"{v.FundName}-{SelectedYear}年{q}季度报告", @$"temp\{v.FundName}-{SelectedYear}年{q}季度报告.pdf");
    //                    if (!r) WeakReferenceMessenger.Default.Send(new ToastMessage(LogLevel.Warning, $"Failed to upload {v.FundName} - {SelectedYear}年{q}季度报告"));
    //                }
    //                catch (Exception e)
    //                {
    //                    WeakReferenceMessenger.Default.Send(new ToastMessage(LogLevel.Warning, $"Failed to upload {v.FundName} - {SelectedYear}年{q}季度报告"));
    //                }
    //            }
    //        }
    //    }
    //}


    [RelayCommand]
    public void Configure()
    {
        var wnd = new ConfigureDisclosureWorkflowWindow();
        wnd.Owner = App.Current.MainWindow;
        wnd.ShowDialog();

        debouncer.Invoke();
    }
}