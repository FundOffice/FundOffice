using FMO.Models;
using System.Text.Json.Serialization;

namespace FMO.Trustee.JsonCMS;


#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。

/// <summary>
/// 基金费用信息实体类
/// </summary>
internal class FundDailyFeeJson : JsonBase
{
    /// <summary>
    /// 产品名称（必填，最大长度300）
    /// </summary>
    [JsonPropertyName("fundName")]
    public string FundName { get; set; }

    /// <summary>
    /// 产品代码（必填，最大长度6）
    /// </summary>
    [JsonPropertyName("fundCode")]
    public string FundCode { get; set; }

    /// <summary>
    /// 费用日期（格式：yyyyMMdd，必填）
    /// </summary>
    [JsonPropertyName("busiDate")]
    public string BusiDate { get; set; }

    // 托管费
    [JsonPropertyName("custodianFeeJt")]
    public string CustodianFeeJt { get; set; } // 计提

    [JsonPropertyName("custodianFeeZf")]
    public string CustodianFeeZf { get; set; } // 支付

    [JsonPropertyName("custodianFeeYe")]
    public string CustodianFeeYe { get; set; } // 余额

    // 运营服务费
    [JsonPropertyName("operationServiceFeeJt")]
    public string OperationServiceFeeJt { get; set; }

    [JsonPropertyName("operationServiceFeeZf")]
    public string OperationServiceFeeZf { get; set; }

    [JsonPropertyName("operationServiceFeeYe")]
    public string OperationServiceFeeYe { get; set; }

    // 管理费
    [JsonPropertyName("managementFeeJt")]
    public string ManagementFeeJt { get; set; }

    [JsonPropertyName("managementFeeZf")]
    public string ManagementFeeZf { get; set; }

    [JsonPropertyName("managementFeeYe")]
    public string ManagementFeeYe { get; set; }

    // 业绩报酬费
    [JsonPropertyName("performanceFeeJt")]
    public string PerformanceFeeJt { get; set; }

    [JsonPropertyName("performanceFeeZf")]
    public string PerformanceFeeZf { get; set; }

    [JsonPropertyName("performanceFeeYe")]
    public string PerformanceFeeYe { get; set; }

    // 销售服务费
    [JsonPropertyName("salesandServiceFeesJt")]
    public string SalesAndServiceFeesJt { get; set; }

    [JsonPropertyName("salesandServiceFeesZf")]
    public string SalesAndServiceFeesZf { get; set; }

    [JsonPropertyName("salesandServiceFeesYe")]
    public string SalesAndServiceFeesYe { get; set; }

    // 投资顾问费
    [JsonPropertyName("investmentConsultantFeeJt")]
    public string InvestmentConsultantFeeJt { get; set; }

    [JsonPropertyName("investmentConsultantFeeZf")]
    public string InvestmentConsultantFeeZf { get; set; }

    [JsonPropertyName("investmentConsultantFeeYe")]
    public string InvestmentConsultantFeeYe { get; set; }

    // 客户服务费
    [JsonPropertyName("customerServiceFeeJt")]
    public string CustomerServiceFeeJt { get; set; }

    [JsonPropertyName("customerServiceFeeZf")]
    public string CustomerServiceFeeZf { get; set; }

    [JsonPropertyName("customerServiceFeeYe")]
    public string CustomerServiceFeeYe { get; set; }

    public FundDailyFee ToObject()
    {
        return new FundDailyFee
        {
            FundCode = FundCode,
            Date = DateOnly.ParseExact(BusiDate, "yyyyMMdd"),
            // 管理费
            ManagerFeeAccrued = ParseDecimal(ManagementFeeJt),
            ManagerFeePaid = ParseDecimal(ManagementFeeZf),
            ManagerFeeBalance = ParseDecimal(ManagementFeeYe),

            // 托管费
            CustodianFeeAccrued = ParseDecimal(CustodianFeeJt),
            CustodianFeePaid = ParseDecimal(CustodianFeeZf),
            CustodianFeeBalance = ParseDecimal(CustodianFeeYe),

            // 外包运营服务费（OperationServiceFee）
            OutsourcingFeeAccrued = ParseDecimal(OperationServiceFeeJt),
            OutsourcingFeePaid = ParseDecimal(OperationServiceFeeZf),
            OutsourcingFeeBalance = ParseDecimal(OperationServiceFeeYe),

            // 业绩报酬费
            PerformanceFeeAccrued = ParseDecimal(PerformanceFeeJt),
            PerformanceFeePaid = ParseDecimal(PerformanceFeeZf),
            PerformanceFeeBalance = ParseDecimal(PerformanceFeeYe),

            // 销售服务费
            SalesFeeAccrued = ParseDecimal(SalesAndServiceFeesJt),
            SalesFeePaid = ParseDecimal(SalesAndServiceFeesZf),
            SalesFeeBalance = ParseDecimal(SalesAndServiceFeesYe),

            // 投资顾问费
            ConsultantFeeAccrued = ParseDecimal(InvestmentConsultantFeeJt),
            ConsultantFeePaid = ParseDecimal(InvestmentConsultantFeeZf),
            ConsultantFeeBalance = ParseDecimal(InvestmentConsultantFeeYe)

        };
    }

}




#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。