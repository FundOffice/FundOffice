using System.ComponentModel;

namespace FMO.Models;


/// <summary>
/// 最多16种基金定期报告类型
/// 不然FundPeriodicReport的ID会溢出
/// </summary>
[TypeConverter(typeof(EnumDescriptionTypeConverter))]
public enum FundReportType
{
    /// <summary>
    /// 每月报告
    /// </summary>
    [Description("月报")]
    MonthlyReport,
    /// <summary>
    /// 每季度报告
    /// </summary>
    [Description("季报")]
    QuarterlyReport,
    /// <summary>
    /// 半年报告
    /// </summary>
    [Description("半年报")]
    SemiAnnualReport,
    /// <summary>
    /// 年度报告
    /// </summary>
    [Description("年报")]
    AnnualReport,
    /// <summary>
    /// 季度更新
    /// </summary>
    [Description("季度更新")]
    QuarterlyUpdate,
}

public interface IPeriodical
{
    int Id { get; }

    int FundId { get; }
    
    FundReportType Type { get; }

    string? FundCode { get; }

    DateOnly PeriodEnd { get; set; }
}

/// <summary>
/// 定期报告
/// </summary>
public class FundPeriodicReport : IPeriodical
{
    public int Id => (PeriodEnd.DayNumber - 719162) << 16 | FundId << 4 | (int)Type;//1970-01-01是公历的第719162天

    public required int FundId { get; set; }

    public string? FundCode { get; set; }

    /// <summary>
    /// 定期报告的最后一天
    /// </summary>
    public DateOnly PeriodEnd { get; set; }

    public FundReportType Type { get; set; }



    public SimpleFile? Word { get; set; }

    public SimpleFile? Excel { get; set; }

    public SimpleFile? Xbrl { get; set; }

    public SimpleFile? Pdf { get; set; }

    public SimpleFile? Sealed { get; set; }
}


/// <summary>
/// 季度更新
/// </summary>
public class FundQuarterlyUpdate : IPeriodical
{
    public int Id => (PeriodEnd.DayNumber - 719162) << 16 | FundId <<4 | (int)Type;

    public required int FundId { get; set; }

    public string? FundCode { get; set; }

    public FundReportType Type => FundReportType.QuarterlyUpdate;



    /// <summary>
    /// 定期报告的最后一天
    /// </summary>
    public DateOnly PeriodEnd { get; set; }

    /// <summary>
    /// 投资者
    /// </summary>
    public SimpleFile? Investor { get; set; }

    /// <summary>
    /// 运行信息
    /// </summary>
    public SimpleFile? Operation { get; set; }
}