using FMO.Models;
using System.Collections.Concurrent;

namespace FMO.Utilities;



public record FundScaleWarningContext(string FundName, int Count, DateOnly Date, DateOnly LimitedDate, DateOnly ClearDate);

/// <summary>
/// 连续N交易日净资产低于500万
/// </summary>
public class FundScaleWarnRule : VerifyRule<DailyValue, NewDay>
{
    public List<int> FundIds { get; set; } = [];

    public bool NewDay { get; set; }

    /// <summary>
    /// fundid tip
    /// </summary>
    public ConcurrentDictionary<int, IDataTip> Tips { get; } = [];

    public override void Init()
    {
        using var db = DbHelper.Base();
        var funds = db.GetCollection<Fund>().Query().Select(x => new { x.Id, x.Name, x.Status }).ToList();

        foreach (var fund in funds.Where(x => x.Status == FundStatus.Normal))
        {
            var dy = db.GetDailyCollection(fund.Id).Query().OrderByDescending(x => x.Date).Where(x => x.NetAsset > 0 && x.NetAsset >= 5000000).FirstOrDefault();
            if (dy is null) continue;

            var start = dy.Date.Year < 2025 ? new DateOnly(2024, 12, 31) : dy.Date;
            int count = Days.TradingDayCountFrom(start);

            if (count > 30)
            {
                var tip = new DataTip<FundScaleWarningContext>()
                {
                    Tags = ["Fund", $"Fund{fund.Id}", nameof(FundScaleWarnRule)],
                    _Context = new FundScaleWarningContext(fund.Name, count, Days.NextTradingDay(start), Days.NextTradingDay(start, 61), Days.NextTradingDay(start, 181))
                };
                Tips.TryAdd(fund.Id, tip);
                Send(tip);
            }
        }


    }

    protected override void ClearParamsOverride()
    {
        FundIds.Clear();
        NewDay = false;
    }

    protected override void OnEntityOverride(IEnumerable<DailyValue> obj)
    {
        FundIds.AddRange(obj.Select(x => x.FundId));
    }

    protected override void OnEntityOverride(IEnumerable<NewDay> obj)
    {
        NewDay = true;
    }

    protected override void VerifyOverride()
    {
        using var db = DbHelper.Base();
        var funds = db.GetCollection<Fund>().Query().Where(x => x.Status == FundStatus.Normal).Select(x => new { x.Id, x.Name, x.Status }).ToList();

        FundIds = FundIds.Distinct().ToList();

        foreach (var fund in funds.Where(x => NewDay || FundIds.Contains(x.Id)))
        {
            var dy = db.GetDailyCollection(fund.Id).Query().OrderByDescending(x => x.Date).Where(x => x.NetAsset > 0 && x.NetAsset >= 5000000).FirstOrDefault();
            if (dy is null) continue;

            var start = dy.Date.Year < 2025 ? new DateOnly(2024, 12, 31) : dy.Date;
            int count = Days.TradingDayCountFrom(start);

            if (Tips.ContainsKey(fund.Id))
            {
                if (Tips.TryRemove(fund.Id, out var t))
                    Revoke(t.Id);
            }

            if (count > 30)
            {
                var tip = new DataTip<FundScaleWarningContext>()
                {
                    Tags = ["Fund", $"Fund{fund.Id}", nameof(FundScaleWarnRule)],
                    _Context = new FundScaleWarningContext(fund.Name, count, Days.NextTradingDay(start), Days.NextTradingDay(start, 61), Days.NextTradingDay(start, 181))
                };
                Tips.TryAdd(fund.Id, tip);
                Send(tip);
            }
        }
    }
}
