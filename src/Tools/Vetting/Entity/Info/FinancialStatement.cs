namespace Vetting.Models.Entities;

/// <summary>
/// 财务报表 (按年份)
/// </summary>
public class FinancialStatement
{
    public int Id { get; set; }
    public string? Year { get; set; }
    public string? TotalAssets { get; set; }
    public string? TotalLiabilities { get; set; }
    public string? OwnersEquity { get; set; }
    public string? Revenue { get; set; }
    public string? Cost { get; set; }
    public string? NetProfit { get; set; }
}
