using FMO.Models;
using System.Text.Json.Serialization;

namespace FMO.Trustee.JsonCSC;


#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。

public class FundDailyFeeJson : JsonBase
{

    /// <summary>
    /// 费用计算日期
    /// </summary>
    [JsonPropertyName("confDate")]
    public string ConfDate { get; set; }

    /// <summary>
    /// 基金代码
    /// </summary>
    [JsonPropertyName("fundCode")]
    public string FundCode { get; set; }

    /// <summary>
    /// 基金名称
    /// </summary>
    [JsonPropertyName("fundName")]
    public string FundName { get; set; }

    #region 费用计提
    /// <summary>
    /// 管理费
    /// </summary>
    [JsonPropertyName("managementFee")]
    public string ManagementFee { get; set; }

    /// <summary>
    /// 投顾费
    /// </summary>
    [JsonPropertyName("investmentFee")]
    public string InvestmentFee { get; set; }

    /// <summary>
    /// 销售服务费
    /// </summary>
    [JsonPropertyName("salesFee")]
    public string SalesFee { get; set; }

    /// <summary>
    /// 托管费
    /// </summary>
    [JsonPropertyName("custodianFee")]
    public string CustodianFee { get; set; }

    /// <summary>
    /// 外包服务费
    /// </summary>
    [JsonPropertyName("outsourcingFee")]
    public string OutsourcingFee { get; set; }

    /// <summary>
    /// 业绩报酬计提
    /// </summary>
    [JsonPropertyName("reward")]
    public string Reward { get; set; }

    /// <summary>
    /// 增值税计提
    /// </summary>
    [JsonPropertyName("addedTax")]
    public string AddedTax { get; set; }

    /// <summary>
    /// 附加税计提
    /// </summary>
    [JsonPropertyName("surTax")]
    public string SurTax { get; set; }

    /// <summary>
    /// 增值税及附加税计提
    /// </summary>
    [JsonPropertyName("addedSurTax")]
    public string AddedSurTax { get; set; }
    #endregion

    #region 费用支付
    /// <summary>
    /// 管理费支付
    /// </summary>
    [JsonPropertyName("managementFeePay")]
    public string ManagementFeePay { get; set; }

    /// <summary>
    /// 投顾费支付
    /// </summary>
    [JsonPropertyName("investmentFeePay")]
    public string InvestmentFeePay { get; set; }

    /// <summary>
    /// 销售服务费支付
    /// </summary>
    [JsonPropertyName("salesFeePay")]
    public string SalesFeePay { get; set; }

    /// <summary>
    /// 托管费支付
    /// </summary>
    [JsonPropertyName("custodianFeePay")]
    public string CustodianFeePay { get; set; }

    /// <summary>
    /// 外包服务费支付
    /// </summary>
    [JsonPropertyName("outsourcingFeePay")]
    public string OutsourcingFeePay { get; set; }

    /// <summary>
    /// 业绩报酬支付
    /// </summary>
    [JsonPropertyName("rewardPay")]
    public string RewardPay { get; set; }

    /// <summary>
    /// 增值税及附加税支付
    /// </summary>
    [JsonPropertyName("addedSurTaxPay")]
    public string AddedSurTaxPay { get; set; }
    #endregion

    #region 费用余额
    /// <summary>
    /// 管理费余额
    /// </summary>
    [JsonPropertyName("managementFeeBalance")]
    public string ManagementFeeBalance { get; set; }

    /// <summary>
    /// 投顾费余额
    /// </summary>
    [JsonPropertyName("investmentFeeBalance")]
    public string InvestmentFeeBalance { get; set; }

    /// <summary>
    /// 销售服务费余额
    /// </summary>
    [JsonPropertyName("salesFeeBalance")]
    public string SalesFeeBalance { get; set; }

    /// <summary>
    /// 托管费余额
    /// </summary>
    [JsonPropertyName("custodianFeeBalance")]
    public string CustodianFeeBalance { get; set; }

    /// <summary>
    /// 外包服务费余额
    /// </summary>
    [JsonPropertyName("outsourcingFeeBalance")]
    public string OutsourcingFeeBalance { get; set; }

    /// <summary>
    /// 业绩报酬余额
    /// </summary>
    [JsonPropertyName("rewardBalance")]
    public string RewardBalance { get; set; }

    /// <summary>
    /// 增值税及附加税余额
    /// </summary>
    [JsonPropertyName("addedSurTaxBalance")]
    public string AddedSurTaxBalance { get; set; }
    #endregion


    public FundDailyFee ToObject()
    {


        return new FundDailyFee
        {
            FundCode = FundCode,
            Date = DateOnly.ParseExact(ConfDate, "yyyyMMdd"),

            ManagerFeeAccrued = ParseDecimal(ManagementFee),
            ManagerFeePaid = ParseDecimal(ManagementFeePay),
            ManagerFeeBalance = ParseDecimal(ManagementFeeBalance),
            PerformanceFeeAccrued = ParseDecimal(Reward),
            PerformanceFeePaid = ParseDecimal(RewardPay),
            PerformanceFeeBalance = ParseDecimal(RewardBalance),
            CustodianFeeAccrued = ParseDecimal(CustodianFee),
            CustodianFeePaid = ParseDecimal(CustodianFeePay),
            CustodianFeeBalance = ParseDecimal(CustodianFeeBalance),
            ConsultantFeeAccrued = ParseDecimal(InvestmentFee),
            ConsultantFeePaid = ParseDecimal(InvestmentFeePay),
            ConsultantFeeBalance = ParseDecimal(InvestmentFeeBalance),
            SalesFeeAccrued = ParseDecimal(SalesFee),
            SalesFeePaid = ParseDecimal(SalesFeePay),
            SalesFeeBalance = ParseDecimal(SalesFeeBalance),
            OutsourcingFeeAccrued = ParseDecimal(OutsourcingFee),
            OutsourcingFeePaid = ParseDecimal(OutsourcingFeePay),
            OutsourcingFeeBalance = ParseDecimal(OutsourcingFeeBalance),
        };
    }

}


#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。