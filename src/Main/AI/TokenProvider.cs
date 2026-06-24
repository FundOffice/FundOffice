using DocumentFormat.OpenXml.Packaging;
using FMO.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FMO.AI;


public enum TokenProviderStyle
{
    None,

    /// <summary>
    /// OpenAI 兼容格式（国内厂商通用）
    /// </summary>
    OpenAI,

    /// <summary>
    /// Anthropic/Claude 格式（优先推荐）
    /// </summary>
    Anthropic,

    /// <summary>
    /// Google Gemini 格式
    /// </summary>
    Google,
}

/// <summary>
/// AI 提供商基类
/// </summary>
public class TokenProvider
{
    public int Id { get; set; }

    public virtual string Company { get; } = "未知提供商";

    /// <summary>
    /// API 风格，子类写死不可改
    /// </summary>
    public virtual TokenProviderStyle Style { get; } = TokenProviderStyle.None;

    public required string Url { get; set; }

    public required string Key { get; set; }

    // ===== 文件上传能力声明 =====

    /// <summary>
    /// 是否支持独立的 docx 文件上传 API（Tier 1）
    /// </summary>
    protected virtual bool SupportsDocxFileUpload => false;

    /// <summary>
    /// 是否支持 docx base64 inline 传入（Tier 2）
    /// </summary>
    protected virtual bool SupportsDocxBase64Inline => false;

    /// <summary>
    /// 独立文件上传，返回 file_id 或文本内容
    /// </summary>
    protected virtual async Task<string> UploadFileAsync(HttpClient client, string filePath)
        => throw new NotSupportedException($"{Company} 不支持文件上传");

    /// <summary>
    /// 使用已上传文件的 file_id 进行问答
    /// </summary>
    protected virtual async Task<string> AskWithFileIdAsync(HttpClient client, string model, string prompt, string fileId)
        => throw new NotSupportedException($"{Company} 不支持 file_id 问答");

    public override string ToString()
    {
        return Company ?? "未设置来源";
    }

    // ===== 纯文本问答 =====

    public string Ask(HttpClient client, string model, string prompt, string message)
    {
        if (string.IsNullOrWhiteSpace(Key))
            throw new InvalidDataException("错误：API密钥未配置");
        if (string.IsNullOrWhiteSpace(Url))
            throw new InvalidDataException("错误：请求地址未配置");

        try
        {
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            string requestBody;
            string responseContent;
            string url = Url;

            switch (Style)
            {
                case TokenProviderStyle.OpenAI:
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Key);

                    var openAiRequest = new
                    {
                        model = model,
                        messages = new[]
                        {
                            new { role = "system", content = prompt },
                            new { role = "user", content = message }
                        },
                        max_completion_tokens = 8192,
                        temperature = 0.1,
                        top_p = 0.95,
                        stream = false,
                        stop = (string?)null,
                        frequency_penalty = 0,
                        presence_penalty = 0
                    };
                    requestBody = JsonSerializer.Serialize(openAiRequest);
                    var openAiResponse = client.PostAsync(url, new StringContent(requestBody, Encoding.UTF8, "application/json")).Result;
                    responseContent = openAiResponse.Content.ReadAsStringAsync().Result;

                    var openAiResult = JsonSerializer.Deserialize<OpenAIResponse>(responseContent);
                    return openAiResult?.choices?[0]?.message?.content ?? "无有效返回";

                case TokenProviderStyle.Anthropic:
                    client.DefaultRequestHeaders.Add("x-api-key", Key);
                    client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

                    var anthropicRequest = new
                    {
                        model = model,
                        max_tokens = 8192,
                        system = prompt,
                        messages = new[]
                        {
                            new { role = "user", content = new[] { new { type = "text", text = message } } }
                        },
                        top_p = 0.95,
                        stream = false,
                        temperature = 0.1,
                        stop_sequences = (string?)null
                    };
                    requestBody = JsonSerializer.Serialize(anthropicRequest);
                    var anthropicResponse = client.PostAsync(url, new StringContent(requestBody, Encoding.UTF8, "application/json")).Result;
                    responseContent = anthropicResponse.Content.ReadAsStringAsync().Result;

                    var anthropicResult = JsonSerializer.Deserialize<AnthropicResponse>(responseContent);
                    return anthropicResult?.content?[0]?.text ?? "无有效返回";

                case TokenProviderStyle.Google:
                    url = url.Replace("{model}", model) + "?key=" + Key;

                    var googleRequest = new
                    {
                        contents = new[]
                        {
                            new
                            {
                                role = "user",
                                parts = new object[]
                                {
                                    new { text = prompt + "\n\n" + message }
                                }
                            }
                        },
                        generationConfig = new
                        {
                            temperature = 0.1,
                            maxOutputTokens = 8192
                        }
                    };
                    requestBody = JsonSerializer.Serialize(googleRequest);
                    var googleResponse = client.PostAsync(url, new StringContent(requestBody, Encoding.UTF8, "application/json")).Result;
                    responseContent = googleResponse.Content.ReadAsStringAsync().Result;

                    var googleResult = JsonSerializer.Deserialize<GoogleResponse>(responseContent);
                    return googleResult?.candidates?[0]?.content?.parts?[0]?.text ?? "无有效返回";

                default:
                    return "错误：不支持的API风格";
            }
        }
        catch (Exception ex)
        {
            return $"调用异常：{ex.Message}";
        }
    }

    // ===== 携带文件的问答（三层降级）=====

    /// <summary>
    /// 携带 docx 文件的问答
    /// Tier 1: 独立文件上传 API → Tier 2: base64 inline → Tier 3: 文本提取
    /// </summary>
    public async Task<string> AskWithFileAsync(
        HttpClient client, string model, string prompt,
        string docxPath, string? textContent = null)
    {
        // Tier 1: 独立文件上传
        if (SupportsDocxFileUpload)
        {
            try
            {
                var result = await UploadFileAsync(client, docxPath);
                return await AskWithFileIdAsync(client, model, prompt, result);
            }
            catch { /* 降级到下一层 */ }
        }

        // Tier 2: base64 inline
        if (SupportsDocxBase64Inline)
        {
            try
            {
                var base64 = Convert.ToBase64String(await File.ReadAllBytesAsync(docxPath));
                return AskWithBase64(client, model, prompt, base64);
            }
            catch { /* 降级到下一层 */ }
        }

        // Tier 3: 文本提取
        var text = textContent ?? ExtractTextFromDocx(docxPath);
        return Ask(client, model, prompt, text);
    }

    /// <summary>
    /// base64 inline 方式调用（Tier 2）
    /// </summary>
    protected string AskWithBase64(HttpClient client, string model, string prompt, string base64)
    {
        if (string.IsNullOrWhiteSpace(Key))
            throw new InvalidDataException("错误：API密钥未配置");

        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        string requestBody;
        string responseContent;

        switch (Style)
        {
            case TokenProviderStyle.OpenAI:
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Key);
                var openAiRequest = new
                {
                    model = model,
                    messages = new object[]
                    {
                        new { role = "system", content = prompt },
                        new
                        {
                            role = "user",
                            content = new object[]
                            {
                                new
                                {
                                    type = "file",
                                    file = new
                                    {
                                        filename = "document.docx",
                                        file_data = $"data:application/vnd.openxmlformats-officedocument.wordprocessingml.document;base64,{base64}"
                                    }
                                },
                                new { type = "text", text = "请从上面的文档中提取基金信息" }
                            }
                        }
                    },
                    max_completion_tokens = 8192,
                    temperature = 0.1,
                    stream = false
                };
                requestBody = JsonSerializer.Serialize(openAiRequest);
                var openAiResponse = client.PostAsync(Url, new StringContent(requestBody, Encoding.UTF8, "application/json")).Result;
                responseContent = openAiResponse.Content.ReadAsStringAsync().Result;
                var openAiResult = JsonSerializer.Deserialize<OpenAIResponse>(responseContent);
                return openAiResult?.choices?[0]?.message?.content ?? "无有效返回";

            case TokenProviderStyle.Anthropic:
                client.DefaultRequestHeaders.Add("x-api-key", Key);
                client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
                var anthropicRequest = new
                {
                    model = model,
                    max_tokens = 8192,
                    system = prompt,
                    messages = new object[]
                    {
                        new
                        {
                            role = "user",
                            content = new object[]
                            {
                                new
                                {
                                    type = "document",
                                    source = new
                                    {
                                        type = "base64",
                                        media_type = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                                        data = base64
                                    }
                                },
                                new { type = "text", text = "请从上面的文档中提取基金信息" }
                            }
                        }
                    },
                    temperature = 0.1,
                    stream = false
                };
                requestBody = JsonSerializer.Serialize(anthropicRequest);
                var anthropicResponse = client.PostAsync(Url, new StringContent(requestBody, Encoding.UTF8, "application/json")).Result;
                responseContent = anthropicResponse.Content.ReadAsStringAsync().Result;
                var anthropicResult = JsonSerializer.Deserialize<AnthropicResponse>(responseContent);
                return anthropicResult?.content?[0]?.text ?? "无有效返回";

            case TokenProviderStyle.Google:
                var url = Url.Replace("{model}", model) + "?key=" + Key;
                var googleRequest = new
                {
                    contents = new object[]
                    {
                        new
                        {
                            role = "user",
                            parts = new object[]
                            {
                                new
                                {
                                    inline_data = new
                                    {
                                        mime_type = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                                        data = base64
                                    }
                                },
                                new { text = prompt + "\n\n请从上面的文档中提取基金信息" }
                            }
                        }
                    },
                    generationConfig = new { temperature = 0.1, maxOutputTokens = 8192 }
                };
                requestBody = JsonSerializer.Serialize(googleRequest);
                var googleResponse = client.PostAsync(url, new StringContent(requestBody, Encoding.UTF8, "application/json")).Result;
                responseContent = googleResponse.Content.ReadAsStringAsync().Result;
                var googleResult = JsonSerializer.Deserialize<GoogleResponse>(responseContent);
                return googleResult?.candidates?[0]?.content?.parts?[0]?.text ?? "无有效返回";

            default:
                return "错误：不支持的API风格";
        }
    }

    /// <summary>
    /// 从 docx 文件提取纯文本（Tier 3 降级方案）
    /// </summary>
    protected static string ExtractTextFromDocx(string docxPath)
    {
        try
        {
            using var doc = WordprocessingDocument.Open(docxPath, false);
            return doc.MainDocumentPart?.Document.Body?.InnerText ?? "";
        }
        catch
        {
            return "";
        }
    }

    /// <summary>
    /// 从 AI 返回文本中提取 JSON 部分
    /// </summary>
    internal static string ExtractJson(string response)
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
}


// ===== 响应实体 =====

// OpenAI 响应实体
public class OpenAIResponse
{
    public OpenAIChoice[]? choices { get; set; }
}
public class OpenAIChoice
{
    public OpenAIMessage? message { get; set; }
}
public class OpenAIMessage
{
    public string? content { get; set; }
}

// Anthropic 响应实体
public class AnthropicResponse
{
    public AnthropicContent[]? content { get; set; }
}
public class AnthropicContent
{
    public string? type { get; set; }
    public string? text { get; set; }
}

// Google Gemini 响应实体
public class GoogleResponse
{
    public GoogleCandidate[]? candidates { get; set; }
}
public class GoogleCandidate
{
    public GoogleContent? content { get; set; }
}
public class GoogleContent
{
    public GooglePart[]? parts { get; set; }
}
public class GooglePart
{
    public string? text { get; set; }
}
