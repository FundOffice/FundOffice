namespace Vetting.Models.Entities;

/// <summary>
/// 投资理念与流程
/// </summary>
public class InvestmentInfo
{
    public int Id { get; set; } = 1;
    // 理念
    public string? Target { get; set; }
    public string? Philosophy { get; set; }
    // 流程
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
}
