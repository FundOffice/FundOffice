namespace Vetting.Copilot.Models.Entities;

/// <summary>
/// 模板关联的推荐产品列表（有序）
/// </summary>
public class TemplateRecommend
{
    public int Id { get; set; }
    /// <summary>文件名（或 "__global__" 表示全局推荐）</summary>
    public string? FileName { get; set; }
    public string? ProviderId { get; set; }
    /// <summary>推荐产品 ID 列表（逗号分隔，按顺序对应 fund_index）</summary>
    public string? FundIds { get; set; }
}
