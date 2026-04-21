using CommunityToolkit.Mvvm.Messaging;
using FMO.Models;
using System.Collections.Concurrent;


namespace FMO.Utilities;

public record FundClearDateMissingContext(string Name, string? Code, DateOnly? Clear, DateTime Last);

public class FundClearDateMissingRule : VerifyRule<EntityChanged<Fund, DateOnly>>
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

    protected override void OnEntityOverride(IEnumerable<EntityChanged<Fund, DateOnly>> obj)
    {
        foreach (var f in obj)
            Params.Add(f.Entity.Id);
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
