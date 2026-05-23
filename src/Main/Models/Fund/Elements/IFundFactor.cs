using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace FMO.Models;

public interface IFundFactor
{
    internal string Id { get; }

    int FundId { get; set; }

    int FlowId { get; set; }

    int ShareId { get; set; }

    string FactorId { get; }
}


public class FundFactor : IFundFactor
{
    //public const string Singleton = "singleton";

    public string Id => $"{FundId}.{FlowId}.{ShareId}.{FactorId}";

    public required int FundId { get; set; }

    public required int FlowId { get; set; }

    /// <summary>
    /// 统一值=Singleton
    /// </summary>
    public int ShareId { get; set; } = ShareClass.Singleton;

    public required string FactorId { get; init; }
}

public class FundFactor<T> : FundFactor
{
    public FundFactor()
    {
    }

    [SetsRequiredMembers]
    public FundFactor(string factorId, int fundId, int flowId, T data)
    {
        FundId = fundId;
        FlowId = flowId;
        FactorId = factorId;
        Data = data;
    }

    [SetsRequiredMembers]
    public FundFactor(string factorId, int fundId, int flowId, int shareId, T data)
    {
        FundId = fundId;
        FlowId = flowId;
        ShareId = shareId;
        FactorId = factorId;
        Data = data;
    }



    public required T Data { get; set; }
}



public class SingletonFactorItem<T>  
{
    protected readonly ImmutableArray<(int FlowId, FundFactor<T> Fact)> _flowGroupCache;

    public SingletonFactorItem(IEnumerable<FundFactor<T>> data)
    {
        _flowGroupCache = data.GroupBy(f => f.FlowId).Select(x => (x.Key, x.First())).ToImmutableArray();
    }

    public virtual T? this[int flowId] => _flowGroupCache.FirstOrDefault(x => x.FlowId <= flowId).Fact is { } f ? f.Data : default;

    public static implicit operator T?(SingletonFactorItem<T> instance) => instance._flowGroupCache.FirstOrDefault().Fact is { } f ? f.Data : default;
}




public class ShareClassFactorItem(IEnumerable<FundFactor<ShareClass[]>> data) : SingletonFactorItem<ShareClass[]>(data)
{
    public (int FlowId, ShareClass[] Fact)[] GetShares() => _flowGroupCache.Select(x => (x.FlowId, x.Fact.Data)).ToArray();

    public override ShareClass[] this[int flowId] => _flowGroupCache.FirstOrDefault(x => x.FlowId <= flowId).Fact is { } f && f.Data.Length > 0 ? f.Data : ShareClass.Default;
}


public class FactorItem<T>
{

    protected readonly ImmutableArray<(int FlowId, FundFactor<T>[] Fact)> _flowGroupCache;

    private ImmutableDictionary<int, InheritMap> _shares;



    public FactorItem(IEnumerable<FundFactor<T>> data, ImmutableDictionary<int, InheritMap> shareConfigMap)
    {
        _flowGroupCache = data.GroupBy(f => f.FlowId).Select(x => (x.Key, x.ToArray())).ToImmutableArray();
        _shares = shareConfigMap;
    }

    public T?[] this[int flowId, params int[] classId] => GetValues(flowId, classId);

    public T? this[int flowId] => this[flowId, ShareClass.Singleton][0];


    /// <summary>
    /// 查值规则
    /// 先找当前flowid，对应share，没有找singleton
    /// </summary>
    /// <param name="flowId"></param>
    /// <param name="classId"></param>
    /// <returns></returns>
    public virtual T?[] GetValues(int flowId, int[] classId)
    {
        if (classId.Length == 0) return [];

        var values = new T?[classId.Length];

        for (var i = 0; i < classId.Length; i++)
        {
            var sc = classId[i];
            values[i] = ResolveValue(flowId, sc);
        }
        return values;
    }

    /// <summary>
    /// 核心查询：按 FlowId 倒序追溯，Singleton 绝对优先
    /// </summary>
    /// <param name="targetFlowId">目标 FlowId</param>
    /// <param name="classId">要查询的份额标识（对应 ShareType.Id）</param>
    /// <returns>匹配到的值，未找到则返回 default(T)</returns>
    /// <summary>
    /// 核心查询：按 Inherit 链回溯，Singleton 绝对优先
    /// </summary>
    /// <summary>
    /// 核心查询：精确匹配 > Singleton > 按 Inherit 向前追溯
    /// </summary>
    private T? ResolveValue(int targetFlowId, int classId)
    {
        // 当前正在查找的份额标识（会随 Inherit 规则动态切换）
        var currentLookupId = classId;

        // 检查targetFlowId，如果没有这个flow的值，应该从inherit开始
        //var excet = _flowGroupCache.FirstOrDefault(x => x.FlowId == targetFlowId);
        //if (excet == default) currentLookupId = _shares.TryGetValue(targetFlowId, out var scs) && scs.FirstOrDefault(x => x.Id == classId) is ShareClass sc  ? sc.Inherit : ShareClass.Singleton;

        // 从 targetFlowId 开始，按时间倒序遍历所有有数据的 Flow
        foreach (var (flowId, facts) in _flowGroupCache.Where(x => x.FlowId <= targetFlowId).OrderByDescending(x => x.FlowId))
        {
            var map = _shares[currentLookupId];
            while (flowId < map.FlowId) // 当前share无效了，因为没有定义
            {
                currentLookupId = map.Inherit;
                map = _shares[currentLookupId];
            }

            // 🥇 优先级1：当前 Flow 精确匹配目标份额
            if (facts.FirstOrDefault(f => f.ShareId == currentLookupId) is FundFactor<T> exactMatch)
            {
                return exactMatch.Data;
            }

            // 🥈 优先级2：当前 Flow 存在 Singleton 值（作为当前层级的兜底）
            if (facts.Length == 1 && facts[0].ShareId == ShareClass.Singleton)
            {
                return facts[0].Data;
            }

        }

        // 🛡️ 全程未匹配到值，返回默认值
        return default;
    }


    //private ShareClass[] GetShareClasses(int flowId) => _shares.TryGetValue(flowId, out var d) ? d : _shares.LastOrDefault(x => x.Key < flowId).Value;


    public virtual (T? Old, T? New)[] GetInheritValues(int flowId, params int[] classId)
    {
        var values = GetValues(flowId, classId);

        var values2 = GetValues(flowId - 1, classId);

        if (values.Distinct().Count() == 1)
            return [(values2[0], values[0])];

        return Enumerable.Range(0, classId.Length).Select(x => (values2[x], values[x])).ToArray();
    }

}