using FMO.Models;
using System.Collections.Concurrent;

namespace FMO.Utilities;


public record FundOverdueContext(string FundName, DateOnly SetupDate, DateOnly Expire, int ExpiredDays);


/// <summary>
/// 基金超期
/// </summary>
public class FundOverdueRule : VerifyRule<NewDay, EntityChanged<DateOnly>>
{

    public ConcurrentDictionary<int, IDataTip> Tips { get; } = [];

    private bool VerifyAll { get; set; }

    private List<EntityChanged<DateOnly>> entityChangeds { get; } = new();

    /// <summary>
    /// 在Home的OnNewDay中会运行一次全基金验证，这里不再运行
    /// </summary>
    public override void Init()
    {
        //using var db = DbHelper.Base();
        //var cur = DateOnly.FromDateTime(DateTime.Today);
        //var funds = db.GetCollection<Fund>().Query().Select(x => new { x.Id, x.Status, x.Name, x.SetupDate }).ToList();

        //var coll = db.GetCollection<FundElements>();
        //foreach (var fund in funds.Where(x => x.Status == FundStatus.Normal || x.Status == FundStatus.StartLiquidation))
        //{
        //    var ele = coll.FindById(fund.Id);
        //    if (ele is not null)
        //    {
        //        var expire = ele.ExpirationDate.Value;

        //        if (expire != default && cur > expire)
        //        {
        //            DataTip<FundOverdueContext> tip = new() { Tags = ["Fund", $"Fund{ele.Id}", nameof(FundOverdueRule)], _Context = new FundOverdueContext(fund.Name, fund.SetupDate, expire, cur.DayNumber - expire.DayNumber) };
        //            Tips.TryAdd(fund.Id, tip);
        //            Send(tip);
        //        }
        //    }
        //}


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

                if (expire != default && cur > expire)
                {
                    DataTip<FundOverdueContext> tip = new() { Tags = ["Fund", $"Fund{ele.Id}", nameof(FundOverdueRule)], _Context = new FundOverdueContext(fund.Name, fund.SetupDate, expire, cur.DayNumber - expire.DayNumber) };
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