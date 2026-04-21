using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FMO.Trustee;


#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
internal class NetValueJson : JsonBase {

    /// <summary>
    /// 净值日期，格式：yyyyMMdd（如 20250405）
    /// </summary>
    [JsonPropertyName("netDate")]
    public string NetDate { get; set; }

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

    /// <summary>
    /// 资产份额（字符串格式）
    /// </summary>
    [JsonPropertyName("assetShares")]
    public string AssetShares { get; set; }

    /// <summary>
    /// 资产净值（字符串格式）
    /// </summary>
    [JsonPropertyName("assetNet")]
    public string AssetNet { get; set; }

    /// <summary>
    /// 资产总值（字符串格式）
    /// </summary>
    [JsonPropertyName("assetValue")]
    public string AssetValue { get; set; }

    /// <summary>
    /// 单位净值（字符串格式）
    /// </summary>
    [JsonPropertyName("netAssetVal")]
    public string NetAssetVal { get; set; }

    /// <summary>
    /// 累计单位净值（字符串格式）
    /// </summary>
    [JsonPropertyName("totalAssetVal")]
    public string TotalAssetVal { get; set; }

    /// <summary>
    /// 核对状态
    /// 0：一致，托管复核一致
    /// 1：不一致，未经托管确认
    /// 3：无托管方产品，无托管方复核
    /// </summary>
    [JsonPropertyName("checkStatus")]
    public string CheckStatus { get; set; }

    /// <summary>
    /// 基金标识
    /// 1 = 分级母
    /// 2 = 分级子
    /// 3 = 非分级
    /// 4 = 分袋母
    /// 5 = 分袋子
    /// </summary>
    [JsonPropertyName("flag")]
    public string Flag { get; set; }

    /// <summary>
    /// 最后更新时间，格式：YYYY-MM-DD HH:MM:SS
    /// </summary>
    [JsonPropertyName("lastUpdateTime")]
    public string LastUpdateTime { get; set; }

    /// <summary>
    /// 追账返账标识
    /// 0 = 未发生过追账
    /// 1 = 发生过追账
    /// </summary>
    [JsonPropertyName("recoverAccountFlag")]
    public string RecoverAccountFlag { get; set; }
}


#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。