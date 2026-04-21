using FMO.Models;
using System.Text.Json.Serialization;

namespace FMO.Trustee.JsonCITICS;

#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。

internal class DistrubutionJson : JsonBase
{
    /// <summary>
    /// 基金账号
    /// </summary>
    [JsonPropertyName("fundAcco")]
    public string FundAccount { get; set; }

    /// <summary>
    /// 客户名称
    /// </summary>
    [JsonPropertyName("custName")]
    public string CustomerName { get; set; }

    /// <summary>
    /// 销售商代码（ZX6 表示直销）
    /// </summary>
    [JsonPropertyName("agencyNo")]
    public string AgencyCode { get; set; }

    /// <summary>
    /// 销售商名称
    /// </summary>
    [JsonPropertyName("agencyName")]
    public string AgencyName { get; set; }

    /// <summary>
    /// 交易账号
    /// </summary>
    [JsonPropertyName("tradeAcco")]
    public string TradeAccount { get; set; }

    /// <summary>
    /// 确认日期（格式：YYYYMMDD）
    /// </summary>
    [JsonPropertyName("confirmDate")]
    public string ConfirmDate { get; set; }

    /// <summary>
    /// TA确认号
    /// </summary>
    [JsonPropertyName("cserialNo")]
    public string TaConfirmNo { get; set; }

    /// <summary>
    /// 分红登记日期
    /// </summary>
    [JsonPropertyName("regDate")]
    public string RegisterDate { get; set; }

    /// <summary>
    /// 红利发放日期
    /// </summary>
    [JsonPropertyName("date")]
    public string DividendDate { get; set; }

    /// <summary>
    /// 基金名称
    /// </summary>
    [JsonPropertyName("fundName")]
    public string FundName { get; set; }

    /// <summary>
    /// 基金代码
    /// </summary>
    [JsonPropertyName("fundCode")]
    public string FundCode { get; set; }

    /// <summary>
    /// 分红基数份额
    /// </summary>
    [JsonPropertyName("totalShare")]
    public string TotalShares { get; set; }

    /// <summary>
    /// 每单位分红金额
    /// </summary>
    [JsonPropertyName("unitProfit")]
    public string UnitProfit { get; set; }

    /// <summary>
    /// 红利总额
    /// </summary>
    [JsonPropertyName("totalProfit")]
    public string TotalProfit { get; set; }

    /// <summary>
    /// 分红方式：
    /// 0 - 红利再投资
    /// 1 - 现金红利
    /// </summary>
    [JsonPropertyName("flag")]
    public string DividendType { get; set; }

    /// <summary>
    /// 实发现金红利
    /// </summary>
    [JsonPropertyName("realBalance")]
    public string RealCashDividend { get; set; }

    /// <summary>
    /// 再投资红利金额
    /// </summary>
    [JsonPropertyName("reinvestBalance")]
    public string ReinvestAmount { get; set; }

    /// <summary>
    /// 再投资份额
    /// </summary>
    [JsonPropertyName("realShares")]
    public string ReinvestShares { get; set; }

    /// <summary>
    /// 再投资日期
    /// </summary>
    [JsonPropertyName("lastDate")]
    public string ReinvestDate { get; set; }

    /// <summary>
    /// 再投资单位净值
    /// </summary>
    [JsonPropertyName("netValue")]
    public string NetValue { get; set; }

    /// <summary>
    /// 实际业绩提成金额
    /// </summary>
    [JsonPropertyName("deductBalance")]
    public string PerformanceFee { get; set; }

    /// <summary>
    /// 客户类型（参见附录4）
    /// </summary>
    [JsonPropertyName("custType")]
    public string CustomerType { get; set; }

    /// <summary>
    /// 证件类型（参见附录4）
    /// </summary>
    [JsonPropertyName("certiType")]
    public string CertificateType { get; set; }

    /// <summary>
    /// 证件号码
    /// </summary>
    [JsonPropertyName("certiNo")]
    public string CertificateNumber { get; set; }

    /// <summary>
    /// 除权除息日
    /// </summary>
    [JsonPropertyName("exDividendDate")]
    public string ExDividendDate { get; set; }


    public TransferRecord ToObject()
    {
        return new TransferRecord
        {
            CustomerIdentity = CertificateNumber,
            CustomerName = CustomerName,
            ExternalId = TaConfirmNo,
            ConfirmedDate = DateOnly.ParseExact(ConfirmDate, "yyyyMMdd"),
            ConfirmedShare = ParseDecimal(ReinvestShares),
            ConfirmedAmount = ParseDecimal(RealCashDividend),
            ConfirmedNetAmount = ParseDecimal(RealCashDividend),
            RequestDate = DateOnly.ParseExact(ExDividendDate, "yyyyMMdd"),
            RequestAmount = 0,
            RequestShare = ParseDecimal(TotalShares),
            Type = TransferRecordType.Distribution,
            Agency = AgencyName,
            CreateDate = DateOnly.FromDateTime(DateTime.Now),
            PerformanceFee = ParseDecimal(PerformanceFee),
            FundCode = FundCode,
            FundName = FundName,
            Source = "api"
        };
    }
}


#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。