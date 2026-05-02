using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FMO.Schedule;

[MissionInfo("TA更新")]
public partial class TAFromMailViewModel : MissionViewModel<TAFromMailMission>
{
    [ObservableProperty]
    public partial string? MailName { get; set; }


    [ObservableProperty]
    public partial int? Interval { get; set; }

    public TAFromMailViewModel(TAFromMailMission m) : base(m)
    {
        Title = "TA更新";

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