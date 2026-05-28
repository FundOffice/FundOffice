using System.ComponentModel;

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
    public int? Month { get; set; }

    public bool Include { get; set; }

    public decimal? Fee { get; set; }
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

public class HugeRedemptionRule
{
    public bool Has { get; set; }

    /// <summary>
    /// 巨额赎回比例
    /// </summary>
    public decimal Ratio { get; set; }

    /// <summary>
    /// 单一投资人规则 
    /// </summary>
    public bool HasRuleForInvestor { get; set; }


    public decimal RatioPerInvestor { get; set; }

    public override string ToString() => Has switch
    {
        true when Ratio > 0 => $"{Ratio * 100}%",
        _ => "未设置"
    };
}


/// <summary>
/// 证券投资基金类型
/// </summary>
[TypeConverter(nameof(EnumDescriptionTypeConverter))]
public enum SecurityFundType
{
    [Description("未设置")] Unk,

    /// <summary>
    /// 固定收益类基金（如债券型基金）
    /// </summary>
    [Description("固定收益类")]
    FixedIncome = 1,

    /// <summary>
    /// 权益类基金（如股票型基金）
    /// </summary>
    [Description("权益类")]
    Equity = 2,

    /// <summary>
    /// 期货及衍生品类基金
    /// </summary>
    [Description("期货和衍生品类")]
    CommodityAndDerivatives = 3,

    /// <summary>
    /// 混合类基金（投资于股票、债券等多种资产）
    /// </summary>
    [Description("混合类")]
    Hybrid = 4,

    /// <summary>
    /// 母基金（FOF，投资于其他基金）
    /// </summary>
    //[Description("母基金")]
    //FundOfFunds = 5
}


[TypeConverter(nameof(EnumDescriptionTypeConverter))]
public enum FundMode
{
    [Description("开放式")] Open,

    [Description("封闭式")] Close,

    [Description("其它")] Other,
}



/// <summary>
/// 类型：开放 封闭
/// </summary>
public class FundModeInfo
{
    public FundMode Mode { get; set; }

    public string? Other { get; set; }

    public override string ToString() => Mode switch { FundMode.Open => "开放式", FundMode.Close => "封闭式", FundMode.Other => Other ?? "未设置", _ => "未设置" };
}


#region 封闭、锁定

[TypeConverter(nameof(EnumDescriptionTypeConverter))]
public enum SealingType
{
    None,

    [Description("无")] No,

    [Description("有")] Has,

    [Description("其它")] Other,
}

/// <summary>
/// 封闭、锁定
/// </summary>
public class SealingRule
{
    /// <summary>
    /// 封闭类型
    /// </summary>
    public SealingType Type { get; set; }

    /// <summary>
    /// 封闭月数
    /// </summary>
    public int Month { get; set; }

    /// <summary>
    /// 其它
    /// </summary>
    public string? Extra { get; set; }

    public override string ToString() => Type switch { SealingType.Has => $"{Month}个月", SealingType.No => "无", _ => Extra ?? "未设置" };
}

#endregion



[TypeConverter(typeof(EnumDescriptionTypeConverter))]
public enum RiskLevel
{
    [Description("未选择")] Unk, R1, R2, R3, R4, R5
}

[TypeConverter(typeof(EnumDescriptionTypeConverter))]
public enum RiskEvaluation
{
    [Description("未选择")] Unk, C1, C2, C3, C4, C5
}



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

    public override string ToString()
    {
        var a = MinDeposit == default ? null : $"{MinDeposit / 10000}万起投" + (AdditionalDeposit > 0 ? $"，追加{AdditionalDeposit / 10000}万起" : "") + (HasRequirement ? Statement : "");
        var b = HasFee ? $"   " + PayMethod switch { FundFeePayType.Out => "价外收取", FundFeePayType.Extra => "额外收取", FundFeePayType.Other => PayOther, _ => "" } + Type switch { FundFeeType.Ratio => $"{Fee}%", FundFeeType.Fix => $"{Fee}元", FundFeeType.Other => Other, _ => "未知费用" } : null;
        var c = HasGuaranteedFee ? $"  保底 {GuaranteedFee}元" : null;
        return (a + b + c) switch { null or "" => "未设置", var x => x };
    }
}

/// <summary>
/// 存续期
/// </summary>
public class FundDuration
{
    /// <summary>
    /// 永续
    /// </summary>
    public bool Infinity { get; set; }


    public int Month { get; set; }


    public override string ToString()
    {
        return Infinity ? "无固定期限" : Month switch { var m when m > 0 && m % 12 == 0 => $"{m / 12}年", > 0 => $"{Month}个月", _ => "未设置" };
    }

}



#region OpenRule

public enum SequenceOrder { Ascend, Descend }

public class OpenRule : ICloneable
{
    static string[] weekhead = ["一", "二", "三", "四", "五",];

    public bool AllowBuy { get; set; } = true;

    public bool AllowSell { get; set; } = true;

    public FundOpenType Type { get; set; }

    /// <summary>
    /// 选择季
    /// Year 1-4
    /// 其它 忽略
    /// </summary>
    public int[]? Quarters { get; set; }

    /// <summary>
    /// 选择月
    /// Year 否则1-12
    /// QuarterFlag 则1-3
    /// Month Week 忽略
    /// </summary>
    public int[]? Months { get; set; }

    /// <summary>
    /// 选择周
    /// Year 否则1-54
    /// QuarterFlag 则1-14
    /// Month 1-5
    /// Week 忽略
    /// </summary>
    public int[]? Weeks { get; set; }

    public SequenceOrder WeekOrder { get; set; }

    /// <summary>
    /// 选择周
    /// Year 否则1-54
    /// QuarterFlag 则1-14
    /// Month 1-5
    /// Week 忽略
    /// </summary>
    public int[]? Dates { get; set; }

    /// <summary>
    /// 选择天
    /// Year 1-365
    /// QuarterFlag 1-92
    /// Month 1-31
    /// Week 7
    /// </summary>
    public SequenceOrder DayOrder { get; set; }

    public bool TradeOrNatural { get; set; }

    /// <summary>
    /// 是否顺延
    /// </summary>
    public bool Postpone‌ { get; set; }

    /// <summary>
    /// 顺延是否跨周
    /// </summary>
    public bool CrossWeek { get; set; }


    private string WeekStr()
    {
        var days = Dates?.Where(x => x < 5);
        if (days is null || !days.Any()) return "";

        if (DayOrder == SequenceOrder.Ascend)
        {
            if (TradeOrNatural)
                return $"第{string.Join('、', days!.Select(x => x))}个交易日";
            else
                return $"{string.Join('、', days!.Select(x => weekhead[x - 1]))}";
        }
        else
        {
            if (TradeOrNatural)
                return $"倒数第{string.Join('、', days!.Select(x => x))}个交易日";
            else
                return $"倒数第{string.Join('、', days!.Select(x => x))}个自然日";
        }
    }


    private string MonthStr()
    {
        if (Dates is null || Dates.Length == 0) return "";

        if (Weeks?.Length > 0)
            return $"{(WeekOrder == SequenceOrder.Ascend ? "" : "倒数")}第{string.Join('、', Weeks.Select(x => x))}个的{WeekStr()}";
        else
            return $"{(DayOrder == SequenceOrder.Ascend ? "" : "倒数")}{(TradeOrNatural ? "第" : "")}{string.Join('、', Dates.Select(x => x))}{(TradeOrNatural ? "个交易" : "")}日开放";
    }

    private string QuarterStr()
    {
        if (Dates is null || Dates.Length == 0) return "";

        if (Months?.Length > 0)
            return $"第{string.Join('、', Months.Select(x => x))}月的{MonthStr()}";
        else if (Weeks?.Length > 0)
            return $"{(WeekOrder == SequenceOrder.Ascend ? "" : "倒数")}第{string.Join('、', Weeks.Select(x => x))}个的{WeekStr()}";
        else
            return $"{(DayOrder == SequenceOrder.Ascend ? "" : "倒数")}第{string.Join('、', Dates.Select(x => x))}个{(TradeOrNatural ? "交易" : "自然")}日开放";
    }

    public override string ToString()
    {
        var pos = AllowBuy && AllowSell ? "" : AllowBuy ? "开放申购" : "开放赎回";


        switch (Type)
        {
            case FundOpenType.Closed:
                return "不开放";
            case FundOpenType.Yearly:
                if (Dates is null || Dates.Length == 0) return "无效的设置";
                if (Quarters?.Length > 0)
                    return $"每年第{string.Join('、', Quarters.Select(x => x))}季度的{QuarterStr()}{pos}{(Postpone ? "，非交易日顺延" : "")}";
                else if (Months?.Length > 0)
                    return $"每年第{string.Join('、', Months.Select(x => x))}月的{MonthStr()}{pos}{(Postpone ? "，非交易日顺延" : "")}";
                else if (Weeks?.Length > 0)
                    return $"每年{(WeekOrder == SequenceOrder.Ascend ? "" : "倒数")}第{string.Join('、', Weeks.Select(x => x + 1))}周的{WeekStr()}{pos}{(Postpone ? "，非交易日顺延" : "")}";
                else
                    return $"每年{(DayOrder == SequenceOrder.Ascend ? "" : "倒数")}第{string.Join('、', Dates.Select(x => x + 1))}个{(TradeOrNatural ? "交易" : "自然")}日开放{pos}{(Postpone ? "，非交易日顺延" : "")}";
            case FundOpenType.Quarterly:
                if (Dates is null || Dates.Length == 0) return "无效的设置";
                return $"每季{QuarterStr()}{pos}{(Postpone ? "，非交易日顺延" : "")}";
            case FundOpenType.Monthly:
                if (Dates is null || Dates.Length == 0) return "无效的设置";
                return $"每月{MonthStr()}{pos}{(Postpone ? "，非交易日顺延" : "")}";
            case FundOpenType.Weekly:
                if (Dates is null || Dates.Length == 0) return "无效的设置";

                return $"每周{WeekStr()}{pos}{(Postpone ? "，非交易日顺延" : "")}";
            case FundOpenType.Daily:
                return $"每日{pos}";
            default:
                return "-";
        }

    }

    public bool IsValid()
    {
        switch (Type)
        {
            case FundOpenType.Closed:
                return true;
            case FundOpenType.Yearly:
                if (Dates is null || Dates.Length == 0) return false;
                return true;
            case FundOpenType.Quarterly:
                if (Dates is null || Dates.Length == 0) return false;
                return true;
            case FundOpenType.Monthly:
                if (Dates is null || Dates.Length == 0) return false;
                return true;
            case FundOpenType.Weekly:
                if (Dates is null || Dates.Length == 0) return false;
                return true;
            case FundOpenType.Daily:
                return true;
            default:
                return false;
        }
    }

    public DateOpenInfo[] Apply(int year)
    {
        if (Type == FundOpenType.Closed)
            return Days.DayInfosByYear(year).Select(x => new DateEx { Date = x.Date, Flag = x.Flag, WeekOfMonth = 1, WeekOfYear = 1, Type = OpenType.None }).ToArray();

        if (Type == FundOpenType.Daily)
            return Days.DayInfosByYear(year).Select(x => new DateEx { Date = x.Date, Flag = x.Flag, WeekOfMonth = 1, WeekOfYear = 1, Type = x.Flag.HasFlag(DayFlag.Trade) ? OpenType.Fixed : OpenType.None }).ToArray();

        var result = Days.DayInfosByYear(year).Select(x => new DateEx { Date = x.Date, Flag = x.Flag, WeekOfMonth = 1, WeekOfYear = 1, Type = OpenType.None }).ToList();

        // 计算周序号
        for (int i = 1; i < result.Count; i++)
        {
            var last = result[i - 1];
            var cur = result[i];
            if (cur.Date.DayOfWeek == DayOfWeek.Monday)
                cur.WeekOfYear = last.WeekOfYear + 1;
            else cur.WeekOfYear = last.WeekOfYear;
        }

        ////////////////////////////////////////////////////////
        // 季度
        if (Type == FundOpenType.Yearly && Quarters?.Length > 0)
        {
            // 排除不符合的季度
            // 计算
            var f = result.Where(x => Array.BinarySearch(Quarters, (x.Date.Month - 1) / 3 + 1) < 0);

            foreach (var x in f)
                x.IsExclude = true;
        }

        // 月
        if (Type switch { FundOpenType.Yearly or FundOpenType.Quarterly => true, _ => false })
        {
            if (Months?.Length > 0)
            {
                // 计算 
                var sq = Type == FundOpenType.Quarterly || Quarters?.Length > 0;
                var f = result.Where(x => Array.BinarySearch(Months, sq ? (x.Date.Month - 1) % 3 + 1 : x.Date.Month) < 0);

                foreach (var x in f)
                    x.IsExclude = true;
            }
        }
        ////////////////////////////////////////////////////////
        ///


        // 周
        if (Type switch { FundOpenType.Yearly or FundOpenType.Quarterly or FundOpenType.Monthly => true, _ => false })
        {
            var sm = Type == FundOpenType.Monthly || Months?.Length > 0;
            var sq = Type == FundOpenType.Quarterly || Quarters?.Length > 0;
            var sw = Weeks?.Length > 0;

            if (sm) // 按月
            {
                // 交易日
                if (TradeOrNatural)
                {
                    if (sw && Dates?.Length > 0) //n 周的第n个交易日 此项手动不可用
                    {
                        IEnumerable<DateEx> orderd = DayOrder == SequenceOrder.Ascend ? result : result.AsEnumerable().Reverse();
                        var sel = orderd.Where(x => !x.IsExclude).GroupBy(x => x.Date.Month);
                        foreach (var m in sel)
                        {
                            foreach (var w in m.GroupBy(x => x.WeekOfYear).Index().Select(x => (Index: x.Index + 1, x.Item)))
                            {
                                bool pairw = Array.BinarySearch(Weeks!, w.Index) >= 0;
                                if (!pairw) continue;

                                foreach (var s in w.Item.Index())
                                {
                                    if (Array.BinarySearch(Dates, s.Index + 1) >= 0)
                                        s.Item.Type = OpenType.Fixed;
                                }
                            }
                        }

                        // 非交易日及顺延
                        var chk = result.Where(x => !x.Flag.HasFlag(DayFlag.Trade) && x.Type == OpenType.Fixed).ToArray();
                        foreach (var item in chk)
                        {
                            item.Type = OpenType.None;

                            //if (Postpone) //顺延
                            {
                                int idx = result.IndexOf(item);
                                for (int i = idx + 1; i < result.Count; i++)
                                {
                                    if (result[i].Type != OpenType.Fixed && !result[i].IsExclude && result[i].Flag.HasFlag(DayFlag.Trade))
                                    {
                                        result[i].Type = OpenType.Fixed;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    else if (Dates?.Length > 0) //月的第n个交易日
                    {
                        IEnumerable<DateEx> orderd = DayOrder == SequenceOrder.Ascend ? result : result.AsEnumerable().Reverse();
                        var sel = orderd.Where(x => !x.IsExclude && x.Flag.HasFlag(DayFlag.Trade)).GroupBy(x => x.Date.Month);

                        foreach (var m in sel)
                        {
                            foreach (var s in m.Index())
                            {
                                if (Array.BinarySearch(Dates, s.Index + 1) >= 0)
                                    s.Item.Type = OpenType.Fixed;
                            }
                        }
                    }
                }
                else// 自然日
                {
                    if (sw && Dates?.Length > 0) //n个周N 
                    {
                        IEnumerable<DateEx> orderd = DayOrder == SequenceOrder.Ascend ? result : result.AsEnumerable().Reverse();
                        var sel = orderd.Where(x => !x.IsExclude).GroupBy(x => (int)x.Date.DayOfWeek);// && Array.BinarySearch(Dates, (int)x.Date.DayOfWeek) >= 0).GroupBy(x => x.Date.Month);
                        foreach (var dw in sel)
                        {
                            if (Array.BinarySearch(Dates, dw.Key) < 0) // 星期几不符合
                                continue;

                            foreach (var m in dw.GroupBy(x => x.Date.Month)) // 月
                            {
                                foreach (var w in m.Index()) //第N周N
                                {
                                    bool pairw = Array.BinarySearch(Weeks!, w.Index + 1) >= 0;
                                    if (!pairw) continue;

                                    w.Item.Type = OpenType.Fixed;
                                }
                            }
                        }


                    }
                    else if (Dates?.Length > 0) //月的第n个交易日
                    {
                        IEnumerable<DateEx> orderd = DayOrder == SequenceOrder.Ascend ? result : result.AsEnumerable().Reverse();
                        var sel = orderd.Where(x => !x.IsExclude).GroupBy(x => x.Date.Month);

                        foreach (var m in sel)
                        {
                            foreach (var s in m.Index())
                            {
                                if (Array.BinarySearch(Dates, s.Index + 1) >= 0)
                                    s.Item.Type = OpenType.Fixed;
                            }
                        }
                    }

                    // 非交易日及顺延
                    var chk = result.Where(x => !x.Flag.HasFlag(DayFlag.Trade) && x.Type == OpenType.Fixed).ToArray();
                    foreach (var item in chk)
                    {
                        item.Type = OpenType.None;

                        if (Postpone) //顺延
                        {
                            int idx = result.IndexOf(item);
                            for (int i = idx + 1; i < result.Count; i++)
                            {
                                if (result[i].Type != OpenType.Fixed && !result[i].IsExclude && result[i].Flag.HasFlag(DayFlag.Trade))
                                {
                                    result[i].Type = OpenType.Fixed;
                                    break;
                                }
                            }
                        }
                    }
                }

            }
            else if (sq) //季
            {
                // 交易日
                if (TradeOrNatural)
                {
                    if (sw && Dates?.Length > 0) //n 周的第n个交易日 此项手动不可用
                    {
                        IEnumerable<DateEx> orderd = DayOrder == SequenceOrder.Ascend ? result : result.AsEnumerable().Reverse();
                        var sel = orderd.Where(x => !x.IsExclude).GroupBy(x => (x.Date.Month - 1) / 3 + 1);
                        foreach (var m in sel)
                        {
                            foreach (var w in m.GroupBy(x => x.WeekOfYear).Index().Select(x => (Index: x.Index + 1, x.Item)))
                            {
                                bool pairw = Array.BinarySearch(Weeks!, w.Index) >= 0;
                                if (!pairw) continue;

                                foreach (var s in w.Item.Index())
                                {
                                    if (Array.BinarySearch(Dates, s.Index + 1) >= 0)
                                        s.Item.Type = OpenType.Fixed;
                                }
                            }
                        }

                        // 非交易日及顺延
                        var chk = result.Where(x => !x.Flag.HasFlag(DayFlag.Trade) && x.Type == OpenType.Fixed).ToArray();
                        foreach (var item in chk)
                        {
                            item.Type = OpenType.None;

                            //if (Postpone) //顺延
                            {
                                int idx = result.IndexOf(item);
                                for (int i = idx + 1; i < result.Count; i++)
                                {
                                    if (result[i].Type != OpenType.Fixed && !result[i].IsExclude && result[i].Flag.HasFlag(DayFlag.Trade))
                                    {
                                        result[i].Type = OpenType.Fixed;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    else if (Dates?.Length > 0) //月的第n个交易日
                    {
                        IEnumerable<DateEx> orderd = DayOrder == SequenceOrder.Ascend ? result : result.AsEnumerable().Reverse();
                        var sel = orderd.Where(x => !x.IsExclude && x.Flag.HasFlag(DayFlag.Trade)).GroupBy(x => (x.Date.Month - 1) / 3 + 1);

                        foreach (var m in sel)
                        {
                            foreach (var s in m.Index())
                            {
                                if (Array.BinarySearch(Dates, s.Index + 1) >= 0)
                                    s.Item.Type = OpenType.Fixed;
                            }
                        }
                    }
                }
                else// 自然日
                {
                    if (sw && Dates?.Length > 0) //n个周N 
                    {
                        IEnumerable<DateEx> orderd = DayOrder == SequenceOrder.Ascend ? result : result.AsEnumerable().Reverse();
                        var sel = orderd.Where(x => !x.IsExclude).GroupBy(x => (int)x.Date.DayOfWeek);// && Array.BinarySearch(Dates, (int)x.Date.DayOfWeek) >= 0).GroupBy(x => x.Date.Month);
                        foreach (var dw in sel)
                        {
                            if (Array.BinarySearch(Dates, dw.Key) < 0) // 星期几不符合
                                continue;

                            foreach (var m in dw.GroupBy(x => (x.Date.Month - 1) / 3 + 1)) // 月
                            {
                                foreach (var w in m.Index()) //第N周N
                                {
                                    bool pairw = Array.BinarySearch(Weeks!, w.Index + 1) >= 0;
                                    if (!pairw) continue;

                                    w.Item.Type = OpenType.Fixed;
                                }
                            }
                        }


                    }
                    else if (Dates?.Length > 0) //月的第n个交易日
                    {
                        IEnumerable<DateEx> orderd = DayOrder == SequenceOrder.Ascend ? result : result.AsEnumerable().Reverse();
                        var sel = orderd.Where(x => !x.IsExclude).GroupBy(x => (x.Date.Month - 1) / 3 + 1);

                        foreach (var m in sel)
                        {
                            foreach (var s in m.Index())
                            {
                                if (Array.BinarySearch(Dates, s.Index + 1) >= 0)
                                    s.Item.Type = OpenType.Fixed;
                            }
                        }
                    }

                    // 非交易日及顺延
                    var chk = result.Where(x => !x.Flag.HasFlag(DayFlag.Trade) && x.Type == OpenType.Fixed).ToArray();
                    foreach (var item in chk)
                    {
                        item.Type = OpenType.None;

                        if (Postpone) //顺延
                        {
                            int idx = result.IndexOf(item);
                            for (int i = idx + 1; i < result.Count; i++)
                            {
                                if (result[i].Type != OpenType.Fixed && !result[i].IsExclude && result[i].Flag.HasFlag(DayFlag.Trade))
                                {
                                    result[i].Type = OpenType.Fixed;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            else //年
            {
                // 交易日
                if (TradeOrNatural)
                {
                    if (sw && Dates?.Length > 0) //n 周的第n个交易日 此项手动不可用
                    {
                        IEnumerable<DateEx> orderd = DayOrder == SequenceOrder.Ascend ? result : result.AsEnumerable().Reverse();
                        var sel = orderd.Where(x => !x.IsExclude).GroupBy(x => x.WeekOfYear);
                        foreach (var w in sel.Index())
                        {
                            bool pairw = Array.BinarySearch(Weeks!, w.Index) >= 0;
                            if (!pairw) continue;

                            foreach (var s in w.Item.Index())
                            {
                                if (Array.BinarySearch(Dates, s.Index + 1) >= 0)
                                    s.Item.Type = OpenType.Fixed;
                            }

                        }

                        // 非交易日及顺延
                        var chk = result.Where(x => !x.Flag.HasFlag(DayFlag.Trade) && x.Type == OpenType.Fixed).ToArray();
                        foreach (var item in chk)
                        {
                            item.Type = OpenType.None;

                            //if (Postpone) //顺延
                            {
                                int idx = result.IndexOf(item);
                                for (int i = idx + 1; i < result.Count; i++)
                                {
                                    if (result[i].Type != OpenType.Fixed && !result[i].IsExclude && result[i].Flag.HasFlag(DayFlag.Trade))
                                    {
                                        result[i].Type = OpenType.Fixed;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    else if (Dates?.Length > 0) //的第n个交易日
                    {
                        IEnumerable<DateEx> orderd = DayOrder == SequenceOrder.Ascend ? result : result.AsEnumerable().Reverse();
                        var sel = orderd.Where(x => !x.IsExclude && x.Flag.HasFlag(DayFlag.Trade));

                        foreach (var s in sel.Index())
                        {
                            if (Array.BinarySearch(Dates, s.Index + 1) >= 0)
                                s.Item.Type = OpenType.Fixed;
                        }

                    }
                }
                else// 自然日
                {
                    if (sw && Dates?.Length > 0) //n个周N 
                    {
                        IEnumerable<DateEx> orderd = DayOrder == SequenceOrder.Ascend ? result : result.AsEnumerable().Reverse();
                        var sel = orderd.Where(x => !x.IsExclude).GroupBy(x => (int)x.Date.DayOfWeek);
                        foreach (var dw in sel)
                        {
                            if (Array.BinarySearch(Dates, dw.Key) < 0) // 星期几不符合
                                continue;

                            foreach (var w in dw.Index()) //第N周N
                            {
                                bool pairw = Array.BinarySearch(Weeks!, w.Index + 1) >= 0;
                                if (!pairw) continue;

                                w.Item.Type = OpenType.Fixed;
                            }

                        }


                    }
                    else if (Dates?.Length > 0) //月的第n个交易日
                    {
                        IEnumerable<DateEx> orderd = DayOrder == SequenceOrder.Ascend ? result : result.AsEnumerable().Reverse();
                        var sel = orderd.Where(x => !x.IsExclude).GroupBy(x => x.Date.Month);

                        foreach (var m in sel)
                        {
                            foreach (var s in m.Index())
                            {
                                if (Array.BinarySearch(Dates, s.Index + 1) >= 0)
                                    s.Item.Type = OpenType.Fixed;
                            }
                        }
                    }

                    // 非交易日及顺延
                    var chk = result.Where(x => !x.Flag.HasFlag(DayFlag.Trade) && x.Type == OpenType.Fixed).ToArray();
                    foreach (var item in chk)
                    {
                        item.Type = OpenType.None;

                        if (Postpone) //顺延
                        {
                            int idx = result.IndexOf(item);
                            for (int i = idx + 1; i < result.Count; i++)
                            {
                                if (result[i].Type != OpenType.Fixed && !result[i].IsExclude && result[i].Flag.HasFlag(DayFlag.Trade))
                                {
                                    result[i].Type = OpenType.Fixed;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }
        else if (Type == FundOpenType.Weekly) //每周
        {
            // 交易日
            if (TradeOrNatural)
            {
                if (Dates?.Length > 0) //每周的第n个交易日
                {
                    IEnumerable<DateEx> orderd = DayOrder == SequenceOrder.Ascend ? result : result.AsEnumerable().Reverse();
                    var sel = orderd.Where(x => !x.IsExclude && x.Flag.HasFlag(DayFlag.Trade)).GroupBy(x => x.WeekOfYear);
                    foreach (var m in sel)
                    {
                        foreach (var d in m.Index())
                        {

                            if (Array.BinarySearch(Dates, d.Index + 1) >= 0)
                                d.Item.Type = OpenType.Fixed;
                        }
                    }

                    // 非交易日及顺延
                    //var chk = result.Where(x => !x.Flag.HasFlag(DayFlag.Trade) && x.Type == OpenType.Fixed).ToArray();
                    //foreach (var item in chk)
                    //{
                    //    item.Type = OpenType.None;

                    //    //if (Postpone) //顺延
                    //    {
                    //        int idx = result.IndexOf(item);
                    //        for (int i = idx + 1; i < result.Count; i++)
                    //        {
                    //            if (result[i].Type != OpenType.Fixed && !result[i].IsExclude && result[i].Flag.HasFlag(DayFlag.Trade))
                    //            {
                    //                result[i].Type = OpenType.Fixed;
                    //                break;
                    //            }
                    //        }
                    //    }
                    //}
                }
                else if (Dates?.Length > 0) //月的第n个交易日
                {
                    IEnumerable<DateEx> orderd = DayOrder == SequenceOrder.Ascend ? result : result.AsEnumerable().Reverse();
                    var sel = orderd.Where(x => !x.IsExclude && x.Flag.HasFlag(DayFlag.Trade)).GroupBy(x => x.Date.Month);

                    foreach (var m in sel)
                    {
                        foreach (var s in m.Index())
                        {
                            if (Array.BinarySearch(Dates, s.Index + 1) >= 0)
                                s.Item.Type = OpenType.Fixed;
                        }
                    }
                }
            }
            else// 自然日
            {
                if (Dates?.Length > 0) //n个周N 
                {
                    IEnumerable<DateEx> orderd = DayOrder == SequenceOrder.Ascend ? result : result.AsEnumerable().Reverse();
                    var sel = orderd.Where(x => !x.IsExclude).GroupBy(x => (int)x.Date.DayOfWeek);
                    foreach (var dw in sel)
                    {
                        if (Array.BinarySearch(Dates, dw.Key) < 0) // 星期几不符合
                            continue;


                        foreach (var w in dw) //第N周N
                        {
                            w.Type = OpenType.Fixed;
                        }

                    }


                    // 非交易日及顺延
                    var chk = result.Where(x => !x.Flag.HasFlag(DayFlag.Trade) && x.Type == OpenType.Fixed).ToArray();
                    foreach (var item in chk)
                    {
                        item.Type = OpenType.None;

                        if (Postpone) //顺延
                        {
                            int idx = result.IndexOf(item);
                            for (int i = idx + 1; i < result.Count; i++)
                            {
                                if (result[i].Type != OpenType.Fixed && !result[i].IsExclude && result[i].Flag.HasFlag(DayFlag.Trade))
                                {
                                    result[i].Type = OpenType.Fixed;
                                    break;
                                }
                            }
                        }
                    }
                }

            }
        }
        return result.ToArray();
    }


    public static DateOpenInfo[] ApplyMany(int year, params OpenRule[]? rules)
    {
        var baseDays = Days.DayInfosByYear(year);
        var result = new DateOpenInfo[baseDays.Length];

        // 初始化全年基础数据
        for (int i = 0; i < baseDays.Length; i++)
        {
            result[i] = new DateOpenInfo
            {
                Date = baseDays[i].Date,
                Flag = baseDays[i].Flag,
                Type = OpenType.None,
                TradeType = OpenTradeType.None
            };
        }

        if (rules?.Length is null or 0)
            return result;

        // 遍历所有规则（或的关系）
        foreach (var rule in rules)
        {
            if (rule is null) continue;

            // 计算当前 rule 的 TradeType
            OpenTradeType ruleTradeType = OpenTradeType.None;
            if (rule.AllowBuy) ruleTradeType |= OpenTradeType.Purchase;
            if (rule.AllowSell) ruleTradeType |= OpenTradeType.Redemption;

            // 获取当前 rule 应用的日期结果
            var ruleResult = rule.Apply(year);
            int len = Math.Min(result.Length, ruleResult.Length);

            for (int i = 0; i < len; i++)
            {
                var item = ruleResult[i];
                var target = result[i];

                // 更新 OpenType（不允许从非None降级为None）
                if (item.Type != OpenType.None)
                    target.Type = item.Type;

                // 更新 TradeType（使用 | 运算合并）
                target.TradeType |= ruleTradeType;
            }
        }

        return result;
    }

    private static int QuarterOfDay(DateOnly d) => (d.Month - 1) / 3;

    public object Clone()
    {
        return new OpenRule
        {
            // 值类型直接复制
            AllowBuy = this.AllowBuy,
            AllowSell = this.AllowSell,
            Type = this.Type,
            WeekOrder = this.WeekOrder,
            DayOrder = this.DayOrder,
            TradeOrNatural = this.TradeOrNatural,
            Postpone = this.Postpone,
            CrossWeek = this.CrossWeek,

            // 数组类型 → 深度克隆（避免引用共用）
            Quarters = this.Quarters?.ToArray(),
            Months = this.Months?.ToArray(),
            Weeks = this.Weeks?.ToArray(),
            Dates = this.Dates?.ToArray()
        };

    }

    public class DateEx : DateOpenInfo
    {
        public bool IsExclude { get; set; }

        public Pair Pair { get; set; }

        public int WeekOfYear { get; set; }

        public int WeekOfMonth { get; set; }

        public int WeekOfQuarter { get; set; }

        public int Calc { get; set; }

        public void PairTo(Pair pair) => Pair |= pair;
    }

    [Flags]
    public enum Pair
    {
        Quarter = 1,

        Month = 2,

        Week = 4,

        Day = 8,

        Ok = Quarter | Month | Week | Day,
    }

}

public enum OpenType
{
    None,

    /// <summary>
    /// 固定
    /// </summary>
    Fixed,

    /// <summary>
    /// 临时
    /// </summary>
    Temperaty,

    /// <summary>
    /// 顺延的
    /// </summary>
    Postpone
}


public interface IDate
{
    public DateOnly Date { get; }
}


[Flags]
public enum OpenTradeType
{
    None,

    Purchase = 1,

    Redemption = 2,

    Both = Purchase | Redemption
}

/// <summary>
/// 日历
/// </summary>
public class DateOpenInfo : IDate
{
    public required DateOnly Date { get; init; }

    public DayFlag Flag { get; set; }

    public OpenType Type { get; set; }

    public OpenTradeType TradeType { get; set; }
}

/// <summary>
/// 基金开放日
/// </summary>
public class FundOpenDay
{
    public int FundId { get; set; }

    public int ShareId { get; set; }

    public DateOnly Date { get; set; }

    public OpenType OpenType { get; set; }

    public OpenTradeType TradeType { get; set; }

    /// <summary>
    /// 来源：托管、电签、手动输入等
    /// </summary>
    public string? Source { get; set; }
}

#endregion





/// <summary>
/// 临时开放
/// </summary>
public class TemporarilyOpenInfo
{
    public bool IsAllowed { get; set; }

    /// <summary>
    /// 仅在合同变更、法规
    /// </summary>
    public bool IsLimited { get; set; }

    public bool AllowPurchase { get; set; }

    public bool AllowRedemption { get; set; }


    public override string ToString() => !IsAllowed ? "不允许临开" : (IsLimited ? "仅合同变更、法规变更时，" : "") + $"允许{(AllowPurchase ? "申购" : "")}{(AllowRedemption ? "赎回" : "")}";
}





[TypeConverter(typeof(EnumDescriptionTypeConverter))]
public enum CoolingPeriodType
{
    [Description("24小时")] OneDay,

    [Description("其它")] Other
}


public class CoolingPeriodInfo
{
    public CoolingPeriodType Type { get; set; }

    public string? Other { get; set; }


    public override string ToString()
    {
        return Type switch
        {
            CoolingPeriodType.OneDay => "24小时",
            CoolingPeriodType.Other => Other ?? "其它",
            _ => "未知"
        };
    }
}


public class CallbackInfo
{
    public bool IsRequired { get; set; }

    public override string ToString()
    {
        return IsRequired ? "需要回访" : "不适用";
    }
}


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





public class FundInvestmentManager
{
    public int Id { get; set; }


    /// <summary>
    /// ParticipantId
    /// </summary>
    public int PersonId { get; set; }

    public int FundId { get; set; }

    /// <summary>
    /// 姓名
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// 简介
    /// </summary>
    public string? Profile { get; set; }


    public DateOnly Start { get; set; }

    public DateOnly End { get; set; }
}













/// <summary>
/// 已在其它地方定义
/// </summary>
//public class BankAccount
//{
//    public int Id { get; set; }


//    // public int OwnerId { get; set; }



//    /// <summary>
//    /// 户名
//    /// </summary>
//    public string? Name { get; set; }

//    /// <summary>
//    /// 账号
//    /// </summary>
//    public string? Number { get; set; }

//    /// <summary>
//    /// 银行
//    /// </summary>
//    public string? Bank { get; set; }

//    /// <summary>
//    /// 银行-支行
//    /// </summary>
//    public string? Branch { get; set; }

//    /// <summary>
//    /// 开户行
//    /// </summary>
//    public string? BankOfDeposit { get => Bank + Branch; set => SetDeposit(value); }


//    public string? LargePayNo { get; set; }

//    /// <summary>
//    /// swift
//    /// </summary>
//    public string? SwiftCode { get; set; }

//    /// <summary>
//    /// 银行地址
//    /// </summary>
//    public string? BankAddress { get; set; }

//    /// <summary>
//    /// 注销
//    /// </summary>
//    public bool IsClosed { get; set; }
//}

