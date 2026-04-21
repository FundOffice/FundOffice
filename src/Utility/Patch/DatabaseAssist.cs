using CommunityToolkit.Mvvm.Messaging;
using FMO.Logging;
using FMO.Models;
using FMO.Trustee;
using LiteDB;
using Serilog;
using System.Text.RegularExpressions;
namespace FMO.Utilities;

internal record PatchRecord(int Id, DateTime Time);

public  static partial class DatabaseAssist
{




    /// <summary>
    /// 自检
    /// </summary>
    public static void SystemValidation()
    {
        using (var db = DbHelper.Base())
        {
            VerifyAndFixInvestorMission(db);

            ///////////
            // call 默认是null，和false不一致，所以为每个fund，设置一个默认值
            VerifyAndFixElements(db);
            ////////////////////////////

            VerifyAndFixDaily(db);
            ////////////////////////

            Patch();
        }


        using (var db = DbHelper.Platform())
        {
            LiteDB.ILiteCollection<PlatformSynchronizeTime> coll = db.GetCollection<PlatformSynchronizeTime>();
            coll.EnsureIndex(x => new { x.Identifier, x.Method }, true);
        }
    }

    private static void VerifyAndFixDaily(BaseDatabase db)
    {
        // 获取所有以 fv_ 开头的表
        var collections = db.GetCollectionNames()
            .Where(name => name.StartsWith("fv_"));

        foreach (var collectionName in collections)
        {
            try
            {
                var collection = db.GetCollection<DailyValue>(collectionName);
                var deletedCount = collection.DeleteMany(x => x.NetValue == 0);
            }
            catch (Exception ex) { LogEx.Error($"{ex.Message}"); }
        }
    }

    /// <summary>
    /// call 默认是null，和false不一致，所以为每个fund，设置一个默认值
    /// </summary>
    /// <param name="db"></param>
    private static void VerifyAndFixElements(BaseDatabase db)
    {
        var pair = db.GetCollection<FundFlow>().Query().Select(x => new { x.FundId, x.Id }).ToArray().OrderBy(x => x.Id).GroupBy(x => x.FundId).Select(x => new { FundId = x.Key, Id = x.First().Id });
        foreach (var item in pair)
        {
            var ele = db.GetCollection<FundElements>().FindById(item.FundId);
            if (!ele.Callback.Changes.ContainsKey(item.Id))
            {
                ele.Callback.SetValue(new(), item.Id);
                db.GetCollection<FundElements>().Update(ele);
            }
        }
    }

    /// <summary>
    /// 校验ta，为TransferRecord的InvestorId为0的设置Id
    /// </summary>
    /// <param name="db"></param>
    private static void VerifyAndFixInvestorMission(BaseDatabase db)
    {
        var d = db.GetCollection<TransferRecord>().FindAll().ToArray();
        var cc = db.GetCollection<Investor>().FindAll().ToArray();
        var cids = cc.Select(x => x.Id).ToArray();

        foreach (var item in d.ExceptBy([0, .. cids], x => x.InvestorId))
        {
            var tmp = cc.Where(x => x.Identity?.Id == item.InvestorIdentity).ToArray();

            // 没有找到investor
            if (tmp.Length == 0)
            {
                var c = new Investor { Name = item.InvestorName, Identity = new Identity { Id = item.InvestorIdentity } };
                db.GetCollection<Investor>().Insert(c);
                item.InvestorId = c.Id;

                WeakReferenceMessenger.Default.Send(new ToastMessage(LogLevel.Info, $"新增投资人 {item.InvestorName}，请完善材料"));
            }
            else if (tmp.Length == 1)
                item.InvestorId = tmp.First().Id;
            else
            {
                Log.Error($"TransferRecord {item.Id} {item.FundName} {item.InvestorName} 与多个Inverstor对应");
                WeakReferenceMessenger.Default.Send(new ToastMessage(LogLevel.Warning, $"{item.FundName} {item.InvestorName} 交易无法对应投资人，因为证件号重复"));
            }
        }
        db.GetCollection<TransferRecord>().Update(d);
    }

    public static void Miggrate()
    {
        using var db = DbHelper.Base();
        var objs = db.GetCollection(nameof(Manager)).FindAll().ToArray();
        var col = db.GetCollectionNames();
        foreach (var item in objs)
        {
            if (item["_id"].Type == BsonType.Int32) return;

            var id = item["_id"].AsString;
            item.Remove("_id");
            item.Add("_id", 1);
            item.Add(nameof(Identity), BsonMapper.Global.ToDocument(new Identity { Type = IDType.OrganizationCode, Id = id }));
        }
        db.DropCollection(nameof(Manager));
        db.GetCollection(nameof(Manager)).Insert(objs);

    }

    /// <summary>
    /// 当首次初始化时
    /// </summary>
    public static void InitPatch()
    {
        using var db = DbHelper.Base();
        var col = db.GetCollection<PatchRecord>();
        col.Upsert(patchs.Select(x => new PatchRecord(x.Key, DateTime.Now)));
    }

    private static void Patch()
    {
        using var db = DbHelper.Base();
        var col = db.GetCollection<PatchRecord>();
        foreach (var (k, v) in patchs)
        {
            if (col.FindById(k) is null)
            {
                v.Invoke(db);
                col.Upsert(new PatchRecord(k, DateTime.Now));
            }
        }
    }


    /// <summary>
    /// 更新结构，增加了OrderRequired
    /// </summary>
    /// <param name="db"></param>
    private static void UpdateTASchcame(BaseDatabase db)
    {
        db.GetCollection<TransferRequest>().Update(db.GetCollection<TransferRequest>().FindAll().ToList());
        db.GetCollection<TransferRecord>().Update(db.GetCollection<TransferRecord>().FindAll().ToList());
    }


    //private static void MapTA(BaseDatabase db)
    //{
    //    var btr = db.GetCollection<RaisingBankTransaction>().FindAll().OrderBy(x => x.Time).ToList();
    //    var orders = db.GetCollection<TransferOrder>().FindAll().OrderBy(x => x.Date).ToList();
    //    var requests = db.GetCollection<TransferRequest>().Find(x => x.IsOrderRequired).OrderBy(x => x.RequestDate).ToList();
    //    var records = db.GetCollection<TransferRecord>().Find(x => x.OrderRequired).OrderBy(x => x.ConfirmedDate).ToList();

    //    // var jo = requests.Join(records, x => x.ExternalId, x => x.ExternalId, (x, y) => new { x, y });

    //    // var unp = requests.Select(x=>x.ExternalId).Except(records.Select(x => x.ExternalRequestId)).Except(records.Select(x=>x.ExternalId)).ToList();
    //    var runp = records.ExceptBy(requests.Select(x => x.ExternalId), x => x.ExternalId).ExceptBy(requests.Select(x => x.ExternalId), x => x.ExternalRequestId).ToList();

    //    // // 构建时间线
    //    // var timeline = btr.Select(x => x.Time).Union(orders.Select(x => new DateTime(x.Date, TimeOnly.MinValue))).Union(requests.Select(x => new DateTime(x.RequestDate, TimeOnly.MinValue))).
    //    //      Union(records.Select(x => new DateTime(x.ConfirmedDate, TimeOnly.MinValue))).Distinct().ToList();

    //    ////////////////////////////////////// record
    //    /// 上对应request 下对应 transaction
    //    // 找已知的map 
    //    var rids = records.Select(x => x.Id).ToList();
    //    var mapTable = db.GetCollection<TransferMapping>();
    //    var exists = mapTable.Find(x => rids.Contains(x.RecordId)).ToList();
    //    foreach (var rec in records)
    //    {
    //        var old = exists.FirstOrDefault(x => x.RecordId == rec.Id) ?? new() { RecordId = rec.Id, OrderId = rec.OrderId };

    //        // 未匹配request
    //        if (old.RequestId == 0)
    //        {
    //            // cms的ExternalRequestId不匹配
    //            if (db.GetCollection<TransferRequest>().FindOne(x => x.FundId == rec.FundId && (x.ExternalId == rec.ExternalRequestId || x.ExternalId == rec.ExternalId)) is TransferRequest req)
    //                old.RequestId = req.Id;
    //            else if (requests.FirstOrDefault(x => x.FundId == rec.FundId && (x.ExternalId == rec.ExternalRequestId || x.ExternalId == rec.ExternalId)) is TransferRequest req2)
    //                old.RequestId = req2.Id;
    //            else if (requests.FirstOrDefault(x => (x.ExternalId == rec.ExternalRequestId || x.ExternalId == rec.ExternalId)) is TransferRequest req4)
    //                old.RequestId = req4.Id;
    //        }

    //        // 未匹配流水
    //        if (string.IsNullOrWhiteSpace(old.TransactionId))
    //        {
    //            var banks = db.GetCollection<InvestorBankAccount>().Find(x => x.OwnerId == rec.InvestorId).Select(x => x.Number);
    //            if (rec.IsSell()) // 只有赎回需要匹配
    //            {
    //                var limit = new DateTime(rec.ConfirmedDate, TimeOnly.MinValue);
    //                foreach (var t in db.GetCollection<RaisingBankTransaction>().Find(x => x.FundId == rec.FundId && x.Direction == TransctionDirection.Pay
    //                        && x.Time >= limit && (banks.Contains(x.CounterNo) || x.CounterName == rec.InvestorName)).OrderByDescending(x => x.Time))
    //                {
    //                    if (t.Amount == rec.RequestAmount)
    //                        old.TransactionId = t.Id;

    //                }
    //            }
    //        }
    //        mapTable.Upsert(old);
    //    }

    //    var list = mapTable.FindAll().ToList();

    //    ////////////////////////////////////// request
    //    /// 上对应order 下对应 record
    //    rids = requests.Select(x => x.Id).ToList();
    //    exists = mapTable.Find(x => rids.Contains(x.RequestId)).ToList();
    //    foreach (var req in requests)
    //    {

    //    }




    //}
}
