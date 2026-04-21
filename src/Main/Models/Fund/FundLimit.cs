namespace FMO.Models;

public enum FundLimitReason
{
    None,

    Year,

    TradingDay
}

/// <summary>
/// 受限
/// </summary>
public class FundLimit
{
    public int Id { get; set; }

    /// <summary>
    /// 停止申购
    /// </summary>
    public bool PurchaseLimited { get; set; }

    /// <summary>
    /// 检查日期
    /// </summary>
    public DateOnly CheckDate { get; set; }

    /// <summary>
    /// 受限日
    /// </summary>
    public DateOnly LimitDate { get; set; }

    public DateOnly LimitBaseDate { get; set; }

    /// <summary>
    /// 预计受限日期，是第61日
    /// </summary>
    public DateOnly PotentialLimitDate { get; set; }


    /// <summary>
    /// 最近的一次开始净资产低于500万
    /// </summary>
    public DateOnly NearestFirstBelow { get; set; }



    /// <summary>
    /// 清仓计算起始日，如果超过500万，重新计算
    /// </summary>
    public DateOnly ClearBaseDate { get; set; }


    public DateOnly PotentialClearDate { get; set; }


    /// <summary>
    /// 受限原因
    /// </summary>
    public FundLimitReason LimitReason { get; set; }

    public bool ShouldClear { get; set; }

    /// <summary>
    /// 净值数据有缺失，结果仅参考
    /// </summary>
    public bool DataMissing { get; set; }

    /// <summary>
    /// 当年累计净资产
    /// </summary>
    public decimal TotalAsset { get; set; }

    /// <summary>
    /// 当年交易日数
    /// </summary>
    public int DaysThisYear { get; set; }

    /// <summary>
    /// 预估年度日均
    /// </summary>
    public decimal EstimatedDailyAssets { get; set; }
}
