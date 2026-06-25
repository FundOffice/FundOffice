namespace Vetting.Models.Entities;

/// <summary>
/// 散装问答 (模板中 {{a1}} {{a2}} 等占位符)
/// Source: 0=种子数据, 其他=VettingReport.Id
/// </summary>
public class QA
{
    public int Id { get; set; }

    /// <summary>来源: 0=种子数据, 其他=VettingReport.Id</summary>
    public int Source { get; set; }
    public string? Question { get; set; }
    public string? Answer { get; set; }
}
