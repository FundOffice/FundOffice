using System.Text.Json.Serialization;


namespace FMO.ESigning.MeiShi;


internal class PredefinedJson
{
    /// <summary>
    /// 产品ID
    /// </summary>
    [JsonPropertyName("productId")]
    public long ProductId { get; set; }

    /// <summary>
    /// 通知模板类型
    /// </summary>
    [JsonPropertyName("noticeTemplateType")]
    public int NoticeTemplateType { get; set; }

    /// <summary>
    /// 是否管理员盖章
    /// </summary>
    [JsonPropertyName("isAdministratorSeal")]
    public int IsAdministratorSeal { get; set; }

    /// <summary>
    /// 文档类型
    /// </summary>
    [JsonPropertyName("documentType")]
    public int DocumentType { get; set; }

    /// <summary>
    /// 发布时间
    /// </summary>
    [JsonPropertyName("publishTime")]
    public DateOnly? PublishTime { get; set; }

    /// <summary>
    /// 通知状态
    /// </summary>
    [JsonPropertyName("noticeStatus")]
    public int NoticeStatus { get; set; }

    [JsonPropertyName("notificationWay")]
    public int[]? NotificationWay { get; set; }
 

    /// <summary>
    /// 文件权限
    /// </summary>
    [JsonPropertyName("fileAuthority")]
    public string? FileAuthority { get; set; }
     

    /// <summary>
    /// 披露条件
    /// </summary>
    [JsonPropertyName("disclosureConditions")]
    public int DisclosureConditions { get; set; }

}

internal class TemporaryOpenJson : PredefinedJson
{
     
     

    /// <summary>
    /// 交易类型列表
    /// </summary>
    [JsonPropertyName("tradeTypeList")]
    public List<int>? TradeTypeList { get; set; }

    /// <summary>
    /// 成立时间
    /// </summary>
    [JsonPropertyName("establishedTime")]
    public DateOnly? EstablishedTime { get; set; }

    /// <summary>
    /// 产品名称
    /// </summary>
    [JsonPropertyName("productName")]
    public string? ProductName { get; set; }

    /// <summary>
    /// 开放日时间
    /// </summary>
    [JsonPropertyName("openDayHours")]
    public string? OpenDayHours { get; set; }
}



internal class HugeRedemptionJson : PredefinedJson
{

    /// <summary>
    /// 巨额赎回比例
    /// </summary>
    [JsonPropertyName("hugeRedeemRatio")]
    public decimal? HugeRedeemRatio { get; set; }

    /// <summary>
    /// 开放日时间
    /// </summary>
    [JsonPropertyName("openDayHours")]
    public string? OpenDayHours { get; set; }

    /// <summary>
    /// 始终份额
    /// </summary>
    [JsonPropertyName("alwaysShare")]
    public decimal? AlwaysShare { get; set; }

    /// <summary>
    /// 份额处理方式
    /// </summary>
    [JsonPropertyName("shareHandlingMethod")]
    public string? ShareHandlingMethod { get; set; }

    /// <summary>
    /// 基金备案编码
    /// </summary>
    [JsonPropertyName("fundFilingCode")]
    public string? FundFilingCode { get; set; }

    /// <summary>
    /// 产品名称
    /// </summary>
    [JsonPropertyName("productName")]
    public string? ProductName { get; set; }
}


internal class FundSetupJson : PredefinedJson
{

    /// <summary>
    /// 成立时间
    /// </summary>
    [JsonPropertyName("establishedTime")]
    public DateOnly? EstablishedTime { get; set; }

    /// <summary>
    /// 产品名称
    /// </summary>
    [JsonPropertyName("productName")]
    public string? ProductName { get; set; }


    /// <summary>
    /// 开放日时间
    /// </summary>
    [JsonPropertyName("openDayHours")]
    public string? OpenDayHours { get; set; }
}

internal class FundScaleWarningJson : PredefinedJson
{
    /// <summary>
    /// 产品名称
    /// </summary>
    [JsonPropertyName("productName")]
    public string? ProductName { get; set; }


    /// <summary>
    /// 开放日时间
    /// </summary>
    [JsonPropertyName("openDayHours")]
    public string? OpenDayHours { get; set; }
}