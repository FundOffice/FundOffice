namespace Vetting.Models.Entities;

/// <summary>
/// 基金/产品信息 (合并产品要素 + 运作指标 + 业绩列表项)
/// </summary>
public class FundInfo
{
    public int Id { get; set; }

    /// <summary>是否推荐</summary>
    public bool Recommend { get; set; }

    // ═══════════════════════════════════════════════
    // 产品要素
    // ═══════════════════════════════════════════════

    public string? Name { get; set; }
    public string? Code { get; set; }
    public string? Duration { get; set; }
    public string? Type { get; set; }
    public string? MinSubscription { get; set; }
    public string? Frequency { get; set; }
    public string? Custodian { get; set; }
    public string? RiskLevel { get; set; }
    public string? BuySellFee { get; set; }
    public string? MgmtFee { get; set; }
    public string? CustodyFee { get; set; }
    public string? Scope { get; set; }
    public string? Restriction { get; set; }
    public string? WarningStoploss { get; set; }
    public string? PerformanceFee { get; set; }
    public string? Dividend { get; set; }
    public string? Other { get; set; }
    public string? EstablishmentDate { get; set; }
    public string? LockupPeriod { get; set; }
    public string? OpeningDay { get; set; }
    public string? FilingOrRegistration { get; set; }

    // ═══════════════════════════════════════════════
    // 运作指标 (原 ProductPerformance)
    // ═══════════════════════════════════════════════

    public string? StrategyType { get; set; }
    public string? NavDate { get; set; }
    public string? Scale { get; set; }
    public string? IssueScale { get; set; }
    public string? CurrentScale { get; set; }
    public string? UnitNav { get; set; }
    public string? CumulativeNav { get; set; }
    public string? AnnualReturn { get; set; }
    public string? MaxDrawdown { get; set; }
    public string? Volatility { get; set; }
    public string? Sharpe { get; set; }
    public string? Calmar { get; set; }
    public string? CumulativeReturn { get; set; }
    public string? Return6M { get; set; }
    public string? Return1Y { get; set; }
    public string? Return1M { get; set; }
}
