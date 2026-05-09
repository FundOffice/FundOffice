using FMO.Models;
using System.Collections.Concurrent;

using FMO.Utilities;
using LiteDB;

namespace FMO.Trigger;
public record FundOverdueContext(string FundName, DateOnly SetupDate, DateOnly Expire, int ExpiredDays);


/// <summary>
/// 基金超期
/// </summary>
public partial class FundOverdueRule : VerifyRule, ITracker<NewDay>,ITracker<EntityChanged<FundElements, DateOnly, int>>
{

    public ConcurrentDictionary<int, IDataTip> Tips { get; } = [];

    private bool VerifyAll { get; set; }

    private List<EntityChanged<FundElements, DateOnly, int>> entityChangeds { get; } = new();

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

    private partial void OnDataArrival(NewDay obj) => VerifyAll = true;

    private partial void OnDataArrival(EntityChanged<FundElements, DateOnly, int> obj)  => entityChangeds.AddRange(obj);

    protected override void VerifyOverride()
    {
        var ids = entityChangeds.Select(x => x.Id).ToList();
        if (ids.Count == 0) return;

        using var db = DbHelper.Base();
        var funds = (VerifyAll ? db.GetCollection<Fund>().Query() : db.GetCollection<Fund>().Query().Where(Query.In("_id", ids.Select(x => new BsonValue(x))))).Select(x => new { x.Id, x.Name, x.Status, x.SetupDate }).ToList();

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