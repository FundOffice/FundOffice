using FMO.Models;
using System.Collections.Concurrent;

namespace FMO.Utilities;


public record FundClearNotFinishedContext(string FundName, string? Code, DateOnly? Clear, DateTime Last);

/// <summary>
/// 基金长期未结束清盘
/// </summary>
public class FundClearNotFinishedRule : VerifyRule<LiquidationFlow, FundEntityRemoved<int>, EntityChanged<Fund, DateOnly>>
{
    private ConcurrentDictionary<int, DataTip> Tips { get; } = [];

    public ConcurrentBag<int> Params { get; } = [];

    public override void Init()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        using var db = DbHelper.Base();
        var finfo = db.GetCollection<Fund>().Query().Select(x => new { x.Id, x.Name, x.Code, x.Status, x.ClearDate, x.LastUpdate }).
            ToList().Where(x => x.Status == FundStatus.StartLiquidation).Where(x => x.ClearDate == default || (x.LastUpdate != default && today.DayNumber - x.ClearDate.DayNumber > 7));

        foreach (var f in finfo)
        {
            DataTip tip = new() { Tags = ["Fund", $"Fund{f.Id}", nameof(FundClearNotFinishedRule)], Context = new FundClearNotFinishedContext(f.Name, f.Code, f.ClearDate == default ? null : f.ClearDate, f.LastUpdate) };
            Tips.TryAdd(f.Id, tip);
            Send(tip);
        }
    }

    protected override void ClearParamsOverride() => Params.Clear();

    protected override void OnEntityOverride(IEnumerable<LiquidationFlow> obj)
    {
        foreach (var f in obj)
            Params.Add(f.FundId);
    }
    protected override void OnEntityOverride(IEnumerable<FundEntityRemoved<int>> obj)
    {
        foreach (var f in obj.Where(x=>x.Type == typeof(LiquidationFlow)))
            Params.Add(f.FundId);
    }

    protected override void OnEntityOverride(IEnumerable<EntityChanged<Fund, DateOnly>> obj)
    {
        foreach (var f in obj.Where(x => x.PropertyName == nameof(Fund.ClearDate)))
            Params.Add(f.Entity.Id);
    }


    protected override void VerifyOverride()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        using var db = DbHelper.Base();
        var ids = Params.ToList();
        var finfo = db.GetCollection<Fund>().Query().Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.Name, x.Code, x.Status, x.ClearDate, x.LastUpdate }).
            ToList().Where(x => x.Status == FundStatus.StartLiquidation).Where(x => x.ClearDate == default || (x.LastUpdate != default && today.DayNumber - x.ClearDate.DayNumber > 7));

        // 需要删除
        foreach (var item in ids.Except(finfo.Select(x => x.Id)))
        {
            if (Tips.TryRemove(item, out var tip))
                Revoke(tip.Id);
        }

        // 需要添加
        foreach (var f in finfo.ExceptBy(Tips.Keys.ToList(), x => x.Id))
        {
            DataTip tip = new() { Tags = ["Fund", $"Fund{f.Id}", nameof(FundClearNotFinishedRule)], Context = new FundClearNotFinishedContext(f.Name, f.Code, f.ClearDate == default ? null : f.ClearDate, f.LastUpdate) };
            Tips.TryAdd(f.Id, tip);
            Send(tip);
        }
    }

}
