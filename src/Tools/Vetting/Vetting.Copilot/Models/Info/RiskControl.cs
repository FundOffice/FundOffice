namespace Vetting.Copilot.Models.Info;

public class RiskControl : IResolve
{
    public int Id { get; set; } = 1;
    public string? SystemIntro { get; set; }
    public string? DecisionMechanism { get; set; }
    public string? RiskMgmtCommittee { get; set; }
    public string? DrawdownControl { get; set; }
    public string? SystemicRiskResponse { get; set; }
    public string? TradingMonitoring { get; set; }
    public string? RiskMeasures { get; set; }
    public string? ManualVsSystem { get; set; }
    public string? RiskMeasurement { get; set; }
    public string? MaxDrawdownTolerance { get; set; }
    public string? TailRisk { get; set; }
    public string? RiskReserve { get; set; }
    public string? LiquidityMgmt { get; set; }
    public string? InsiderTradingPrevention { get; set; }
    public string? EmployeeTradingMonitor { get; set; }
    public string? ProductFairness { get; set; }

    public object? Resolve(string propertyName) => propertyName switch
    {
        nameof(SystemIntro) => SystemIntro,
        nameof(DecisionMechanism) => DecisionMechanism,
        nameof(RiskMgmtCommittee) => RiskMgmtCommittee,
        nameof(DrawdownControl) => DrawdownControl,
        nameof(SystemicRiskResponse) => SystemicRiskResponse,
        nameof(TradingMonitoring) => TradingMonitoring,
        nameof(RiskMeasures) => RiskMeasures,
        nameof(ManualVsSystem) => ManualVsSystem,
        nameof(RiskMeasurement) => RiskMeasurement,
        nameof(MaxDrawdownTolerance) => MaxDrawdownTolerance,
        nameof(TailRisk) => TailRisk,
        nameof(RiskReserve) => RiskReserve,
        nameof(LiquidityMgmt) => LiquidityMgmt,
        nameof(InsiderTradingPrevention) => InsiderTradingPrevention,
        nameof(EmployeeTradingMonitor) => EmployeeTradingMonitor,
        nameof(ProductFairness) => ProductFairness,
        _ => null,
    };
}
