using FMO.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FMO.AI;

/// <summary>
/// 基金文档 AI 解析器
/// 从 docx 文件中提取基金信息，返回 ReadonlyFundInfo
/// </summary>
public class FundDocxAiParser
{
    private readonly TokenProvider _provider;
    private readonly string _model;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public FundDocxAiParser(TokenProvider provider, string model)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _model = model ?? throw new ArgumentNullException(nameof(model));
    }

    /// <summary>
    /// 从 docx 文件解析基金信息，返回 ReadonlyFundInfo
    /// </summary>
    /// <param name="docxPath">docx 文件路径</param>
    /// <returns>解析结果，失败返回 null</returns>
    public async Task<ReadonlyFundInfo?> ParseAsync(string docxPath)
    {
        if (!File.Exists(docxPath))
            throw new FileNotFoundException("文件不存在", docxPath);

        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromMinutes(5);

        string prompt = FundDocxPrompt.Build();

        // 调用 AI 接口（三层降级：文件上传 → base64 → 文本提取）
        var response = await _provider.AskWithFileAsync(client, _model, prompt, docxPath);

        if (string.IsNullOrWhiteSpace(response) || response.StartsWith("调用异常"))
            return null;

        // 从 AI 返回中提取 JSON
        var json = TokenProvider.ExtractJson(response);

        // 反序列化为内部 DTO
        var dto = JsonSerializer.Deserialize<AiParsedFundInfo>(json, _jsonOptions);
        if (dto == null)
            return null;

        // 转换为 ReadonlyFundInfo
        return AiParsedFundInfoConverter.ToReadonlyFundInfo(dto);
    }

    /// <summary>
    /// 从已提取的文本解析基金信息（跳过文件上传）
    /// </summary>
    /// <param name="textContent">已提取的文档文本</param>
    /// <returns>解析结果，失败返回 null</returns>
    public async Task<ReadonlyFundInfo?> ParseFromTextAsync(string textContent)
    {
        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromMinutes(5);

        string prompt = FundDocxPrompt.Build();

        var response = _provider.Ask(client, _model, prompt, textContent);

        if (string.IsNullOrWhiteSpace(response) || response.StartsWith("调用异常"))
            return null;

        var json = TokenProvider.ExtractJson(response);

        var dto = JsonSerializer.Deserialize<AiParsedFundInfo>(json, _jsonOptions);
        if (dto == null)
            return null;

        return AiParsedFundInfoConverter.ToReadonlyFundInfo(dto);
    }
}
