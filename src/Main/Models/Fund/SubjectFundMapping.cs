namespace FMO.Models;

/// <summary>
/// 子产品映射
/// </summary>
public class SubjectFundMapping
{
    public string Id => FundCode;

    public required string FundName { get; set; }

    public required string FundCode { get; set; }

    /// <summary>
    /// 主产品
    /// </summary>
    public string? MasterName { get; set; }

    /// <summary>
    /// 主产品
    /// </summary>
    public string? MasterCode { get; set; }

    /// <summary>
    /// 协会
    /// </summary>
    public string? AmacCode { get; set; }

    /// <summary>
    /// 份额名
    /// </summary>
    public string? ShareClass { get; set; }

    public FundStatus Status { get; set; }
}