using System.Text.Json.Serialization;

namespace FMO.Trustee;

#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。

internal class VirtualNetValueJson : JsonBase
{
    /// <summary>
    /// 业务日期，格式：YYYY-MM-DD
    /// </summary>
    [JsonPropertyName("date")]
    public string BusinessDate { get; set; }

    /// <summary>
    /// 客户名称
    /// </summary>
    [JsonPropertyName("custName")]
    public string InvestorName { get; set; }

    /// <summary>
    /// 基金账号
    /// </summary>
    [JsonPropertyName("fundAcco")]
    public string FundAccount { get; set; }

    /// <summary>
    /// 产品代码
    /// </summary>
    [JsonPropertyName("fundCode")]
    public string FundCode { get; set; }

    /// <summary>
    /// 产品名称
    /// </summary>
    [JsonPropertyName("fundName")]
    public string FundName { get; set; }

    /// <summary>
    /// 持仓份额
    /// </summary>
    [JsonPropertyName("shares")]
    public decimal Shares { get; set; }

    /// <summary>
    /// 计提方式：1-估值计提；2-TA计提
    /// </summary>
    [JsonPropertyName("flag")]
    public string AccrualMethod { get; set; }

    /// <summary>
    /// 虚拟业绩报酬金额
    /// </summary>
    [JsonPropertyName("virtualBalance")]
    public decimal VirtualPerformanceFee { get; set; }

    /// <summary>
    /// 虚拟净值
    /// </summary>
    [JsonPropertyName("virtualAssetVal")]
    public string VirtualNetAssetValue { get; set; }

    /// <summary>
    /// 实际净值
    /// </summary>
    [JsonPropertyName("netAssetVal")]
    public string ActualNetAssetValue { get; set; }

    /// <summary>
    /// 累计净值
    /// </summary>
    [JsonPropertyName("totalAssetVal")]
    public string TotalNetAssetValue { get; set; }

    /// <summary>
    /// 虚拟扣减份额
    /// </summary>
    [JsonPropertyName("virtualDeductionShare")]
    public decimal VirtualDeductionShare { get; set; }

    /// <summary>
    /// 净值核对状态：
    /// 0-一致，托管复核一致；
    /// 1-不一致，未经托管确认；
    /// 2-处理中；
    /// 3-无托管方产品，无托管方复核
    /// </summary>
    [JsonPropertyName("checkStatus")]
    public string NetValueCheckStatus { get; set; }
}

#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。