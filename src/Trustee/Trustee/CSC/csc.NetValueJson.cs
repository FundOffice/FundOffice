using System.Text.Json.Serialization;

namespace FMO.Trustee;


#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
internal class NetValueJson : JsonBase
{
    /// <summary>
    /// 净值日期，格式：yyyyMMdd，长度 8
    /// </summary>
    [JsonPropertyName("netDate")]
    public string NetDate { get; set; } // String(8)

    /// <summary>
    /// 基金代码，长度 32
    /// </summary>
    [JsonPropertyName("fundCode")]
    public string FundCode { get; set; } // String(32)

    /// <summary>
    /// 基金名称，长度 250
    /// </summary>
    [JsonPropertyName("fundName")]
    public string FundName { get; set; } // String(250)

    /// <summary>
    /// 资产份额，长度 20（字符串）
    /// </summary>
    [JsonPropertyName("assetShares")]
    public string AssetShares { get; set; } // String(20)

    /// <summary>
    /// 资产净值，长度 20（字符串）
    /// </summary>
    [JsonPropertyName("assetNet")]
    public string AssetNet { get; set; } // String(20)

    /// <summary>
    /// 资产总额，长度 20；分级基金子基金时指母基金资产总额
    /// </summary>
    [JsonPropertyName("assetTotal")]
    public string AssetTotal { get; set; } // String(20)

    /// <summary>
    /// 单位净值，长度 10（字符串）
    /// </summary>
    [JsonPropertyName("netAssetVal")]
    public string NetAssetVal { get; set; } // String(10)

    /// <summary>
    /// 累计单位净值，长度 10（字符串）
    /// </summary>
    [JsonPropertyName("totalAssetVal")]
    public string TotalAssetVal { get; set; } // String(10)

    /// <summary>
    /// 每万份基金收益，仅货币基金有效，长度 10（字符串）
    /// </summary>
    [JsonPropertyName("ettFundIncome")]
    public string EttFundIncome { get; set; } // String(10)

    /// <summary>
    /// 七日年化收益率（%），仅货币基金有效，长度 10（字符串）
    /// </summary>
    [JsonPropertyName("sevenDayRate")]
    public string SevenDayRate { get; set; } // String(10)

    /// <summary>
    /// 基金级别：0=母，1=A，2=B，3=C，... 长度 2
    /// </summary>
    [JsonPropertyName("fundLevel")]
    public string FundLevel { get; set; } // String(2)
}


#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
