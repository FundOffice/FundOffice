using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FMO.Utilities;

namespace FMO.Schedule;

/// <summary>
/// DailyFromMailView.xaml 的交互逻辑
/// </summary>
public partial class DailyFromMailView : UserControl
{
    public DailyFromMailView()
    {
        InitializeComponent();
    }
}





[MissionTitle("净值更新")]
public partial class DailyFromMailViewModel : MissionViewModel<DailyFromMailMission>
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAvailable))]
    public partial string? MailName { get; set; }


    [ObservableProperty]
    public partial int? Interval { get; set; }


    public override bool IsAvailable => MailName?.Length > 5 && MailName.IsMail();

    public DailyFromMailViewModel(DailyFromMailMission m) : base(m)
    {
        Title = "净值更新";

        MailName = m.MailName;
        Interval = m.Interval == 0 ? null : m.Interval;
    }

    [RelayCommand]
    public async Task RebuildData()
    {
        Mission.IgnoreHistory = true;
        await Task.Run(() => Mission.Work());
        Mission.IgnoreHistory = false;
    }
}