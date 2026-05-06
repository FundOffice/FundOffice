using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FMO.Utilities;
using Serilog;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
namespace FMO.Schedule;


public record RemoveMissionMessage(MissionViewModel ViewModel);

public partial class MissionViewModel : ObservableObject, IRecipient<MissionMessage>, IRecipient<MissionProgressMessage>, IRecipient<MissionWorkMessage>
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

    /// <summary>
    /// 展开状态
    /// </summary>
    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    public ObservableCollection<string> Logs { get; private set; } = null!;

    /// <summary>
    /// 后台创建，不可取消，删除
    /// </summary>
    public virtual bool IsBackground => false;

    private readonly SynchronizationContext? _syncContext;

    public int Id { get; }

    public MissionViewModel(Mission mission)
    {
        WeakReferenceMessenger.Default.RegisterAll(this);
        MissionType = mission.GetType();

        try
        {
            LastRunTime = mission.LastRun;
            NextRunTime = mission.NextRun;
            IsActivated = mission.IsEnabled;
        }
        catch (Exception ex) { Log.Error($"无法初始化任务ViewModel{ex.Message}"); }

        Id = mission.Id;

        Title = $"Mission:{Id}";

        //Task.Run(() =>
        //{
        //    using var db = DbHelper.Mission();
        //    var data = db.GetCollection<MissionRecord>().Find(x => x.MissionId == Id).OrderByDescending(x => x.Time).Take(10).Select(x => $"{x.Time}\n{x.Record}").ToArray();
        //    RunOnUIThread(() => Logs = [.. data]);

        //    WorkLog = data.FirstOrDefault();
        //});
        _syncContext = SynchronizationContext.Current;
    }



    [RelayCommand]
    public void DoManualSetNextRunTime(bool set)
    {
        if (set && NextRunTime is not null && NextRunTime.Value.Date.Add(NextRunTime.Value.TimeOfDay) is DateTime t && t > DateTime.Now)
        {
            MissionSchedule.ManualSetNextRun(Id, t);
        }
        else
        {
            using var db = DbHelper.Mission();
            var mission = db.GetCollection<Mission>().FindById(Id);
            NextRunTime = mission?.NextRun;
        }

        ManualSetNextRun = false;
    }

    [RelayCommand]
    public void DeleteMission(MissionViewModel mission)
    {
        WeakReferenceMessenger.Default.Send(new RemoveMissionMessage(mission));
    }


    [RelayCommand]
    public void ShowLog()
    {
        using var db = DbHelper.Mission();
        WorkLog = string.Join("\n\n", db.GetCollection<MissionRecord>().Find(x => x.MissionId == Id).
            OrderByDescending(x => x.Time).Take(10).Select(x => $"{x.Time}\n{x.Record}"));

        IsLogVisible = WorkLog?.Length > 0;
    }

    partial void OnIsExpandedChanged(bool value)
    {
        if(value)
        {
            using var db = DbHelper.Mission();
            var log = db.GetCollection<MissionRecord>().Find(x => x.MissionId == Id).OrderByDescending(x => x.Time).FirstOrDefault();//.Take(10).Select(x => $"{x.Time}\n{x.Record}").ToArray();
            WorkLog = $"{log.Time}\n{log.Record}".Trim();
        }
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
            NextRunTime = message.NextRun;
        }
    }

    public void Receive(MissionProgressMessage message)
    {
        if (message.Id == Id) ProgressValue = message.Progress;
    }

    public void Receive(MissionWorkMessage message)
    {
        if (Id != message.Id)
            return;

        RunOnUIThread(() =>
        {
            Logs.Add(message.Log);
            if (Logs.Count > 10)
                Logs.RemoveAt(0);

            WorkLog = message.Log;
        });

    }

    private void RunOnUIThread(Action action)
    {
        // 如果已经在正确线程 → 直接执行
        if (SynchronizationContext.Current == _syncContext)
        {
            action();
        }
        else
        {
            // 否则 → 自动切回创建线程
            _syncContext?.Post(_ => action(), null);
        }
    }

}


public partial class MissionViewModel<T> : MissionViewModel where T : Mission
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

        if (Mission is not null && e.PropertyName == nameof(IsActivated) && IsActivated != Mission.IsEnabled)
        {
            Mission.IsEnabled = IsActivated;
            NextRunTime = Mission.NextRun;
            MissionSchedule.SaveChanges(Mission);
        }

    }

    public static bool CanValueAssignToProperty(object? value, PropertyInfo targetProp)
    {
        if (targetProp.PropertyType.IsValueType && Nullable.GetUnderlyingType(targetProp.PropertyType) == null)
        {
            // 目标是【不可空值类型】（如 int）
            // 来源不能是 null，且类型能匹配
            return value is not null && targetProp.PropertyType.IsAssignableFrom(value.GetType());
        }

        // 其他情况（引用类型、可空类型）
        return targetProp.PropertyType.IsAssignableFrom(value?.GetType());
    }
}



public partial class OnceMissionViewModel : MissionViewModel
{

    public OnceMissionViewModel(OnceMission mission) : base(mission)
    {
        Title = mission.Name;
        Description = mission.Description ?? "后台任务";
    }

    public override bool IsBackground => true;
}