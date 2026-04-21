using FMO.Models;
using System.Text.Json.Serialization;

namespace FMO.Trustee;


#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
internal class NetValueJson : JsonBase
{

    /// <summary>
    /// 产品代码，最大长度 6
    /// </summary>
    [JsonPropertyName("fundCode")]
    public string FundCode { get; set; } // String(6)

    /// <summary>
    /// 产品名称，最大长度 300
    /// </summary>
    [JsonPropertyName("fundName")]
    public string FundName { get; set; } // String(300)

    /// <summary>
    /// 净值日期，格式：yyyymmdd，最大长度 8
    /// </summary>
    [JsonPropertyName("navDate")]
    public string NavDate { get; set; } // String(8)，格式：yyyyMMdd

    /// <summary>
    /// 资产份额，保留两位小数
    /// </summary>
    [JsonPropertyName("assetVol")]
    public string AssetVol { get; set; } // 保留两位小数

    /// <summary>
    /// 资产净值，保留两位小数
    /// </summary>
    [JsonPropertyName("assetNav")]
    public string AssetNav { get; set; } // 保留两位小数

    /// <summary>
    /// 资产总值，保留两位小数
    /// </summary>
    [JsonPropertyName("totalAsset")]
    public string TotalAsset { get; set; } // 保留两位小数

    /// <summary>
    /// 单位净值，保留四位小数（清盘日保留8位）
    /// 注意：实际处理时需根据是否清盘动态控制小数位数
    /// </summary>
    [JsonPropertyName("nav")]
    public string Nav { get; set; } // 通常保留四位，清盘时8位

    /// <summary>
    /// 累计单位净值，保留四位小数（清盘日保留8位）
    /// 注意：实际处理时需根据是否清盘动态控制小数位数
    /// </summary>
    [JsonPropertyName("accumulativeNav")]
    public string AccumulativeNav { get; set; } // 通常保留四位，清盘时8位

    /// <summary>
    /// 预留字段1，最大长度 500
    /// </summary>
    [JsonPropertyName("remark1")]
    public string Remark1 { get; set; } // String(500)

    /// <summary>
    /// 预留字段2，最大长度 500
    /// </summary>
    [JsonPropertyName("remark2")]
    public string Remark2 { get; set; } // String(500)


    
}

#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。