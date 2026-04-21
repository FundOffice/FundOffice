using System.Text.Json.Serialization;

namespace FMO.ESigning.MeiShi;

internal class FundInfoJson
{
    /// <summary>
    /// 产品ID
    /// </summary>
    [JsonPropertyName("productId")]
    public long ProductId { get; set; }

    /// <summary>
    /// 产品名称
    /// </summary>
    [JsonPropertyName("productName")]
    public string ProductName { get; set; } = string.Empty;

    /// <summary>
    /// 创建日期/成立日期
    /// </summary>
    [JsonPropertyName("setDate")]
    public DateTime? SetDate { get; set; }

    /// <summary>
    /// 基金备案编号
    /// </summary>
    [JsonPropertyName("fundRecordNumber")]
    public string? FundRecordNumber { get; set; }

    /// <summary>
    /// 份额备案编号
    /// </summary>
    [JsonPropertyName("shareRecordNumber")]
    public string? ShareRecordNumber { get; set; }

    /// <summary>
    /// 内部产品代码
    /// </summary>
    [JsonPropertyName("pbInternalProductCode")]
    public string? PbInternalProductCode { get; set; }

    /// <summary>
    /// 风险类型
    /// </summary>
    [JsonPropertyName("riskType")]
    public int RiskType { get; set; }

    /// <summary>
    /// 发布状态
    /// </summary>
    [JsonPropertyName("publishStatus")]
    public int PublishStatus { get; set; }

    /// <summary>
    /// 预约状态
    /// </summary>
    [JsonPropertyName("appointmentStatus")]
    public int AppointmentStatus { get; set; }

    /// <summary>
    /// 交易类型（字符串格式的数组）
    /// </summary>
    [JsonPropertyName("tradeTypes")]
    public string? TradeTypes { get; set; }

    /// <summary>
    /// 货币类型
    /// </summary>
    [JsonPropertyName("currency")]
    public string? Currency { get; set; }

    /// <summary>
    /// 父产品ID
    /// </summary>
    [JsonPropertyName("parentProductId")]
    public long? ParentProductId { get; set; }

    /// <summary>
    /// 基金等级
    /// </summary>
    [JsonPropertyName("fundLevel")]
    public int FundLevel { get; set; }

    /// <summary>
    /// 产品全称
    /// </summary>
    [JsonPropertyName("productFullName")]
    public string? ProductFullName { get; set; }

    /// <summary>
    /// 募集名称
    /// </summary>
    [JsonPropertyName("raiseName")]
    public string? RaiseName { get; set; }

    /// <summary>
    /// 募集账户
    /// </summary>
    [JsonPropertyName("raiseAccount")]
    public string? RaiseAccount { get; set; }

    /// <summary>
    /// 银行名称
    /// </summary>
    [JsonPropertyName("bankName")]
    public string? BankName { get; set; }

    /// <summary>
    /// 募集银行支行
    /// </summary>
    [JsonPropertyName("raiseBankName")]
    public string? RaiseBankName { get; set; }

    /// <summary>
    /// 付款账户
    /// </summary>
    [JsonPropertyName("payAccount")]
    public string? PayAccount { get; set; }
}