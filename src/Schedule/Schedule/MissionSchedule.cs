using FMO.Logging;
using LiteDB;

namespace FMO.Schedule;


public static class MissionSchedule
{
    /// <summary>
    /// 默认每分钟一次
    /// </summary>
    private static System.Timers.Timer _taskTimer { get; } = new System.Timers.Timer(60000);

    /// <summary>
    /// 用于倒计时
    /// </summary>
    private static System.Timers.Timer _secondTimer { get; } = new System.Timers.Timer(1000);


    static HashSet<Mission> missions = [];// new HashSet<Mission>();

    public static Mission[] Missions => missions.ToArray();


    public static void Init()
    {
        using var db = new MissionDatabase();
        var ms = db.GetCollection<BsonDocument>("Mission").FindAll().ToList();// db.GetCollection<Mission>().FindAll().ToArray();

        missions = [.. ms.Select(x =>
        {
            try
            {
                var mission = BsonMapper.Global.ToObject<Mission>(x);
                mission.Init();
                return mission;
            }
            catch(Exception e) { LogEx.Error($"无法加载mission{x["_id"]}\n{x.ToString()}\n{e.Message}\n{e.StackTrace}"); return null; }
        }).Where(x => x is not null && x is not FillFundDailyMission).OrderBy(x => x!.GetType().Name switch { nameof(MailCacheMission) => 0, _ => x.Id })];


        // 不再使用
        //missions.RemoveWhere(x => x is FillFundDailyMission);

        // 清理Log
        db.GetCollection<MissionRecord>().DeleteMany(x => x.Time < DateTime.Now.AddMonths(-2));

#if DEBUG
        foreach (var m in missions)
            m.IsEnabled = false;
#endif

        _taskTimer.Elapsed += _taskTimer_Elapsed;
        _taskTimer.Start();


        //延时执行一次
        Task.Run(async () => { await Task.Delay(8000); DoWork(DateTime.Now); });
    }

    private static void _taskTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        DoWork(e.SignalTime);
    }

    private static void DoWork(DateTime t)
    {
        foreach (var item in missions)
            item.OnTime(t);
    }


    public static void Register(Mission mission)
    {
        mission.Init();
        missions.Add(mission);
    }

    public static void SaveChanges(Mission m)
    {
        using var db = new MissionDatabase();
        db.GetCollection<Mission>().Upsert(m);
        if (!missions.Contains(m)) missions.Add(m);
    }

    public static void Unregister(int id)
    {
        var m = missions.FirstOrDefault(x => x.Id == id);
        if (m is not null)
        {
            missions.Remove(m);
            using var db = new MissionDatabase();
            db.GetCollection<Mission>().Delete(id);
        }
    }

    internal static void ManualSetNextRun(int id, DateTime time)
    {
        var m = missions.FirstOrDefault(x => x.Id == id);
        if (m is null) return;
        m.NextRun = time;
        SaveChanges(m);
    }
}
