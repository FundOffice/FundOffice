namespace FMO.Models;

/// <summary>
/// 风险测评
/// </summary>
public class RiskAssessment
{
    public int Id { get; set; }

    public int InvestorId { get; set; }

    public DateOnly Date { get; set; }

    public RiskEvaluation Level { get; set; }
    
    public string? Path { get; set; }

    public FileMeta? File { get; set; }
}