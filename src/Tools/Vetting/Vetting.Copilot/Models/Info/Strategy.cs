namespace Vetting.Copilot.Models.Info;

public class Strategy : IResolve
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Manager { get; set; }
    public string? Scale { get; set; }
    public string? Type { get; set; }
    public string? StockType { get; set; }
    public string? Concentration { get; set; }
    public string? Turnover { get; set; }
    public string? MarketImpact { get; set; }
    public string? HedgeTool { get; set; }
    public string? RiskExposure { get; set; }
    public string? Capacity { get; set; }
    public string? SameStrategyCount { get; set; }
    public string? FactorPool { get; set; }
    public string? HoldingPeriod { get; set; }
    public string? WeightAllocation { get; set; }
    public string? WarningStoploss { get; set; }
    public string? Replicated { get; set; }
    public string? StyleExposure { get; set; }
    public string? CapacityAndRisk { get; set; }

    public object? Resolve(string propertyName) => propertyName switch
    {
        nameof(Name) => Name,
        nameof(Manager) => Manager,
        nameof(Scale) => Scale,
        nameof(Type) => Type,
        nameof(StockType) => StockType,
        nameof(Concentration) => Concentration,
        nameof(Turnover) => Turnover,
        nameof(MarketImpact) => MarketImpact,
        nameof(HedgeTool) => HedgeTool,
        nameof(RiskExposure) => RiskExposure,
        nameof(Capacity) => Capacity,
        nameof(SameStrategyCount) => SameStrategyCount,
        nameof(FactorPool) => FactorPool,
        nameof(HoldingPeriod) => HoldingPeriod,
        nameof(WeightAllocation) => WeightAllocation,
        nameof(WarningStoploss) => WarningStoploss,
        nameof(Replicated) => Replicated,
        nameof(StyleExposure) => StyleExposure,
        nameof(CapacityAndRisk) => CapacityAndRisk,
        _ => null,
    };
}
