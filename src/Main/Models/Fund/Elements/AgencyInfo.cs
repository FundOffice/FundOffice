namespace FMO.Models;

/// <summary>
/// 托管、外包、投顾
/// </summary>
public class AgencyInfo
{
    public bool HasAgency { get; set; }

    public string? Name { get; set; }


    public bool HasFee { get; set; }


    public FundFeeType FeeType { get; set; }


    public decimal Fee { get; set; }

    public bool HasGuaranteedFee { get; set; }

    /// <summary>
    /// 保底费用/年
    /// </summary>
    public decimal GuaranteedFee { get; set; }


    /// <summary>
    /// 特殊类型
    /// </summary>
    public string? Other { get; set; }

}