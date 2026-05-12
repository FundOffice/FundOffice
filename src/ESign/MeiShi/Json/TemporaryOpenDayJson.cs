using FMO.Utilities;
using System.Text.Json.Serialization;
using static FMO.ESigning.MeiShi.TemporaryOpenDayWithRedemJson;


//[assembly: InternalsVisibleTo("TestMeiShi")]


namespace FMO.ESigning.MeiShi;




internal class TemporaryOpenDayJson
{
    /// <summary>
    /// 开放类型
    /// </summary>
    [JsonPropertyName("openType")]
    public string OpenType { get; set; } = "0";

    /// <summary>
    /// 启动状态
    /// </summary>
    [JsonPropertyName("startStatus")]
    public int StartStatus { get; set; } = 1;

    /// <summary>
    /// 产品ID
    /// </summary>
    [JsonPropertyName("productId")]
    public required long ProductId { get; set; }

    /// <summary>
    /// 开放范围
    /// </summary>
    [JsonPropertyName("openScope")]
    public int OpenScope { get; set; } = 1;

    /// <summary>
    /// 交易类型列表 = [1, 2];
    /// </summary>
    [JsonPropertyName("tradeTypes")]
    public required int[] TradeTypes { get; set; }

    /// <summary>
    /// 生效配置
    /// </summary>
    [JsonPropertyName("effectiveConfiguration")]
    public int EffectiveConfiguration { get; set; } = 1;

    /// <summary>
    /// 通知规则 提前天数
    /// </summary>
    [JsonPropertyName("noticeRule")]
    public int NoticeRule { get; set; } = 0;

    /// <summary>
    /// 通知方式列表
    /// </summary>
    [JsonPropertyName("notificationList")]
    public int[] NotificationList { get; set; } = [1, 2];

    /// <summary>
    /// 预约规则
    /// </summary>
    [JsonPropertyName("bookingRule")]
    public int BookingRule { get; set; } = -1;

    /// <summary>
    /// 预约结束规则
    /// </summary>
    [JsonPropertyName("bookingEndRule")]
    public int BookingEndRule { get; set; } = 0;


    /// <summary>
    /// 产品名称
    /// </summary>
    [JsonPropertyName("productName")]
    public string ProductName { get; set; } = "$([char]37995)$([char]20139)$([char]19990)$([char]23480)5$([char]21495)";

    /// <summary>
    /// 签约开始规则
    /// </summary>
    [JsonPropertyName("signingStartRules")]
    public int SigningStartRules { get; set; } = -1;

    /// <summary>
    /// 签约结束规则
    /// </summary>
    [JsonPropertyName("signingEndRules")]
    public int SigningEndRules { get; set; } = 0;


    /// <summary>
    /// 时间范围配置
    /// </summary>
    [JsonPropertyName("timeRanges")]
    public List<TimeRange>? TimeRanges { get; set; }

    /// <summary>
    /// 签约结束时间点
    /// </summary>
    [JsonPropertyName("signingEndRulesTime")]
    public string SigningEndRulesTime { get; set; } = "23:59";


    public class TimeRange
    {
        public TimeRange(DateOnly date)
        {
            StartTime = new DateTime(date, default).ToUniversalTime().TimeStampByMilliseconds();
            EndTime = StartTime + 86399000;
        }

        /// <summary>
        /// 开始时间戳（毫秒）
        /// </summary>
        [JsonPropertyName("startTime")]
        public long StartTime { get; set; }

        /// <summary>
        /// 结束时间戳（毫秒）
        /// </summary>
        [JsonPropertyName("endTime")]
        public long EndTime { get; set; }
    }
}


internal class TemporaryOpenDayWithRedemJson : TemporaryOpenDayJson
{
    

    /// <summary>
    /// 签约赎回开始规则
    /// </summary>
    [JsonPropertyName("signingAndRedemptionStartRules")]
    public int SigningAndRedemptionStartRules { get; set; } = -1;

    /// <summary>
    /// 签约赎回结束规则
    /// </summary>
    [JsonPropertyName("signingAndRedemptionEndRules")]
    public int SigningAndRedemptionEndRules { get; set; } = 0;

    /// <summary>
    /// 签约赎回结束时间UTC
    /// </summary>
    [JsonPropertyName("signingAndRedemptionEndRulesTime")]
    public required string SigningAndRedemptionEndRulesTime { get; set; }

    

    /// <summary>
    /// 赎回开始规则
    /// </summary>
    [JsonPropertyName("redemptionStartRules")]
    public int RedemptionStartRules { get; set; } = -1;

    /// <summary>
    /// 赎回结束规则
    /// </summary>
    [JsonPropertyName("redemptionEndRules")]
    public int RedemptionEndRules { get; set; } = 0;

    /// <summary>
    /// 赎回结束时间点
    /// </summary>
    [JsonPropertyName("redemptionEndRulesTime")]
    public string RedemptionEndRulesTime { get; set; } = "23:59";

   
}
