namespace Vetting.Models.Entities;

/// <summary>
/// 模板关联的推荐产品列表（有序）
/// </summary>
public class TemplateRecommend
{
    public int Id { get; set; }
    public string? FileHash { get; set; }
    public string? ProviderId { get; set; }
    /// <summary>逗号分隔的 FundInfo.Id 列表，按推荐顺序</summary>
    public string? FundIds { get; set; }
}
