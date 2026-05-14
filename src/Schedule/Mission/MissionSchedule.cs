using CommunityToolkit.Mvvm.Messaging;
using FMO.Logging;
using FMO.Utilities;
using LiteDB;

namespace FMO.Schedule;


public static class MissionSchedule
{
    /// <summary>
    /// 默认每分钟一次
    /// </summary>
    private static System.Timers.Timer _taskTimer { get; } = new System.Timers.Timer(60000);

    private static PeriodicTimer _timer { get; } = new PeriodicTimer(TimeSpan.FromMinutes(1));

    /// <summary>
    /// 用于倒计时
    /// </summary>
    private static System.Timers.Timer _secondTimer { get; } = new System.Timers.Timer(1000);


    static Dictionary<int, Mission> missions = [];// new HashSet<Mission>();

    public static Mission[] Missions => missions.Values.ToArray();


    public static void Init()
    {
        using var db = DbHelper.Mission();
        var ms = db.GetCollection("Mission").Query().Where(Query.Not(nameof(OnceMission.IsFinished), true)).Where(Query.Not(nameof(Mission.IsAborted),true)).ToList();

        missions = ms.Select(x =>
        {
            try
            {
                var mission = BsonMapper.Global.ToObject<Mission>(x);

                mission.Init();
                return mission;
            }
            catch (Exception e) { LogEx.Error($"无法加载mission{x["_id"]}\n{x.ToString()}\n{e.Message}\n{e.StackTrace}"); return null; }
        }).OfType<Mission>().OrderBy(x => x!.GetType().Name switch { "MailCacheMission" => 0, _ => x.Id }).ToDictionary(x => x.Id);


        // 清理Log
        db.GetCollection<MissionRecord>().DeleteMany(x => x.Time < DateTime.Now.AddMonths(-2));

#if DEBUG
        foreach (var m in missions)
            m.Value.IsEnabled = false;
#endif


        Task.Run(async () =>
        {
            // 等待10秒，等其他模块加载完成
            await Task.Delay(10000);

            while (await _timer.WaitForNextTickAsync())
            {
                DoWork(DateTime.Now);
            }
        });
        //_taskTimer.Elapsed += _taskTimer_Elapsed;
        //_taskTimer.Start();


        //延时执行一次
        //Task.Run(async () => { await Task.Delay(8000); DoWork(DateTime.Now); });
    }

    private static void _taskTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        DoWork(e.SignalTime);
    }

    private static void DoWork(DateTime t)
    {
        Parallel.ForEach(missions, async item => await item.Value.OnTime(t));
    }


    public static Mission Register(Mission mission)
    {
        mission.Init();
        missions[mission.Id] = mission;

        using var db = DbHelper.Mission();
        db.GetCollection<Mission>().Upsert(mission);

        Task.Run(()=> WeakReferenceMessenger.Default.Send(mission));
        return mission;
    }

    public static void SaveChanges(Mission m)
    {
        using var db = DbHelper.Mission();
        db.GetCollection<Mission>().Upsert(m);

        missions[m.Id] = m;
    }

    public static void Unregister(int id)
    {
        missions.Remove(id);
        using var db = DbHelper.Mission();
        db.GetCollection<Mission>().UpdateMany($"{{ IsAborted : true }}", $"_id={id}");

        Task.Run(()=> WeakReferenceMessenger.Default.Send(new MissionOverMessage(id)));
    }

    internal static void ManualSetNextRun(int id, DateTime time)
    {
        if (missions.TryGetValue(id, out var m))
        {
            m.NextRun = time;
            SaveChanges(m);
        }
    }
}
