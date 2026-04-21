using FMO.Models;
using System.Text.Json.Serialization;

namespace FMO.Trustee.JsonCITICS;

#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。

/// <summary>
/// 金融产品费用数据模型
/// </summary>
internal class FundDailyFeeJson : JsonBase
{
    /// <summary>
    /// 产品代码
    /// </summary>
    [JsonPropertyName("fcpdm")]
    public string ProductCode { get; set; }

    /// <summary>
    /// 分级产品代码
    /// </summary>
    [JsonPropertyName("ffjdm")]
    public string? ClassificationCode { get; set; }

    /// <summary>
    /// 产品名称
    /// </summary>
    [JsonPropertyName("fcpmc")]
    public string ProductName { get; set; }

    /// <summary>
    /// 业务日期，格式：YYYY - MM - DD
    /// </summary>
    [JsonPropertyName("cdate")]
    public string BusinessDate { get; set; }

    #region 管理费相关
    /// <summary>
    /// 计提管理费
    /// </summary>
    [JsonPropertyName("jtglf")]
    public string AccruedManagementFee { get; set; }

    /// <summary>
    /// 支付管理费
    /// </summary>
    [JsonPropertyName("zfglf")]
    public string PaidManagementFee { get; set; }

    /// <summary>
    /// 未支付管理费
    /// </summary>
    [JsonPropertyName("wzfglf")]
    public string UnpaidManagementFee { get; set; }
    #endregion

    #region 业绩报酬相关
    /// <summary>
    /// 计提业绩报酬
    /// </summary>
    [JsonPropertyName("jtyjbc")]
    public string AccruedPerformanceFee { get; set; }

    /// <summary>
    /// 支付业绩报酬
    /// </summary>
    [JsonPropertyName("zfyjbc")]
    public string PaidPerformanceFee { get; set; }

    /// <summary>
    /// 未支付业绩报酬
    /// </summary>
    [JsonPropertyName("wzfyjbc")]
    public string UnpaidPerformanceFee { get; set; }
    #endregion

    #region 托管费相关
    /// <summary>
    /// 计提托管费
    /// </summary>
    [JsonPropertyName("jttgf")]
    public string AccruedCustodyFee { get; set; }

    /// <summary>
    /// 支付托管费
    /// </summary>
    [JsonPropertyName("zftgf")]
    public string PaidCustodyFee { get; set; }

    /// <summary>
    /// 未支付托管费
    /// </summary>
    [JsonPropertyName("wzftgf")]
    public string UnpaidCustodyFee { get; set; }
    #endregion

    #region 行政服务费相关
    /// <summary>
    /// 计提行政服务费
    /// </summary>
    [JsonPropertyName("jtxzfwf")]
    public string AccruedAdministrativeFee { get; set; }

    /// <summary>
    /// 支付行政服务费
    /// </summary>
    [JsonPropertyName("zfxzfwf")]
    public string PaidAdministrativeFee { get; set; }

    /// <summary>
    /// 未支付行政服务费
    /// </summary>
    [JsonPropertyName("wzfxzfwf")]
    public string UnpaidAdministrativeFee { get; set; }
    #endregion

    #region 销售服务费相关
    /// <summary>
    /// 计提销售服务费
    /// </summary>
    [JsonPropertyName("jtxsfwf")]
    public string AccruedSalesServiceFee { get; set; }

    /// <summary>
    /// 支付销售服务费
    /// </summary>
    [JsonPropertyName("zfxsfwf")]
    public string PaidSalesServiceFee { get; set; }

    /// <summary>
    /// 未支付销售服务费
    /// </summary>
    [JsonPropertyName("wzfxsfwf")]
    public string UnpaidSalesServiceFee { get; set; }
    #endregion

    #region 投资顾问费相关
    /// <summary>
    /// 计提投资顾问费
    /// </summary>
    [JsonPropertyName("jttzgwf")]
    public string AccruedInvestmentAdvisoryFee { get; set; }

    /// <summary>
    /// 支付投资顾问费
    /// </summary>
    [JsonPropertyName("zftzgwf")]
    public string PaidInvestmentAdvisoryFee { get; set; }

    /// <summary>
    /// 未支付投资顾问费
    /// </summary>
    [JsonPropertyName("wzftzgwf")]
    public string UnpaidInvestmentAdvisoryFee { get; set; }
    #endregion

    #region 增值税附加税相关
    /// <summary>
    /// 计提增值税附加税
    /// </summary>
    [JsonPropertyName("jtzzsfjs")]
    public string AccruedVatSurcharge { get; set; }

    /// <summary>
    /// 支付增值税附加税
    /// </summary>
    [JsonPropertyName("zfzzsfjs")]
    public string PaidVatSurcharge { get; set; }

    /// <summary>
    /// 未支付增值税附加税
    /// </summary>
    [JsonPropertyName("wzfzzsfjs")]
    public string UnpaidVatSurcharge { get; set; }
    #endregion

    #region 审计费相关
    /// <summary>
    /// 计提审计费
    /// </summary>
    [JsonPropertyName("jtsjf")]
    public string AccruedAuditFee { get; set; }

    /// <summary>
    /// 支付审计费
    /// </summary>
    [JsonPropertyName("zfsjf")]
    public string PaidAuditFee { get; set; }

    /// <summary>
    /// 未支付审计费
    /// </summary>
    [JsonPropertyName("wzfsjf")]
    public string UnpaidAuditFee { get; set; }
    #endregion


    public FundDailyFee ToObject()
    {
        var code = ClassificationCode switch { "M" => ProductCode, null => ProductCode, var n => n };



        return new FundDailyFee
        {
            FundCode = code,
            Date = DateOnly.ParseExact(BusinessDate, "yyyy-MM-dd"),
            ManagerFeeAccrued = ParseDecimal(AccruedManagementFee),
            ManagerFeePaid = ParseDecimal(PaidManagementFee),
            ManagerFeeBalance = ParseDecimal(UnpaidManagementFee),
            PerformanceFeeAccrued = ParseDecimal(AccruedPerformanceFee),
            PerformanceFeePaid = ParseDecimal(PaidPerformanceFee),
            PerformanceFeeBalance = ParseDecimal(UnpaidPerformanceFee),
            CustodianFeeAccrued = ParseDecimal(AccruedCustodyFee),
            ConsultantFeePaid = ParseDecimal(PaidCustodyFee),
            ConsultantFeeBalance = ParseDecimal(UnpaidCustodyFee),
            OutsourcingFeeAccrued = ParseDecimal(AccruedAdministrativeFee),
            OutsourcingFeePaid = ParseDecimal(PaidAdministrativeFee),
            OutsourcingFeeBalance = ParseDecimal(UnpaidAdministrativeFee),
            SalesFeeAccrued = ParseDecimal(AccruedSalesServiceFee),
            SalesFeePaid = ParseDecimal(PaidSalesServiceFee),
            SalesFeeBalance = ParseDecimal(UnpaidSalesServiceFee),
            ConsultantFeeAccrued = ParseDecimal(AccruedInvestmentAdvisoryFee),
            CustodianFeePaid = ParseDecimal(PaidInvestmentAdvisoryFee),
            CustodianFeeBalance = ParseDecimal(UnpaidInvestmentAdvisoryFee),
        };
    }

}

#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。