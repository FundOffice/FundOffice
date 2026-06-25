namespace Vetting.Models.Entities;

/// <summary>
/// 股东信息
/// </summary>
public class Shareholder
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Ratio { get; set; }
    public string? Intro { get; set; }
    public string? Nature { get; set; }
    public string? PaidInAmount { get; set; }
    public string? IdentityBrief { get; set; }
    public string? CompanyRole { get; set; }
    public string? IsCoreResearch { get; set; }
    public string? CompanyPosition { get; set; }
}
