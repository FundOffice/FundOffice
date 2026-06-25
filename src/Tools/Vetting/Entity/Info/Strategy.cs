namespace Vetting.Models.Entities;

/// <summary>
/// 投资策略
/// </summary>
public class Strategy
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Manager { get; set; }
    public string? Scale { get; set; }
    public string? Type { get; set; }
    public string? Capacity { get; set; }
    public string? SameStrategyCount { get; set; }
    public string? FactorPool { get; set; }
    public string? CapacityAndRisk { get; set; }
    public string? Replicated { get; set; }
    public string? StyleExposure { get; set; }
    public string? Turnover { get; set; }
    public string? HoldingPeriod { get; set; }
    public string? WeightAllocation { get; set; }
    public string? WarningStoploss { get; set; }
}
