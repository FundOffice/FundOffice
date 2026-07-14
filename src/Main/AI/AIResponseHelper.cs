using FMO.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FMO.AI;

/// <summary>
/// AI 响应处理工具类
/// 提供 JSON 提取和 FundInfo 转换功能
/// </summary>
public static class AIResponseHelper
{
    /// <summary>
    /// 从 AI 返回文本中提取 JSON 部分
    /// </summary>
    public static string ExtractJson(string response)
    {
        // 去除 ```json ... ``` 包裹
        var match = Regex.Match(response, @"```(?:json)?\s*([\s\S]*?)```");
        if (match.Success) return match.Groups[1].Value.Trim();

        // 尝试直接找 { ... }
        var start = response.IndexOf('{');
        var end = response.LastIndexOf('}');
        if (start >= 0 && end > start)
            return response.Substring(start, end - start + 1);

        return response;
    }

    /// <summary>
    /// 将缓存的原始 AI JSON 转换为 ReadonlyFundInfo
    /// </summary>
    public static ReadonlyFundInfo? ToFundInfo(string fundInfoJson)
    {
        if (string.IsNullOrWhiteSpace(fundInfoJson)) return null;

        try
        {
            // 反序列化为 AiParsedFundInfo
            var dto = JsonSerializer.Deserialize<AiParsedFundInfo>(fundInfoJson, FundDocxAIParser.JsonOptions);
            if (dto is null) return null;

            // 转换为 FundFactor[]
            var factors = AiParsedFundInfoConverter.ToFactors(dto);

            // 填充 ReadonlyFundInfo
            var info = new ReadonlyFundInfo();
            info.FillBy(factors);
            return info;
        }
        catch
        {
            return null;
        }
    }
}