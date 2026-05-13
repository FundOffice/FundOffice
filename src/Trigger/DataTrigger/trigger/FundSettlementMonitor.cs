using FMO.Models;
using FMO.Schedule;
using FMO.Settings;
using FMO.Utilities;
using LiteDB;
using Schedule;

namespace FMO.Trigger;

[AbilityUnit(SettingSections.TransferMonitor, "交收监控", "交易申请后，监控投资人是否打款、赎回是否交收，未交收会提醒")]
public partial class FundSettlementMonitor : ITracker<IEnumerable<TransferRequest>>
{
    internal record History(int FundId, DateOnly Date, int MissionId);

    private ILiteCollection<History> GetCollection(LiteDatabase db) => db.GetCollection<History>($"r_{nameof(FundSettlementMonitor)}");

    public override void Init()
    {
        // 检查有没有漏掉的
        var limit = DateOnly.FromDateTime(DateTime.Now).DayNumber - 10;
        using var db = DbHelper.Base();
        var rid = db.GetCollection<TransferRequest>().Query().Where(x => x.RequestDate.DayNumber > limit).Select(x => new { x.FundId, Date = x.RequestDate }).ToList();

        if (rid.Count == 0) return;

        using var tdb = DbHelper.Tracker();
        var done = GetCollection(tdb).Query().Select(x => new { x.FundId, x.Date }).ToList();

        var missed = rid.Except(done).ToArray();

        if (missed.Length == 0) return;

        var mdic = db.GetCollection<Fund>().Query().Select(x => new { x.Id, x.Name }).ToArray().ToDictionary(x => x.Id, x => x.Name);

        foreach (var item in missed)
        {
            Core(tdb, mdic[item.FundId], item.FundId, item.Date);
        }
    }

    private partial void OnDataArrival(IEnumerable<TransferRequest> obj)
    {
        using var db = DbHelper.Tracker();
        foreach (var fv in obj.GroupBy(x => x.FundId))
        {
            var fid = fv.Key;
            var fn = fv.First().FundName;
            foreach (var day in fv.GroupBy(x => x.RequestDate))
            {
                var openDay = day.Key;
                Core(db, fn, fid, openDay);
            }
        }

    }

    private bool Core(LiteDatabase db, string fn, int fid, DateOnly openDay)
    {
        var gos = Days.TradingDaysBetween(openDay, DateOnly.FromDateTime(DateTime.Now));

        if (gos.Count > 5) return false;

        // 未创建
        if (GetCollection(db).FindOne(x => x.FundId == fid && x.Date == openDay) is null)
        {
            var m = MissionSchedule.Register(new SettlementMonitorMission
            {
                Name = "交收监控",
                Description = $"监控{fn}-{openDay}的交收情况",
                FundId = fid,
                OpenDay = openDay,
            });

            db.GetCollection<History>($"r_{nameof(FundSettlementMonitor)}").Upsert(new History(fid, openDay, m.Id));
        }

        return true;
    }
}