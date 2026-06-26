namespace Vetting.Copilot.Models.Info;

public class FinancialStatement : IResolve
{
    public int Id { get; set; }
    public string? Year { get; set; }
    public string? TotalAssets { get; set; }
    public string? TotalLiabilities { get; set; }
    public string? OwnersEquity { get; set; }
    public string? Revenue { get; set; }
    public string? Cost { get; set; }
    public string? NetProfit { get; set; }

    public object? Resolve(string propertyName) => propertyName switch
    {
        nameof(Year) => Year,
        nameof(TotalAssets) => TotalAssets,
        nameof(TotalLiabilities) => TotalLiabilities,
        nameof(OwnersEquity) => OwnersEquity,
        nameof(Revenue) => Revenue,
        nameof(Cost) => Cost,
        nameof(NetProfit) => NetProfit,
        _ => null,
    };
}
