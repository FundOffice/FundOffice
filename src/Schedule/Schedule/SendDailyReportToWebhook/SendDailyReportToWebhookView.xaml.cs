using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using static FMO.Schedule.SendDailyReportToWebhookMission;

namespace FMO.Schedule;

/// <summary>
/// SendDailyReportToWebhookView.xaml 的交互逻辑
/// </summary>
public partial class SendDailyReportToWebhookView : UserControl
{
    public SendDailyReportToWebhookView()
    {
        InitializeComponent();
    }
}


[MissionTitle("发送日报")]
public partial class SendDailyReportToWebhookViewModel : MissionViewModel<SendDailyReportToWebhookMission>
{
    [ObservableProperty]
    TimeOnly _time;

    [ObservableProperty]
    ObservableCollection<WebHookDsp> _webHooks;


    public SendDailyReportToWebhookViewModel(SendDailyReportToWebhookMission mission) : base(mission)
    {
        Title = "发送日报";

        Time = Mission!.Time;
        if (!Mission.WebHooks.Any())
        {
            Mission.WebHooks.Add(new());
            MissionSchedule.SaveChanges(Mission);
        }
        WebHooks = new ObservableCollection<WebHookDsp>(Mission.WebHooks.Select(x => new WebHookDsp { IsEnabled = x.IsEnabled, Url = x.Url }));
        foreach (var x in WebHooks)
        {
            x.PropertyChanged += OnItemChanged;
        }
    }


    private void OnItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        Mission.WebHooks = WebHooks.Select(x => new WebHookInfo(x.IsEnabled, x.Url)).ToList();
        MissionSchedule.SaveChanges(Mission);
    }

    public partial class WebHookDsp : ObservableObject
    {
        [ObservableProperty]
        bool _isEnabled;

        [ObservableProperty]
        string? _Url;
    }
}

public class DateTimeToTimeOnlyConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            DateTime t => TimeOnly.FromDateTime(t),
            TimeOnly t => new DateTime(DateOnly.MinValue, t),
            _ => value
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            DateTime t => TimeOnly.FromDateTime(t),
            TimeOnly t => new DateTime(DateOnly.MinValue, t),
            _ => value
        };
    }
}