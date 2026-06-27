namespace Vetting.Copilot.Models.Info;

public class FinancialStatement : IResolve
{
    public int Id { get; set; }
    public string? Year { get; set; }
    public string? TotalAssets { get; set; }
    public string? TotalLiabilities { get; set; }
    public string? OwnersEquity { get; set; }
    public string? Revenue { get; set; }
    public string? OperatingCost { get; set; }
    public string? GrossProfit { get; set; }
    public string? OperatingProfit { get; set; }
    public string? TotalProfit { get; set; }
    public string? IncomeTax { get; set; }
    public string? NetProfit { get; set; }
    public string? OperatingCashFlow { get; set; }
    public string? InvestingCashFlow { get; set; }
    public string? FinancingCashFlow { get; set; }
    public string? CashEquivalents { get; set; }
    public string? AssetLiabilityRatio { get; set; }
    public string? GrossMargin { get; set; }
    public string? NetMargin { get; set; }

    public object? Resolve(string propertyName) => propertyName switch
    {
        nameof(Year) => Year,
        nameof(TotalAssets) => TotalAssets,
        nameof(TotalLiabilities) => TotalLiabilities,
        nameof(OwnersEquity) => OwnersEquity,
        nameof(Revenue) => Revenue,
        nameof(OperatingCost) => OperatingCost,
        nameof(GrossProfit) => GrossProfit,
        nameof(OperatingProfit) => OperatingProfit,
        nameof(TotalProfit) => TotalProfit,
        nameof(IncomeTax) => IncomeTax,
        nameof(NetProfit) => NetProfit,
        nameof(OperatingCashFlow) => OperatingCashFlow,
        nameof(InvestingCashFlow) => InvestingCashFlow,
        nameof(FinancingCashFlow) => FinancingCashFlow,
        nameof(CashEquivalents) => CashEquivalents,
        nameof(AssetLiabilityRatio) => AssetLiabilityRatio,
        nameof(GrossMargin) => GrossMargin,
        nameof(NetMargin) => NetMargin,
        _ => null,
    };
}
