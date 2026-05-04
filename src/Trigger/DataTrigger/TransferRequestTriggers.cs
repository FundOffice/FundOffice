using FMO.Logging;
using FMO.Models;
using FMO.Todo;
using FMO.Utilities;
using FMO.Schedule;
using Schedule;

namespace FMO.Trigger;

internal static partial class TransferRequestTriggers
{

    /// <summary>
    /// 
    /// </summary>
    /// <param name="dv"></param>
    /// <exception cref="NotImplementedException"></exception>
    [HookData]
    public static void AutoHugeRedemption(IEnumerable<TransferRequest> tr)
    {
        using var db = DbHelper.Base();
        // 忽略子份额，按基金分组
        foreach (var fv in tr.GroupBy(x => x.FundId))
        {
            foreach (var item in fv.GroupBy(x => x.RequestDate))
            {
                // 计算开放日的总份额
                var openday = item.Key;
                string fundName = fv.First().FundName;
                var code = fv.First().FundCode!;

                var dv = db.GetDailyCollection(fv.Key).FindOne(x => x.Date == openday);
                if (dv is null || dv.NetAsset == 0)
                {
                    LogEx.Warning($"基金净值数据异常，无法进行巨额赎回监控，基金：{fundName}，日期：{openday}");
                    continue;
                }


                // 赎回份额
                // 方案一，统计tr中的，但可能存在数据不全的情况（如部分赎回记录未同步到系统），导致监控失效
                var redemMoney = item.Where(x => x.RequestType is TransferRequestType.Redemption or TransferRequestType.ForceRedemption).Sum(x => x.RequestShare * dv.NetValue + x.RequestAmount);

                var ratio = (dv.NetAsset - redemMoney) / dv.NetAsset;

                var defRatio = db.GetCollection<FundElements>().FindById(fv.Key).HugeRedemptionRatio.Value;
                if (defRatio == 0)
                {
                    
                    TodoService.Register(new FundElementMissingTodo { FundCode = code, FundName =  fundName, Missing = "巨额赎回" });
                    LogEx.Warning($"基金巨额赎回监控未设置阈值，无法进行监控，基金：{fundName}");
                    continue;
                }

                // 发生巨额赎回，生成Todo
                if (ratio > defRatio)
                {
                    TodoService.Register(new HugeRedemptionTodo
                    {
                        FundCode = code,
                        FundName = fundName,
                        OpenDay = openday,
                        CreateTime = DateTime.Now,
                        DefinedRatio = defRatio,
                        RealRatio = ratio,
                        FundId = fv.Key,
                    });
                }

            }

        }
    }



    /// <summary>
    /// 监控基金交收情况
    /// </summary>
    /// <param name="tr"></param>
    [HookData]
    public static void SettlementMonitor(IEnumerable<TransferRequest> tr)
    {
        foreach (var fv in tr.GroupBy(x => x.FundId))
        {
            var fid = fv.Key;
            foreach (var day in fv.GroupBy(x => x.RequestDate))
            {
                var openDay = day.Key;

                var gos = Days.TradingDaysBetween(openDay, DateOnly.FromDateTime(DateTime.Now));

                if (gos.Count > 5) continue;

                MissionSchedule.Register(new SettlementMonitorMission
                {
                    Name = "交收监控",
                    Description = $"监控{fv.First().FundName}-{openDay}的交收情况",
                    FundId = fid,
                    OpenDay = openDay,
                });
            }
        }

    }

}
