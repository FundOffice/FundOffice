namespace Vetting.Models.Entities;

/// <summary>
/// 证照资质
/// </summary>
public class License
{
    public int Id { get; set; } = 1;

    /// <summary>
    /// 非会员/观察会员/正式会员
    /// </summary>
    public string? FundAssociationMember { get; set; }

    /// <summary>
    ///
    /// </summary>
    public bool InvestmentAdvisor { get; set; }
}
