namespace Vetting.Models.Entities;

/// <summary>
/// 投资理念/策略概述
/// </summary>
public class InvestmentPhilosophy
{
    public int Id { get; set; } = 1;
    public string? Target { get; set; }
    public string? Philosophy { get; set; }
}
