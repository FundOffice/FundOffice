using CommunityToolkit.Mvvm.Messaging;

using FMO.Models;
using FMO.Utilities;
using MoT;

namespace FMO.Schedule;

/// <summary>
/// 任务
/// </summary>
public abstract class Mission
{
    public int Id { get; set; }


    public DateTime? LastRun { get; set; }

    public DateTime? NextRun { get; set; }

    public bool IsEnabled { get => field; set { field = value; if (value) SetNextRun(); } }

    public bool IsWorking { get; private set; }

    /// <summary>
    /// 废弃
    /// </summary>
    public bool IsAborted { get; set; }



    //private string? _log;

    //public string? WorkLog { get => _log; protected set { _log = value; WeakReferenceMessenger.Default.Send(new MissionWorkMessage(Id, value ?? "")); } }


    public async Task OnTime(DateTime time)
    {
        if (!IsEnabled || IsWorking || NextRun is null) return;

        if (time < NextRun) return;

        if (LastRun is not null && NextRun < LastRun)
        {
            SetNextRun();
            if (NextRun < LastRun)
            {
                IsEnabled = false;
                return;
            }
        }

        // 设为永不执行
        NextRun = DateTime.MaxValue;
        await Work();
    }

    public async Task<ErrorReturn> Work()
    {
        IsWorking = true;
        WeakReferenceMessenger.Default.Send(new MissionMessage { Id = Id, IsWorking = true });
        ErrorReturn r;

        var log = "";
        DateTime now = DateTime.Now;

        try
        {
            r = await WorkOverride();
            log = r.Error;
            LastRun = now;

            // 已废弃 或 一次性任务，完成就释放
            if (IsAborted || (this is OnceMission mm && mm.IsFinished))
                MissionSchedule.Unregister(Id);
            else //if (r.Successed) 
                SetNextRun();
        }
        catch (Exception e)
        {
            log += $"Error {e}";
            r = new(false, $"Mission Error {Id} {e}");
            Logg.Error(e, $"Mission Error {Id}");

            WeakReferenceMessenger.Default.Send(new MissionFailedMessage(Id, e));
            //TodoService.Register(new JustNotifyTodo { CreateTime = now, Message = $"Mission Error {Id} {e}" });
            //WeakReferenceMessenger.Default.Send(new ToastMessage(ToastLevel.Error, $"[{Id}]任务执行出错，请查看log"));
        }


        MissionRecord rec = new() { MissionId = Id, Record = log ?? "", Time = now };
        using (var db = DbHelper.Mission())
        {
            db.GetCollection<Mission>().Upsert(this);
            db.GetCollection<MissionRecord>().Insert(rec);
        }

        IsWorking = false;
        WeakReferenceMessenger.Default.Send(new MissionMessage { Id = Id, IsWorking = false, LastRun = LastRun, NextRun = NextRun });
        WeakReferenceMessenger.Default.Send(new MissionWorkMessage(Id, log ?? "", now));
        return r;
    }

    /// <summary>
    /// 返回false表示执行失败，且不会重试
    /// true可以继续执行下一次
    /// </summary>
    /// <returns></returns>
    protected abstract Task<ErrorReturn> WorkOverride();


    protected virtual void SetNextRun()
    {
        NextRun = DateTime.MaxValue;
    }

    public virtual void Init()
    {
        SetNextRun();
    }

    /// <summary>
    /// 因为缺少配置，设为不再运行
    /// </summary>
    protected void SetUnavaliable()
    {
        IsEnabled = false;
        WeakReferenceMessenger.Default.Send(this);
    }

    //protected void SendLog(string log) { Debug.WriteLine(log); WeakReferenceMessenger.Default.Send(new MissionWorkMessage(Id, log)); }

}

/// <summary>
/// 一次性
/// </summary>
public abstract class OnceMission : Mission
{
    public bool IsFinished { get; protected set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    public OnceMission()
    {
        IsEnabled = true;
    }

}