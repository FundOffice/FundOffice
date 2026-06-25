namespace Vetting.Models.Entities;

/// <summary>
/// 实控人/穿透股东
/// </summary>
public class BeneficialOwner
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Penetration { get; set; }
    public string? Intro { get; set; }
}
