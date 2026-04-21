using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FMO.Logging;
using FMO.Models;
using FMO.Utilities;
using LiteDB;
using Serilog;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;

namespace FMO.Trustee;

internal record FundTrusteePair(int FundId, ITrustee Trustee, bool IsCleared);


/// <summary>
/// 执行结果
/// </summary>
/// <param name="Method"></param>
/// <param name="Returns"></param>
public record TrusteeWorkResult(string Method, IList<TrusteeWorker.WorkReturn> Returns);


//public record TrusteeWorkRecord(string Identifier, string Method, DateOnly Begin, DateOnly End, int Count);

/// <summary>
/// 产品对应api
/// </summary>
/// <param name="FundId"></param>
/// <param name="Identifier"></param>
public record TrusteeApiMap(int FundId, string Identifier);




public partial class TrusteeWorker : ObservableObject
{
    public class WorkConfig
    {
        public WorkConfig()
        {
        }

        [SetsRequiredMembers]
        public WorkConfig(string id) => Id = id;

        public required string Id { get; set; }

        /// <summary>
        /// 间隔时间 分
        /// </summary>
        public int Interval { get; set; } = 15;

        /// <summary>
        /// 上次执行时间
        /// </summary>
        public DateTime Last { get; set; }


        [BsonIgnore]
        public SemaphoreSlim Semaphore { get; } = new SemaphoreSlim(1, 1);

        public int GetLastRunIndex() => (int)(Last.Ticks / TimeSpan.TicksPerMinute / Interval);
    }



    public record WorkReturn(string Name, ReturnCode Code, object? Data = null);

    public const string TableRaisingBalance = "api_raising_balance";

    /// <summary>
    /// 每天检查一次Log，太大就清理
    /// </summary>
    private DateTime _clearLogTime = default;

    internal List<FundTrusteePair> Maps { get; } = new();

    private Dictionary<string, TrusteeMethodShotRange> _workRange = new();

    private DateOnly StartOfAny { get; }

    //private PeriodicTimer periodicTimer;

    ITrustee[] Trustees { get; }

    private WorkConfig RaisingBalanceConfig { get; set; }

    private WorkConfig TransferRecordConfig { get; set; }

    private WorkConfig DailyFeeConfig { get; set; }
    private WorkConfig TransferRequestConfig { get; set; }
    private WorkConfig RaisingAccountTransctionConfig { get; set; }



    private WorkConfig NetValueConfig { get; set; }



    (WorkConfig Config, Func<IEnumerable<ITrustee>, Task> Command)[] tasks;

    public TrusteeWorker(ITrustee[] trustees)
    {
        WeakReferenceMessenger.Default.RegisterAll(this);


        WorkConfig[] cfg;
        TrusteeApiMap[] maps;
        using (var db = DbHelper.Platform())
        {
            cfg = db.GetCollection<WorkConfig>().FindAll().ToArray();
            maps = db.GetCollection<TrusteeApiMap>().FindAll().ToArray();
        }
        RaisingBalanceConfig = cfg.FirstOrDefault(x => x.Id == nameof(ITrustee.QueryRaisingBalance)) ?? new(nameof(ITrustee.QueryRaisingBalance));
        TransferRecordConfig = cfg.FirstOrDefault(x => x.Id == nameof(ITrustee.QueryTransferRecords)) ?? new(nameof(ITrustee.QueryTransferRecords)) { Interval = 60 }; // 每6个小时
        TransferRequestConfig = cfg.FirstOrDefault(x => x.Id == nameof(ITrustee.QueryTransferRequests)) ?? new(nameof(ITrustee.QueryTransferRequests));
        DailyFeeConfig = cfg.FirstOrDefault(x => x.Id == nameof(ITrustee.QueryFundDailyFee)) ?? new(nameof(ITrustee.QueryFundDailyFee)) { Interval = 60 * 12 }; // 每天一次
        RaisingAccountTransctionConfig = cfg.FirstOrDefault(x => x.Id == nameof(ITrustee.QueryRaisingAccountTransction)) ?? new(nameof(ITrustee.QueryRaisingAccountTransction));
        NetValueConfig = cfg.FirstOrDefault(x => x.Id == nameof(ITrustee.QueryNetValue)) ?? new(nameof(ITrustee.QueryNetValue)) { Interval = 30 }; // 每个小时


        RaisingBalanceConfig.Interval = 15;
        TransferRecordConfig.Interval = 60;
        TransferRequestConfig.Interval = 60;
        DailyFeeConfig.Interval = 60 * 12;
        RaisingAccountTransctionConfig.Interval = 15;
        NetValueConfig.Interval = 30;


        // 基金映射
        using (var db = DbHelper.Base())
        {
            var funds = db.GetCollection<Fund>().Query().Select(x => new { x.Id, x.SetupDate, x.Trustee, x.Status }).ToList();

            foreach (var item in funds)
            {
                if (trustees.FirstOrDefault(x => x.IsSuit(item.Trustee)) is ITrustee trustee)
                    Maps.Add(new(item.Id, trustee, item.Status > FundStatus.StartLiquidation));
            }


            // 所有任务的最小日期
            StartOfAny = db.GetCollection<Manager>().Query().First().SetupDate;
            // 防止空集合
            try { StartOfAny = funds.Where(x => x.SetupDate.Year > 1970).Min(x => x.SetupDate); } catch { }

        }



        Trustees = trustees;
        foreach (var t in trustees)
            t.Prepare();


        tasks = [
                // 募集余额查询任务：
                // 使用 RaisingBalanceConfig 配置（如执行间隔、上次运行时间等）
                // 触发 QueryRaisingBalanceOnceCommand 命令执行单次查询
                (Config: RaisingBalanceConfig, Command: QueryRaisingBalanceOnce),

                // 募集户流水查询任务：
                // 使用 RaisingAccountTransctionConfig 配置
                // 触发 QueryRaisingAccountTransctionOnceCommand 命令执行单次查询
                (Config: RaisingAccountTransctionConfig, Command: QueryRaisingAccountTransctionOnce),

                // 交易申请查询任务：
                // 使用 TransferRequestConfig 配置
                // 触发 QueryTransferRequestOnceCommand 命令执行单次查询
                (Config: TransferRequestConfig, Command: QueryTransferRequestOnce),

                // 交易确认查询任务：
                // 使用 TransferRecordConfig 配置
                // 触发 QueryTransferRecordOnceCommand 命令执行单次查询
                (Config: TransferRecordConfig, Command: QueryTransferRecordOnce),

                // 日常费用查询任务：
                // 使用 DailyFeeConfig 配置
                // 触发 QueryDailyFeeOnceCommand 命令执行单次查询
                (Config: DailyFeeConfig, Command: QueryDailyFeeOnce),

                (Config: NetValueConfig, Command: QueryNetValueOnce),
             ];
    }


    public ITrustee? Find(int fundId) => Maps.FirstOrDefault(x => x.FundId == fundId)?.Trustee;

    #region Impl

    /// <summary>
    /// 获取募集户余额
    /// </summary>
    /// <returns></returns>
    private async Task QueryRaisingBalanceImpl(IEnumerable<ITrustee>? trustees = null)
    {
        try
        {
            WeakReferenceMessenger.Default.Send(new ToastMessage(LogLevel.Info, $"开始同步 募集户余额"));
            if (trustees is null || trustees.Any()) trustees = Trustees;
            List<WorkReturn> ret = new();
            // 保存数据库
            using var db = DbHelper.Base();

            foreach (var tr in trustees)
            {
                if (!tr.IsValid)
                {
                    ret.Add(new(tr.Title, ReturnCode.ConfigInvalid));
                    continue;
                }

                try
                {
                    var rc = await tr.QueryRaisingBalance();

                    ///
                    // 保存数据库 
                    if (rc.Data is not null)
                        db.GetCollection<FundBankBalance>().Upsert(rc.Data);

                    ret.Add(new(tr.Title, rc.Code, rc.Data));
                }
                catch (Exception e)
                {
                    ret.Add(new(tr.Title, ReturnCode.Unknown));
                    Log.Error($"QueryRaisingBalanceOnce {e}");
                }
            }

            // 保存ret，程序加载时恢复，并生成消息
            db.DropCollection(TableRaisingBalance);
            db.GetCollection<WorkReturn>(TableRaisingBalance).Insert(ret);

            WeakReferenceMessenger.Default.Send(new TrusteeWorkResult(nameof(ITrustee.QueryRaisingBalance), ret));
            RaisingBalanceConfig.Last = DateTime.Now;
            Save(RaisingBalanceConfig);
        }
        catch (Exception e)
        {
            Log.Error($"{nameof(QueryRaisingBalanceOnce)} {e.Message}");
        }
    }



    /// <summary>
    /// 获取交易申请记录
    /// 
    /// 中信不返回当日数据
    /// 
    /// </summary>
    /// <returns></returns>
    private async Task QueryTransferRequestImpl(IEnumerable<ITrustee>? trustees = null)
    {
        try
        {
            WeakReferenceMessenger.Default.Send(new ToastMessage(LogLevel.Info, $"开始同步 交易申请"));
            if (trustees is null || trustees.Any()) trustees = Trustees;

            List<WorkReturn> ret = new();
            // 保存数据库
            var method = nameof(ITrustee.QueryTransferRequests);

            foreach (var tr in trustees)
            {
                if (!tr.IsValid)
                {
                    ret.Add(new(tr.Title, ReturnCode.ConfigInvalid));
                    continue;
                }

                try
                {
                    // 获取历史区间
                    var range = GetWorkedRange(tr.Identifier, method);

                    DateOnly begin = range.End, end = DateOnly.FromDateTime(DateTime.Now);
                    if (begin == end) begin = Days.PrevTradingDay(begin);

                    var rc = await tr.QueryTransferRequests(begin, end);
                    if (rc.Code != ReturnCode.Success && rc.Code != ReturnCode.TrafficLimit)
                    {
                        LogEx.Error($"{rc.Code} {rc.Data?.Count} {tr.Title} 获取的交易申请记录数据异常");
                        WeakReferenceMessenger.Default.Send(new ToastMessage(LogLevel.Error, $"{tr.Title} 获取的交易申请记录数据异常"));
                    }
                    ///
                    // 保存数据库，部分成功的也保存
                    if (rc.Data?.Count > 0)
                    {
                        // 如果返回有失败的，更新end
                        if (rc.Code != ReturnCode.Success)
                            end = rc.Data.Max(x => x.RequestDate);

                        // 统一更新处理
                        DataTracker.OnBatchTransferRequest(rc.Data);

                        // 如果有unset，表示数据异常，不保存进度
                        if (rc.Data?.Any(x => x.InvestorName == "unset" || x.FundName == "unset" || x.InvestorIdentity == "unset") ?? false)
                        {
                            ret.Add(new(tr.Title, ReturnCode.DataIsNotWellFormed, rc.Data));
                            WeakReferenceMessenger.Default.Send(new ToastMessage(LogLevel.Error, $"{tr.Title} 获取的交易申请记录存在未识别的主体"));
                            break;
                        }
                    }

                    // 更新进度
                    UpdateWorkedRange(range, begin, end);
                }
                catch (Exception e)
                {
                    ret.Add(new(tr.Title, ReturnCode.Unknown));

                    Log.Error($"QueryTransferRequestOnce {e}");
                }
            }

            // 保存ret，程序加载时恢复，并生成消息
            //db.DropCollection(TableRaisingBalance);
            //db.GetCollection<WorkReturn>(TableRaisingBalance).Insert(ret);

            WeakReferenceMessenger.Default.Send(new TrusteeWorkResult(method, ret));
            TransferRequestConfig.Last = DateTime.Now;
            Save(TransferRequestConfig);
        }
        catch (Exception e)
        {
            Log.Error($"{nameof(QueryTransferRequestOnce)} {e.Message}");
        }
    }



    /// <summary>
    /// 获取交易确认记录
    /// </summary>
    /// <returns></returns>
    private async Task QueryTransferRecordImpl(IEnumerable<ITrustee>? trustees = null)
    {
        try
        {
            WeakReferenceMessenger.Default.Send(new ToastMessage(LogLevel.Info, $"开始同步 交易确认"));
            if (trustees is null || trustees.Any()) trustees = Trustees;

            List<WorkReturn> ret = new();
            // 保存数据库
            using var db = DbHelper.Base();
            var funds = db.GetCollection<Fund>().FindAll().ToArray();
            var method = nameof(ITrustee.QueryTransferRecords);


            foreach (var tr in trustees)
            {
                if (!tr.IsValid)
                {
                    ret.Add(new(tr.Title, ReturnCode.ConfigInvalid));
                    continue;
                }

                try
                {
                    // 获取历史区间
                    var range = GetWorkedRange(tr.Identifier, method);

                    DateOnly begin = range.End, end = DateOnly.FromDateTime(DateTime.Now);
                    if (begin == end) begin = Days.PrevTradingDay(begin);

                    var rc = await tr.QueryTransferRecords(begin, end);

                    ///
                    // 保存数据库 
                    if (rc.Data?.Count > 0)
                    {
                        DataTracker.OnBatchTransferRecord(rc.Data);
                    }

                    // 如果有unset，表示数据异常，不保存进度
                    if (rc.Data?.Any(x => x.InvestorName == "unset" || x.FundName == "unset" || x.InvestorIdentity == "unset") ?? false)
                        break;

                    // 更新进度
                    UpdateWorkedRange(range, begin, end);

                    // 合并记录
                    ret.Add(new(tr.Title, rc.Code, rc.Data));

                }
                catch (Exception e)
                {
                    ret.Add(new(tr.Title, ReturnCode.Unknown));

                    Log.Error($"QueryTransferRecordOnce {e}");
                }
            }

            // 保存ret，程序加载时恢复，并生成消息
            //db.DropCollection(TableRaisingBalance);
            //db.GetCollection<WorkReturn>(TableRaisingBalance).Insert(ret);

            WeakReferenceMessenger.Default.Send(new TrusteeWorkResult(method, ret));
            TransferRecordConfig.Last = DateTime.Now;
            Save(TransferRecordConfig);
        }
        catch (Exception e)
        {
            Log.Error($"{nameof(QueryTransferRecordOnce)} {e.Message}");
        }
    }

    /// <summary>
    /// 获取每日费用明细
    /// </summary>
    /// <returns></returns>
    private async Task QueryDailyFeeImpl(IEnumerable<ITrustee>? trustees = null)
    {
        try
        {
            if (trustees is null || trustees.Any()) trustees = Trustees;

            List<WorkReturn> ret = new();
            // 保存数据库
            using var db = DbHelper.Base();

            var method = nameof(ITrustee.QueryFundDailyFee);

            foreach (var tr in trustees)
            {
                if (!tr.IsValid)
                {
                    ret.Add(new(tr.Title, ReturnCode.ConfigInvalid));
                    continue;
                }

                try
                {
                    // 获取历史区间
                    var range = GetWorkedRange(tr.Identifier, method);
                    DateOnly begin = range.End, end = DateOnly.FromDateTime(DateTime.Now);
                    if (begin == end) begin = begin.AddDays(-5);


                    var rc = await tr.QueryFundDailyFee(begin, end);

                    ///
                    // 保存数据库 
                    if (rc.Data is not null)
                    {
                        // 对齐Fund
                        foreach (var fs in rc.Data.GroupBy(x => x.FundCode))
                        {
                            var (f, c) = db.FindFundByCode(fs.Key);
                            if (f is null) continue;
                            foreach (var item in fs)
                            {
                                item.FundId = f.Id;
                                item.Class = c;
                            }
                        }

                        db.GetCollection<FundDailyFee>().Upsert(rc.Data);
                    }

                    // 更新进度
                    UpdateWorkedRange(range, begin, end);

                    // 合并记录
                    ret.Add(new(tr.Title, rc.Code, rc.Data));

                }
                catch (Exception e)
                {
                    ret.Add(new(tr.Title, ReturnCode.Unknown));
                    Log.Error($"QueryDailyFeeOnce {e}");
                }
            }

            // 保存ret，程序加载时恢复，并生成消息
            //db.DropCollection(TableRaisingBalance);
            //db.GetCollection<WorkReturn>(TableRaisingBalance).Insert(ret);

            WeakReferenceMessenger.Default.Send(new TrusteeWorkResult(method, ret));
            DailyFeeConfig.Last = DateTime.Now;
            Save(DailyFeeConfig);
        }
        catch (Exception e)
        {
            Log.Error($"{nameof(QueryDailyFeeOnce)} {e.Message}");
        }
    }


    /// <summary>
    /// 获取募集户流水
    /// </summary>
    /// <returns></returns>
    private async Task QueryRaisingAccountTransctionImpl(IEnumerable<ITrustee>? trustees = null)
    {
        WeakReferenceMessenger.Default.Send(new ToastMessage(LogLevel.Info, $"开始同步 募集户流水"));
        if (trustees is null || trustees.Any()) trustees = Trustees;

        List<WorkReturn> ret = new();
        // 保存数据库 
        var method = nameof(ITrustee.QueryRaisingAccountTransction);

        foreach (var tr in trustees)
        {
            if (!tr.IsValid)
            {
                ret.Add(new(tr.Title, ReturnCode.ConfigInvalid));
                continue;
            }

            try
            {
                // 获取历史区间
                var range = GetWorkedRange(tr.Identifier, method);

                DateOnly begin = range.End, end = DateOnly.FromDateTime(DateTime.Now);
                if (begin == end) begin = Days.PrevTradingDay(begin);

                var rc = await tr.QueryRaisingAccountTransction(begin, end);

                ///
                // 保存数据库 
                if (rc.Data is not null)
                {
                    DataTracker.OnRaisingBankTransaction(rc.Data);
                }

                // 合并记录
                ret.Add(new(tr.Title, rc.Code, rc.Data));


                // 更新进度
                UpdateWorkedRange(range, begin, end);
            }
            catch (Exception e)
            {
                ret.Add(new(tr.Title, ReturnCode.Unknown));
                Log.Error($"QueryRaisingAccountTransctionOnce {e}");
            }
        }

        // 保存ret，程序加载时恢复，并生成消息
        //db.DropCollection(TableRaisingBalance);
        //db.GetCollection<WorkReturn>(TableRaisingBalance).Insert(ret);

        WeakReferenceMessenger.Default.Send(new TrusteeWorkResult(nameof(ITrustee.QueryRaisingAccountTransction), ret));
        RaisingAccountTransctionConfig.Last = DateTime.Now;
        Save(RaisingAccountTransctionConfig);
    }



    /// <summary>
    /// 查询净值
    /// </summary>
    /// <returns></returns>

    private async Task QueryNetValueImpl(IEnumerable<ITrustee>? trustees = null)
    {
        if (trustees is null || trustees.Any()) trustees = Trustees;

        
        List<WorkReturn> ret = new();
        var method = nameof(ITrustee.QueryNetValue);
        foreach (var tr in trustees)
        {
            if (!tr.IsValid)
            {
                ret.Add(new(tr.Title, ReturnCode.ConfigInvalid));
                continue;
            }

            try
            {
                // 获取历史区间
                var range = GetWorkedRange(tr.Identifier, method);

                DateOnly begin = range.End, end = DateOnly.FromDateTime(DateTime.Now);
                if (begin == end) begin = Days.PrevTradingDay(begin);

                var rc = await tr.QueryNetValue(begin, end);

                ///
                // 保存数据库 
                if (rc.Data is not null)
                {
                    DataTracker.OnDailyValue(rc.Data);
                }

                // 合并记录
                ret.Add(new(tr.Title, rc.Code, rc.Data));


                // 更新进度
                UpdateWorkedRange(range, begin, end);
            }
            catch (Exception e)
            {
                ret.Add(new(tr.Title, ReturnCode.Unknown));
                Log.Error($"{method} {e}");
            }

            WeakReferenceMessenger.Default.Send(new TrusteeWorkResult(nameof(ITrustee.QueryNetValue), ret));
            NetValueConfig.Last = DateTime.Now;
            Save(NetValueConfig);
        }
    }
    #endregion

    /// <summary>
    /// 获取募集户流水
    /// </summary>
    /// <returns></returns>
    public async Task QueryRaisingAccountTransctionOnce(IEnumerable<ITrustee>? trustees = null) => await RunTask(QueryRaisingAccountTransctionImpl(trustees));

    /// <summary>
    /// 获取交易确认记录
    /// </summary>
    /// <returns></returns>
    public async Task QueryTransferRecordOnce(IEnumerable<ITrustee>? trustees = null) => await RunTask(QueryTransferRecordImpl(trustees));

    /// <summary>
    /// 获取每日费用明细
    /// </summary>
    /// <returns></returns>
    public async Task QueryDailyFeeOnce(IEnumerable<ITrustee>? trustees = null) => await RunTask(QueryDailyFeeImpl(trustees));

    /// <summary>
    /// 获取交易申请记录
    /// 
    /// 中信不返回当日数据
    /// 
    /// </summary>
    /// <returns></returns>
    public async Task QueryTransferRequestOnce(IEnumerable<ITrustee>? trustees = null) => await RunTask(QueryTransferRequestImpl(trustees));

    /// <summary>
    /// 获取募集户余额
    /// </summary>
    /// <returns></returns>
    public async Task QueryRaisingBalanceOnce(IEnumerable<ITrustee>? trustees = null) => await RunTask(QueryRaisingBalanceImpl(trustees));


    /// <summary>
    /// 查询净值
    /// </summary>
    /// <returns></returns>
    public async Task QueryNetValueOnce(IEnumerable<ITrustee>? trustees = null) => await RunTask(QueryNetValueImpl());

    public async Task QueryNetValueOnce(int fundId, string code, DateOnly begin, DateOnly end)
    {
        if (Maps.LastOrDefault(x => x.FundId == fundId) is FundTrusteePair pair)
        {
            var rc = await pair.Trustee.QueryNetValue(begin, end, code);

            ///
            // 保存数据库 
            if (rc.Data is not null)
            {
                DataTracker.OnDailyValue(rc.Data);
            }
        }
        else WeakReferenceMessenger.Default.Send(new ToastMessage(LogLevel.Warning, "未发现对应的API"));
    }


    private async Task RunTask(Task task, [CallerMemberName] string name = "")
    {
        WeakReferenceMessenger.Default.Send(new TrusteeRunMessage(name, true));

        try { await task; } catch (Exception e) { Log.Error($"{name} {e.Message}"); }

        WeakReferenceMessenger.Default.Send(new TrusteeRunMessage(name, false));
    }


    [RelayCommand]
    public void Rebuild(string method)
    {
        using (var pdf = DbHelper.Platform()) //删除记录
            pdf.GetCollection("TrusteeMethodShotRange").Delete(method);
    }


    private TrusteeMethodShotRange GetWorkedRange(string idf, string method)
    {
        string key = $"{idf}.{method}";
        using var db = DbHelper.Platform();
        var r = db.GetCollection<TrusteeMethodShotRange>().FindById(key);
        if (r is not null && r.End < StartOfAny) r.End = StartOfAny;
        return r ?? new(key, StartOfAny, StartOfAny);
    }

    private void UpdateWorkedRange(TrusteeMethodShotRange r, DateOnly begin, DateOnly end)
    {
        r.Merge(begin, end);
        using var pdb = DbHelper.Platform();
        pdb.GetCollection<TrusteeMethodShotRange>().Upsert(r);
    }


    //private async void OnTimer2(object? state)
    //{
    //    var t = DateTime.Now;

    //    // 分钟位
    //    var minute = t.Ticks / TimeSpan.TicksPerMinute; //  new DateTime(t.Year, t.Month, t.Day, t.Hour, t.Minute, 0);
    //    if (minute % RaisingBalanceConfig.Interval == 0)
    //    {
    //        await RaisingBalanceConfig.Semaphore.WaitAsync();
    //        try
    //        {
    //            // 检验是否与上次运行时间不一样
    //            //if (t.Hour != RaisingBalanceConfig.Last.Hour || t.Minute != RaisingBalanceConfig.Last.Minute)
    //            if (minute / RaisingBalanceConfig.Interval != RaisingBalanceConfig.GetLastRunIndex())
    //                await QueryRaisingBalanceOnceCommand.ExecuteAsync(null);
    //        }
    //        catch { }
    //        finally { RaisingBalanceConfig.Semaphore.Release(); }
    //    }

    //    // 募集户流水
    //    if (minute % RaisingAccountTransctionConfig.Interval == 0)
    //    {
    //        await RaisingAccountTransctionConfig.Semaphore.WaitAsync();
    //        try
    //        {
    //            if (minute / RaisingAccountTransctionConfig.Interval != RaisingAccountTransctionConfig.GetLastRunIndex())
    //                await QueryRaisingAccountTransctionOnceCommand.ExecuteAsync(null);
    //        }
    //        catch { }
    //        finally { RaisingAccountTransctionConfig.Semaphore.Release(); }
    //    }

    //    // 交易申请 
    //    if (minute % TransferRequestConfig.Interval == 0)
    //    {
    //        await TransferRequestConfig.Semaphore.WaitAsync();
    //        try
    //        {
    //            if (minute / TransferRequestConfig.Interval != TransferRequestConfig.GetLastRunIndex())
    //                await QueryTransferRequestOnceCommand.ExecuteAsync(null);
    //        }
    //        catch { }
    //        finally { TransferRequestConfig.Semaphore.Release(); }
    //    }

    //    // 交易确认
    //    if (minute % TransferRecordConfig.Interval == 0)
    //    {
    //        await TransferRecordConfig.Semaphore.WaitAsync();
    //        try
    //        {
    //            if (minute / TransferRecordConfig.Interval != TransferRecordConfig.GetLastRunIndex())
    //                await QueryTransferRecordOnceCommand.ExecuteAsync(null);
    //        }
    //        catch { }
    //        finally { TransferRecordConfig.Semaphore.Release(); }
    //    }


    //    // 费用
    //    if (minute % DailyFeeConfig.Interval == 0)
    //    {
    //        await DailyFeeConfig.Semaphore.WaitAsync();
    //        try
    //        {
    //            if (minute / DailyFeeConfig.Interval != DailyFeeConfig.GetLastRunIndex())
    //                await QueryDailyFeeOnceCommand.ExecuteAsync(null);
    //        }
    //        catch { }
    //        finally { DailyFeeConfig.Semaphore.Release(); }
    //    }
    //}

    private async void LoopOnce()
    {
        var now = DateTime.Now;
        var minuteIndex = now.Ticks / TimeSpan.TicksPerMinute;

        /// 每天检查清理log
        if ((now - _clearLogTime).Days > 1)
        {
            ClearLog();
            _clearLogTime = now;
        }

        // 是否非工作时间 8-19点
        bool offwork = (now.Hour < 8 || now.Hour >= 19);

        if (offwork) return;

        var ava = Maps.Where(x => !x.IsCleared).Select(x => x.Trustee).Distinct().ToList();


        foreach (var (Config, Task) in tasks)
        {
            var interval = offwork && 60 > Config.Interval ? 60 : Config.Interval;

            if (minuteIndex % interval == 0)
            {
                await Config.Semaphore.WaitAsync();
                try
                {
                    if (minuteIndex / Config.Interval != Config.GetLastRunIndex())
                    {
                        await Task(ava);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"Trustee Worker Error executing command: {ex.Message}");
                }
                finally
                {
                    Config.Semaphore.Release();
                }
            }
        }
    }

    internal void Start()
    {
        Task.Run(async () =>
        {
            using var periodicTimer = new PeriodicTimer(TimeSpan.FromSeconds(5));
            while (await periodicTimer.WaitForNextTickAsync())
                LoopOnce();
        });
    }
    private void Save(WorkConfig workConfig)
    {
        using var db = DbHelper.Platform();
        db.GetCollection<WorkConfig>().Upsert(workConfig);
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

    private DateOnly StartOfAnyWork()
    {
        using var db = DbHelper.Base();
        var dates = db.GetCollection<Fund>().Query().Select(x => x.SetupDate).ToList();
        dates.Add(db.GetCollection<Manager>().FindById(1).SetupDate);

        return dates.Where(x => x != default).Min();
    }
}
