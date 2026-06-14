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
        Converters = { new JsonStringEnumConverter() },
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
    /// 统一处理 AI 响应，任何失败路径都会保存原始内容到 temp
    /// </summary>
    private static AiParseResult? ProcessResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            SaveToTemp("", response);
            return null;
        }

        if (response.StartsWith("调用异常"))
        {
            SaveToTemp("", response);
            throw new InvalidOperationException(response);
        }

        return ParseResponse(response);
    }

    private static AiParseResult? ParseResponse(string response)
    {
        // 从 AI 返回中提取 JSON
        var json = TokenProvider.ExtractJson(response);

        // 反序列化为内部 DTO（含置信度 + 真实类型）
        AiParsedFundInfo? dto;
        try
        {
            dto = JsonSerializer.Deserialize<AiParsedFundInfo>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            var path = SaveToTemp(json, response);
            throw new JsonException($"AI 返回的 JSON 解析失败（已保存到 {path}）: {ex.Message}", ex);
        }

        if (dto == null)
        {
            SaveToTemp(json, response);
            return null;
        }

        // 转换为 FundFactor[]
        var factors = AiParsedFundInfoConverter.ToFactors(dto);

        return new AiParseResult
        {
            ParsedInfo = dto,
            Factors = factors,
        };
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
