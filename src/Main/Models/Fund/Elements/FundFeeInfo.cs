namespace FMO.Models;

public class FundFeeInfo
{
    public FundFeeType Type { get; set; }


    public bool HasFee { get; set; }

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

    public override string ToString() => !HasFee ? "-" : Type switch { FundFeeType.Fix => $"固定费用：{Fee}元 / 年", FundFeeType.Ratio => $"{Fee}% / 年", FundFeeType.Other => Other, _ => $"未设置" } + (GuaranteedFee > 0 ? $" 有保底：{GuaranteedFee} / 年" : "");
}


public class PartRedemptionFee
{
    public int Month { get; set; }

    public bool Include { get; set; }

    public decimal Fee { get; set; }
}


public class RedemptionFeeInfo
{
    public FundFeeType Type { get; set; }


    public bool HasFee { get; set; }

    public decimal Fee { get; set; }

    /// <summary>
    /// 特殊类型
    /// </summary>
    public string? Other { get; set; }

    public List<PartRedemptionFee>? Parts { get; set; }


    public override string? ToString()
    {
        return !HasFee ? "-" : Type switch
        {
            FundFeeType.Fix => $"固定费用：{Fee}元 / 年",
            FundFeeType.Ratio => $"{Fee}% / 年",
            FundFeeType.ByTime => $"持有时间T：" + FeeByTimeString(),
            FundFeeType.Other => Other,
            _ => $"未设置"
        };
    }

    private string FeeByTimeString()
    {
        if (Parts is null || Parts.Count == 0) return "";

        string s = "";
        for (int i = 0; i < Parts?.Count; i++)
        {
            var p = Parts[i];
            if (i == 0)
                s += $"T{(!p.Include ? '<' : '≤')}{p.Month}月, {p.Fee}%";
            else if (i == Parts.Count - 1)
                s += $"；T{(Parts[i - 1].Include ? '>' : '≥')}{Parts[i - 1].Month}月, {p.Fee}%";
            else s += $"；{Parts[i - 1].Month}月{(Parts[i - 1].Include ? '<' : '≤')}T{(!p.Include ? '<' : '≤')}{p.Month}月, {p.Fee}%";
        }
        return s;
    }
}