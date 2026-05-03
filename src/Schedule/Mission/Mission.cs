using CommunityToolkit.Mvvm.Messaging;
using FMO.Models;
using FMO.Todo;
using FMO.Utilities;
using Serilog;
using System.Diagnostics.CodeAnalysis;

namespace FMO.Schedule;

/// <summary>
/// 任务
/// </summary>
public abstract class Mission
{
    public int Id { get; set; }

    public DateTime? LastRun { get; set; }

    public DateTime? NextRun { get; set; }


    bool _isEnabled;
    public bool IsEnabled { get => _isEnabled; set { _isEnabled = value; if (value) SetNextRun(); } }

    public bool IsWorking { get; private set; }


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
            LastRun = now;
            if (r.Successed) SetNextRun();
        }
        catch (Exception e)
        {
            log += $"Error {e}";
            r = new(false, $"Mission Error {Id} {e}");
            Log.Error($"Mission Error {Id} {e}");

            TodoService.Register(new JustNotifyTodo { CreateTime = now , Message = $"Mission Error {Id} {e}" });
            //WeakReferenceMessenger.Default.Send(new ToastMessage(LogLevel.Error, $"[{Id}]任务执行出错，请查看log"));
        }


        MissionRecord rec = new() { MissionId = Id, Record = log, Time = now };
        using (var db = DbHelper.Mission())
        {
            db.GetCollection<Mission>().Upsert(this);
            db.GetCollection<MissionRecord>().Insert(rec);
        }

        IsWorking = false;
        WeakReferenceMessenger.Default.Send(new MissionMessage { Id = Id, IsWorking = false, LastRun = LastRun, NextRun = NextRun });
        WeakReferenceMessenger.Default.Send(new MissionWorkMessage(Id, log, now));
        return r;
    }

    /// <summary>
    /// 返回false表示执行失败，且不会重试
    /// true可以继续执行下一次
    /// </summary>
    /// <returns></returns>
    protected virtual async Task<ErrorReturn> WorkOverride()
    {
        return await Task.FromResult(new ErrorReturn(true));
    }

    protected virtual void SetNextRun()
    {
        NextRun = DateTime.MaxValue;
    }

    public virtual void Init()
    {
        SetNextRun();
    }


    //protected void SendLog(string log) { Debug.WriteLine(log); WeakReferenceMessenger.Default.Send(new MissionWorkMessage(Id, log)); }

}

public class MissionInfoAttribute : Attribute
{
    [SetsRequiredMembers]
    public MissionInfoAttribute(string name, string description = "")
    {
        Title = name;
        Description = description;
    }

    public required string Title { get; set; }

    public string Description { get; }
}







public struct MissionRecord
{
    public int MissionId { get; set; }

    public DateTime Time { get; set; }

    public string Record { get; set; }
}
