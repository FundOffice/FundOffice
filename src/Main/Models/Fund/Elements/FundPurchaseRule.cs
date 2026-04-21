namespace FMO.Models;

public class FundPurchaseRule
{
    /// <summary>
    /// 起投
    /// </summary>
    public int MinDeposit { get; set; }

    /// <summary>
    /// 追加金额
    /// </summary>
    public int AdditionalDeposit { get; set; }

    /// <summary>
    /// 有附加要求
    /// </summary>
    public bool HasRequirement { get; set; }

    /// <summary>
    /// 附加要求
    /// </summary>
    public string? Statement { get; set; }

    /// <summary>
    /// 是否收费
    /// </summary>
    public bool HasFee { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public FundFeeType Type { get; set; }


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

    public FundFeePayType PayMethod { get; set; }

    public string? PayOther { get; set; }


    public string FeeStatement => HasFee ? PayMethod switch { FundFeePayType.Out => "价外收取", FundFeePayType.Extra => "额外收取", FundFeePayType.Other => PayOther, _ => "" } +
                                            Type switch { FundFeeType.Ratio => $"{Fee}%", FundFeeType.Fix => $"{Fee}元", FundFeeType.Other => Other, _ => "未知费用" } +
                                            HasGuaranteedFee switch { true => $"  保底 {GuaranteedFee}元", _ => "" } : "-";
}