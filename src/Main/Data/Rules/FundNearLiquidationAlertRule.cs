using FMO.Models;
using System.Collections.Concurrent;

namespace FMO.Utilities;



public record FundNearLiquidationContext(string FundName, DateOnly SetupDate, DateOnly Expire);

/// <summary>
/// 基金临近清盘（<1年）
/// </summary>
public class FundNearLiquidationAlertRule : VerifyRule<NewDay, EntityChanged<DateOnly>>
{
    public ConcurrentDictionary<int, IDataTip> Tips { get; } = [];

    private bool VerifyAll { get; set; }

    private List<EntityChanged<DateOnly>> entityChangeds { get; } = new();

    public override void Init()
    {

    }

    protected override void ClearParamsOverride()
    {
        VerifyAll = false;
        entityChangeds.Clear();
    }

    protected override void OnEntityOverride(IEnumerable<NewDay> obj) => VerifyAll = true;


    protected override void OnEntityOverride(IEnumerable<EntityChanged<DateOnly>> obj) => entityChangeds.AddRange(obj);


    protected override void VerifyOverride()
    {
        var ids = entityChangeds.Where(x => x.Type == typeof(FundElements) && x.Id is int).Select(x => x.Id).OfType<int>().ToList();
        if (ids.Count == 0) return;

        using var db = DbHelper.Base();
        var funds = db.GetCollection<Fund>().Find(x => VerifyAll || ids.Contains(x.Id)).Select(x => new { x.Id, x.Name, x.Status, x.SetupDate }).ToList();

        var cur = DateOnly.FromDateTime(DateTime.Today);
        var coll = db.GetCollection<FundElements>();
        foreach (var fund in funds.Where(x => x.Status == FundStatus.Normal || x.Status == FundStatus.StartLiquidation))
        {
            var ele = coll.FindById(fund.Id);
            if (ele is not null)
            {
                var expire = ele.ExpirationDate.Value;

                if (expire.DayNumber - cur.DayNumber < 365)
                {
                    DataTip<FundNearLiquidationContext> tip = new() { Tags = ["Fund", $"Fund{ele.Id}", nameof(FundNearLiquidationContext)], _Context = new FundNearLiquidationContext(fund.Name, fund.SetupDate, expire) };
                    Tips.TryAdd(fund.Id, tip);
                    Send(tip);
                }
                else
                {
                    if (Tips.ContainsKey(fund.Id))
                    {
                        Revoke(Tips[fund.Id].Id);
                        Tips.TryRemove(fund.Id, out var t);
                    }
                }
            }
        }
    }
}