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

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RebuildDataCommand))]
    public partial bool RedoAll { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RebuildDataCommand))]
    public partial int? RedoCount { get; set; }

    public bool CanRedo => RedoAll || RedoCount is > 0;

    public DisclosureFromMailViewModel(DisclosureFromMailMission m) : base(m)
    {
        Title = "信披更新";

        MailName = m.MailName;
        Interval = m.Interval == 0 ? null : m.Interval;

        _initialized = true;
    }



    [RelayCommand(CanExecute = nameof(CanRedo))]
    public async Task RebuildData()
    {
        Mission.Param = new(true, RedoAll, RedoCount ?? 0);
        await Task.Run(() => Mission.Work());
        Mission.Param = null;
    }
}