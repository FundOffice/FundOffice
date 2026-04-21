using CommunityToolkit.Mvvm.Messaging;
using FMO.Models;
using Serilog;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Windows.Controls;

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


    private string? _log;
    public string? WorkLog { get => _log; protected set { _log = value; WeakReferenceMessenger.Default.Send(new MissionWorkMessage(Id, value ?? "")); } }

    public void OnTime(DateTime time)
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
        IsWorking = true;
        Task.Run(() => Work());
    }

    public bool Work()
    {
        IsWorking = true;
        WeakReferenceMessenger.Default.Send(new MissionMessage { Id = Id, IsWorking = true });
        var r = false;
        try
        {
            WorkLog = "";
            DateTime now = DateTime.Now;
            try
            {
                r = WorkOverride();
                LastRun = now;
                if (r) SetNextRun();
            }
            catch (Exception e)
            {
                WorkLog = $"Error {e}";
                Log.Error($"Mission Error {Id} {e}");
                WeakReferenceMessenger.Default.Send(new ToastMessage(LogLevel.Error, $"[{Id}]任务执行出错，请查看log"));
            }

            using (var db = new MissionDatabase())
            {
                db.GetCollection<Mission>().Upsert(this);
                db.GetCollection<MissionRecord>().Insert(new MissionRecord { MissionId = Id, Record = WorkLog, Time = now });
            }
        }
        catch (Exception e)
        {
            Log.Error($"Work {e}");
        }

        IsWorking = false;
        WeakReferenceMessenger.Default.Send(new MissionMessage { Id = Id, IsWorking = false, LastRun = LastRun, NextRun = NextRun });
        return r;
    }


    protected virtual bool WorkOverride()
    {
        return true;
    }

    protected virtual void SetNextRun()
    {
        NextRun = DateTime.MaxValue;
    }

    public virtual void Init()
    {
        SetNextRun();
    }


    protected void SendLog(string log) { Debug.WriteLine(log); WeakReferenceMessenger.Default.Send(new MissionWorkMessage(Id, log)); }

}

public class MissionTitleAttribute : Attribute
{
    [SetsRequiredMembers]
    public MissionTitleAttribute(string v)
    {
        Title = v;
    }

    public required string Title { get; set; }
}



public class MissionTemplate
{
    // public required string Id { get; init; }

    public required string Title { get; init; }



    public required Func<Mission, AutomationViewModelBase> CreateViewModel { get; init; }

    public required Func<Mission> CreateMission { get; init; }

    public required Func<UserControl> CreateView { get; init; }
}

public interface IMissionTemplateProvider
{

    public IList<MissionTemplate> Templates { get; }
}




public static class MissionTemplateManager
{

    public static ConcurrentDictionary<Type, MissionTemplate> Templates { get; } = new();

    public static MissionTemplate CacheMailMissionTemplate { get; }

    static MissionTemplateManager()
    {
        Register(typeof(DailyFromMailMission), new MissionTemplate
        {
            Title = "净值更新",
            CreateMission = () => new DailyFromMailMission(),
            CreateView = () => new DailyFromMailView(),
            CreateViewModel = x => new DailyFromMailViewModel((x as DailyFromMailMission)!)
        });

        Register(typeof(MailCacheMission), new MissionTemplate
        {
            Title = "邮件缓存",
            CreateMission = () => new MailCacheMission(),
            CreateView = () => new MailCacheView(),
            CreateViewModel = x => new MailCacheViewModel((x as MailCacheMission)!)
        });
        Register(typeof(SendDailyReportToWebhookMission), new MissionTemplate
        {
            Title = "发送日报",
            CreateMission = () => new SendDailyReportToWebhookMission(),
            CreateView = () => new SendDailyReportToWebhookView(),
            CreateViewModel = x => new SendDailyReportToWebhookViewModel((x as SendDailyReportToWebhookMission)!)
        });
        Register(typeof(TAFromMailMission), new MissionTemplate
        {
            Title = "TA更新",
            CreateMission = () => new TAFromMailMission(),
            CreateView = () => new TAFromMailView(),
            CreateViewModel = x => new TAFromMailViewModel((x as TAFromMailMission)!)
        });

        Register(typeof(DisclosureFromMailMission), new MissionTemplate
        {
            Title = "信披更新",
            CreateMission = () => new DisclosureFromMailMission(),
            CreateView = () => new DisclosureFromMailView(),
            CreateViewModel = x => new DisclosureFromMailViewModel((x as DisclosureFromMailMission)!)
        });



        CacheMailMissionTemplate = Templates[typeof(MailCacheMission)];



    }


    public static void Register(Type type, MissionTemplate template)
    {
        Templates[type] = template;
    }

    public static AutomationViewModelBase? MakeViewModel(Mission mission)
    {
        return Templates.TryGetValue(mission.GetType(), out var tpl) ? tpl.CreateViewModel(mission) : null;
    }


    public static object? MakeView(Mission mission)
    {
        return Templates.TryGetValue(mission.GetType(), out var tpl) ? tpl.CreateView() : null;
    }
     
    internal static object? MakeView(Type missionType)
    {
        return Templates.TryGetValue(missionType, out var tpl) ? tpl.CreateView() : null;
    }
}