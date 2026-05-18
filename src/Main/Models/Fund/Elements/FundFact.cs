namespace FMO.Models;

public interface IFundFact
{
    internal string Id { get; }

    int FundId { get; set; }

    int FlowId { get; set; }

    int ShareId { get; set; }

    string FactId { get; }
}




public class FundFact<T> : IFundFact
{
    public string Id => $"{FundId}.{FlowId}.{ShareId}.{FactId}";

    public int FundId { get; set; }
    public int FlowId { get; set; }
    public int ShareId { get; set; }

    public required string FactId { get; init; }

    public required T Data { get; set; }


}


public static class FactHelper
{
    /// <summary>
    /// 将 IFundFact 数组还原为 FundElements 对象（无反射，按属性显式处理）
    /// </summary>
    public static FundElements FromFact(this IFundFact[] facts)
    {
        if (facts == null || facts.Length == 0)
            return new FundElements();

        var fundId = facts.First().FundId;
        var elements = new FundElements { Id = fundId };

        // 按 FactId 分组，便于按属性批量处理
        var factGroups = facts.GroupBy(f => f.FactId).ToDictionary(g => g.Key, g => g.ToList());

        // ==================== Mutable<T> Properties (ShareId == -1) ====================
        RestoreMutable(factGroups, elements, fundId, e => e.FullName);
        RestoreMutable(factGroups, elements, fundId, e => e.ShortName);
        RestoreMutable(factGroups, elements, fundId, e => e.SecurityFundType);
        RestoreMutable(factGroups, elements, fundId, e => e.FundModeInfo);
        RestoreMutable(factGroups, elements, fundId, e => e.SealingRule);
        RestoreMutable(factGroups, elements, fundId, e => e.RiskLevel);
        RestoreMutable(factGroups, elements, fundId, e => e.DurationInMonths);
        RestoreMutable(factGroups, elements, fundId, e => e.ExpirationDate);
        RestoreMutable(factGroups, elements, fundId, e => e.CollectionAccount);
        RestoreMutable(factGroups, elements, fundId, e => e.CustodyAccount);
        RestoreMutable(factGroups, elements, fundId, e => e.ShareClasses);
        RestoreMutable(factGroups, elements, fundId, e => e.StopLine);
        RestoreMutable(factGroups, elements, fundId, e => e.WarningLine);
        RestoreMutable(factGroups, elements, fundId, e => e.OpenDayInfo);
        RestoreMutable(factGroups, elements, fundId, e => e.FundOpenRule);
        RestoreMutable(factGroups, elements, fundId, e => e.TrusteeInfo);
        RestoreMutable(factGroups, elements, fundId, e => e.TrusteeFee);
        RestoreMutable(factGroups, elements, fundId, e => e.OutsourcingInfo);
        RestoreMutable(factGroups, elements, fundId, e => e.OutsourcingFee);
        RestoreMutable(factGroups, elements, fundId, e => e.InvestmentManagers);
        RestoreMutable(factGroups, elements, fundId, e => e.InvestmentManager);
        RestoreMutable(factGroups, elements, fundId, e => e.PerformanceBenchmark);
        RestoreMutable(factGroups, elements, fundId, e => e.InvestmentObjective);
        RestoreMutable(factGroups, elements, fundId, e => e.InvestmentScope);
        RestoreMutable(factGroups, elements, fundId, e => e.InvestmentStrategy);
        RestoreMutable(factGroups, elements, fundId, e => e.TemporarilyOpenInfo);
        RestoreMutable(factGroups, elements, fundId, e => e.HugeRedemptionRatio);
        RestoreMutable(factGroups, elements, fundId, e => e.CoolingPeriod);
        RestoreMutable(factGroups, elements, fundId, e => e.Callback);
        RestoreMutable(factGroups, elements, fundId, e => e.ManageFeePay);

        // ==================== PortionMutable<T> Properties (ShareId != -1) ====================
        RestorePortionMutable(factGroups, elements, fundId, e => e.LockingRule);
        RestorePortionMutable(factGroups, elements, fundId, e => e.ManageFee);
        RestorePortionMutable(factGroups, elements, fundId, e => e.SubscriptionRule);
        RestorePortionMutable(factGroups, elements, fundId, e => e.PurchasRule);
        RestorePortionMutable(factGroups, elements, fundId, e => e.RedemptionFee);
        RestorePortionMutable(factGroups, elements, fundId, e => e.PerformanceFeeStatement);

        return elements;
    }

    // ==================== Helper: Restore Mutable<T> ====================
    private static void RestoreMutable<T>(
        Dictionary<string, List<IFundFact>> groups,
        FundElements elements,
        int fundId,
        Func<FundElements, Mutable<T>> getter) where T : notnull
    {
        var prop = getter(elements);
        if (!groups.TryGetValue(prop.Name, out var facts)) return;

        var mutable = new Mutable<T>(prop.Name);
        foreach (var fact in facts.Where(f => f.ShareId == -1))
        {
            if (fact is FundFact<T> ff && ff.Data != null)
            {
                mutable.SetValue(ff.Data, fact.FlowId);
            }
        }
        // 仅当有值时才赋值，避免覆盖默认初始化
        if (mutable.Changes.Any())
            getter(elements).Changes.Clear(); // 清空默认空实例
        var target = getter(elements);
        foreach (var (flowId, value) in mutable.Changes)
            target.SetValue(value, flowId);
    }

    // ==================== Helper: Restore PortionMutable<T> ====================
    private static void RestorePortionMutable<T>(
        Dictionary<string, List<IFundFact>> groups,
        FundElements elements,
        int fundId,
        Func<FundElements, PortionMutable<T>> getter) where T : notnull
    {
        var prop = getter(elements);
        if (!groups.TryGetValue(prop.Name, out var facts)) return;

        var portion = new PortionMutable<T>(prop.Name);
        foreach (var fact in facts.Where(f => f.ShareId != -1))
        {
            if (fact is FundFact<T> ff && ff.Data != null)
            {
                portion.SetValue(fact.ShareId, ff.Data, fact.FlowId);
            }
        }
        // 仅当有值时才赋值
        if (portion.Changes.Any())
            getter(elements).Changes.Clear();
        var target = getter(elements);
        foreach (var (flowId, shareDict) in portion.Changes)
            foreach (var (shareId, value) in shareDict)
                target.SetValue(shareId, value, flowId);
    }

    public static IFundFact[] FromElement(FundElements fundElements)
    {
        var facts = new List<IFundFact>();
        var fundId = fundElements.Id;



        // ==================== Mutable<T> Properties ====================
        AddMutableFacts(facts, fundId, fundElements.FullName);
        AddMutableFacts(facts, fundId, fundElements.ShortName);
        AddMutableFacts(facts, fundId, fundElements.SecurityFundType);
        AddMutableFacts(facts, fundId, fundElements.FundModeInfo);
        AddMutableFacts(facts, fundId, fundElements.SealingRule);
        AddMutableFacts(facts, fundId, fundElements.RiskLevel);
        AddMutableFacts(facts, fundId, fundElements.DurationInMonths);
        AddMutableFacts(facts, fundId, fundElements.ExpirationDate);
        AddMutableFacts(facts, fundId, fundElements.CollectionAccount);
        AddMutableFacts(facts, fundId, fundElements.CustodyAccount);
        AddMutableFacts(facts, fundId, fundElements.ShareClasses);
        AddMutableFacts(facts, fundId, fundElements.StopLine);
        AddMutableFacts(facts, fundId, fundElements.WarningLine);
        AddMutableFacts(facts, fundId, fundElements.OpenDayInfo);
        AddMutableFacts(facts, fundId, fundElements.FundOpenRule);
        AddMutableFacts(facts, fundId, fundElements.TrusteeInfo);
        AddMutableFacts(facts, fundId, fundElements.TrusteeFee);
        AddMutableFacts(facts, fundId, fundElements.OutsourcingInfo);
        AddMutableFacts(facts, fundId, fundElements.OutsourcingFee);
        AddMutableFacts(facts, fundId, fundElements.InvestmentManagers);
        AddMutableFacts(facts, fundId, fundElements.InvestmentManager);
        AddMutableFacts(facts, fundId, fundElements.PerformanceBenchmark);
        AddMutableFacts(facts, fundId, fundElements.InvestmentObjective);
        AddMutableFacts(facts, fundId, fundElements.InvestmentScope);
        AddMutableFacts(facts, fundId, fundElements.InvestmentStrategy);
        AddMutableFacts(facts, fundId, fundElements.TemporarilyOpenInfo);
        AddMutableFacts(facts, fundId, fundElements.HugeRedemptionRatio);
        AddMutableFacts(facts, fundId, fundElements.CoolingPeriod);
        AddMutableFacts(facts, fundId, fundElements.Callback);
        AddMutableFacts(facts, fundId, fundElements.ManageFeePay);

        // ==================== PortionMutable<T> Properties ====================
        AddPortionMutableFacts(facts, fundId, fundElements.LockingRule);
        AddPortionMutableFacts(facts, fundId, fundElements.ManageFee);
        AddPortionMutableFacts(facts, fundId, fundElements.SubscriptionRule);
        AddPortionMutableFacts(facts, fundId, fundElements.PurchasRule);
        AddPortionMutableFacts(facts, fundId, fundElements.RedemptionFee);
        AddPortionMutableFacts(facts, fundId, fundElements.PerformanceFeeStatement);

        return facts.ToArray();
    }


    // Helper: extract facts from Mutable<T> (single-share properties)
    static void AddMutableFacts<T>(List<IFundFact> facts, int fundId, Mutable<T>? mutable) where T : notnull
    {
        if (mutable is null) return;
        foreach (var (flowId, value) in mutable.Changes)
        {
            facts.Add(new FundFact<T>
            {
                FundId = fundId,
                FlowId = flowId,
                ShareId = -1,              // -1 represents single/unified share
                FactId = mutable.Name,     // Property name as FactId
                Data = value
            });
        }
    }

    // Helper: extract facts from PortionMutable<T> (share-specific properties)
    static void AddPortionMutableFacts<T>(List<IFundFact> facts, int fundId, PortionMutable<T>? portion) where T : notnull
    {
        if (portion is null) return;
        foreach (var (flowId, shareDict) in portion.Changes)
        {
            foreach (var (shareId, value) in shareDict)
            {
                facts.Add(new FundFact<T>
                {
                    FundId = fundId,
                    FlowId = flowId,
                    ShareId = shareId,     // Specific share class ID
                    FactId = portion.Name,
                    Data = value
                });
            }
        }
    }
}