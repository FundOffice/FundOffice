using CommunityToolkit.Mvvm.Messaging;
using FMO.Logging;
using FMO.Models;
using System.Collections.Concurrent;


namespace FMO.Utilities;

public record FundDailyMissingContext(string FundName, int Count, IEnumerable<DateOnly> MissingSample);

/// <summary>
/// 基金净值缺失校验
/// </summary>
public class FundDailyMissingRule : VerifyRule<DailyValue, EntityChanged<Fund, DateOnly>>
{

    public List<DailyValue> Dailies { get; } = [];

    public List<EntityChanged<Fund, DateOnly>> FundClear { get; } = [];

    public ConcurrentDictionary<int, List<DateOnly>> MissDays { get; } = [];

    /// <summary>
    /// fundid tip
    /// </summary>
    public ConcurrentDictionary<int, IDataTip> Tips { get; } = [];


    //public override Type[] Related { get; } = [typeof(DailyValue)];

    public override void Init()
    {

        using var db = DbHelper.Base();
        var finfo = db.GetCollection<Fund>().Query().Where(x =>/*(int)x.Status >= (int)FundStatus.Normal &&*/ x.SetupDate.Year > 1970).Select(x => new { x.Id, x.Name, x.SetupDate, x.ClearDate }).ToList();

        foreach (var f in finfo)
        {
            var has = db.GetDailyCollection(f.Id).Query().OrderBy(x => x.Date).Select(x => x.Date).ToList();
            if (has.Count == 0)
                has.Add(f.SetupDate);

            DateOnly begin = f.SetupDate, end = f.ClearDate == default ? DateOnly.FromDateTime(DateTime.Now).AddDays(-1) : f.ClearDate;

            List<DateOnly> missing = Days.TradingDaysBetween(begin, end).Except(has).ToList();
            if (missing.Count == 0) continue;


            var tip = new DataTip<FundDailyMissingContext>()
            {
                Tags = ["Fund", $"Fund{f.Id}", nameof(FundDailyMissingRule)],
                _Context = new FundDailyMissingContext(f.Name, missing.Count, missing.Take(5))
            };
            Tips.TryAdd(f.Id, tip);
            Send(tip);
        }

    }

    protected override void ClearParamsOverride()
    {
        Dailies.Clear();
        FundClear.Clear();
    }

    protected override void OnEntityOverride(IEnumerable<DailyValue> obj)
    {
        foreach (var v in obj)
            Dailies.Add(v);
    }

    protected override void OnEntityOverride(IEnumerable<EntityChanged<Fund, DateOnly>> obj)
    {
        foreach (var v in obj)
            FundClear.Add(v);
    }

    protected override void VerifyOverride()
    {
        using var db = DbHelper.Base();

        List<int> skip = new();
        // 检查清盘日期变化的
        try
        {
            foreach (var ec in FundClear.GroupBy(x => x.Entity.Id).Select(g => g.Last()))
            {
                var f = ec.Entity;
                var has = db.GetDailyCollection(f.Id).Query().OrderBy(x => x.Date).Select(x => x.Date).ToList();
                if (has.Count == 0)
                    has.Add(f.SetupDate);

                DateOnly begin = f.SetupDate, end = f.ClearDate == default ? DateOnly.FromDateTime(DateTime.Now) : f.ClearDate;

                List<DateOnly> missing = Days.TradingDaysBetween(begin, end).Except(has).ToList();

                // 跳过下方的检查
                skip.Add(f.Id);

                if (Tips.ContainsKey(f.Id) && Tips[f.Id] is DataTip<FundDailyMissingContext> old)
                {
                    WeakReferenceMessenger.Default.Send(new DataTipRemove(old.Id));
                    Tips.TryRemove(f.Id, out var _);
                }
                if (missing.Count > 0)
                {
                    var tip = new DataTip<FundDailyMissingContext>()
                    {
                        Tags = ["Fund", $"Fund{f.Id}", nameof(FundDailyMissingRule)],
                        _Context = new FundDailyMissingContext(f.Name, missing.Count, missing.Take(5))
                    };
                    Tips.TryAdd(f.Id, tip);
                    Send(tip);
                }
            }
        }
        catch (Exception e)
        {
            LogEx.Error(e);
        }

        // 净值更新
        try
        {
            foreach (var g in Dailies.GroupBy(x => new { x.FundId, x.Class }))
            {
                int fundId = g.Key.FundId;
                if (skip.Contains(fundId)) continue;//上方已经检查过了

                if (Tips.ContainsKey(fundId) && Tips[fundId] is DataTip<FundDailyMissingContext> tip)
                {
                    var f = db.GetCollection<Fund>().FindById(fundId);
                    DateOnly begin = f.SetupDate, end = f.ClearDate == default ? DateOnly.FromDateTime(DateTime.Now) : f.ClearDate;

                    var has = db.GetDailyCollection(f.Id).Query().OrderBy(x => x.Date).Select(x => x.Date).ToList();
                    if (has.Count == 0)
                        has.Add(f.SetupDate);
                    List<DateOnly> missing = Days.TradingDaysBetween(begin, end).Except(has).ToList();


                    // 删除旧的
                    WeakReferenceMessenger.Default.Send(new DataTipRemove(tip.Id));

                    if (missing.Count == 0)
                    {
                        Tips.TryRemove(fundId, out var _);
                        continue;
                    }
                    tip._Context = tip._Context! with { Count = missing.Count, MissingSample = missing.Take(5) };
                    Send(tip);
                }
            }
        }
        catch (Exception e)
        {
            LogEx.Error(e);
        }
    }
}
