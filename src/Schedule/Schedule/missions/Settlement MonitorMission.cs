using DocumentFormat.OpenXml.Spreadsheet;
using FMO.Models;
using FMO.Schedule;
using FMO.Todo;
using FMO.Utilities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Schedule.missions;


/// <summary>
/// 交收监控
/// </summary>
public class SettlementMonitorMission : OnceMission
{
    /// <summary>
    /// 交易申请Id
    /// </summary>
    //public int TransferRequestId { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public required int FundId { get; set; }

    /// <summary>
    /// 开放日
    /// </summary>
    public required DateOnly OpenDay { get; set; }

    /// <summary>
    /// 运行间隔
    /// </summary>
    public int Interval { get; set; } = 15;

    protected override void SetNextRun()
    {
        NextRun = (LastRun ?? DateTime.Now).AddMinutes(Interval);
        if (NextRun < DateTime.Now) NextRun = DateTime.Now.AddMinutes(Interval);
    }


    protected override async Task<ErrorReturn> WorkOverride()
    {
        using var db = DbHelper.Base();
        var requests = db.GetCollection<TransferRequest>().Find(x => x.FundId == FundId && x.RequestDate == OpenDay).ToArray();

        // 获取已知账户
        var min = OpenDay.ToDateTime(TimeOnly.MaxValue);
        var ids = requests.Select(x => x.InvestorId).Distinct().ToList();
        var dicBank = db.GetCollection<InvestorBankAccount>().Find(x => ids.Contains(x.OwnerId)).GroupBy(x => x.OwnerId).ToDictionary(x => x.Key);
        var trans = db.GetCollection<RaisingBankTransaction>().Find(x => x.Time > min).ToList();

        List<int> reqSettle = [];

        // 检查有没有对应的流水
        foreach (var req in requests.Where(x => x.RequestType.IsBuy()))
        {
            if (trans.FirstOrDefault(x => x.FundId == req.FundId && x.Direction == TransctionDirection.Receive && DateOnly.FromDateTime(x.Time.Date) <= req.RequestDate &&
                    (dicBank[req.InvestorId].Any(y => y.Number == x.CounterNo) || x.CounterName == req.InvestorName) && x.Amount == req.RequestAmount) is RaisingBankTransaction transaction)
                reqSettle.Add(req.Id);
        }

        foreach (var req in requests.Where(x => x.RequestType.IsSell()))
        {
            if (trans.FirstOrDefault(x => x.FundId == req.FundId && x.Direction == TransctionDirection.Pay && DateOnly.FromDateTime(x.Time.Date) > req.RequestDate &&
                    (dicBank[req.InvestorId].Any(y => y.Number == x.CounterNo) || x.CounterName == req.InvestorName)) is RaisingBankTransaction transaction)
                reqSettle.Add(req.Id);
        }

        // 未交收项
        var unsettled = requests.ExceptBy(reqSettle, x => x.Id).ToArray();
        if (unsettled.Length == 0) // 全部交收
        {
            IsFinished = true;
            TodoService.Unregister($"Settlement_{FundId}_{OpenDay.DayNumber}");
        }
        else
        {
            var msg = string.Join('\n', unsettled.Select(x => $"{x.InvestorName} {EnumDescriptionTypeConverter.GetEnumDescription(x.RequestType)} {x.RequestShare} {x.RequestAmount}"));
            TodoService.Register(new JustNotifyTodo { CreateTime = DateTime.Now, UniqueId = $"Settlement_{FundId}_{OpenDay.DayNumber}", Message = msg });
        }
        return new(true);
    }
}


public static class Monitor
{


    [HookData]
    public static void OnTransferRequest(IEnumerable<TransferRequest> tr)
    {
        foreach (var fv in tr.GroupBy(x=>x.FundId))
        {
            var fid = fv.Key;
            foreach (var day in fv.GroupBy(x=>x.RequestDate))
            {
                var openDay = day.Key;

                var gos =  Days.TradingDaysBetween(openDay, DateOnly.FromDateTime(DateTime.Now));

                if (gos.Count > 5) continue;

                MissionSchedule.Register(new SettlementMonitorMission
                {
                    FundId = fid,
                    OpenDay = openDay,
                });
            }
        }

    }
}