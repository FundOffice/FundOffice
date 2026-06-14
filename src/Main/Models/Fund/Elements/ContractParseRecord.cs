using System.Text.Json;
using System.Text.Json.Serialization;

namespace FMO.Models;

/// <summary>
/// 合同 AI 解析结果缓存记录
/// </summary>
public class ContractParseRecord
{
    /// <summary>文件 MD5 hash（来自 FileMeta.Hash），作为主键</summary>
    public string Id { get; set; } = "";
    /// <summary>解析时间</summary>
    public DateTime ParsedAt { get; set; }
    /// <summary>ReadonlyFundInfo 序列化 JSON</summary>
    public string FundInfoJson { get; set; } = "";

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// 将缓存的 JSON 反序列化为 ReadonlyFundInfo
    /// </summary>
    public ReadonlyFundInfo? ToFundInfo() =>
        JsonSerializer.Deserialize<ReadonlyFundInfo>(FundInfoJson, _jsonOptions);
}
