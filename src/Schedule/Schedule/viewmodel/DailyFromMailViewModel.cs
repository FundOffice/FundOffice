using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FMO.Utilities;

namespace FMO.Schedule;

[MissionInfo("净值更新")]
public partial class DailyFromMailViewModel : MissionViewModel<DailyFromMailMission>
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAvailable))]
    public partial string? MailName { get; set; }


    [ObservableProperty]
    public partial int? Interval { get; set; }


    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RebuildDataCommand))]
    public partial bool RedoAll { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RebuildDataCommand))]
    public partial int? RedoCount { get; set; }

    public override bool IsAvailable => MailName?.Length > 5 && MailName.IsMail();



    public DailyFromMailViewModel(DailyFromMailMission m) : base(m)
    {
        Title = "净值更新";

        MailName = m.MailName;
        Interval = m.Interval == 0 ? null : m.Interval;
    }
    public bool CanRedo => RedoAll || RedoCount is > 0;

    [RelayCommand(CanExecute = nameof(CanRedo))]
    public async Task RebuildData()
    {
        Mission.Param = new(true, RedoAll, RedoCount ?? 0);
        await Task.Run(() => Mission.Work());
        Mission.Param = null;
    }
}