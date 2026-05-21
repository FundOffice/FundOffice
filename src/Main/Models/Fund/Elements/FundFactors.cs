using FMO.Logging;
using System.Collections.Immutable;

namespace FMO.Models;



public partial class FundFactors
{

    /// <summary>
    /// FlowId → 该flow实际生效的ShareType配置（已填充继承）
    /// </summary>
    //private readonly ImmutableDictionary<int, ShareType[]> _shareConfigMap = null!;

    public FundFactors(IEnumerable<IFundFactor> factories) => __AutoInitializeCtor(factories);

    //public FundFactors(IEnumerable<IFundFactor> facts)
    //{
    //    // 按flow倒序，class正序，保证读的时候最新优先，唯一份额的值优先
    //    var g = facts.Where(x => x.FactorId is not null).OrderByDescending(x => x.FlowId).ThenBy(x => x.ShareId).GroupBy(x => x.FactorId).ToDictionary(x => x.Key, x => x.AsEnumerable());

    //    ShareTypes = new(Filter<ShareType[]>(FactorFields.ShareClasses, g));

    //    _shareConfigMap = BuildInheritedShareConfigMap(ShareTypes.GetShares());


    //    FullName = new(Filter<string>(FactorFields.FullName, g));

    //    ShortName = new(Filter<string>(FactorFields.ShortName, g));


    //    ManageFee = new(Filter<FundFeeInfo>(FactorFields.ManageFee, g), _shareConfigMap);
    //}

    /// <summary>
    /// 构建带继承规则的 ShareType 映射
    /// 规则：如果当前flow没有配置shares，则继承前一个flow的配置
    /// Singleton模式：如果某flow是singleton，后续flow未配置时继承该singleton
    /// </summary>
    private static ImmutableDictionary<int, InheritMap> BuildInheritedShareConfigMap((int FlowId, ShareClass[] Shares)[] rawShares)
    {
        if (rawShares == null || rawShares.Length == 0) return [];

        var dict = new Dictionary<int, InheritMap[]>();

        var result = new Dictionary<int, InheritMap>();

        // 📌 按 FlowId 升序：保证后定义的配置能覆盖先定义（业务上"最新优先"）
        var sorted = rawShares.OrderBy(x => x.FlowId).ToArray();

        foreach (var (flowId, shares) in sorted)
        {
            if (shares == null || shares.Length == 0) continue;

            // 🔍 冲突检测：Singleton 不能与其他份额共存
            bool hasSingleton = shares.Any(s => s.Id == ShareClass.Singleton);
            bool hasOthers = shares.Any(s => s.Id != ShareClass.Singleton);

            if (hasSingleton && hasOthers)
            {
                LogEx.Error($"FlowId={flowId}: 份额配置冲突，同时存在 Singleton 和其他份额。该配置将被忽略，查询时自动向前追溯。");
                continue; // 跳过此配置，让查询逻辑按继承链向前找
            }

            // ✅ 为每个份额创建/更新继承映射 不覆盖
            foreach (var share in shares)
                result.TryAdd(share.Id, new InheritMap(ShareId: share.Id, FlowId: flowId, Inherit: share.Inherit));

        }

        // 🛡️ 兜底：确保 Singleton 有默认映射（防止空查询时崩溃）
        if (!result.ContainsKey(ShareClass.Singleton))
        {
            result[ShareClass.Singleton] = new InheritMap(ShareId: ShareClass.Singleton, FlowId: 0, Inherit: ShareClass.Singleton);
        }

        return result.ToImmutableDictionary();
    }




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
                //case FactorFields.ShareClasses:
                //     ShareTypes = new(g[FactorFields.ShareTypes].OfType<FundFactor<ShareType[]>>());
                //    break;
                // 新增字段只需要在这里加case
        }
    }


}

/// <summary>
/// 
/// </summary>
/// <param name="ShareId">当前的Id</param>
/// <param name="FlowId">在哪个Flow中定义的，>它就按shareid找，&lt它找inherit</param>
/// <param name="Inherit">继承自</param>
public record InheritMap(int ShareId, int FlowId, int Inherit);