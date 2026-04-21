using CommunityToolkit.Mvvm.Messaging;
using FMO.Logging;
using FMO.Models;
using LiteDB;
using Serilog;
using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace FMO.Utilities;


public record class FundTip(int FundId, string FundName, TipType Type, string? Tip);

public record UniformTip(TipType Type, object? Tip);

public record FundTipMessage(int FundId);

public record FundsTipCountMessage(int Count);

/// <summary>
/// 插入数据后处理列表
/// </summary>
/// <param name="Id"></param>
public record PostHandleIds(int Id);






/// <summary>
/// 数据校验
/// </summary>
public static partial class DataTracker
{

    public static FundTipList FundTips { get; } = new();

    public static ConcurrentDictionary<TipType, string?> UniformTips { get; } = new();



    static DataTracker()
    {
        FundTips.CollectionChanged += () => WeakReferenceMessenger.Default.Send(new FundsTipCountMessage(FundTips.Count));
    }


    /// <summary>
    /// 检查基金存储文件夹
    /// </summary>
    /// <param name="funds"></param>
    public static void CheckFundFolder(IEnumerable<Fund> funds)
    {
        // 基金文件夹
        var dis = new DirectoryInfo(@"files\funds").GetDirectories();
        foreach (var f in funds)
        {
            if (f.Code?.Length > 4)
            {
                var di = dis.FirstOrDefault(x => x.Name.StartsWith($"{f.Id}.")) ?? dis.FirstOrDefault(x => x.Name.StartsWith(f.Code));

                var name = $"{f.Id}.{f.Code}.{f.Name}";

                string folder = $"files\\funds\\{name}";

                if (di is null)
                {
                    Directory.CreateDirectory(folder);
                    continue;
                }
                if (di.Name != name)
                {
                    Directory.Move(di.FullName, folder);
                    Log.Warning($"基金 {f.Code} 名称已更新 [{di.Name}] -> [{f.Name}]");
                }

                FundHelper.Map(f, folder);
            }
        }
    }


    //public static void CheckShareIsPair(IEnumerable<Fund> funds)
    //{
    //    // 验证最新净值中的份额（Share）与 FundShareRecord 中的份额是否一致
    //    using var db = DbHelper.Base();

    //    // 遍历所有的基金（funds）
    //    foreach (var fund in funds)
    //    {
    //        // 获取该基金的每日净值数据中，净值大于0的记录，并找到最新日期的记录
    //        var last = db.GetDailyCollection(fund.Id).Find(x => x.NetValue > 0).MaxBy(x => x.Date);

    //        // 如果没有找到有效的净值记录，则跳过当前基金
    //        if (last is null) continue;

    //        // 获取基金的 FundShareRecord 记录和 TransferRecord 记录
    //        var c = db.GetCollection<FundShareRecord>().Find(x => x.FundId == fund.Id);
    //        var ta = db.GetCollection<TransferRecord>().Find(x => x.FundId == fund.Id);

    //        // 如果没有 TA 记录或 FundShareRecord 数据为空，则添加提示：没有TA数据
    //        if (!ta.Any())
    //        {
    //            FundTips.Add(new FundTip(fund.Id, fund.Name, TipType.FundNoTARecord, "没有TA数据"));
    //            continue;
    //        }
    //        else if (FundTips.FirstOrDefault(x => x.FundId == fund.Id && x.Type == TipType.FundNoTARecord) is FundTip tip) // 清除错误信息
    //        {
    //            FundTips.Remove(tip);
    //            WeakReferenceMessenger.Default.Send(new FundTipMessage(fund.Id));
    //        }

    //        // 获取 TA 记录中确认日期（ConfirmedDate）最大的日期
    //        var lta = ta.Max(x => x.ConfirmedDate);

    //        // 如果 FundShareRecord 为空，或者最新记录的日期早于 TA 的最新确认日期，则重建 FundShareRecord
    //        if (c is null || !c.Any() || c.Max(x => x.Date) < lta)
    //        {
    //            db.RebuildFundShareRecord(fund.Id); // 重建该基金的份额记录
    //            c = db.GetCollection<FundShareRecord>().Find(x => x.FundId == fund.Id); // 重新获取份额记录
    //        }

    //        // 找到在 FundShareRecord 中，日期不晚于最新净值日期的最后一条记录
    //        var sh = c.OrderBy(x => x.Date).LastOrDefault(x => x.Date <= last.Date);

    //        // 检查份额是否一致，如果不一致则添加提示：基金份额与估值表不一致
    //        if (sh!.Share != last.Share)
    //        {
    //            FundTips.Add(new FundTip(fund.Id, fund.Name, TipType.FundShareNotPair, "基金份额与估值表不一致"));

    //            // 修改 trustee中的work日期
    //            using var pd = DbHelper.Platform();
    //            var rs = pd.GetCollection<TrusteeMethodShotRange>().Find(x => x.Id.EndsWith(nameof(ITrustee.QueryTransferRecords))).ToArray();
    //            foreach (var item in rs)
    //            {
    //                if (item.End > sh.Date)
    //                    pd.GetCollection<TrusteeMethodShotRange>().Update(new TrusteeMethodShotRange(item.Id, item.Begin, sh.Date));
    //            }

    //            // 发送基金提示消息（例如用于界面通知）
    //            WeakReferenceMessenger.Default.Send(new FundTipMessage(fund.Id));
    //            continue;
    //        }
    //        else if (FundTips.FirstOrDefault(x => x.FundId == fund.Id && x.Type == TipType.FundShareNotPair) is FundTip tip) // 清除错误信息
    //        {
    //            FundTips.Remove(tip);
    //            WeakReferenceMessenger.Default.Send(new FundTipMessage(fund.Id));
    //        }

    //    }

    //}
    //public static void CheckShareIsPair(int fid)
    //{
    //    // 验证最新的净值中份额与share是否一致
    //    using var db = DbHelper.Base();
    //    var fund = db.GetCollection<Fund>().FindById(fid);

    //    var last = db.GetDailyCollection(fund.Id).Find(x => x.NetValue > 0).MaxBy(x => x.Date);
    //    if (last is null)
    //    {
    //        WeakReferenceMessenger.Default.Send(new FundTipMessage(fid));
    //        return;
    //    }
    //    var c = db.GetCollection<FundShareRecord>().Find(x => x.FundId == fund.Id);
    //    var lta = db.GetCollection<TransferRecord>().Find(x => x.FundId == fund.Id).Max(x => x.ConfirmedDate);
    //    if (c is null || !c.Any() || c.Max(x => x.Date < lta))
    //    {
    //        db.RebuildFundShareRecord(fund.Id);
    //        c = db.GetCollection<FundShareRecord>().Find(x => x.FundId == fund.Id);
    //    }

    //    if (c is null || !c.Any())
    //    {
    //        FundTips.Add(new FundTip(fund.Id, fund.Name, TipType.FundNoTARecord, "没有TA数据"));
    //        WeakReferenceMessenger.Default.Send(new FundTipMessage(fid));
    //        return;
    //    }

    //    var sh = c.LastOrDefault(x => x.Date < last.Date);

    //    if (sh?.Share != last.Share)
    //    {
    //        FundTips.Add(new FundTip(fund.Id, fund.Name, TipType.FundShareNotPair, "基金份额与估值表不一致"));
    //        WeakReferenceMessenger.Default.Send(new FundTipMessage(fid));
    //        return;
    //    }

    //    FundTips.Remove(x => x.Type == TipType.FundShareNotPair);
    //    WeakReferenceMessenger.Default.Send(new FundTipMessage(fid));

    //    DataValidationInfo validationInfo = new DataValidationInfo
    //    {
    //        Related = [new(typeof(DailyValue), 0, fid), new(typeof(TransferRecord), 0, fid)]
    //    };
    //}


    public static void CheckIsExpired(IEnumerable<Fund> funds)
    {
        using var db = DbHelper.Base();
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
                    FundTips.Add(new FundTip(fund.Id, fund.Name, TipType.OverDue, $"基金已超期{cur.DayNumber - expire.DayNumber}天"));
                    WeakReferenceMessenger.Default.Send(new FundTipMessage(fund.Id));
                }
            }
        }
    }

    //public static void CheckInvestorBalance()
    //{
    //    using var db = DbHelper.Base();
    //    var coll = db.GetCollection<InvestorBalance>().FindAll().OrderBy(x => x.Id).ToList();
    //    var tas = db.GetCollection<TransferRecord>().FindAll().ToList();
    //    IEnumerable<(long key, DateOnly date)> ta = tas.Select(x => new { id = InvestorBalance.MakeId(x.InvestorId, x.FundId), date = x.ConfirmedDate }).
    //        GroupBy(x => x.id).Select(x => (key: x.Key, date: x.Max(y => y.date))).OrderBy(x => x.key);

    //    foreach (var item in ta)
    //    {
    //        var o = coll.FirstOrDefault(x => x.Id == item.key);
    //        if (o is null || o.Date != item.date)
    //        {
    //            var (c, f) = InvestorBalance.ParseId(item.key);
    //            var tf = tas.Where(x => x.InvestorId == c && x.FundId == f);
    //            var share = tf.Sum(x => x.ShareChange());
    //            var deposit = tf.Where(x => x.Type switch { TransferRecordType.Subscription or TransferRecordType.Purchase or TransferRecordType.MoveIn or TransferRecordType.SwitchIn or TransferRecordType.TransferIn => true, _ => false }).Sum(x => x.ConfirmedNetAmount);
    //            var withdraw = tf.Where(x => x.Type switch { TransferRecordType.Redemption or TransferRecordType.Redemption or TransferRecordType.MoveOut or TransferRecordType.SwitchOut or TransferRecordType.TransferOut or TransferRecordType.Distribution => true, _ => false }).Sum(x => x.ConfirmedNetAmount);

    //            db.GetCollection<InvestorBalance>().Upsert(new InvestorBalance { FundId = f, InvestorId = c, Share = share, Deposit = deposit, Withdraw = withdraw, Date = tf.Max(x => x.ConfirmedDate) });
    //        }
    //    }


    //}

    /// <summary>
    /// 检查TA中的fundid,investor id是不是0
    /// </summary>
    public static void CheckTAMissOwner()
    {
        using var db = DbHelper.Base();
        var e1 = db.GetCollection<TransferRequest>().Count(x => x.FundId == 0 || x.InvestorId == 0);
        var e2 = db.GetCollection<TransferRecord>().Count(x => x.FundId == 0 || x.InvestorId == 0);

        if (e1 + e2 > 0)
            UniformTips.AddOrUpdate(TipType.TANoOwner, $"发现未关联Request {e1}个，Record {e2}个", (x, y) => $"发现未关联Request {e1}个，Record {e2}个");
        else UniformTips.AddOrUpdate(TipType.TANoOwner, x => null, (x, y) => null);

        WeakReferenceMessenger.Default.Send(new TipChangeMessage(TipType.TANoOwner));
        //WeakReferenceMessenger.Default.Send(new UniformTip(TipType.TANoOwner, $"发现未关联Request {e1}个，Record {e2}个"));
    }

    public static void CheckTransferRequestMissing()
    {
        using var db = DbHelper.Base();
        if (db.GetCollection<TransferMapping>().Count(x => x.RecordId != 0 && x.RequestId == 0) is int cc)
            UniformTips.AddOrUpdate(TipType.TANoOwner, $"发现未关联Request {cc}个", (x, y) => $"发现未关联Request {cc}个");
        else UniformTips.AddOrUpdate(TipType.TANoOwner, x => null, (x, y) => null);

        WeakReferenceMessenger.Default.Send(new TipChangeMessage(TipType.TransferRequestMissing));
    }


    //public static Debouncer CheckTAMissOwnerInvoker = new(CheckTAMissOwner, 500);

    //public static Debouncer CheckTransferRequestMissingInvoker = new(CheckTransferRequestMissing, 500);

    /// <summary>
    /// 检查并匹配订单
    /// </summary>
    /// <param name="db"></param>
    public static void CheckPairOrder(BaseDatabase db)
    {
        var orders = db.GetCollection<TransferOrder>().FindAll().ToArray();
        var tas = db.GetCollection<TransferRecord>().FindAll().Where(x => TAHelper.RequiredOrder(x.Type)).ToArray();

        // 检查缺失request
        var miss = db.GetCollection<TransferMapping>().Find(x => x.RequestId == 0 && x.RecordId != 0).ToList();


        // 清除不存在的order
        var bad = tas.Where(x => x.OrderId != 0).ExceptBy(orders.Select(x => x.Id), x => x.OrderId).ToArray();
        foreach (var item in bad)
            item.OrderId = 0;

        // 清理对不上的
        bad = tas.Where(x => x.OrderId != 0).Join(orders, x => x.OrderId, x => x.Id, (x, y) => (x, y)).Where(x => x.x.FundId != x.y.FundId || x.x.InvestorId != x.y.InvestorId).Select(x => x.x).ToArray();
        foreach (var item in bad)
            item.OrderId = 0;

        db.GetCollection<TransferRecord>().Update(bad);

        List<TransferRecord> changed = new();
        // 未匹配
        var un = orders.ExceptBy(tas.Select(x => x.OrderId), x => x.Id).ToArray();
        foreach (var od in un)
        {
            // 找当日或后的第一个ta
            DateOnly date = tas.Where(x => x.FundId == od.FundId && x.InvestorId == od.InvestorId).OrderBy(x => x.RequestDate).FirstOrDefault(x => x.RequestDate >= od.Date)?.RequestDate ?? default;

            // 可能有同日多ta
            var list = tas.Where(x => x.FundId == od.FundId && x.InvestorId == od.InvestorId && x.RequestDate == date);

            // 单独匹配
            foreach (var record in list)
            {
                if (od.Date > record.RequestDate) continue;
                switch (od.Type)
                {
                    case TransferOrderType.FirstTrade:
                    case TransferOrderType.Buy:
                        if (od.Number != record.RequestAmount)
                            continue;
                        break;
                    case TransferOrderType.Share:
                        if (od.Number != record.RequestShare)
                            continue;
                        break;
                    case TransferOrderType.Amount:
                        if (od.Number != record.RequestAmount)
                            continue;
                        break;
                    case TransferOrderType.RemainAmout:
                        if ((od.Number / record.RequestAmount) switch { < 0.99m => true, > 1.01m => true, _ => false })
                            continue;
                        break;
                    default:
                        break;
                }

                record.OrderId = od.Id;
                changed.Add(record);
                break;

            }

            // 合并匹配
            switch (od.Type)
            {
                case TransferOrderType.FirstTrade:
                case TransferOrderType.Buy:
                    if (od.Number != list.Sum(x => x.RequestAmount))
                        continue;
                    break;
                case TransferOrderType.Share:
                    if (od.Number != list.Sum(x => x.RequestShare))
                        continue;
                    break;
                case TransferOrderType.Amount:
                    if (od.Number != list.Sum(x => x.RequestAmount))
                        continue;
                    break;
                case TransferOrderType.RemainAmout:
                    if ((od.Number / list.Sum(x => x.ConfirmedAmount)) switch { < 0.99m => true, > 1.01m => true, _ => false })
                        continue;
                    break;
            }

            foreach (var item in list)
            {
                item.OrderId = od.Id;
                changed.Add(item);
            }

        }
        db.GetCollection<TransferRecord>().Update(changed);
    }

    public static bool IsPair(TransferOrder order, TransferRecord record)
    {
        if (order.FundId != record.FundId) return false;
        if (order.InvestorId != record.InvestorId) return false;

        if (order.Date > record.RequestDate) return false;

        var orderbuy = order.Type switch { TransferOrderType.FirstTrade or TransferOrderType.Buy => true, _ => false };
        if (orderbuy && !TAHelper.IsBuy(record.Type)) return false;
        if (!orderbuy && !TAHelper.IsSell(record.Type)) return false;

        if (orderbuy && order.Number != record.RequestAmount) return false;
        if (!orderbuy)
        {
            switch (order.Type)
            {
                case TransferOrderType.FirstTrade:
                case TransferOrderType.Buy:
                    if (order.Number != record.RequestAmount)
                        return false;
                    break;
                case TransferOrderType.Share:
                    if (order.Number != record.RequestShare)
                        return false;
                    break;
                case TransferOrderType.Amount:
                    if (order.Number != record.RequestAmount)
                        return false;
                    break;
                case TransferOrderType.RemainAmout:
                    if ((order.Number / record.ConfirmedAmount) switch { < 0.99m => true, > 1.01m => true, _ => false })
                        return false;
                    break;
                default:
                    break;
            }
        }
        return true;
    }


    public static string? GetUniformTip(TipType tip) => UniformTips.TryGetValue(tip, out var text) ? text : null;


    public static void OnFundCleared(Fund f)
    {
        foreach (var m in FundTips.Where(x => x.FundId == f.Id && x.Type == TipType.OverDue))
            FundTips.Remove(m);

        WeakReferenceMessenger.Default.Send(new FundTipMessage(f.Id));
    }


    //public static void OnNewTransferRecord(TransferRecord r)
    //{

    //}


    public static void OnDailyValue(IEnumerable<DailyValue> data)
    {
        var dailyValues = data.Where(x => x.NetValue != 0).ToList();
        if (dailyValues.Count == 0) return;

        using var db = DbHelper.Base();
        foreach (var g in dailyValues.GroupBy(x => (x.FundId, x.Class)))
        {
            var table = db.GetDailyCollection(g.Key.FundId, g.Key.Class);

            ////当source=api，则不更新source=sheet
            //var old = table.Find(x => x.Source == DailySource.Sheet).Select(x => x.Id).ToArray();
            ////var old = table.Query().Where(x => x.Source == DailySource.Sheet).Select(x => (long)x.Id).ToList();

            //foreach (var src in g.GroupBy(x => x.Source))
            //{
            //    if(src.Key == DailySource.Sheet)
            //        table.Upsert(src.AsEnumerable());
            //    else
            //        table.Upsert(src.ExceptBy(old, x => x.Id));
            //}

            try
            {

                // 如果是api，只更新非sheet
                var dic = table.Find(x => x.Source == DailySource.Sheet).OrderBy(x => x.Id).Select(x => x.Id).ToList();

                foreach (var item in g.GroupBy(x => x.Source))
                {
                    if (item.Key != DailySource.Sheet)
                    {
                        var nonsheet = item.ExceptBy(dic, x => x.Id);
                        table.Upsert(nonsheet);

                        //// 通知
                        Parallel.ForEach(nonsheet, x =>
                        {
                            WeakReferenceMessenger.Default.Send(new FundDailyUpdateMessage(x.FundId, x));
                        });
                    }
                    else
                    {
                        // 获取这些文档的业务 Id 列表
                        var sheetIds = item.Select(x => x.Id).ToHashSet();

                        // 删除表中已存在的这些 Id 的旧记录（无论来源）
                        table.DeleteMany(x => sheetIds.Contains(x.Id));

                        table.Upsert(item.AsEnumerable());
                        //// 通知
                        Parallel.ForEach(item, x =>
                        {
                            WeakReferenceMessenger.Default.Send(new FundDailyUpdateMessage(x.FundId, x));
                        });
                    }
                }
            }
            catch (Exception e)
            {
                LogEx.Error($"{g.Key.FundId}", e);
            }


            //foreach (var item in g)
            //{ 
            //    //if (item.SheetPath is null && dic.TryGetValue(item.Id, out var value))
            //    //    item.SheetPath = value.SheetPath;
            //}
            //table.Upsert(nonsheet);
        }

        // 更新管理规模
        var dates = dailyValues.Select(x => x.Date).Distinct().ToList();
        UpdateManageSacle(dates);

        // 修改 FundShareRecord
        // 一个版本是用dv算，当份额与前一日不同时，录入
        // 一个版本是用ta算， 
        var comparer = Comparer<DailyValue>.Create((a, b) => a.Date.CompareTo(b.Date));
        foreach (var g in dailyValues.Where(x => x.Class == null).OrderBy(x => x.Date).GroupBy(x => x.FundId))
        {
            var fid = g.Key;
            List<FundShareRecordByDaily> add = new();
            var col = db.GetCollection<FundShareRecordByDaily>();

            var exists = col.Find(x => x.FundId == fid).ToDictionary(x => x.Date, x => x);

            // 检查是否与前一天share不一样
            var sd = g.Min(x => x.Date);

            // 与历史记录比较，补充缺失的部分
            var last = col.Find(x => x.Date < sd).LastOrDefault();
            var lastd = last?.Date ?? default;
            var middle = db.GetDailyCollection(g.Key).Find(x => x.Date >= lastd && x.Date <= sd).OrderBy(x => x.Date).ToList();
            if (middle.Count > 0 && middle[0].Date is DateOnly datem && col.FindOne(x => x.FundId == fid && x.Date == datem) == null)
                add.Add(new FundShareRecordByDaily(g.Key, middle[0].Date, middle[0].Share));

            for (int i = 1; i < middle.Count; i++)
            {
                if (middle[i - 1].Share != middle[i].Share)
                    add.Add(new FundShareRecordByDaily(g.Key, middle[i].Date, middle[i].Share));
            }

            if (sd.DayNumber > 20) sd = sd.AddDays(-20);
            var ed = g.Max(x => x.Date);
            var ds = db.GetDailyCollection(g.Key).Find(x => x.Date > sd && x.Date < ed).OrderBy(x => x.Id).ToArray();
            foreach (var dy in g)
            {
                // 使用二分查找找到前一日记录
                int index = ds.BinarySearch(dy, comparer);
                index = index < 0 ? ~index - 1 : index - 1;

                DailyValue? previous = index < 0 || index >= ds.Length ? null : ds[index];

                // 检查份额变化
                if (previous != null && previous.Share != dy.Share && dy.Share != 0)
                    add.Add(new FundShareRecordByDaily(fid, dy.Date, dy.Share));

                // 删除错误值
                if (previous != null && previous.Share == dy.Share && exists.TryGetValue(dy.Date, out var errid))
                    col.Delete(errid.Id);


                //var pre = ds.FirstOrDefault(x => x.Date < dy.Date);
                //if (pre != null && pre.Share != dy.Share)
                //    add.Add(new(g.Key, dy.Date, dy.Share));
            }

            col.Upsert(add);
            var ee = col.Find(x => x.FundId == fid).OrderBy(x => x.Date).ToList();

            // 删除连续相同的值
            for (int i = 1; i < ee.Count; i++)
            {
                if (ee[i].Share == ee[i - 1].Share)
                    col.Delete(ee[i].Id);
            }

            OnFundShareRecord(add);

        }


        //// 通知
        //Parallel.ForEach(dailyValues, x =>
        //{
        //    WeakReferenceMessenger.Default.Send(new FundDailyUpdateMessage(x.FundId, x));
        //});


        // 检验
        VerifyRules.OnEntityArrival(dailyValues);


    }

    public static void UpdateManageSacle(IEnumerable<DateOnly> dates)
    {
        using var db = DbHelper.Base();
        // 获取所有名称包含"fv_开关"的集合（表）名称
        var fvCollections = db.GetCollectionNames().Where(c => Regex.IsMatch(c, @"fv_\d+$")).ToList();

        //IEnumerable<BsonValue> array = dates.Select(x => BsonMapper.Global.ToDocument(x));
        var array = dates.Select(x => new BsonValue(x.DayNumber));

        Dictionary<DateOnly, decimal> assets = dates.ToDictionary(x => x, _ => 0m);

        // 遍历每个符合条件的集合，执行查询并转换结果
        foreach (var collectionName in fvCollections)
        {
            var collection = db.GetCollection(collectionName);

            // 1. 构建查询条件：Date在指定日期列表中
            var query = Query.In("Date.DayNumber", dates.Select(d => new BsonValue(d.DayNumber)).ToArray());

            // 2. 执行查询获取文档（仅包含需要的字段以提高效率）
            var results = collection.Find(query).Select(x => new { Date = BsonMapper.Global.ToObject<DateOnly>(x["Date"].AsDocument), NetAsset = x["NetAsset"].AsDecimal }).ToList();

            foreach (var item in results)
            {
                assets[item.Date] += item.NetAsset;
            }
        }

        db.GetCollection<DailyManageSacle>().Upsert(assets.Select(x => new DailyManageSacle(x.Key, x.Value)));
    }


    private static void OnFundShareRecord(List<FundShareRecordByDaily> add)
    {
        VerifyRules.OnEntityArrival(add);
    }

    private static void OnFundShareRecord(List<FundShareRecordByTransfer> add)
    {
        VerifyRules.OnEntityArrival(add);
    }
    public static void OnEntityChanged(EntityChanged<Fund, DateOnly> changed)
    {
        if (changed.PropertyName == nameof(Fund.ClearDate) && changed.New != default)
            VerifyRules.OnEntityArrival([changed]);
    }
    public static void OnEntityChanged(EntityChanged<DateOnly> changed)
    {
        VerifyRules.OnEntityArrival([changed]);
    }
    public static void OnEntityChanged(LiquidationFlow d)
    {
        VerifyRules.OnEntityArrival([d]);
    }

    //public static void OnEntityDeleted<T>(EntityRemoved<T> entityDeleted)
    //{
    //    VerifyRules.OnEntityArrival([entityDeleted]);
    //}
    /// <summary>
    ///  关联订单
    /// </summary>
    /// <param name="same"></param>
    public static void LinkOrder(params TransferRecord[] same)
    {
        if (same.Length == 0) return;

        using var db = DbHelper.Base();
        db.BeginTrans();
        foreach (var item in same)
        {
            db.GetCollection<ManualLinkOrder>().Upsert(new ManualLinkOrder(item.Id, item.OrderId, item.ExternalId!, item.ExternalRequestId!));

            // 更新request
            db.GetCollection<TransferRequest>().UpdateMany($"{{\"OrderId\":{item.OrderId}}}", $"RequestId={item.RequestId}");
        }
        db.Commit();

        WeakReferenceMessenger.Default.Send(same.Select(x => new LinkOrderMessage(x.Id, x.OrderId, x.RequestId)));

        //foreach (var item in same)
        //    WeakReferenceMessenger.Default.Send(new TransferRecordLinkOrderMessage(item.Id, item.OrderId));
    }

    public static void OnBatchTransferRequest(IList<TransferRequest> data)
    {
        // 匹配订单
        using var db = DbHelper.Base();

        SaveRequests(db, data);

        try { PostHandleTransferRequests(db, data); }
        catch (Exception ex) { Log.Error($"{ex}"); }


        WeakReferenceMessenger.Default.Send(data);

    }


    public static void OnBatchTransferRecord(IList<TransferRecord> records)
    {
        if (records.Count == 0) return;

        using var db = DbHelper.Base();
        SaveRecords(records, db);

        // 通知UI
        try
        {
            WeakReferenceMessenger.Default.Send(records);
        }
        catch { }

        PostHandleTransferRecords(db, records);

    }

    private static void SaveRequests(BaseDatabase db, IList<TransferRequest> data)
    {
        if (data is null || data.Count == 0) return;

        var manager = db.GetCollection<Manager>().Query().First();

        // 对齐数据   
        foreach (var r in data)
        {
            if (r.Agency == manager.Name)
                r.Agency = "直销";

            // code 匹配
            var (f, c) = db.FindFundByCode(r.FundCode);
            if (f is not null)
            {
                r.FundId = f.Id;
                r.FundName = f.Name;
                if (!string.IsNullOrWhiteSpace(c))
                    r.ShareClass = c;
                continue;
            }
            else Log.Error($"QueryTransferRequests 发现未知的产品{r.FundName} {r.FundCode}");
        }

        // 不在库中的投资人
        var customers = db.GetCollection<Investor>().Query().Where(x => x.Identity != null).Select(x => new { IdNo = x.Identity!.Id, Id = x.Id }).ToList().ToDictionary(x => x.IdNo);
        foreach (var r in data)
        {
            if (customers.TryGetValue(r.InvestorIdentity, out var cus))
                r.InvestorId = cus.Id;
            else // 添加数据
            {
                var c = new Investor { Name = r.InvestorName, Identity = new Identity { Id = r.InvestorIdentity } };
                db.GetCollection<Investor>().Insert(c);
                r.InvestorId = c.Id;
            }
        }

        // 对齐id 
        //if (db.GetCollection<TransferRequest>().Query().Select(x => new { x.Id, x.ExternalId, x.FundId, x.OrderId }).ToList() is var olds &&
        //    olds.Count > 0 && olds.ToDictionary(r => (r.FundId, r.ExternalId), r => (r.Id, r.OrderId)) is var keymap)
        //    foreach (var r in data)
        //    {
        //        var key = (r.FundId, r.ExternalId);
        //        if (keymap.TryGetValue(key, out var exist))
        //        {
        //            r.KeepNotDefault(exist);
        //            r.Id = exist.Id;
        //            if (r.OrderId == 0 && exist.OrderId != 0)
        //                r.OrderId = exist.OrderId;
        //        }
        //    }
        var eid = data.Select(x => x.ExternalId).ToList();
        var col = db.GetCollection<TransferRequest>().Find(x => x.ExternalId != null && eid.Contains(x.ExternalId)).ToDictionary(x => x.ExternalId!);
        foreach (var r in data)
        {
            if (col.TryGetValue(r.ExternalId!, out var old))
                r.ReplaceAndKeep(old);
        }



        // 如果是api来的，清除其它来源
        var dlf = data.Where(x => x.Source == "api").Select(x => x.FundId).Distinct().ToList();
        db.GetCollection<TransferRequest>().DeleteMany(x => x.Source != "api" && dlf.Contains(x.FundId));

        // 取消 招商会增加一条取消的申请，还对方有确认，原申请和取消申请的值是一样的，标记一下
        foreach (var can in data.Where(x => x.RequestType == TransferRequestType.Abort))
        {
            var todo = data.Where(x => x.FundId == can.FundId && x.InvestorId == can.InvestorId && x.RequestDate == can.RequestDate && x.RequestAmount == can.RequestAmount && x.RequestShare == can.RequestShare);
            foreach (var x in todo)
                x.IsCanceled = true;

            //var reqd = db.GetCollection<TransferRequest>().Find(x => x.FundId == can.FundId && x.InvestorId == can.InvestorId && x.RequestDate == can.RequestDate && x.RequestAmount == can.RequestAmount && x.RequestShare == can.RequestShare).ToList();
            db.Execute($"UPDATE {nameof(TransferRequest)} SET IsCanceled = true WHERE FundId={can.FundId} AND InvestorId={can.InvestorId} AND RequestDate=@date AND RequestShare={can.RequestAmount} AND RequestShare={can.RequestShare}", BsonMapper.Global.ToDocument(can.RequestDate));
        }


        db.GetCollection<TransferRequest>().Upsert(data);

        db.GetCollection<PostHandleIds>("ph_request").Upsert(data.Select(x => new PostHandleIds(x.Id)));
    }
    private static void SaveRecords(IList<TransferRecord> records, BaseDatabase db)
    {
        var manager = db.GetCollection<Manager>().Query().First();

        // 对齐数据   
        foreach (var r in records)
        {
            if (r.Agency == manager.Name)
                r.Agency = "直销";


            // code 匹配
            var (f, c) = db.FindFundByCode(r.FundCode);
            if (f is not null)
            {
                r.FundId = f.Id;
                r.FundName = f.Name;
                if (!string.IsNullOrWhiteSpace(c))
                    r.ShareClass = c;
                continue;
            }
            else Log.Error($"QueryTransferRequests 发现未知的产品{r.FundName} {r.FundCode}");
        }

        // 不在库中的投资人
        var customers = db.GetCollection<Investor>().Query().Where(x => x.Identity != null).Select(x => new { IdNo = x.Identity!.Id, Id = x.Id }).ToList().ToDictionary(x => x.IdNo);
        foreach (var r in records)
        {
            if (customers.TryGetValue(r.InvestorIdentity, out var cus))
                r.InvestorId = cus.Id;
            else if (r.InvestorName != "unset")// 添加数据
            {
                var c = new Investor { Name = r.InvestorName, Identity = new Identity { Id = r.InvestorIdentity } };
                db.GetCollection<Investor>().Insert(c);
                r.InvestorId = c.Id;
            }
        }

        // 对齐id    
        //if (db.GetCollection<TransferRecord>().Query().Select(x => new { x.Id, x.ExternalId, x.FundId, x.OrderId }).ToList() is var olds &&
        //    olds.Count > 0 && olds.ToDictionary(r => (r.FundId, r.ExternalId), r => (r.Id, r.OrderId)) is var keyToIdMap)
        //    foreach (var r in records)
        //    {
        //        // 不再处理手动录的
        //        var key = (r.FundId, r.ExternalId);
        //        if (keyToIdMap.TryGetValue(key, out var exist))
        //        {
        //            r.Id = exist.Id;
        //            if (r.OrderId == 0 && exist.OrderId != 0)
        //                r.OrderId = exist.OrderId;
        //        }
        //    }
        var eid = records.Select(x => x.ExternalId).ToList();
        var col = db.GetCollection<TransferRecord>().Find(x => x.ExternalId != null && eid.Contains(x.ExternalId)).ToDictionary(x => x.ExternalId!);
        foreach (var r in records)
        {
            if (col.TryGetValue(r.ExternalId!, out var old))
                r.ReplaceAndKeep(old);
        }

        // 对齐request
        var requests = db.GetCollection<TransferRequest>().Query().Select(x => new { x.Id, x.OrderId, x.ExternalId }).ToList();
        foreach (var item in requests.Join(records, x => x.ExternalId, x => x.ExternalRequestId, (request, confirm) => new { request, confirm }))
        {
            item.confirm.RequestId = item.request.Id;
            if (item.confirm.OrderId == 0 && item.request.OrderId != 0)
                item.confirm.OrderId = item.request.OrderId;
        }

        WeakReferenceMessenger.Default.Send(records.Select(x => new LinkOrderMessage(x.Id, x.OrderId, x.RequestId)));

        // bak
        {
            var data = db.GetCollection("ta_record_bak").FindAll().Select(x => new { ExId = x[nameof(TransferRecord.ExternalId)], OrderId = x[nameof(TransferRecord.OrderId)] }).ToDictionary(x => x.ExId);

            var confirm = db.GetCollection<TransferRecord>().FindAll().ToList();
            var request = db.GetCollection<TransferRequest>().FindAll().ToDictionary(x => x.Id);

            foreach (var c in confirm)
            {
                var key = c.ExternalId!.Split('.')[1];

                if (data.TryGetValue(key, out var value))
                {
                    c.OrderId = value.OrderId;

                    if (c.RequestId != 0 && request.TryGetValue(c.RequestId, out var r))
                        r.OrderId = value.OrderId;
                }
            }
            db.GetCollection<ManualLinkOrder>().Upsert(confirm.Where(x => x.OrderId != 0).Select(x => new ManualLinkOrder(x.Id, x.OrderId, x.ExternalId!, x.ExternalRequestId!)));
            db.GetCollection<TransferRecord>().Update(confirm);
            db.GetCollection<TransferRequest>().Update(request.Select(x => x.Value));
        }

        // 如果是api来的，清除其它来源
        var dlf = records.Where(x => x.Source == "api").Select(x => x.FundId).Distinct().ToList();
        db.GetCollection<TransferRecord>().DeleteMany(x => x.Source != "api" && dlf.Contains(x.FundId));

        db.GetCollection<TransferRecord>().Upsert(records);
        db.GetCollection<PostHandleIds>("ph_record").Upsert(records.Select(x => new PostHandleIds(x.Id)));
    }

    private static void PostHandleTransferRequests(BaseDatabase db, IList<TransferRequest>? data = null)
    {
        if (data is null)
        {
            var ids = db.GetCollection<PostHandleIds>("ph_request").FindAll().Select(x => x.Id).ToList();
            data = db.GetCollection<TransferRequest>().Find(x => ids.Contains(x.Id)).ToList();
        }

        //MapRequestRecord(data);

        MapRequestToOrder();

        var handled = data.Select(x => x.Id).ToList();
        db.GetCollection<PostHandleIds>("ph_request").DeleteMany(x => handled.Contains(x.Id));
    }


    private static void PostHandleTransferRecords(BaseDatabase db, IList<TransferRecord>? records = null)
    {
        if (records is null)
        {
            var dids = db.GetCollection<PostHandleIds>("ph_record").FindAll().Select(x => x.Id).ToList();
            records = db.GetCollection<TransferRecord>().Find(x => dids.Contains(x.Id)).ToList();
        }

        var tableRecord = db.GetCollection<TransferRecord>();
        var tableBalance = db.GetCollection<InvestorBalance>();

        // 更新投资人平衡表 
        db.BeginTrans();
        foreach (var g in records.GroupBy(x => (x.InvestorId, x.FundId)))
        {
            UpdateInvestorBalance(tableRecord, tableBalance, g.Key.InvestorId, g.Key.FundId, g.Min(x => x.ConfirmedDate));
            UpdateInvestorEntry(tableRecord, db.GetCollection<InvestorFundEntry>(), g.Key.InvestorId, g.Key.FundId, g.Min(x => x.ConfirmedDate));
        }
        db.Commit();

        // 更新基金份额平衡表
        db.BeginTrans();
        var t2 = db.GetCollection<FundShareRecordByTransfer>();
        List<FundShareRecordByTransfer> fsr = new();
        foreach (var g in records.GroupBy(x => x.FundId))
            fsr.AddRange(UpdateFundShareBalanceByTransfer(tableRecord, t2, g.Key, g.Min(x => x.ConfirmedDate)));
        db.Commit();


        var ids = records.Select(x => x.FundId).Distinct().ToList();
        var funds = db.GetCollection<Fund>().Find(x => ids.Contains(x.Id)).ToList();

        // 检查基金是否份额是否为0，如果是这样，最后的ta设为清盘 
        foreach (var g in fsr.OrderByDescending(x => x.Date).GroupBy(x => x.FundId))
        {
            if (g.First().Share == 0) //清盘
            {
                var fid = g.Key;
                var last = g.First().Date;
                db.BeginTrans();
                foreach (var item in db.GetCollection<TransferRecord>().Find(x => x.FundId == fid && x.ConfirmedDate == last).ToList())
                {
                    // 校验一定是赎回类型
                    if (item.Type != TransferRecordType.Redemption && item.Type != TransferRecordType.ForceRedemption)
                    {
                        Log.Error($"基金 {fid} 清盘时， 最后 TransferRecordType = {item.Type}，应为 赎回");
                        db.Rollback();
                        break;
                    }

                    item.IsLiquidating = true;
                    db.GetCollection<TransferRecord>().Update(item);

                    // 同步request
                    //db.Execute($"UPDATE {nameof(TransferRequest)} SET IsLiquidating = true WHERE Id = {item.RequestId}");
                }
                db.Commit();
            }
        }
        var liq = db.GetCollection<TransferRecord>().Find(x => x.IsLiquidating).Select(x => x.RequestId).Distinct().ToList();
        db.GetCollection<TransferRequest>().UpdateMany("{\"IsLiquidating\" : true}", BsonExpression.Create("_id IN @ids", new BsonDocument { ["ids"] = new BsonArray(liq.Select(x => new BsonValue(x))) }));


        //var eq = db.Execute("UPDATE TransferRequest SET IsLiquidating = true WHERE Id IN (  SELECT RequestId FROM TransferRecord WHERE IsLiquidating = true );");

        //db.GetCollection<TransferRequest>().Include<TransferRecord>(x=>x.Id)

        // map
        //var mids = records.Select(x => x.RequestId).ToList();
        //var map = db.GetCollection<TransferMapping>().Find(x => mids.Contains(x.RequestId)).ToList().Join(records, x => x.RequestId, x => x.RequestId, (a, b) => new { a, b.Id });
        //foreach (var item in map)
        //    item.a.RecordId = item.Id;
        //db.GetCollection<TransferMapping>().Upsert(map.Select(x => x.a));

        Task.Run(() => VerifyRules.OnEntityArrival(fsr));

        var handled = records.Select(x => x.Id).ToList();
        db.GetCollection<PostHandleIds>("ph_record").DeleteMany(x => handled.Contains(x.Id));
    }

    public static void OnFundShareRecordByTransfer(IList<FundShareRecordByTransfer> list)
    {

    }


    private static void MapRequestRecord(IList<TransferRequest> data)
    {
        // 对应request 和 record
        using var db = DbHelper.Base();
        var dataIds = data.Select(x => x.Id).ToList();

        // 匹配request 和 record，有record没有request的
        var requests = data.OrderBy(x => x.RequestDate).Where(x => x.RequiredOrder()).ToList();
        var records = db.GetCollection<TransferRecord>().FindAll().OrderBy(x => x.RequestDate).Where(x => x.RequiredOrder()).Select(x => new { x.Id, x.ExternalId, x.ExternalRequestId }).ToList();

        // sb 招商 ExternalRequestId 不对应
        var rq = requests.Join(records, x => x.ExternalId, x => x.ExternalRequestId, (r, q) => new { RequestId = q.Id, RecordId = r.Id }).
            Union(requests.Join(records, x => x.ExternalId, x => x.ExternalId, (r, q) => new { RequestId = q.Id, RecordId = r.Id })).DistinctBy(x => x.RequestId);

        // 提取关联中的请求ID和记录ID用于查询现有映射
        var relatedRequestIds = rq.Select(x => x.RequestId).ToList();
        var relatedRecordIds = rq.Select(x => x.RecordId).ToList();

        // 查询数据库中已有的相关映射
        var existingMappings = db.GetCollection<TransferMapping>()
            .Find(x => relatedRequestIds.Contains(x.RequestId) || relatedRecordIds.Contains(x.RecordId))
            .ToList();

        // 整合映射关系：合并现有映射和新关联
        var finalMappings = new List<TransferMapping>();
        foreach (var relation in rq)
        {
            // 查找现有映射中是否有匹配的记录
            var existing = existingMappings.FirstOrDefault(m =>
                m.RequestId == relation.RequestId || m.RecordId == relation.RecordId);

            if (existing is not null)
            {
                // 打印
                if (db.GetCollection<TransferRequest>().FindById(existing.RequestId) is TransferRequest oldq)
                    Debug.WriteLine("a" + oldq.PrintProperties());

                if (db.GetCollection<TransferRecord>().FindById(existing.RecordId) is TransferRecord oldr)
                    Debug.WriteLine("b" + oldr.PrintProperties());

                // 打印
                if (db.GetCollection<TransferRequest>().FindById(relation.RequestId) is TransferRequest newq)
                    Debug.WriteLine("c" + newq.PrintProperties());

                if (db.GetCollection<TransferRecord>().FindById(relation.RecordId) is TransferRecord newr)
                    Debug.WriteLine("d" + newr.PrintProperties());


                // 合并现有映射和当前关联信息
                var merged = new TransferMapping
                {
                    RequestId = relation.RequestId,
                    RecordId = relation.RecordId
                };
                merged.Merge(existing); // 使用现有Merge方法合并字段
                finalMappings.Add(merged);
            }
            else
            {
                // 新建映射关系
                finalMappings.Add(new TransferMapping
                {
                    RequestId = relation.RequestId,
                    RecordId = relation.RecordId,
                    IsMaunal = false, // 默认非手动映射
                    Conflict = false  // 默认无冲突
                });
            }
        }
        db.GetCollection<TransferMapping>().Upsert(finalMappings);
    }

    /// <summary>
    /// 关联Order 和 request
    /// </summary>
    /// <param name="data"></param>
    public static void MapRequestToOrder2(IList<TransferRequest> data)
    {
        using var db = DbHelper.Base();
        var rids = data.Select(x => x.Id).ToList();
        var mapTable = db.GetCollection<TransferMapping>();

        // 排除已经匹配的
        var maped = mapTable.Find(x => rids.Contains(x.RequestId) && x.OrderId != 0).Select(x => x.RecordId).ToList();

        var unmapRequest = data.ExceptBy(maped, x => x.Id);
        var oTable = db.GetCollection<TransferOrder>();

        // 有 orderid 没有 requestid
        var ordermaps = mapTable.Query().Where(x => x.RequestId == 0 && x.OrderId != 0).ToList().Select(x => new { map = x, order = oTable.FindById(x.OrderId) }).ToList();

        var reqDates = unmapRequest.OrderBy(x => x.RequestDate).GroupBy(x => ((long)x.FundId << 32) | (long)x.InvestorId).ToDictionary(x => x.Key);
        foreach (var om in ordermaps)
        {
            var o = om.order;
            var gid = ((long)o.FundId << 32) | (long)o.InvestorId;

            if (reqDates.ContainsKey(gid))
            {
                List<TransferRequest> req = [.. reqDates[gid]];

                // 找 o.Date 后一个日期的所有同fundId InvestorId的 request
                // 使用二分查找找到第一个符合条件的请求
                int index = req.Select(x => x.RequestDate).ToList().BinarySearch(o.Date);
                if (index < 0) index = ~index;

                var take = 1;
                for (int i = index + 1; i < req.Count; i++, take++)
                {
                    if (req[i].RequestDate != req[index].RequestDate)
                        break;
                }

                var may = req.Skip(index).Take(take);

                bool pair = false;
                switch (o.Type)
                {
                    case TransferOrderType.FirstTrade:
                    case TransferOrderType.Buy:
                    case TransferOrderType.Amount:
                    case TransferOrderType.RemainAmout:
                        pair = o.Number == may.Sum(x => x.RequestAmount);
                        break;
                    case TransferOrderType.Share:
                        pair = o.Number == may.Sum(x => x.RequestShare);
                        break;
                }

                if (pair)
                    om.map.OrderId = o.Id;

            }
        }

        // 没有在map中的order
        List<TransferMapping> newMap = new();
        var unmapedOrder = oTable.FindAll().ExceptBy(mapTable.Query().Select(x => x.OrderId).ToList(), x => x.Id).ToList();
        foreach (var o in unmapedOrder)
        {
            // 筛选同Fund 同investor
            var gid = ((long)o.FundId << 32) | (long)o.InvestorId;

            if (reqDates.ContainsKey(gid))
            {
                List<TransferRequest> req = [.. reqDates[gid]];

                // 找 o.Date 后一个日期的所有同fundId InvestorId的 request
                // 使用二分查找找到第一个符合条件的请求
                int index = req.Select(x => x.RequestDate).ToList().BinarySearch(o.Date);
                if (index < 0) index = ~index;

                var take = 1;
                for (int i = index + 1; i < req.Count; i++, take++)
                {
                    if (req[i].RequestDate != req[index].RequestDate)
                        break;
                }

                // 日期和类型匹配
                var may = req.Skip(index).Take(take).Where(x => x.IsCompatible(o));

                bool pair = false;

                // 检验 金额 一致，默认同一天只有一个订单，可能多个申请，因为多卡打款，申请有多个
                switch (o.Type)
                {
                    case TransferOrderType.FirstTrade:
                    case TransferOrderType.Buy:
                    case TransferOrderType.Amount:
                    case TransferOrderType.RemainAmout:
                        pair = o.Number == may.Sum(x => x.RequestAmount);
                        break;
                    case TransferOrderType.Share:
                        pair = o.Number == may.Sum(x => x.RequestShare);
                        break;
                }

                if (pair)
                    foreach (var item in may)
                        newMap.Add(new TransferMapping { OrderId = o.Id, RequestId = item.Id });

            }
        }



        db.GetCollection<TransferMapping>().Upsert(ordermaps.Select(x => x.map));
        db.GetCollection<TransferMapping>().Upsert(newMap);
    }

    public static void MapRequestToOrder()
    {
        using var db = DbHelper.Base();
        var unmapRequset = db.GetCollection<TransferRequest>().Find(x => x.OrderId == 0).ToList();
        var mapedOrderIds = db.GetCollection<TransferRequest>().Query().Where(x => x.OrderId != 0).Select(x => x.OrderId).ToList();
        var unmapOrder = db.GetCollection<TransferOrder>().Find(x => !mapedOrderIds.Contains(x.Id)).ToList();

        List<LinkOrderMessage> msg = new();
        db.BeginTrans();
        var reqDict = unmapRequset.OrderBy(x => x.RequestDate).GroupBy(x => ((long)x.FundId << 32) | (long)x.InvestorId).ToDictionary(x => x.Key);
        foreach (var o in unmapOrder)
        {
            // 筛选同Fund 同investor
            var gid = ((long)o.FundId << 32) | (long)o.InvestorId;

            if (reqDict.ContainsKey(gid))
            {
                List<TransferRequest> req = [.. reqDict[gid]];

                // 找 o.Date 后一个日期的所有同fundId InvestorId的 request
                // 使用二分查找找到第一个符合条件的请求
                int index = req.Select(x => x.RequestDate).ToList().BinarySearch(o.Date);
                if (index < 0) index = ~index;

                var take = 1;
                for (int i = index + 1; i < req.Count; i++, take++)
                {
                    if (req[i].RequestDate != req[index].RequestDate)
                        break;
                }

                // 日期和类型匹配
                var may = req.Skip(index).Take(take).Where(x => x.IsCompatible(o));

                bool pair = false;

                // 检验 金额 一致，默认同一天只有一个订单，可能多个申请，因为多卡打款，申请有多个
                switch (o.Type)
                {
                    case TransferOrderType.FirstTrade:
                    case TransferOrderType.Buy:
                    case TransferOrderType.Amount:
                    case TransferOrderType.RemainAmout:
                        pair = o.Number == may.Sum(x => x.RequestAmount);
                        break;
                    case TransferOrderType.Share:
                        pair = o.Number == may.Sum(x => x.RequestShare);
                        break;
                }

                if (pair)
                {
                    foreach (var item in may)
                    {
                        item.OrderId = o.Id;
                        msg.Add(new LinkOrderMessage(0, item.OrderId, item.Id));
                    }
                    db.GetCollection<TransferRequest>().Update(may);
                }
            }
        }

        db.Commit();
        WeakReferenceMessenger.Default.Send(msg.AsEnumerable());
    }


    /// <summary>
    /// 更新基金份额平衡表
    /// </summary>
    /// <param name="db"></param>
    /// <param name="fundId"></param>
    public static List<FundShareRecordByTransfer> UpdateFundShareBalanceByTransfer(ILiteCollection<TransferRecord> table, ILiteCollection<FundShareRecordByTransfer> tableSR, int fundId, DateOnly from = default)
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
        return list;
    }


    /// <summary>
    ///  更新投资人平衡表
    /// </summary>
    /// <param name="table"></param>
    /// <param name="tableIB"></param>
    /// <param name="investorId"></param>
    /// <param name="fundId"></param>
    /// <param name="from"></param>
    public static void UpdateInvestorBalance(ILiteCollection<TransferRecord> table, ILiteCollection<InvestorBalance> tableIB, int investorId, int fundId, DateOnly from = default)
    {
        var tf = table.Find(x => x.FundId == fundId && x.InvestorId == investorId).ToList();
        if (tf.Count == 0) return;

        var share = tf.Sum(x => x.ShareChange());
        var deposit = tf.Where(x => x.Type switch { TransferRecordType.Subscription or TransferRecordType.Purchase or TransferRecordType.MoveIn or TransferRecordType.SwitchIn or TransferRecordType.TransferIn => true, _ => false }).Sum(x => x.ConfirmedNetAmount);
        var withdraw = tf.Where(x => x.Type switch { TransferRecordType.Redemption or TransferRecordType.ForceRedemption or TransferRecordType.MoveOut or TransferRecordType.SwitchOut or TransferRecordType.TransferOut or TransferRecordType.Distribution => true, _ => false }).Sum(x => x.ConfirmedNetAmount);

        var cur = new InvestorBalance { FundId = fundId, InvestorId = investorId, Share = share, Deposit = deposit, Withdraw = withdraw, Date = tf.Max(x => x.ConfirmedDate) };
        tableIB.Upsert(cur);


        //var data = table.Find(x => x.FundId == fundId && x.InvestorId == investorId).GroupBy(x => x.ConfirmedDate).OrderBy(x => x.Key);
        //var list = new List<InvestorBalance>();
        //foreach (var tf in data)
        //{
        //    var share = tf.Sum(x => x.ShareChange());
        //    var deposit = tf.Where(x => x.Type switch { TransferRecordType.Subscription or TransferRecordType.Purchase or TransferRecordType.MoveIn or TransferRecordType.SwitchIn or TransferRecordType.TransferIn => true, _ => false }).Sum(x => x.ConfirmedNetAmount);
        //    var withdraw = tf.Where(x => x.Type switch { TransferRecordType.Redemption or TransferRecordType.ForceRedemption or TransferRecordType.MoveOut or TransferRecordType.SwitchOut or TransferRecordType.TransferOut or TransferRecordType.Distribution => true, _ => false }).Sum(x => x.ConfirmedNetAmount);

        //    var last = list.LastOrDefault() ?? new();
        //    var cur = new InvestorBalance { FundId = fundId, InvestorId = investorId, Share = share, Deposit = deposit, Withdraw = withdraw, Date = tf.Key };
        //    list.Add(cur);
        //}

        // tableIB.DeleteMany(x => x.FundId == fundId && x.InvestorId == investorId && x.Date >= from);
        // tableIB.Upsert(list);
    }


    public static void UpdateInvestorEntry(ILiteCollection<TransferRecord> table, ILiteCollection<InvestorFundEntry> tableIB, int investorId, int fundId, DateOnly from = default)
    {
        var data = table.Find(x => x.FundId == fundId && x.InvestorId == investorId).GroupBy(x => x.ConfirmedDate).OrderBy(x => x.Key);
        var list = new List<InvestorFundEntry>();
        decimal share = 0;
        InvestorFundEntry fe = new() { FundId = fundId, InvestorId = investorId };
        foreach (var tf in data)
        {
            foreach (var item in tf.OrderBy(x => x.ConfirmedDate).GroupBy(x => x.ConfirmedDate))
            {
                var sc = item.Sum(x => x.ShareChange());
                if (fe.FirstBuy == default && share == 0 && sc > 0)
                {
                    fe.FirstBuy = item.Key;
                    list.Add(fe);
                }
                else if (fe.FirstBuy != default && fe.SellOut == default && share > 0 && sc + share == 0)
                {
                    fe.SellOut = item.Key;
                    fe = new() { FundId = fundId, InvestorId = investorId };
                }
                share += sc;
            }
        }

        tableIB.DeleteMany(x => x.FundId == fundId && x.InvestorId == investorId);
        tableIB.Upsert(list);
    }

    public static void RebuildTARelation()
    {
        using var db = DbHelper.Base();
        var orders = db.GetCollection<TransferOrder>().FindAll().OrderBy(x => x.Date).ToList();
        var requests = db.GetCollection<TransferRequest>().FindAll().OrderBy(x => x.RequestDate).Where(x => x.RequiredOrder()).ToList();
        var records = db.GetCollection<TransferRecord>().FindAll().OrderBy(x => x.RequestDate).Where(x => x.RequiredOrder()).ToList();
        var man = db.GetCollection<ManualLinkOrder>().FindAll().ToList();

        // 关联req rec
        foreach (var (c, r) in records.Join(requests, x => x.ExternalRequestId, x => x.ExternalId, (confirm, request) => (confirm, request)))
            c.RequestId = r.Id;

        // 先按手动的关联
        var manc = records.Join(man, x => x.ExternalId, x => x.ExternalId, (confirm, link) => (confirm, link)).ToList();
        foreach (var (c, m) in manc)
        {
            c.OrderId = m.OrderId;
            records.Remove(c);
        }
        var manr = requests.Join(man, x => x.ExternalId, x => x.ExternalRequestId, (request, link) => (request, link)).ToList();
        foreach (var (r, m) in manr)
        {
            r.OrderId = m.OrderId;
            requests.Remove(r);
        }


        //// 对应request 和 record
        //List<TransferMapping> map = new();
        //var rq = requests.Join(records, x => x.ExternalId, x => x.ExternalRequestId, (q, r) => new TransferMapping { RequestId = q.Id, RecordId = r.Id });
        //map.AddRange(rq);

        //// 有request 无record
        //rq = requests.ExceptBy(records.Select(x => x.ExternalRequestId), x => x.ExternalId).Join(records, x => x.ExternalId, x => x.ExternalId, (q, r) => new TransferMapping { RequestId = q.Id, RecordId = r.Id });
        //map.AddRange(rq);

        //foreach (var item in requests.ExceptBy(map.Select(x => x.RequestId), x => x.Id))
        //    map.Add(new TransferMapping { RequestId = item.Id });

        //// 有  record 无 request
        //foreach (var item in records.ExceptBy(map.Select(x => x.RecordId), x => x.Id))
        //    map.Add(new TransferMapping { RecordId = item.Id });

        // 对应order
        var reqDates = requests.GroupBy(x => ((long)x.FundId << 32) | (long)x.InvestorId).ToDictionary(x => x.Key);
        var recDates = records.GroupBy(x => ((long)x.FundId << 32) | (long)x.InvestorId).ToDictionary(x => x.Key);
        var mapedRequestId = new List<int>();
        foreach (var o in orders)
        {
            var gid = ((long)o.FundId << 32) | (long)o.InvestorId;

            //bool needtestrec = false;

            if (reqDates.ContainsKey(gid))
            {
                List<TransferRequest> req = [.. reqDates[gid]];

                // 找 o.Date 后一个日期的所有同fundId InvestorId的 request
                // 使用二分查找找到第一个符合条件的请求
                int index = req.Select(x => x.RequestDate).ToList().BinarySearch(o.Date);
                if (index < 0) index = ~index;

                var take = 1;
                for (int i = index + 1; i < req.Count; i++, take++)
                {
                    if (req[i].RequestDate != req[index].RequestDate)
                        break;
                }

                var may = req.Skip(index).Take(take);

                bool pair = false;
                switch (o.Type)
                {
                    case TransferOrderType.FirstTrade:
                    case TransferOrderType.Buy:
                    case TransferOrderType.Amount:
                    case TransferOrderType.RemainAmout:
                        pair = o.Number == may.Sum(x => x.RequestAmount);
                        break;
                    case TransferOrderType.Share:
                        pair = o.Number == may.Sum(x => x.RequestShare);
                        break;
                }

                if (pair)
                {
                    //var rids = may.Select(x => x.Id);
                    foreach (var item in may)
                    {
                        item.OrderId = o.Id;
                        mapedRequestId.Add(item.Id);
                    }
                    //foreach (var item in map.Where(x => rids.Contains(x.RequestId)).ToArray())
                    //{
                    //    // 有冲突
                    //    if (item.OrderId != 0 && item.OrderId != o.Id)
                    //        map.Add(new TransferMapping { OrderId = o.Id, RequestId = item.RequestId, RecordId = item.RecordId });
                    //    else
                    //        item.OrderId = o.Id;
                    //}
                }
                // else needtestrec = true;
            }

            //if (needtestrec && recDates.ContainsKey(gid))
            //{
            //    List<TransferRecord> rec = recDates[gid].ToList();

            //    // 找 o.Date 后一个日期的所有同fundId InvestorId的 request
            //    // 使用二分查找找到第一个符合条件的请求
            //    int index = rec.Select(x => x.RequestDate).ToList().BinarySearch(o.Date);
            //    if (index < 0) index = ~index;

            //    var take = 1;
            //    for (int i = index + 1; i < rec.Count; i++, take++)
            //    {
            //        if (rec[i].RequestDate != rec[index].RequestDate)
            //            break;
            //    }

            //    var may = rec.Skip(index).Take(take);

            //    bool pair = false;
            //    switch (o.Type)
            //    {
            //        case TransferOrderType.FirstTrade:
            //        case TransferOrderType.Buy:
            //        case TransferOrderType.Amount:
            //        case TransferOrderType.RemainAmout:
            //            pair = o.Number == may.Sum(x => x.RequestAmount);
            //            break;
            //        case TransferOrderType.Share:
            //            pair = o.Number == may.Sum(x => x.RequestShare);
            //            break;
            //    }

            //    if (pair)
            //    {
            //        var rids = may.Select(x => x.Id);
            //        foreach (var item in map.Where(x => rids.Contains(x.RecordId)).ToArray())
            //        {
            //            // 有冲突
            //            if (item.OrderId != 0 && item.OrderId != o.Id)
            //                map.Add(new TransferMapping { OrderId = o.Id, RequestId = item.RequestId, RecordId = item.RecordId });
            //            else
            //                item.OrderId = o.Id;
            //        }
            //    }
            //}

        }
        // 上面连接的，同步到record中
        foreach (var (c, r) in records.Join(requests.IntersectBy(mapedRequestId, x => x.Id), x => x.RequestId, x => x.Id, (confirm, request) => (confirm, request)))
            c.OrderId = r.OrderId;

        db.GetCollection<TransferRequest>().Update(requests);
        db.GetCollection<TransferRecord>().Update(records);
        ////if (db.GetCollection<TransferMapping>().Query().FirstOrDefault() is not null)
        //var col = db.GetCollection<TransferMapping>();
        //col.DeleteAll();
        //col.InsertBulk(map);
    }

    public static void OnDeleteTransferRecord(int id)
    {
        throw new NotImplementedException();
    }

    public static void OnRaisingBankTransaction(IList<RaisingBankTransaction> data)
    {
        using var db = DbHelper.Base();
        db.GetCollection<RaisingBankTransaction>().Upsert(data);

        WeakReferenceMessenger.Default.Send(data);
    }

    public static void OnBatchTransferOrder(IList<TransferOrder> data)
    {
        // 匹配订单
        using var db = DbHelper.Base();

        db.GetCollection<TransferOrder>().Upsert(data);

        WeakReferenceMessenger.Default.Send(data);
    }
}


public class ThreadSafeList<T> : IEnumerable<T>
{
    protected readonly List<T> _innerList = new List<T>();
    protected readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

    public delegate void CollectionChangedHandler();

    public CollectionChangedHandler? CollectionChanged;

    public virtual void Add(T item)
    {
        _lock.EnterWriteLock();
        try
        {
            _innerList.Add(item);
        }
        finally
        {
            _lock.ExitWriteLock();
            CollectionChanged?.Invoke();
        }
    }
    public void Remove(T item)
    {
        _lock.EnterWriteLock();
        try
        {
            _innerList.Remove(item);
        }
        finally
        {
            _lock.ExitWriteLock();
            CollectionChanged?.Invoke();
        }
    }
    public void Remove(Func<T, bool> cond)
    {
        _lock.EnterWriteLock();
        try
        {
            foreach (var item in _innerList.Where(cond).ToArray())
                _innerList.Remove(item);
        }
        finally
        {
            _lock.ExitWriteLock();
            CollectionChanged?.Invoke();
        }
    }

    public T Get(int index)
    {
        _lock.EnterReadLock();
        try
        {
            return _innerList[index];
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public IEnumerator<T> GetEnumerator() => _innerList.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public int Count
    {
        get
        {
            _lock.EnterReadLock();
            try
            {
                return _innerList.Count;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }
}

public class FundTipList : ThreadSafeList<FundTip>
{
    public override void Add(FundTip item)
    {
        _lock.EnterWriteLock();
        bool add = false;
        try
        {
            // 不重复添加
            if (!_innerList.Any(x => x.FundId == item.FundId && x.Type == item.Type))
            {
                _innerList.Add(item);
                add = true;
            }
        }
        finally
        {
            _lock.ExitWriteLock();
            if (add)
                CollectionChanged?.Invoke();
        }
    }
}

