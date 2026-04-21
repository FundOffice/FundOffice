using FMO.Models;
using LiteDB;
using System.Collections.Concurrent;


namespace FMO.Utilities;


public record class FundSharePairErrorDate(DateOnly Date, DateOnly? RequestDate, decimal? ShareByDaily, decimal? ShareByTransfer)
{
    public bool IsDiff => ShareByDaily != ShareByTransfer;
}

public record FundSharePairContext(string FundName, int ByDailyCount, int ByTransferCount, IEnumerable<FundSharePairErrorDate> Diff);


public class FundSharePairRule : VerifyRule<FundShareRecordByDaily, FundShareRecordByTransfer>
{

    public ConcurrentDictionary<int, IDataTip> Tips { get; } = [];


    //private List<FundShareRecord> Records { get; } = new();

    //private List<TransferRecord> Records2 { get; } = new();

    private List<int> FundIds { get; } = new();


    protected override void OnEntityOverride(IEnumerable<FundShareRecordByDaily> obj)
    {
        foreach (var item in obj)
            FundIds.Add(item.FundId);
    }

    protected override void OnEntityOverride(IEnumerable<FundShareRecordByTransfer> obj)
    {
        foreach (var item in obj)
            FundIds.Add(item.FundId);
    }




    protected override void VerifyOverride()
    {
        var fids = FundIds.Distinct().ToList();//Records.Select(x => x.FundId).Union(Records2.Select(x => x.FundId)).Distinct().ToList();

        using var db = DbHelper.Base();
        var finfo = db.GetCollection<Fund>().Query().Where(x => fids.Contains(x.Id) && x.SetupDate != default).Select(x => new { x.Id, x.Name }).ToList();
        // 排除清盘后的0
        var byta = db.GetCollection<FundShareRecordByTransfer>().Find(x => x.Share > 0).OrderBy(x => x.Date).GroupBy(x => x.FundId).ToDictionary(x => x.Key, x => x.AsEnumerable());
        var bydy = db.GetCollection<FundShareRecordByDaily>().Find(x => x.Share > 0).OrderBy(x => x.Date).GroupBy(x => x.FundId).ToDictionary(x => x.Key, x => x.AsEnumerable());


        foreach (var f in finfo)
        {
            var ta = byta.ContainsKey(f.Id) ? byta[f.Id].ToList() : [];
            var dy = bydy.ContainsKey(f.Id) ? bydy[f.Id].ToList() : [];


            // 完全一样 或 特殊情况，当第一个share一致，但是时间不一致
            if (IsEqual(ta, dy)) //if (same || (ta.Count > 0 && dy.Count > 0 && ta[0].Share == dy[0].Share && ta.Skip(1).SequenceEqual(dy.Skip(1))))
            {
                if (Tips.ContainsKey(f.Id))
                {
                    Revoke(Tips[f.Id].Id);
                    Tips.TryRemove(f.Id, out var t);
                }
            }
            else
            {
                var allDates = ta.Select(x => x.Date).Union(dy.Select(x => x.Date)).OrderBy(d => d);
                var context = (from date in allDates
                               join taItem in ta on date equals taItem.Date into taJoined
                               from taItem in taJoined.DefaultIfEmpty()
                               join dyItem in dy on date equals dyItem.Date into dyJoined
                               from dyItem in dyJoined.DefaultIfEmpty()
                               select new FundSharePairErrorDate(date, taItem?.RequestDate, dyItem?.Share, taItem?.Share)).ToList();

                if (IsEqual(context))
                {
                    if (Tips.ContainsKey(f.Id))
                    {
                        Revoke(Tips[f.Id].Id);
                        Tips.TryRemove(f.Id, out var t);
                    }
                    continue;
                }


                if(Tips.ContainsKey(f.Id))
                {
                    Tips.TryRemove(f.Id, out var t);
                    Revoke(t.Id);
                }

                DataTip<FundSharePairContext> tip = new() { Tags = ["Fund", $"Fund{f.Id}", nameof(FundSharePairRule)], _Context = new FundSharePairContext(f.Name, dy.Count, ta.Count, context) };
                Tips.TryAdd(f.Id, tip);
                Send(tip);
            }
        }
    }

    protected override void ClearParamsOverride()
    {
        FundIds.Clear();
    }

    public override void Init()
    {
        using var db = DbHelper.Base();

        var finfo = db.GetCollection<Fund>().Query().Where(x => x.SetupDate != default).Select(x => new { x.Id, x.Name }).ToList();

        ILiteCollection<FundShareRecordByTransfer> tta = db.GetCollection<FundShareRecordByTransfer>();
        ILiteCollection<FundShareRecordByDaily> tdy = db.GetCollection<FundShareRecordByDaily>();
        var tt = db.GetCollection<TransferRecord>();

        var byta = tta.Find(x => x.Share > 0).OrderBy(x => x.Date).GroupBy(x => x.FundId).ToDictionary(x => x.Key, x => x.AsEnumerable());
        var bydy = tdy.Find(x => x.Share > 0).OrderBy(x => x.Date).GroupBy(x => x.FundId).ToDictionary(x => x.Key, x => x.AsEnumerable());


        foreach (var f in finfo)
        {
            var ta = byta.ContainsKey(f.Id) ? byta[f.Id].ToList() : RebuildByTransfer(tt, tta, f.Id);// [];
            var dy = bydy.ContainsKey(f.Id) ? bydy[f.Id].ToList() : RebuildByDaily(db.GetDailyCollection(f.Id), tdy, f.Id);//[];

            //var same = ta.SequenceEqual(dy);

            if (IsEqual(ta, dy)) continue;
            //if (same) continue;

            // 特殊情况，当第一个share一致，但是时间不一致
            //if (ta.Count > 0 && dy.Count > 0 && ta[0].Share == dy[0].Share && ta.Skip(1).SequenceEqual(dy.Skip(1)))
            //    continue;

            var allDates = ta.Select(x => x.Date).Union(dy.Select(x => x.Date)).OrderBy(d => d);
            var context = (from date in allDates
                           join taItem in ta on date equals taItem.Date into taJoined
                           from taItem in taJoined.DefaultIfEmpty()
                           join dyItem in dy on date equals dyItem.Date into dyJoined
                           from dyItem in dyJoined.DefaultIfEmpty()
                           select new FundSharePairErrorDate(date, taItem?.RequestDate, dyItem?.Share, taItem?.Share)).ToList();

            if (IsEqual(context)) continue;
            //for (int i = context.Count - 1; i > 0; i--)
            //{
            //    if (context[i].ShareByDaily == context[i].ShareByTransfer && context[i - 1].ShareByDaily == context[i - 1].ShareByTransfer)
            //        context.RemoveAt(i);
            //}


            //ta.GroupJoin(dy, x => x.Date, x => x.Date, (t, ds) => new { t, ds }).SelectMany(x => x.ds.DefaultIfEmpty(), (t, d) => new FundSharePairErrorDate { Date = .Date, ShareByDaily = t.Share, ShareByTransfer = d.)
            DataTip<FundSharePairContext> tip = new() { Tags = ["Fund", $"Fund{f.Id}", nameof(FundSharePairRule)], _Context = new FundSharePairContext(f.Name, dy.Count, ta.Count, context) };
            Tips.TryAdd(f.Id, tip);
            Send(tip);

        }

    }

    /// <summary>
    /// 对于T+2确认的，ta会晚于dy
    /// </summary>
    /// <param name="ta"></param>
    /// <param name="dy"></param>
    /// <returns></returns>
    private bool IsEqual(IList<FundShareRecordByTransfer> ta, IList<FundShareRecordByDaily> dy)
    {
        int count = ta?.Count ?? 0;

        if (count == 0 || count != dy?.Count) return false;

        // 第一项只要求share，不要求时间，可能不一样
        if (ta![0].Share != dy[0].Share) return false;

        for (int i = 1; i < count; i++)
        {
            if (ta![i].Share != dy[i].Share) return false;

            // dy的日期应该单于ta request 和 date 之间
            if (dy[i].Date < ta[i].RequestDate || dy[i].Date > ta[i].Date)
                return false;
        }

        return true;
    }

    private bool IsEqual(List<FundSharePairErrorDate> data)
    {
        if (data.Count == 0) return false;
        if (data.Count > 0 && data[0].ShareByDaily != data[0].ShareByTransfer) return false;

        for (int i = 1; i < data.Count - 1; i++)
        {
            var item = data[i];
            var next = data[i + 1];

            //T+2
            if (next.ShareByDaily is null && item.ShareByTransfer is null && item.ShareByDaily == next.ShareByTransfer && next.RequestDate <= item.Date && next.Date >= item.Date)
            {
                data[i] = item with { ShareByTransfer = next.ShareByTransfer };
                data.RemoveAt(i + 1);
                continue;
            }

            if (item.ShareByTransfer != 0 && item.ShareByTransfer != item.ShareByDaily)
                return false;
        }
        return true;
    }

    /// <summary>
    /// 更新基金份额平衡表
    /// </summary>
    /// <param name="db"></param>
    /// <param name="fundId"></param>
    private static List<FundShareRecordByTransfer> RebuildByTransfer(ILiteCollection<TransferRecord> table, ILiteCollection<FundShareRecordByTransfer> tableSR, int fundId, DateOnly from = default)
    {
        var old = tableSR.Query().OrderByDescending(x => x.Date).Where(x => x.FundId == fundId && x.Date < from).FirstOrDefault();

        var data = table.Find(x => x.FundId == fundId && x.ConfirmedDate >= from).GroupBy(x => x.ConfirmedDate).OrderBy(x => x.Key);
        var list = new List<FundShareRecordByTransfer>();
        if (old is not null) list.Add(old);
        foreach (var item in data)
        {
            if (item.Sum(x => x.ShareChange()) is decimal change && change != 0)
                list.Add(new FundShareRecordByTransfer(fundId, item.First().RequestDate, item.Key, change + (list.Count > 0 ? list[^1].Share : 0)));
        }
        tableSR.DeleteMany(x => x.FundId == fundId && x.Date >= from);
        tableSR.Upsert(list);
        return list.LastOrDefault()?.Share == 0 ? list.SkipLast(1).ToList() : list;
    }

    private static List<FundShareRecordByDaily> RebuildByDaily(ILiteCollection<DailyValue> tdy, ILiteCollection<FundShareRecordByDaily> ttd, int fundId)
    {
        List<FundShareRecordByDaily> add = new();

        var dys = tdy.Find(x => x.Share > 0).OrderBy(x => x.Date).Select(x => new { x.Date, x.Share }).ToList();
        if (dys.Count == 0) return add;
        add.Add(new FundShareRecordByDaily(fundId, dys[0].Date, dys[0].Share));

        for (int i = 1; i < dys.Count; i++)
        {
            if (dys[i].Share != dys[i - 1].Share)
                add.Add(new FundShareRecordByDaily(fundId, dys[i].Date, dys[i].Share));
        }
        ttd.InsertBulk(add);
        return add;
    }
}
