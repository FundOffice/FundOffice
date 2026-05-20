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




public class FundFactor<T> : IFundFactor
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
        ShareId = -1;
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

    public string Id => $"{FundId}.{FlowId}.{ShareId}.{FactorId}";

    public int FundId { get; set; }

    public int FlowId { get; set; }

    public int ShareId { get; set; }

    public required string FactorId { get; init; }

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

    public override ShareClass[] this[int flowId] => _flowGroupCache.FirstOrDefault(x => x.FlowId <= flowId).Fact is { } f && f.Data.Length > 1 ? f.Data : [ShareClass.DefaultShare];
}


public class FactorItem<T>
{

    protected readonly ImmutableArray<(int FlowId, FundFactor<T>[] Fact)> _flowGroupCache;

    public FactorItem(IEnumerable<FundFactor<T>> data)
    {
        _flowGroupCache = data.GroupBy(f => f.FlowId).Select(x => (x.Key, x.ToArray())).ToImmutableArray();
    }

    public T?[] this[int flowId, params int[] classId] => GetValues(flowId, classId);

    public T? this[int flowId] => this[flowId, -1][0];


    public virtual T?[] GetValues(int flowId, params int[] classId)
    {
        if (classId.Length == 0) return [];

        var values = new T[classId.Length];
          
        for (var i = 0; i < classId.Length; i++)
        {
            var sc = classId[i];
            bool set = false;
            foreach (var (flowid, data) in _flowGroupCache.Where(x => x.FlowId <= flowId))
            {
                if (data.FirstOrDefault(x => x.ShareId == sc) is FundFactor<T> fact)
                {
                    set = true;
                    values[i] = fact.Data;
                    break;
                }
                else if (data.Length == 1 && data[0].ShareId == -1)
                {
                    set = true;
                    values[i] = data[0].Data;
                    break;
                }
            }
            // 匹配最初一个flow的 shareid = -1 数据
            if (!set && _flowGroupCache.LastOrDefault().Fact.FirstOrDefault() is FundFactor<T> fa && fa.ShareId == -1)
                values[i] = fa.Data;
        }
        return values;
    }

    public virtual (T? Old, T? New)[] GetInheritValues(int flowId, params int[] classId)
    {
        var values = GetValues(flowId, classId);
        var values2 = GetValues(flowId - 1, classId);

        return Enumerable.Range(0, classId.Length).Select(x => (values[x], values2[x])).ToArray();
    }

}

public class FundFactors
{
    public FundFactors(IEnumerable<IFundFactor> facts)
    {
        // 按flow倒序，class正序，保证读的时候最新优先，唯一份额的值优先
        var g = facts.Where(x=>x.FactorId is not null).OrderByDescending(x => x.FlowId).ThenBy(x => x.ShareId).GroupBy(x => x.FactorId).ToDictionary(x => x.Key, x => x.AsEnumerable());

        FullName = new(Filter<string>(FactorFields.FullName, g));

        ShortName = new(Filter<string>(FactorFields.ShortName, g));

        ShareClasses = new(Filter<ShareClass[]>(FactorFields.ShareClasses, g));

        ManageFee = new(Filter<FundFeeInfo>(FactorFields.ManageFee, g));
    }

    public FactorItem<string> FullName { get; private set; }


    public FactorItem<string> ShortName { get; private set; }

    public ShareClassFactorItem ShareClasses { get; private set; }

    public FactorItem<FundFeeInfo> ManageFee { get; private set; }

    // ................其它类型

    private IEnumerable<FundFactor<T>> Filter<T>(string field, Dictionary<string, IEnumerable<IFundFactor>> g)
    {
        return g.TryGetValue(field, out var d) ? d.OfType<FundFactor<T>>() : [];
    }

    public void Update(string field, IEnumerable<IFundFactor> facts)
    {
        if (facts == null || !facts.Any()) return;

        var g = facts.OrderByDescending(x => x.FlowId).ThenBy(x => x.ShareId).GroupBy(x => x.FactorId).ToDictionary(x => x.Key, x => x.AsEnumerable());

        // 严格匹配你的硬编码属性更新
        switch (field)
        {
            case FactorFields.FullName:
                FullName = new(g[FactorFields.FullName].OfType<FundFactor<string>>());
                break;
            case FactorFields.ShortName:
                ShortName = new(g[FactorFields.ShortName].OfType<FundFactor<string>>());
                break;
            case FactorFields.ShareClasses:
                ShareClasses = new(g[FactorFields.ShareClasses].OfType<FundFactor<ShareClass[]>>());
                break;
                // 新增字段只需要在这里加case
        }
    }


}

