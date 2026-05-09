using FMO.Models;
using FMO.Utilities;
using LiteDB;
using System.Collections.Concurrent;

namespace FMO.Trigger;


public record FundClearNotFinishedContext(string FundName, string? Code, DateOnly? Clear, DateTime Last);

/// <summary>
/// 基金长期未结束清盘
/// </summary>
public partial class FundClearNotFinishedRule : VerifyRule, ITracker<FundFlow>, ITracker<EntityRemoved<FundFlow, int>>, ITracker<EntityChanged<Fund, DateOnly>>
{
    private ConcurrentDictionary<int, DataTip> Tips { get; } = [];

    public List<int> Params { get; } = [];

    private List<int> FlowId { get; } = [];

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

    protected override void ClearParamsOverride()
    {
        Params.Clear();
        FlowId.Clear();
    }

    private partial void OnDataArrival(FundFlow obj)  { if(obj is LiquidationFlow) Params.Add(obj.FundId); }

    private partial void OnDataArrival(EntityChanged<Fund, DateOnly> obj) => Params.Add(obj.Entity.Id);

    private partial void OnDataArrival(EntityRemoved<FundFlow, int> obj) => FlowId.Add(obj.Id);



    protected override void VerifyOverride()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        using var db = DbHelper.Base();

        var typen = $"{typeof(LiquidationFlow).FullName},{typeof(LiquidationFlow).Assembly.GetName().Name}";

        if(FlowId.Count > 0)
            Params.AddRange(db.GetCollection<FundFlow>().Query().Where(Query.In("_id", FlowId.Select(x => new BsonValue(x)))).
                Where(Query.EQ("_type", typen)).Select(x => x.FundId).ToArray());

        if (Params.Count == 0) return;

        var finfo = db.GetCollection<Fund>().Query().Where(Query.In("_id", Params.Distinct().Select(x => new BsonValue(x)))).
            Where(x => x.Status == FundStatus.StartLiquidation).
            Select(x => new { x.Id, x.Name, x.Code, x.Status, x.ClearDate, x.LastUpdate }).
            ToList().Where(x => x.ClearDate == default || (x.LastUpdate != default && today.DayNumber - x.ClearDate.DayNumber > 7));

        // 需要删除
        foreach (var item in Params.Except(finfo.Select(x => x.Id)))
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
