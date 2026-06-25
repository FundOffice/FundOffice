namespace Vetting.Models.Entities;

/// <summary>
/// IT/技术支持
/// </summary>
public class ITSupport
{
    public int Id { get; set; } = 1;
    public string? TeamDemand { get; set; }
    public string? Headcount { get; set; }
    public string? SupportScope { get; set; }
    public string? SelfDeveloped { get; set; }
    public string? KeyFeatures { get; set; }
    public string? AnnualInvestment { get; set; }
    public string? EmergencyResponse { get; set; }
}
