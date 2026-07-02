namespace Vetting.Copilot.Models.Entities;

/// <summary>
/// 文件专属问题 — 模板中 {{a1}} {{a2}} 等散装占位符
/// </summary>
public class FileSpecialQuestion
{
    public int Id { get; set; }
    /// <summary>文件名</summary>
    public string? FileName { get; set; }
    public string? Provider { get; set; }
    public int Index { get; set; }
    public string? Question { get; set; }
}

public class SpecialAnswer
{
    public int Id { get; set; }
    public int QuestionId { get; set; }
    public string? Identifier { get; set; }
    public string? Value { get; set; }
}