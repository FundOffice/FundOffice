namespace FMO.IO.AMAC;

public class PendingReportInfo
{
    /// <summary>
    /// 截止日期
    /// </summary>
    public required string disc_date { get; set; }

    /// <summary>
    /// S编码
    /// </summary>
    public required string stock_code { get; set; }

    /// <summary>
    /// 报告名
    /// </summary>
    public required string report_name { get; set; }
}
