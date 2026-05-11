using FMO.Models;
using FMO.Schedule;
using FMO.Settings;
using Schedule;

namespace FMO.Trigger;

[AbilityUnit(SettingSections.TransferMonitor, "交收监控", "交易申请后，监控投资人是否打款、赎回是否交收，未交收会提醒")]
public partial class FundSettlementMonitor : ITracker<IEnumerable<TransferRequest>>
{
    private partial void OnDataArrival(IEnumerable<TransferRequest> obj) 
    {
        foreach (var fv in obj.GroupBy(x => x.FundId))
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