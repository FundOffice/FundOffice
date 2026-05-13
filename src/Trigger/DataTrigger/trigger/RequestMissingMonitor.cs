using FMO.Models;
using FMO.Schedule;
using FMO.Settings;
using FMO.Utilities;
using LiteDB;
using Schedule;

namespace FMO.Trigger;

/// <summary>
/// 监控订单是否忘了录单
/// </summary>
[AbilityUnit(SettingSections.TransferMonitor, "漏录单监管", "监控订单是否忘了录单，需要接入托管API")]
public partial class RequestMissingMonitor : ITracker<IEnumerable<TransferOrder>>
{
    internal record History(int Id, int MissionId);

    private static ILiteCollection<History> GetCollection(LiteDatabase db) => db.GetCollection<History>($"r_{nameof(RequestMissingMonitor)}");

    public override void Init()
    {
        // 检查有没有漏掉的，只检查10天内的
        var limit = DateOnly.FromDateTime(DateTime.Now).DayNumber - 10;
        using var db = DbHelper.Base();
        var rid = db.GetCollection<TransferOrder>().Query().Where(x => x.Date.DayNumber > limit).Select(x => x.Id).ToList();

        using var tdb = DbHelper.Tracker();
        var done = GetCollection(tdb).Query().Select(x => x.Id).ToList();

        var missed = rid.Except(done).ToArray();

        if (missed.Length > 0) OnDataArrival(db.GetCollection<TransferOrder>().Query().Where(Query.In("_id", missed.Select(x => new BsonValue(x)))).ToArray());

    }

    private partial void OnDataArrival(IEnumerable<TransferOrder> obj)
    {
        // 获取成立日期
        using var db = DbHelper.Base();
        var dicSetup = db.GetCollection<Fund>().Query().Select(x => new { x.Id, x.SetupDate }).ToList().ToDictionary(x => x.Id, x => x.SetupDate);



        var today = DateOnly.FromDateTime(DateTime.Now);

        foreach (var order in obj)
        {
            // 认购订单
            if (dicSetup == default)
            {
                Check(order, db);
                continue;
            }

            // 如果是历史订单，今天已过开放日，跳过
            if (order.OpenDate.Year > 2000 && today > order.OpenDate)
                continue;

            Check(order, db);
        }
    }

    private static void Check(TransferOrder order, BaseDatabase db)
    {
        // 检查有没有对应的request

        var req = db.GetCollection<TransferRequest>().FindOne(x => x.OrderId == order.Id);
        if (req is not null)
            return;

        using var trd = DbHelper.Tracker();
        if (GetCollection(trd).FindOne(x => x.Id == order.Id) is null)
        {
            // 建立监控mission
            var ms = new OrderEntryMonitorMission
            {
                FundId = order.FundId,
                FundName = order.FundName,
                OpenDay = order.OpenDate,
                SignDate = order.Date,
                Name = $"追踪订单是否录单",
                Description = $"""
                {order.FundName} 
                投资人：{order.InvestorName} 
                {EnumDescriptionTypeConverter.GetEnumDescription(order.Type)} {order.Number}
                """,

            };

            MissionSchedule.Register(ms);

            GetCollection(trd).Upsert(new History(order.Id, ms.Id));
        }
    }
}