using CommunityToolkit.Mvvm.Messaging;
using FMO.Logging;
using FMO.Models;
using FMO.Trustee;
using FMO.Utilities;
using LiteDB;
using Serilog;
using System.Runtime.CompilerServices;

namespace FMO.ESigning;



/// <summary>
/// 失败记录
/// </summary>
/// <param name="Platform"></param>
public record SigningWorkFailure(string Platform, string Method, DateTime Time, string Message);

public record SigningRunMessage(string Name, bool IsRunning);

public class ESigningWorker
{
    public ESigningWorker(IEnumerable<ISigning> enumerable)
    {
        ESigningPlatforms = enumerable.ToArray();
    }


    private DateTime _beginDate = new DateTime(2000, 1, 1);//new DateTime(2025, 8, 10);

    private DateTime _lastWorkTime = default;

    public ISigning[] ESigningPlatforms { get; }

    public async Task SyncCustmersOnce(IEnumerable<ISigning>? signings = null) => await RunTask(SyncCustmersImpl(signings));


    public async Task SyncCustmersImpl(IEnumerable<ISigning>? signings = null)
    {
        if (signings is null || signings.Any()) signings = ESigningPlatforms;

        SigningLoger.LogWorker(nameof(SyncCustmersOnce));

        // 获取历史
        using var db = DbHelper.Platform();
        foreach (var sign in signings)
        {
            var config = db.GetCollection<ISigningConfig>().FindById(sign.Id);
            if (!config?.IsEnable ?? true) continue;

            // 获取历史 
            var rec = db.GetCollection<SigningWorkRecord>().FindById(sign.Id) ?? new() { Id = sign.Id };
            if (rec.QueryCustomerTime == default)
                rec.QueryCustomerTime = _beginDate;

            try
            {
                var customers = await sign.QueryCustomerAsync(rec.QueryCustomerTime);

                // 合并
                if (customers?.Length > 0)
                {
                    MergeCustomers(customers);

                    rec.QueryCustomerTime = customers.Max(x => x.CreateTime);
                    db.GetCollection<SigningWorkRecord>().Upsert(rec);
                }
            }
            catch (Exception e)
            {
                LogEx.Error(e);
            }
        }
    }


    public async Task SyncQualificationsOnce(IEnumerable<ISigning>? signings = null) => await RunTask(SyncQualificationsImpl(signings));


    public async Task SyncQualificationsImpl(IEnumerable<ISigning>? signings = null)
    {
        if (signings is null || signings.Any()) signings = ESigningPlatforms;

        SigningLoger.LogWorker(nameof(SyncQualificationsOnce));

        // 获取历史
        using var db = DbHelper.Platform();

        List<InvestorQualification> qs = new();
        foreach (var sign in signings)
        {
            var config = db.GetCollection<ISigningConfig>().FindById(sign.Id);
            if (!config?.IsEnable ?? true) continue;

            var rec = db.GetCollection<SigningWorkRecord>().FindById(sign.Id) ?? new() { Id = sign.Id };
            if (rec.QueryQualificationTime == default)
                rec.QueryQualificationTime = _beginDate;

            var exist_ids = GetExistsQualificationIds(sign.Id);
            var data = await sign.QueryQualificationAsync(rec.QueryQualificationTime);

            // 排除已存在的
            foreach (var item in data.ExceptBy(exist_ids, x => x.ExternalId))
            {
                await Task.Delay(500);
                await sign.QueryQualificationAsync(item);
                qs.Add(item);
            }

            rec.QueryQualificationTime = new DateTime(data.Max(x => x.Date), default);
            db.GetCollection<SigningWorkRecord>().Upsert(rec);
        }

        using (var db2 = DbHelper.Base())
            db2.GetCollection<InvestorQualification>().Upsert(qs);

    }



    public async Task SyncOrdersOnce(IEnumerable<ISigning>? signings = null) => await RunTask(SyncOrdersImpl(signings));


    public async Task SyncOrdersImpl(IEnumerable<ISigning>? signings = null)
    {
        if (signings is null || signings.Any()) signings = ESigningPlatforms;

        SigningLoger.LogWorker(nameof(SyncOrdersOnce));

        // 获取历史
        using var db = DbHelper.Platform();

        var cidMap = GetInvestorIdMap();
        List<TransferOrder> orders = new();
        foreach (var sign in signings)
        {
            var config = db.GetCollection<ISigningConfig>().FindById(sign.Id);
            if (!config?.IsEnable ?? true) continue;

            var rec = db.GetCollection<SigningWorkRecord>().FindById(sign.Id) ?? new() { Id = sign.Id };
            if (rec.QueryOrderTime == default)
                rec.QueryOrderTime = _beginDate;


            // 已存在的订单
            var exist_ids = GetExistsOrderIds(sign.Id);
            try
            {
                var data = await sign.QueryOrderAsync(rec.QueryOrderTime);

                var db2 = DbHelper.Base();

                // 排除已存在的
                foreach (var item in data.OrderBy(x => x.Date))
                {
                    if (item is null) continue;

                    await Task.Delay(500);

                    var (f, c) = db2.FindByName(item.FundName!);
                    if (f is not null)
                    {
                        item.FundId = f.Id;
                        item.FundName = f.Name;
                        item.ShareClass = c;
                    }
                    else { LogEx.Error($"Sync Order Fund Not Exists {item.FundName}, in {item.InvestorName} {item.Date} {item.Type}"); }

                    // 已存在的
                    if (exist_ids.TryGetValue(item.ExternalId!, out var oid))
                        item.Id = oid;

                    if (cidMap.TryGetValue(item.InvestorIdentity!, out var cid))
                        item.InvestorId = cid;
                    else LogEx.Error($"Sync Order Investor Not Exists {item.InvestorName}/{item.InvestorIdentity}, in {item.InvestorName} {item.Date} {item.Type}");

                    if (!await sign.QueryOrderAsync(item))
                        LogEx.Error($"获取Order文件失败 Customer:{item.InvestorName} Fund{item.FundName} {item.Date}");
                    orders.Add(item);
                }

                rec.QueryOrderTime = new DateTime(data.Max(x => x.Date), default);
                db.GetCollection<SigningWorkRecord>().Upsert(rec);
            }
            catch (Exception e)
            {
                LogEx.Error(e);
            }
        }

        DataTracker.OnBatchTransferOrder(orders);

    }














    private static string[] GetExistsQualificationIds(string identity)
    {
        using var db = DbHelper.Base();
        return db.GetCollection<InvestorQualification>().Find(x => x.Source == identity && x.ExternalId != null).Select(x => x.ExternalId!).ToArray() ?? [];
    }


    private static Dictionary<string, int> GetExistsOrderIds(string identity)
    {
        using var db = DbHelper.Base();
        return db.GetCollection<TransferOrder>().Find(x => x.Source == identity && x.ExternalId != null).Select(x => new { x.Id, x.ExternalId }).ToArray().ToDictionary(x => x.ExternalId!, x => x.Id);
    }

    private static Dictionary<string, int> GetInvestorIdMap()
    {
        using var db = DbHelper.Base();
        return db.GetCollection<Investor>().Query().Where(x => x.Identity != null).Select(x => new { x.Id, No = x.Identity!.Id }).ToArray().ToDictionary(x => x.No, x => x.Id);
    }


    /// <summary>
    /// 合并记录到数据库
    /// </summary>
    /// <param name="customers"></param>
    private static void MergeCustomers(Investor[] customers)
    {
        using var db = DbHelper.Base();
        // 获取已存在的
        var exist_ids = db.GetCollection<Investor>().FindAll().ToList();//.Where(x => data.Any(y => y.Item1.Identity == x.Identity)).ToArray();
        var manager = db.GetCollection<Manager>().FindOne(x => x.IsMaster);

        List<Investor> list = new();
        //
        foreach (var item in customers)
        {
            int idx = exist_ids.FindIndex(0, x => x.Identity?.Id == item.Identity?.Id);

            if (idx == -1)
            {
                if (exist_ids.Find(x => x.Name == item.Name && x.Identity == default) is Investor old)
                {
                    if (string.IsNullOrWhiteSpace(old.Phone)) old.Phone = item.Phone;
                    if (string.IsNullOrWhiteSpace(old.Email)) old.Email = item.Email;

                    if (old.Identity?.Type == IDType.Unknown)
                        old.Identity = item.Identity;

                    old.RiskEvaluation = item.RiskEvaluation;

                    if (old.Type == default) old.Type = item.Type;
                    list.Add(old);
                }
                else if (!item.Name.Contains("test"))
                    list.Add(item);
            }
            else
            {
                var old = exist_ids[idx];
                bool changed = false;
                if (string.IsNullOrWhiteSpace(old.Phone)) { old.Phone = item.Phone; changed = true; }
                if (string.IsNullOrWhiteSpace(old.Email)) { old.Email = item.Email; changed = true; }

                if (old.Identity?.Type == IDType.Unknown)
                {
                    old.Identity = item.Identity;
                    changed = true;
                }

                if (old.RiskEvaluation != item.RiskEvaluation)
                {
                    old.RiskEvaluation = item.RiskEvaluation;
                    changed = true;
                }

                if (old.Type == default) { old.Type = item.Type; changed = true; }
                if (item.Name == manager?.Name) { old.Type = AmacInvestorType.Manager; changed = true; }


                if (changed)
                    list.Add(old);
            }
        }

        db.GetCollection<Investor>().Upsert(list);

        //通知更新UI
        Task.Run(() =>
        {
            foreach (var item in list)
                WeakReferenceMessenger.Default.Send(item);
        });
    }

    private async Task RunTask(Task task, [CallerMemberName] string name = "")
    {
        WeakReferenceMessenger.Default.Send(new SigningRunMessage(name, true));

        try { await task; } catch (Exception e) { Log.Error($"{name} {e.Message}"); }

        WeakReferenceMessenger.Default.Send(new SigningRunMessage(name, false));
    }

    internal void Start()
    {
        Task.Run(async () =>
        {
            // 每分钟轮询
            using var periodicTimer = new PeriodicTimer(TimeSpan.FromSeconds(60));
            while (await periodicTimer.WaitForNextTickAsync())
                await LoopOnce();
        });
    }

    /// <summary>
    /// 每工作日8-18点，每小时整点运行一次
    /// </summary>
    private async Task LoopOnce()
    {
        var now = DateTime.Now;

        // 仅工作日，工作日间
        if (now.Hour < 8 || now.Hour > 18)
            return;
        if (!Days.IsTradingDay(now))
            return;

        if (now - _lastWorkTime < TimeSpan.FromHours(3))
            return;

        try
        {
            await SyncCustmersOnce(ESigningPlatforms);
        }
        catch (Exception e)
        {
            LogEx.Error(e);
        }


        try
        {
            await SyncQualificationsOnce(ESigningPlatforms);
        }
        catch (Exception e)
        {
            LogEx.Error(e);
        }

        try
        {
            await SyncOrdersOnce(ESigningPlatforms);
        }
        catch (Exception e)
        {
            LogEx.Error(e);
        }


        _lastWorkTime = now;
    }



    private void ClearLog()
    {
        using var fs = new FileStream("data\\platformlog.db", FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (fs.Length > 1024 * 1024 * 500)
        {
            var db = new LiteDatabase(@$"FileName=data\platformlog.db;Connection=Shared");
            // 条目
            var total = db.GetCollection<TrusteeCallHistory>().Count();
            var mid = db.GetCollection<TrusteeCallHistory>().Query().Skip(total / 2).Limit(1).FirstOrDefault();
            if (mid is not null)
                db.GetCollection<TrusteeCallHistory>().DeleteMany(x => x.Time.Date < mid.Time);
        }
    }
}