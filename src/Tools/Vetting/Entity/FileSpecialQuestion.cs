namespace Vetting.Models.Entities;

/// <summary>
/// 文件专属问题 — 模板中 {{a1}} {{a2}} 等散装占位符
/// 每个问题关联到具体文件(FileHash)和顺序(Index)
/// </summary>
public class FileSpecialQuestion
{
    public int Id { get; set; }

    /// <summary>文件哈希，关联到具体模板文件</summary>
    public string? FileHash { get; set; }

    public string? Provider { get; set; }

    /// <summary>占位符序号 (对应 {{a1}} 的 1)</summary>
    public int Index { get; set; }

    /// <summary>问题描述</summary>
    public string? Question { get; set; }
     
}


public class SpecialAnswer
{
    public int Id { get; set; }

    public int QuestionId { get; set; }

    /// <summary>
    /// manual
    /// provider
    /// </summary>
    public string? Identifier { get; set; }

    public string? Value { get; set; }
}