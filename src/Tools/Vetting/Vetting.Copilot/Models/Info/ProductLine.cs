namespace Vetting.Copilot.Models.Info;

public class ProductLine : IResolve
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? StrategyType { get; set; }
    public string? SpecificStrategy { get; set; }
    public string? RepresentProduct { get; set; }
    public string? Manager { get; set; }
    public string? FundCount { get; set; }
    public string? Scale { get; set; }
    public string? TradingScale { get; set; }
    public string? Capacity { get; set; }

    public object? Resolve(string propertyName) => propertyName switch
    {
        nameof(Name) => Name,
        nameof(StrategyType) => StrategyType,
        nameof(SpecificStrategy) => SpecificStrategy,
        nameof(RepresentProduct) => RepresentProduct,
        nameof(Manager) => Manager,
        nameof(FundCount) => FundCount,
        nameof(Scale) => Scale,
        nameof(TradingScale) => TradingScale,
        nameof(Capacity) => Capacity,
        _ => null,
    };
}
