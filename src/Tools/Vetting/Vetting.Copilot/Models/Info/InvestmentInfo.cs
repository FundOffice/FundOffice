namespace Vetting.Copilot.Models.Info;

public class InvestmentInfo : IResolve
{
    public int Id { get; set; } = 1;
    public string? Target { get; set; }
    public string? Philosophy { get; set; }
    public string? Research { get; set; }
    public string? Decision { get; set; }
    public string? Trading { get; set; }
    public string? Evaluation { get; set; }
    public string? RiskControl { get; set; }
    public string? PortfolioAdjust { get; set; }
    public string? PositionBuilding { get; set; }
    public string? CommitteeRole { get; set; }
    public string? ResearchAuthority { get; set; }
    public string? SystemAndData { get; set; }
    public string? DataStorage { get; set; }
    public string? TradingControl { get; set; }
    public string? TradingErrorFix { get; set; }
    public string? AbnormalTrading { get; set; }
    public string? AccountFairness { get; set; }

    public object? Resolve(string propertyName) => propertyName switch
    {
        nameof(Target) => Target,
        nameof(Philosophy) => Philosophy,
        nameof(Research) => Research,
        nameof(Decision) => Decision,
        nameof(Trading) => Trading,
        nameof(Evaluation) => Evaluation,
        nameof(RiskControl) => RiskControl,
        nameof(PortfolioAdjust) => PortfolioAdjust,
        nameof(PositionBuilding) => PositionBuilding,
        nameof(CommitteeRole) => CommitteeRole,
        nameof(ResearchAuthority) => ResearchAuthority,
        nameof(SystemAndData) => SystemAndData,
        nameof(DataStorage) => DataStorage,
        nameof(TradingControl) => TradingControl,
        nameof(TradingErrorFix) => TradingErrorFix,
        nameof(AbnormalTrading) => AbnormalTrading,
        nameof(AccountFairness) => AccountFairness,
        _ => null,
    };
}
