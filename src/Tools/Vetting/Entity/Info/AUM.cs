namespace Vetting.Models.Entities;

/// <summary>
/// 资产管理规模 (按年份)
/// </summary>
public class AUM
{
    public int Id { get; set; }
    public string? Year { get; set; }
    public string? Scale { get; set; }
}
