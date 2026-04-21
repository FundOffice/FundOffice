using System.Text.Json.Serialization;

namespace FMO.Trustee;

#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。

internal class PerformanceJson : JsonBase
{
    [JsonPropertyName("fundAcco")]
    public string FundAccount { get; set; } // 基金账号

    [JsonPropertyName("custName")]
    public string InvestorName { get; set; } // 客户名称

    [JsonPropertyName("custType")]
    public string CustomerType { get; set; } // 客户类型

    [JsonPropertyName("agencyNo")]
    public string AgencyCode { get; set; } // 销售商代码

    [JsonPropertyName("agencyName")]
    public string AgencyName { get; set; } // 销售商名称

    [JsonPropertyName("tradeAcco")]
    public string TradeAccount { get; set; } // 交易账号

    [JsonPropertyName("businFlag")]
    public string BusinessType { get; set; } // 业务类型（参考映射表）

    [JsonPropertyName("sortFlag")]
    public string SortFlag { get; set; } // 处理类型: 0-保底处理 1-业绩提成

    [JsonPropertyName("requestDate")]
    public string RequestDate { get; set; } // 申请日期

    [JsonPropertyName("confirmDate")]
    public string ConfirmDate { get; set; } // 确认日期

    [JsonPropertyName("fundCode")]
    public string FundCode { get; set; } // 基金代码

    [JsonPropertyName("shareTypeCn")]
    public string ShareTypeName { get; set; } // 份额类别

    [JsonPropertyName("cserialNo")]
    public string TaConfirmNo { get; set; } // TA确认号

    [JsonPropertyName("registDate")]
    public string RegisterDate { get; set; } // 份额注册日期

    [JsonPropertyName("shares")]
    public string Shares { get; set; } // 发生份额

    [JsonPropertyName("beginDate")]
    public string BeginDate { get; set; } // 期初日期

    [JsonPropertyName("oriNav")]
    public string OriNav { get; set; } // 期初单位净值

    [JsonPropertyName("oriTotalNav")]
    public string OriTotalNav { get; set; } // 期初累计净值

    [JsonPropertyName("nav")]
    public string Nav { get; set; } // 期末单位净值

    [JsonPropertyName("totalNav")]
    public string TotalNav { get; set; } // 期末累计净值

    [JsonPropertyName("currRatio")]
    public string CurrentRatio { get; set; } // 当前收益率

    [JsonPropertyName("yearRatio")]
    public string YearRatio { get; set; } // 年化收益率

    [JsonPropertyName("oriBalance")]
    public string OriBalance { get; set; } // 应提成/保底金额

    [JsonPropertyName("factBalance")]
    public string FactBalance { get; set; } // 实际提成/保底金额

    [JsonPropertyName("factShares")]
    public string FactShares { get; set; } // 实际提成/保底份额

    [JsonPropertyName("bonusBalance")]
    public string BonusBalance { get; set; } // 分红总金额

    [JsonPropertyName("oriCserialNo")]
    public string OriginalTaConfirmNo { get; set; } // 原确认单号

    [JsonPropertyName("hold")]
    public string HoldDays { get; set; } // 持有天数

    [JsonPropertyName("indexYearRatio")]
    public string IndexYearRatio { get; set; } // 证券指数年化收益率

    [JsonPropertyName("beginIndexPrice")]
    public string BeginIndexPrice { get; set; } // 期初指数价格

    [JsonPropertyName("endIndexPrice")]
    public string EndIndexPrice { get; set; } // 期末指数价格

    [JsonPropertyName("calcFlag")]
    public string CalcFlag { get; set; } // 试算标识：0-计提，1-试算
}

#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。