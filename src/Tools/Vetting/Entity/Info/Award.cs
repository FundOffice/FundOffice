namespace Vetting.Models.Entities;

/// <summary>
/// 奖项
/// </summary>
public class Award
{
    public int Id { get; set; }
    public string? Time { get; set; }
    public string? Entity { get; set; }
    public string? Name { get; set; }
    public string? Evaluator { get; set; }
}
