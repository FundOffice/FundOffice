using CommunityToolkit.Mvvm.ComponentModel;
using FMO.Trustee;
using LiteDB;
using System.Windows;

namespace FMO;

/// <summary>
/// TrusteeLogViewerWindow.xaml 的交互逻辑
/// </summary>
public partial class TrusteeLogViewerWindow : Window
{
    public TrusteeLogViewerWindow()
    {
        InitializeComponent();
    }
}


public partial class TrusteeLogViewerWindowViewModel : ObservableObject
{
    private ILiteDatabase _db { get; } = new LiteDatabase(@$"FileName=data\platformlog.db;Connection=Shared");

    public TrusteeLogViewerWindowViewModel()
    {
        Trustees = TrusteeGallay.Trustees.Select(x => new TrusteeInfo(x.Title, x.Identifier)).ToArray();
        Functions = _db.GetCollection<TrusteeCallHistory>().Query().Select(x => x.Method).ToList().Distinct().ToArray();


    }

    [ObservableProperty]
    public partial TrusteeInfo[] Trustees { get; set; } = [];


    [ObservableProperty]
    public partial string[] Functions { get; set; } = [];


    [ObservableProperty]
    public partial TrusteeCallHistory[] Logs { get; set; } = [];


    [ObservableProperty]
    public partial IEnumerable<TrusteeCallHistory>? LogsByDate { get; set; } = null;



    [ObservableProperty]
    public partial TrusteeInfo? SelectedTrustee { get; set; }

    [ObservableProperty]
    public partial string? SelectedFunction { get; set; }

    [ObservableProperty]
    public partial DateTime? SelectedDate { get; set; }

    [ObservableProperty]
    public partial TrusteeCallHistory? SelectedLog { get; set; }


    [ObservableProperty]
    public partial DateTime[] Dates { get; set; } = [];

    [ObservableProperty]
    public partial TimeSpan[] Times { get; set; } = [];

    partial void OnSelectedFunctionChanged(string? value)
    {
        UpdateLogs();
    }

    partial void OnSelectedTrusteeChanged(TrusteeInfo? value)
    {
        UpdateLogs();
    }

    private void UpdateLogs()
    {
        if (SelectedTrustee is null || string.IsNullOrWhiteSpace(SelectedFunction))
            Logs = [];
        else
            Logs = _db.GetCollection<TrusteeCallHistory>().Find(x => x.Identifier == SelectedTrustee.Idenntifier && x.Method == SelectedFunction).OrderByDescending(x => x.Time).Take(400).ToArray();

        Dates = Logs.Select(x => x.Time.Date).Distinct().ToArray();

        OnSelectedDateChanged(Dates.FirstOrDefault());
        SelectedLog = null;
        //Times = Logs.Select(x => x.Time.TimeOfDay).Distinct().ToArray();
    }

    partial void OnSelectedDateChanged(DateTime? value)
    {
        if (value is null) LogsByDate = null;
        else LogsByDate = Logs.Where(x => x.Time.Date == value);
    }


    public record TrusteeInfo(string Name, string Idenntifier);
}