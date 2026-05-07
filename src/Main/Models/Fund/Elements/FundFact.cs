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

    public T? Data { get; set; }


}


public static class FactHelper
{
    public static IFundFact[] FromElement(FundElements fundElements)
    {
        var facts = new List<IFundFact>();
        var fundId = fundElements.Id;

        // Helper: extract facts from Mutable<T> (single-share properties)
        void AddMutableFacts<T>(Mutable<T>? mutable) where T : notnull
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
        void AddPortionMutableFacts<T>(PortionMutable<T>? portion) where T : notnull
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

        // ==================== Mutable<T> Properties ====================
        AddMutableFacts(fundElements.FullName);
        AddMutableFacts(fundElements.ShortName);
        AddMutableFacts(fundElements.SecurityFundType);
        AddMutableFacts(fundElements.FundModeInfo);
        AddMutableFacts(fundElements.SealingRule);
        AddMutableFacts(fundElements.RiskLevel);
        AddMutableFacts(fundElements.DurationInMonths);
        AddMutableFacts(fundElements.ExpirationDate);
        AddMutableFacts(fundElements.CollectionAccount);
        AddMutableFacts(fundElements.CustodyAccount);
        AddMutableFacts(fundElements.ShareClasses);
        AddMutableFacts(fundElements.StopLine);
        AddMutableFacts(fundElements.WarningLine);
        AddMutableFacts(fundElements.OpenDayInfo);
        AddMutableFacts(fundElements.FundOpenRule);
        AddMutableFacts(fundElements.TrusteeInfo);
        AddMutableFacts(fundElements.TrusteeFee);
        AddMutableFacts(fundElements.OutsourcingInfo);
        AddMutableFacts(fundElements.OutsourcingFee);
        AddMutableFacts(fundElements.InvestmentManagers);
        AddMutableFacts(fundElements.InvestmentManager);
        AddMutableFacts(fundElements.PerformanceBenchmark);
        AddMutableFacts(fundElements.InvestmentObjective);
        AddMutableFacts(fundElements.InvestmentScope);
        AddMutableFacts(fundElements.InvestmentStrategy);
        AddMutableFacts(fundElements.TemporarilyOpenInfo);
        AddMutableFacts(fundElements.HugeRedemptionRatio);
        AddMutableFacts(fundElements.CoolingPeriod);
        AddMutableFacts(fundElements.Callback);
        AddMutableFacts(fundElements.ManageFeePay);

        // ==================== PortionMutable<T> Properties ====================
        AddPortionMutableFacts(fundElements.LockingRule);
        AddPortionMutableFacts(fundElements.ManageFee);
        AddPortionMutableFacts(fundElements.SubscriptionRule);
        AddPortionMutableFacts(fundElements.PurchasRule);
        AddPortionMutableFacts(fundElements.RedemptionFee);
        AddPortionMutableFacts(fundElements.PerformanceFeeStatement);

        return facts.ToArray();
    }
}