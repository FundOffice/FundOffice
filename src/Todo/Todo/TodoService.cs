using CommunityToolkit.Mvvm.Messaging;
using FMO.Logging;
using FMO.Models;
using FMO.Utilities;
using LiteDB;
using System.Collections.Concurrent;

namespace FMO.Todo;

public static class TodoService
{
    private static ConcurrentBag<ITodo> _Todos = [];

    public static ITodo[]? GetAll() => _Todos.Where(x => x.Status == TotoStatus.None).ToArray();

    public static void Register<T>(T todo) where T : ITodo
    {
        _Todos.Add(todo);

        using var db = DbHelper.Base();

        // 如果UniqueId不为null，说明这是一个具有唯一标识的Todo，需要先将之前的同类Todo标记为已忽略
        if (todo.UniqueId is not null)
            db.GetCollection<ITodo>().UpdateMany($"{{ '{nameof(ITodo.Status)}':'{nameof(TotoStatus.Ignored)}' }}", $"$.{nameof(ITodo.UniqueId)}='{todo.UniqueId}'");
        
        db.GetCollection<ITodo>().Insert(todo);
        WeakReferenceMessenger.Default.Send((ITodo)todo);
    }

    public static void Initialize()
    {
        using var db = DbHelper.Base();
        var col = db.GetCollection<BsonDocument>("Todo");
        foreach (var doc in col.Find($"$.{nameof(ITodo.Status)}='{nameof(TotoStatus.None)}'"))
        {
            try
            {
                ITodo todo = BsonMapper.Global.Deserialize<ITodo>(doc);

                _Todos.Add(todo);
            }
            catch
            {
                // 类型不对、数据损坏 → 直接跳过
                continue;
            }
        }


        DataTracker.Hook(AutoHugeRedemption);
    }


    /// <summary>
    /// 
    /// </summary>
    /// <param name="dv"></param>
    /// <exception cref="NotImplementedException"></exception>
    public static void AutoHugeRedemption(IEnumerable<TransferRequest> tr)
    {
        using var db = DbHelper.Base();
        // 忽略子份额，按基金分组
        foreach (var fv in tr.GroupBy(x => x.FundId))
        {
            foreach (var item in fv.GroupBy(x => x.RequestDate))
            {
                // 计算开放日的总份额
                var openday = item.Key;
                string fundName = fv.First().FundName;
                var code = fv.First().FundCode!;

                var dv = db.GetDailyCollection(fv.Key).FindOne(x => x.Date == openday);
                if (dv is null || dv.NetAsset == 0)
                {
                    LogEx.Warning($"基金净值数据异常，无法进行巨额赎回监控，基金：{fundName}，日期：{openday}");
                    continue;
                }


                // 赎回份额
                // 方案一，统计tr中的，但可能存在数据不全的情况（如部分赎回记录未同步到系统），导致监控失效
                var redemMoney = item.Where(x => x.RequestType is TransferRequestType.Redemption or TransferRequestType.ForceRedemption).Sum(x => x.RequestShare * dv.NetValue + x.RequestAmount);

                var ratio = (dv.NetAsset - redemMoney) / dv.NetAsset;

                var defRatio = db.GetCollection<FundElements>().FindById(fv.Key).HugeRedemptionRatio.Value;
                if (defRatio == 0)
                {
                    LogEx.Warning($"基金巨额赎回监控未设置阈值，无法进行监控，基金：{fundName}");
                    continue;
                }

                // 发生巨额赎回，生成Todo
                if (ratio > defRatio)
                {
                    Register(new HugeRedemptionTodo
                    {
                        FundCode = code,
                        FundName = fundName,
                        OpenDay = openday,
                        CreateTime = DateTime.Now,
                        DefinedRatio = defRatio,
                        RealRatio = ratio,
                        FundId = fv.Key,
                    });
                }

            }

        }


    }

}