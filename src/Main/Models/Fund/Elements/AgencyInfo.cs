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


    public override string ToString()
    {
        if (!HasAgency || string.IsNullOrWhiteSpace(Name)) return "未设置";


        return Name + (!HasFee ? "无费用" : FeeType switch { FundFeeType.Fix => $"固定费用：{Fee}元 / 年", FundFeeType.Ratio => $"{Fee}% / 年", FundFeeType.Other => Other, _ => $"未设置" } + (GuaranteedFee > 0 ? $" 有保底：{GuaranteedFee} / 年" : ""));
    }
}