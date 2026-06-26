namespace Vetting.Copilot.Models.Entities;

/// <summary>
/// 模板关联的推荐产品列表（有序）
/// </summary>
public class TemplateRecommend
{
    public int Id { get; set; }
    public string? FileHash { get; set; }
    public string? ProviderId { get; set; }
    public string? FundIds { get; set; }
}
