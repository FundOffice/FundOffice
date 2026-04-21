using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FMO.Logging;
using Serilog;
using System.ComponentModel;
using System.Reflection;

namespace FMO.Schedule;


public record RemoveMissionMessage(AutomationViewModelBase ViewModel);

public partial class AutomationViewModelBase : ObservableObject, IRecipient<MissionMessage>, IRecipient<MissionProgressMessage>, IRecipient<MissionWorkMessage>
{
    public Type MissionType { get; }

    /// <summary>
    /// 后台任务
    /// </summary>
    //public bool IsViewNotFound { get; init; }

    [ObservableProperty]
    public partial bool IsActivated { get; set; }

    [ObservableProperty]
    public partial string? Title { get; set; }

    [ObservableProperty]
    public partial DateTime? LastRunTime { get; set; }


    [ObservableProperty]
    public partial DateTime? NextRunDate { get; set; }


    [ObservableProperty]
    public partial DateTime? NextRunTime { get; set; }

    [ObservableProperty]
    public partial string? Description { get; set; }

    [ObservableProperty]
    public partial bool ManualSetNextRun { get; set; }

    [ObservableProperty]
    public partial bool IsWorking { get; set; }

    [ObservableProperty]
    public partial double ProgressValue { get; set; }


    [ObservableProperty]
    public partial bool IsLogVisible { get; set; }

    [ObservableProperty]
    public partial string? WorkLog { get; set; }

    public int Id { get; }

    public AutomationViewModelBase(Mission mission)
    {
        WeakReferenceMessenger.Default.RegisterAll(this);
        MissionType = mission.GetType();

        try
        {
            LastRunTime = mission.LastRun;
            NextRunDate = mission.NextRun;
            NextRunTime = mission.NextRun;
            IsActivated = mission.IsEnabled;
        }
        catch (Exception ex) { Log.Error($"无法初始化任务ViewModel{ex.Message}"); }

        Id = mission.Id;
    }



    [RelayCommand]
    public void DoManualSetNextRunTime(bool set)
    {
        if (set && NextRunDate is not null && NextRunTime is not null && NextRunDate.Value.Date.Add(NextRunTime.Value.TimeOfDay) is DateTime t && t > DateTime.Now)
        {
            MissionSchedule.ManualSetNextRun(Id, t);
        }
        else
        {
            using var db = new MissionDatabase();
            var mission = db.GetCollection<Mission>().FindById(Id);
            NextRunDate = mission?.NextRun;
            NextRunTime = mission?.NextRun;
        }

        ManualSetNextRun = false;
    }

    [RelayCommand]
    public void DeleteMission(AutomationViewModelBase mission)
    {
        WeakReferenceMessenger.Default.Send(new RemoveMissionMessage(mission));
    }


    [RelayCommand]
    public void ShowLog()
    {
        using var db = new MissionDatabase();
        WorkLog = string.Join("\n\n", db.GetCollection<MissionRecord>().Find(x => x.MissionId == Id).
            OrderByDescending(x => x.Time).Take(10).Select(x => $"{x.Time}\n{x.Record}"));

        IsLogVisible = WorkLog?.Length > 0; 
    }
    partial void OnNextRunTimeChanged(DateTime? value)
    {

    }

    public void Receive(MissionMessage message)
    {
        if (Id != message.Id) return;

        IsWorking = message.IsWorking;

        if (message.LastRun is not null)
            LastRunTime = message.LastRun.Value;


        if (message.NextRun is not null)
        {
            NextRunDate = message.NextRun;
            NextRunTime = message.NextRun;
        }
    }

    public void Receive(MissionProgressMessage message)
    {
        if (message.Id == Id) ProgressValue = message.Progress;
    }

    public void Receive(MissionWorkMessage message)
    {
        if (Id == message.Id) WorkLog = message.Log;
    }
}


public partial class MissionViewModel<T> : AutomationViewModelBase where T : Mission
{
    protected T Mission { get; set; }


    public virtual bool IsAvailable => true;


    public MissionViewModel(T mission) : base(mission)
    {
        Mission = mission;
    }


    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunOnceCommand))]
    public partial bool CanRunOnce { get; set; } = true;


    //public virtual void AfterRun() { }


    [RelayCommand(CanExecute = nameof(CanRunOnce))]
    public async Task RunOnce()
    {
        CanRunOnce = false;
        await Task.Run(() => Mission.Work());
        CanRunOnce = true;

        //try { AfterRun(); } catch(Exception e) { LogEx.Error(e); }
    }


    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (Mission is not null)
        {
            switch (e.PropertyName)
            {
                case nameof(IsActivated):
                    if (IsActivated != Mission.IsEnabled)
                    {
                        Mission.IsEnabled = IsActivated;
                        NextRunDate = Mission.NextRun;
                        NextRunTime = Mission.NextRun;
                        using var db = new MissionDatabase();
                        db.GetCollection<Mission>().Upsert(Mission);
                    }
                    break;

                default:
                    break;
            }


            try
            {
                if (e.PropertyName is not null && Mission.GetType().GetProperty(e.PropertyName) is PropertyInfo p && p.CanWrite)
                {
                    var v = p.GetValue(Mission);
                    PropertyInfo vmp = GetType().GetProperty(e.PropertyName)!;
                    var vm = vmp.GetValue(this);
                    if (v != vm && vmp.PropertyType.IsAssignableTo(p.PropertyType))
                    {
                        p.SetValue(Mission, vm);
                        MissionSchedule.SaveChanges(Mission);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Save Mission {ex}");
            }


        }
    }
}