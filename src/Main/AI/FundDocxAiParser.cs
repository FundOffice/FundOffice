using FMO.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FMO.AI;

/// <summary>
/// AI 解析结果，包含原始 DTO 和提取的 Factors
/// </summary>
public class AiParseResult
{
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
public class FundDocxAiParser
{
    private readonly TokenProvider _provider;
    private readonly string _model;

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(), new ConfidenceWrapperConverterFactory() },
    };

    public FundDocxAiParser(TokenProvider provider, string model)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _model = model ?? throw new ArgumentNullException(nameof(model));
    }

    /// <summary>
    /// 从 docx 文件解析基金信息
    /// </summary>
    /// <param name="docxPath">docx 文件路径</param>
    /// <param name="progress">token 计数进度报告（已接收 token 数）</param>
    /// <returns>解析结果，失败返回 null</returns>
    public async Task<AiParseResult?> ParseAsync(string docxPath, IProgress<int>? progress = null)
    {
        if (!File.Exists(docxPath))
            throw new FileNotFoundException("文件不存在", docxPath);

        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromMinutes(5);

        string prompt = FundDocxPrompt.Build();

        // 调用 AI 接口（三层降级：文件上传 → base64 → 文本提取）
        var response = await _provider.AskWithFileAsync(client, _model, prompt, docxPath, progress: progress);

        return ProcessResponse(response);
    }

    /// <summary>
    /// 从已提取的文本解析基金信息（跳过文件上传）
    /// </summary>
    /// <param name="textContent">已提取的文档文本</param>
    /// <param name="progress">token 计数进度报告</param>
    /// <returns>解析结果，失败返回 null</returns>
    public async Task<AiParseResult?> ParseFromTextAsync(string textContent, IProgress<int>? progress = null)
    {
        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromMinutes(5);

        string prompt = FundDocxPrompt.Build();

        var response = await _provider.AskAsync(client, _model, prompt, textContent, progress);

        return ProcessResponse(response);
    }

    /// <summary>
    /// 统一处理 AI 响应，尽可能保留有效数据，错误记录到 Warnings
    /// </summary>
    private static AiParseResult? ProcessResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            SaveToTemp("", response);
            return new AiParseResult
            {
                ParsedInfo = new AiParsedFundInfo(),
                Factors = [],
                Warnings = ["AI 返回为空"]
            };
        }

        if (response.StartsWith("调用异常"))
        {
            SaveToTemp("", response);
            return new AiParseResult
            {
                ParsedInfo = new AiParsedFundInfo(),
                Factors = [],
                Warnings = [response]
            };
        }

        return ParseResponse(response);
    }

    private static AiParseResult ParseResponse(string response)
    {
        var warnings = new List<string>();

        // 从 AI 返回中提取 JSON
        var json = TokenProvider.ExtractJson(response);

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

        if (dto == null)
        {
            SaveToTemp(json, response);
            return new AiParseResult
            {
                ParsedInfo = new AiParsedFundInfo(),
                Factors = [],
                Warnings = warnings.Count > 0 ? warnings : ["JSON 反序列化结果为空"]
            };
        }

        // 转换为 FundFactor[]
        var factors = AiParsedFundInfoConverter.ToFactors(dto);

        return new AiParseResult
        {
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
