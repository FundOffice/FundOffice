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




public class SingletonValueFactorItem<T> where T : struct
{
    /// <summary>
    /// 按flowid 倒序序，保证FirstOrDefault拿到的就是当前flowid的值
    /// </summary>
    protected readonly ImmutableArray<(int FlowId, FundFactor<T> Factor)> _flowGroupCache;
    public SingletonValueFactorItem(IEnumerable<FundFactor<T>> data)
    {
        _flowGroupCache = data.GroupBy(f => f.FlowId).Select(x => (x.Key, x.First())).ToImmutableArray();
    }
    public virtual T? this[int flowId] => _flowGroupCache.FirstOrDefault(x => x.FlowId <= flowId).Factor is { } f ? f.Data : null;


    public static implicit operator T?(SingletonValueFactorItem<T> instance) => instance.Current;

    public bool HasValue => _flowGroupCache.Length > 0;

    public T? Current => _flowGroupCache.FirstOrDefault().Factor is { } f ? f.Data : null;
}


public class SingletonFactorItem<T> where T : class
{
    /// <summary>
    /// 按flowid 倒序序，保证FirstOrDefault拿到的就是当前flowid的值
    /// </summary>
    protected readonly ImmutableArray<(int FlowId, FundFactor<T> Factor)> _flowGroupCache;

    public SingletonFactorItem(IEnumerable<FundFactor<T>> data)
    {
        _flowGroupCache = data.GroupBy(f => f.FlowId).Select(x => (x.Key, x.First())).ToImmutableArray();
    }

    public virtual T? this[int flowId] => _flowGroupCache.FirstOrDefault(x => x.FlowId <= flowId).Factor is { } f ? f.Data : null;

    public static implicit operator T?(SingletonFactorItem<T> instance) => instance.Current;

    public bool HasValue => _flowGroupCache.Length > 0;

    public T? Current => _flowGroupCache.FirstOrDefault().Factor is { } f ? f.Data : null;
}




public class ShareClassFactorItem(IEnumerable<FundFactor<ShareClass[]>> data) : SingletonFactorItem<ShareClass[]>(data)
{
    public (int FlowId, ShareClass[] Factor)[] GetShares() => _flowGroupCache.Select(x => (x.FlowId, x.Factor.Data)).ToArray();

    public override ShareClass[] this[int flowId] => _flowGroupCache.FirstOrDefault(x => x.FlowId <= flowId).Factor is { } f && f.Data.Length > 0 ? f.Data : [];
}


public class FactorItem<T> where T : class
{
    /// <summary>
    /// 按flowid 倒序，保证FirstOrDefault拿到的就是当前flowid的值
    /// </summary>
    protected readonly ImmutableSortedDictionary<int, FundFactor<T>[]> _flowGroupCache;
    private readonly (int FlowId, int[] ClassIds)[] _shares;
    private ImmutableDictionary<int, InheritMap> _shareMap;



    public FactorItem(IEnumerable<FundFactor<T>> data, (int FlowId, ShareClass[] Factor)[] shares, ImmutableDictionary<int, InheritMap> shareConfigMap)
    {
        _flowGroupCache = data.GroupBy(f => f.FlowId).ToImmutableSortedDictionary(g => g.Key, g => g.ToArray(), Comparer<int>.Create((x, y) => y.CompareTo(x)));
        _shares = shares.Select(x => (x.FlowId, x.Factor.Select(f => f.Id).ToArray())).ToArray();
        _shareMap = shareConfigMap;
    }

    public T?[] this[int flowId, params int[] classId] => GetValues(flowId, classId);

    public T?[] this[int flowId] => GetValues(flowId);

    public virtual T?[] Current => GetValues(_flowGroupCache.FirstOrDefault().Key);

    public bool HasValue => _flowGroupCache.Count > 0;

    /// <summary>
    /// 查值规则
    /// 先找当前flowid，对应share
    /// </summary>
    /// <param name="flowId"></param>
    /// <param name="classId"></param>
    /// <returns></returns>
    public virtual T?[] GetValues(int flowId, int[] classId)
    {
        if (classId?.Length is null or 0) return [];

        var resolved = classId.Select(x => Resolve(flowId, x)).ToArray();
        if (resolved.Distinct().Count() == 1) // 继承自同一个值
            return [GetValue(resolved[0])];

        return resolved.Select(x => GetValue(x)).ToArray();
    }


    public virtual T?[] GetValues(int flowId) => GetValues(flowId, _shares.FirstOrDefault(x => x.FlowId <= flowId).ClassIds);


    public (T? Old, T? New) GetValueWithInherit(int flowId, int classId)
    {
        var (c, p) = ResolveWithInherit(flowId, classId);
        return (GetValue(p), GetValue(c));
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

        // 从 targetFlowId 开始，按时间倒序遍历所有有数据的 Flow
        foreach (var (flowId, facts) in _flowGroupCache.Where(x => x.Key <= targetFlowId).OrderByDescending(x => x.Key))
        {
            while (flowId < ShareClass.GetFlow(currentLookupId)) // 当前share无效了，因为没有定义
            {
                if (_shareMap.TryGetValue(currentLookupId, out var map) && _shareMap.ContainsKey(map.Inherit) && map.Inherit < currentLookupId)
                    currentLookupId = map.Inherit;
                else // 没有更上层的 Inherit 定义了，直接跳出循环
                    break;
            }
            if (flowId < ShareClass.GetFlow(currentLookupId))// 当前share无效了，因为没有定义
                return default;

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

    private (int FlowId, int Index) Resolve(int targetFlowId, int classId)
    {
        var currentLookupId = classId;

        foreach (var kvp in _flowGroupCache)
        {
            var flowId = kvp.Key;

            // 跳过比目标 FlowId 大的数据（因为本身是倒序，所以这些是最先遍历到的）
            if (flowId > targetFlowId)
                continue;

            var facts = kvp.Value;

            // shareId 可能因为 inherit关系向上追溯，直到找到一个有效的shareId或者没有了inherit为止
            // shareId 根据flowId的变化可能会失效，所以需要在每个flowId层级都检查一下当前shareId是否有效，如果无效了就继续往上追溯inherit，直到找到一个有效的shareId或者没有了inherit为止
            while (flowId < ShareClass.GetFlow(currentLookupId))
            {
                if (_shareMap.TryGetValue(currentLookupId, out var map) && _shareMap.ContainsKey(map.Inherit) && map.Inherit < currentLookupId)
                    currentLookupId = map.Inherit;
                else
                    break;
            } 

            // 当前share无效了，因为没有定义
            // flowId 早于 currentLookupId定义的flow
            if (flowId < ShareClass.GetFlow(currentLookupId))
                return default;

            for (var i = 0; i < facts.Length; i++)
                if (facts[i].ShareId == currentLookupId)
                    return (flowId, i);


            // 多份额统一要素 
            if (kvp.Value.Length == 1 && kvp.Value[0].ShareId == ShareClass.MakeId(flowId, 0))
                return (flowId, 0);
        }

        return default;
    }

    private ((int FlowId, int Index) Current, (int FlowId, int Index) Inherited) ResolveWithInherit(int targetFlowId, int classId)
    {
        var currentLookupId = classId;
        (int FlowId, int Index) current = default;
        (int FlowId, int Index) inherited = default;
        var foundCurrent = false;

        foreach (var kvp in _flowGroupCache)
        {
            var flowId = kvp.Key;

            // 跳过比目标 FlowId 大的数据（因为本身是倒序，所以这些是最先遍历到的）
            if (flowId > targetFlowId)
                continue;

            var facts = kvp.Value;

            // shareId 可能因为 inherit 关系向上追溯，直到找到一个有效的 shareId 或者没有了 inherit 为止
            while (flowId < ShareClass.GetFlow(currentLookupId))
            {
                if (_shareMap.TryGetValue(currentLookupId, out var map) && _shareMap.ContainsKey(map.Inherit) && map.Inherit < currentLookupId)
                    currentLookupId = map.Inherit;
                else
                    break;
            }

            // 当前 share 无效了，因为没有定义
            if (flowId < ShareClass.GetFlow(currentLookupId))
                return (current, inherited);

            // 🥇 当前 Flow 精确匹配目标份额
            for (var i = 0; i < facts.Length; i++)
            {
                if (facts[i].ShareId == currentLookupId)
                {
                    if (!foundCurrent)
                    {
                        current = (flowId, i);
                        foundCurrent = true;
                    }
                    else
                    {
                        // 已找到 Current，第二次匹配即为再向前一层的继承值
                        inherited = (flowId, i);
                        return (current, inherited);
                    }
                    break;
                }
            }


            // 多份额统一要素 
            if (kvp.Value.Length == 1 && kvp.Value[0].ShareId == ShareClass.MakeId(flowId, 0))
            {
                if (!foundCurrent)
                {
                    current = (flowId, 0);
                    foundCurrent = true;
                }
                else
                {
                    // 已找到 Current，第二次匹配即为再向前一层的继承值
                    inherited = (flowId, 0);
                    return (current, inherited);
                }
            }
        }

        return (current, inherited);
    }


    /// <summary>
    /// 返回当前和继承的值，如果当前值全相同，则只返回一条记录，否则返回每条记录的当前和继承值
    /// </summary>
    /// <param name="flowId"></param>
    /// <param name="classId"></param>
    /// <returns></returns>
    public virtual (T? Old, T? New)[] GetInheritValues(int flowId, params int[] classId)
    {
        var idx = Enumerable.Range(0, classId.Length).Select(x => ResolveWithInherit(flowId, classId[x])).ToArray();
        //if (idx.Distinct().Count() == 1) // 同源
        //    return [(GetValue(idx[0].Inherited), GetValue(idx[0].Current))];

        return idx.Select(x => (GetValue(x.Inherited), GetValue(x.Current))).ToArray();
    }

    private T? GetValue((int FlowId, int Index) p) => p.FlowId > 0 ? _flowGroupCache[p.FlowId][p.Index].Data : default;

}

//public class ValueFactorItem<T> where T : struct
//{
//    /// <summary>
//    /// 按flowid 倒序，保证FirstOrDefault拿到的就是当前flowid的值
//    /// </summary>
//    protected readonly ImmutableSortedDictionary<int, FundFactor<T>[]> _flowGroupCache;
//    private readonly (int FlowId, int[] ClassIds)[] _shares;
//    private ImmutableDictionary<int, InheritMap> _shareMap;



//    public ValueFactorItem(IEnumerable<FundFactor<T>> data, (int FlowId, ShareClass[] Factor)[] shares, ImmutableDictionary<int, InheritMap> shareConfigMap)
//    {
//        _flowGroupCache = data.GroupBy(f => f.FlowId).ToImmutableSortedDictionary(g => g.Key, g => g.ToArray(), Comparer<int>.Create((x, y) => y.CompareTo(x)));
//        _shares = shares.Select(x => (x.FlowId, x.Factor.Select(f => f.Id).ToArray())).ToArray();
//        _shareMap = shareConfigMap;
//    }

//    public T?[] this[int flowId, params int[] classId] => GetValues(flowId, classId);

//    public T?[] this[int flowId] => GetValues(flowId);

//    public bool HasValue => _flowGroupCache.Length > 0;

//    /// <summary>
//    /// 查值规则
//    /// 先找当前flowid，对应share，没有找singleton
//    /// </summary>
//    /// <param name="flowId"></param>
//    /// <param name="classId"></param>
//    /// <returns></returns>
//    public virtual T?[] GetValues(int flowId, int[] classId)
//    {
//        if (classId?.Length is null or 0) return [];

//        var values = new T?[classId.Length];

//        for (var i = 0; i < classId.Length; i++)
//        {
//            var sc = classId[i];
//            values[i] = ResolveValue(flowId, sc);
//        }
//        return values;
//    }


//    public virtual T?[] GetValues(int flowId) => GetValues(flowId, _shares.FirstOrDefault(x => x.FlowId <= flowId).ClassIds);

//    public virtual T?[] Current => GetValues(_flowGroupCache.FirstOrDefault().FlowId);

//    /// <summary>
//    /// 核心查询：按 FlowId 倒序追溯，Singleton 绝对优先
//    /// </summary>
//    /// <param name="targetFlowId">目标 FlowId</param>
//    /// <param name="classId">要查询的份额标识（对应 ShareType.Id）</param>
//    /// <returns>匹配到的值，未找到则返回 default(T)</returns>
//    /// <summary>
//    /// 核心查询：按 Inherit 链回溯，Singleton 绝对优先
//    /// </summary>
//    /// <summary>
//    /// 核心查询：精确匹配 > Singleton > 按 Inherit 向前追溯
//    /// </summary>
//    private T? ResolveValue(int targetFlowId, int classId)
//    {
//        // 当前正在查找的份额标识（会随 Inherit 规则动态切换）
//        var currentLookupId = classId;

//        // 检查targetFlowId，如果没有这个flow的值，应该从inherit开始
//        //var excet = _flowGroupCache.FirstOrDefault(x => x.FlowId == targetFlowId);
//        //if (excet == default) currentLookupId = _shares.TryGetValue(targetFlowId, out var scs) && scs.FirstOrDefault(x => x.Id == classId) is ShareClass sc  ? sc.Inherit : ShareClass.Singleton;

//        // 从 targetFlowId 开始，按时间倒序遍历所有有数据的 Flow
//        foreach (var (flowId, facts) in _flowGroupCache.Where(x => x.FlowId <= targetFlowId).OrderByDescending(x => x.FlowId))
//        {
//            var map = _shareMap[currentLookupId];
//            while (flowId < map.FlowId) // 当前share无效了，因为没有定义
//            {
//                currentLookupId = map.Inherit;
//                map = _shareMap[currentLookupId];
//            }

//            // 🥇 优先级1：当前 Flow 精确匹配目标份额
//            if (facts.FirstOrDefault(f => f.ShareId == currentLookupId) is FundFactor<T> exactMatch)
//            {
//                return exactMatch.Data;
//            }

//            // 🥈 优先级2：当前 Flow 存在 Singleton 值（作为当前层级的兜底）
//            if (facts.Length == 1 && facts[0].ShareId == ShareClass.Singleton)
//            {
//                return facts[0].Data;
//            }

//        }

//        // 🛡️ 全程未匹配到值，返回默认值
//        return null;
//    }

//    public virtual (T? Old, T? New)[] GetInheritValues(int flowId, params int[] classId)
//    {
//        var values = GetValues(flowId, classId);

//        var values2 = GetValues(flowId - 1, classId);

//        if (values.Distinct().Count() == 1)
//            return [(values2[0], values[0])];

//        return Enumerable.Range(0, classId.Length).Select(x => (values2[x], values[x])).ToArray();
//    }

//}