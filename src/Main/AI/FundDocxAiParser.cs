using FMO.Models;
using FundOffice.Copilot.Providers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FMO.AI;

/// <summary>
/// AI 解析结果，包含原始 DTO 和提取的 Factors
/// </summary>
public class AIParseResult
{
    public required string Json { get; set; }

    /// <summary>
    /// AI 返回的原始 DTO（含置信度）
    /// </summary>
    public required AiParsedFundInfo ParsedInfo { get; init; }

    /// <summary>
    /// 从 DTO 提取的 FundFactor 数组
    /// </summary>
    public required IFundFactor[] Factors { get; init; }

    /// <summary>
    /// 解析过程中的警告/错误信息（部分字段解析失败时记录在此）
    /// </summary>
    public List<string> Warnings { get; init; } = [];
}

/// <summary>
/// 基金文档 AI 解析器
/// 从 docx 文件中提取基金信息
/// </summary>
public class FundDocxAIParser
{
    private readonly AIChatAdapter _adapter;

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(), new ConfidenceWrapperConverterFactory() },
    };

    public FundDocxAIParser(AIChatAdapter adapter)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
    }

    /// <summary>
    /// 从 docx 文件解析基金信息
    /// </summary>
    /// <param name="docxPath">docx 文件路径</param>
    /// <param name="progress">token 计数进度报告（已接收 token 数）</param>
    /// <returns>解析结果，失败返回 null</returns>
    public async Task<AIParseResult?> ParseAsync(string docxPath, IProgress<int>? progress = null)
    {
        if (!File.Exists(docxPath))
            throw new FileNotFoundException("文件不存在", docxPath);

        try
        {
            // 调用 AI 接口（两层降级：base64 → 文本提取）
            var response = await _adapter.AskWithFileAsync(docxPath, progress: progress);
            return ProcessResponse(response);
        }
        catch (TokenProviderException ex)
        {
            SaveToTemp("", ex.Message);
            return new AIParseResult
            {
                Json = "",
                ParsedInfo = new AiParsedFundInfo(),
                Factors = [],
                Warnings = [$"AI 调用失败: {ex.Kind} - {ex.Message}"]
            };
        }
    }

    /// <summary>
    /// 从已提取的文本解析基金信息（跳过文件上传）
    /// </summary>
    /// <param name="textContent">已提取的文档文本</param>
    /// <param name="progress">token 计数进度报告</param>
    /// <returns>解析结果，失败返回 null</returns>
    public async Task<AIParseResult?> ParseFromTextAsync(string textContent, IProgress<int>? progress = null)
    {
        try
        {
            var response = await _adapter.AskFromTextAsync(textContent, progress);
            return ProcessResponse(response);
        }
        catch (TokenProviderException ex)
        {
            SaveToTemp("", ex.Message);
            return new AIParseResult
            {
                Json = "",
                ParsedInfo = new AiParsedFundInfo(),
                Factors = [],
                Warnings = [$"AI 调用失败: {ex.Kind} - {ex.Message}"]
            };
        }
    }

    /// <summary>
    /// 统一处理 AI 响应，尽可能保留有效数据，错误记录到 Warnings
    /// </summary>
    private static AIParseResult? ProcessResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            SaveToTemp("", response);
            return new AIParseResult
            {
                Json = "",
                ParsedInfo = new AiParsedFundInfo(),
                Factors = [],
                Warnings = ["AI 返回为空"]
            };
        }

        return ParseResponse(response);
    }

    private static AIParseResult ParseResponse(string response)
    {
        var warnings = new List<string>();

        // 从 AI 返回中提取 JSON
        var json = AIResponseHelper.ExtractJson(response);

        // 尝试整体反序列化
        AiParsedFundInfo? dto;
        try
        {
            dto = JsonSerializer.Deserialize<AiParsedFundInfo>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            // 整体反序列化失败，保存到 temp 并尝试逐字段解析
            SaveToTemp(json, response);
            warnings.Add($"JSON 整体解析失败: {ex.Message}");
            dto = ParsePerProperty(json, warnings);
        }

        // 反序列化为内部 DTO
        var dto = JsonSerializer.Deserialize<AiParsedFundInfo>(json, _jsonOptions);
        if (dto == null)
        {
            SaveToTemp(json, response);
            return new AIParseResult
            {
                Json = json,
                ParsedInfo = new AiParsedFundInfo(),
                Factors = [],
                Warnings = warnings.Count > 0 ? warnings : ["JSON 反序列化结果为空"]
            };
        }

        // 转换为 FundFactor[]
        var factors = AiParsedFundInfoConverter.ToFactors(dto);

        return new AIParseResult
        {
            Json = json,
            ParsedInfo = dto,
            Factors = factors,
            Warnings = warnings
        };
    }

    /// <summary>
    /// 逐字段解析 JSON，尽可能保留有效数据
    /// </summary>
    private static AiParsedFundInfo? ParsePerProperty(string json, List<string> warnings)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var dto = new AiParsedFundInfo();

            foreach (var prop in typeof(AiParsedFundInfo).GetProperties())
            {
                if (!root.TryGetProperty(prop.Name, out var element) &&
                    !root.TryGetProperty(char.ToLower(prop.Name[0]) + prop.Name[1..], out element))
                    continue;

                try
                {
                    var value = JsonSerializer.Deserialize(element.GetRawText(), prop.PropertyType, JsonOptions);
                    prop.SetValue(dto, value);
                }
                catch
                {
                    warnings.Add($"字段 {prop.Name} 解析失败，已跳过");
                }
            }

            return dto;
        }
        catch (JsonException ex)
        {
            warnings.Add($"JSON 格式无效，无法解析: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 将 AI 原始响应保存到临时文件夹，方便排查问题
    /// </summary>
    private static string SaveToTemp(string json, string rawResponse)
    {
        Directory.CreateDirectory("temp");
        var fileName = $"parse_{DateTime.Now:yyyyMMdd_HHmmss}.json";
        var path = Path.Combine("temp", fileName);

        var content = json != rawResponse
            ? $"// ===== Extracted JSON =====\n{json}\n\n// ===== Raw Response =====\n{rawResponse}"
            : json;

        File.WriteAllText(path, content);
        return path;
    }
}
