namespace Vetting.Copilot.Models.Info;

public class FundInfo : IResolve
{
    public int Id { get; set; }
    public bool Recommend { get; set; }
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

    public object? Resolve(string propertyName) => propertyName switch
    {
        nameof(Name) => Name,
        nameof(Code) => Code,
        nameof(Duration) => Duration,
        nameof(Type) => Type,
        nameof(MinSubscription) => MinSubscription,
        nameof(Frequency) => Frequency,
        nameof(Custodian) => Custodian,
        nameof(RiskLevel) => RiskLevel,
        nameof(BuySellFee) => BuySellFee,
        nameof(MgmtFee) => MgmtFee,
        nameof(CustodyFee) => CustodyFee,
        nameof(Scope) => Scope,
        nameof(Restriction) => Restriction,
        nameof(WarningStoploss) => WarningStoploss,
        nameof(PerformanceFee) => PerformanceFee,
        nameof(Dividend) => Dividend,
        nameof(Other) => Other,
        nameof(EstablishmentDate) => EstablishmentDate,
        nameof(LockupPeriod) => LockupPeriod,
        nameof(OpeningDay) => OpeningDay,
        nameof(FilingOrRegistration) => FilingOrRegistration,
        nameof(StrategyType) => StrategyType,
        nameof(NavDate) => NavDate,
        nameof(Scale) => Scale,
        nameof(IssueScale) => IssueScale,
        nameof(CurrentScale) => CurrentScale,
        nameof(UnitNav) => UnitNav,
        nameof(CumulativeNav) => CumulativeNav,
        nameof(AnnualReturn) => AnnualReturn,
        nameof(MaxDrawdown) => MaxDrawdown,
        nameof(Volatility) => Volatility,
        nameof(Sharpe) => Sharpe,
        nameof(Calmar) => Calmar,
        nameof(CumulativeReturn) => CumulativeReturn,
        nameof(Return6M) => Return6M,
        nameof(Return1Y) => Return1Y,
        nameof(Return1M) => Return1M,
        _ => null,
    };
}
