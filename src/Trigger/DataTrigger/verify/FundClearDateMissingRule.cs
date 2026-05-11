using CommunityToolkit.Mvvm.Messaging;
using FMO.Models;
using FMO.Settings;
using FMO.Utilities;
using System.Collections.Concurrent;


namespace FMO.Trigger;

public record FundClearDateMissingContext(string Name, string? Code, DateOnly? Clear, DateTime Last);


[AbilityUnit(SettingSections.FundflowMonitor, "基金清盘流程缺失", "基金已清盘，但未设置清盘流程")]
public partial class FundClearDateMissingRule : VerifyRule, ITracker<EntityChanged<Fund, DateOnly>>
{


    private ConcurrentDictionary<int, DataTip> Tips { get; } = [];

    public ConcurrentBag<int> Params { get; } = [];

    //public override Type[] Related { get; } = [typeof(Fund)];

    public override void Init()
    {

        using var db = DbHelper.Base();
        var finfo = db.GetCollection<Fund>().Query().Select(x => new { x.Id, x.Name, x.Code, x.Status, x.ClearDate, x.LastUpdate }).
            ToList().Where(x => x.Status >= FundStatus.StartLiquidation).Where(x => x.ClearDate == default || (x.LastUpdate != default && x.ClearDate > DateOnly.FromDateTime(x.LastUpdate)));

        foreach (var f in finfo.Where(x => x.Status >= FundStatus.StartLiquidation))
        {
            DataTip tip = new() { Tags = ["Fund", $"Fund{f.Id}", nameof(FundClearDateMissingRule)], Context = new FundClearDateMissingContext(f.Name, f.Code, f.ClearDate == default ? null : f.ClearDate, f.LastUpdate) };
            Tips.TryAdd(f.Id, tip);
            Send(tip);
        }
    }

    private partial void OnDataArrival(EntityChanged<Fund, DateOnly> obj)
    {
        Params.Add(obj.Entity.Id);
    }


    protected override void VerifyOverride()
    {
        var arr = Params.ToList();

        using var db = DbHelper.Base();
        var error = db.GetCollection<Fund>().Query().Where(x => arr.Contains(x.Id)).Select(x => new { x.Id, x.Name, x.Code, x.Status, x.ClearDate, x.LastUpdate }).ToList().Where(x => x.Status >= FundStatus.StartLiquidation && x.ClearDate == default);

        var filterd = Tips.Where(x => arr.Contains(x.Key));

        //removed 
        var removed = error.Any() ? filterd.ExceptBy(error.Select(x => x.Id), x => x.Key) : filterd;
        foreach (var item in removed)
        {
            Tips.Remove(item.Key, out var _);
            WeakReferenceMessenger.Default.Send(new DataTipRemove(item.Value.Id));
        }

        // 不关心重复的

        // add
        var add = Tips.Count > 0 ? error.ExceptBy(filterd.Select(x => x.Key), x => x.Id) : error;
        foreach (var f in add)
        {
            DataTip tip = new() { Tags = ["Fund", $"Fund{f.Id}"], Context = new FundClearDateMissingContext(f.Name, f.Code, f.ClearDate == default ? null : f.ClearDate, f.LastUpdate) };
            Tips.TryAdd(f.Id, tip);
            Send(tip);
        }
    }


    protected override void ClearParamsOverride() => Params.Clear();

}
