using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FMO.Schedule;

[MissionInfo("信披更新")]
public partial class DisclosureFromMailViewModel : MissionViewModel<DisclosureFromMailMission>
{
    [ObservableProperty] 
    public partial string? MailName { get; set; }


    [ObservableProperty]
    public partial int? Interval { get; set; }


    [ObservableProperty]
    public partial DateTime StartDate { get; set; }


    public DisclosureFromMailViewModel(DisclosureFromMailMission m) : base(m)
    {
        Title = "信披更新";

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