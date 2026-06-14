using FMO.Models;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;

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
    /// API 风格，子类提供默认值，运行时可改
    /// </summary>
    public virtual TokenProviderStyle Style { get; set; } = TokenProviderStyle.None;

    public virtual string Url { get; set; } = "";

    public virtual string Key { get; set; } = "";

    /// <summary>
    /// 选中的模型名称
    /// </summary>
    public virtual string Model { get; set; } = "";

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
    protected virtual async Task<string> AskWithFileIdAsync(HttpClient client, string model, string prompt, string fileId, IProgress<int>? progress = null)
        => throw new NotSupportedException($"{Company} 不支持 file_id 问答");

    public override string ToString()
    {
        return Company ?? "未设置来源";
    }

    // ===== 纯文本问答 =====

    public async Task<string> AskAsync(HttpClient client, string model, string prompt, string message, IProgress<int>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(Key))
            throw new InvalidDataException("错误：API密钥未配置");
        if (string.IsNullOrWhiteSpace(Url))
            throw new InvalidDataException("错误：请求地址未配置");

        var useStream = progress is not null;

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
                        max_completion_tokens = 16384,
                        temperature = 0.1,
                        top_p = 0.95,
                        stream = useStream,
                        stop = (string?)null,
                        frequency_penalty = 0,
                        presence_penalty = 0
                    };
                    requestBody = JsonSerializer.Serialize(openAiRequest);

                    if (useStream)
                    {
                        return await StreamOpenAiResponse(client, url, requestBody, progress!);
                    }
                    else
                    {
                        var openAiResponse = await client.PostAsync(url, new StringContent(requestBody, Encoding.UTF8, "application/json"));
                        responseContent = await openAiResponse.Content.ReadAsStringAsync();
                        var openAiResult = JsonSerializer.Deserialize<OpenAIResponse>(responseContent);
                        return openAiResult?.choices?[0]?.message?.content ?? "无有效返回";
                    }

                case TokenProviderStyle.Anthropic:
                    client.DefaultRequestHeaders.Add("x-api-key", Key);
                    client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

                    var anthropicRequest = new
                    {
                        model = model,
                        max_tokens = 16384,
                        system = prompt,
                        messages = new[]
                        {
                            new { role = "user", content = new[] { new { type = "text", text = message } } }
                        },
                        top_p = 0.95,
                        stream = useStream,
                        temperature = 0.1,
                        stop_sequences = (string?)null
                    };
                    requestBody = JsonSerializer.Serialize(anthropicRequest);

                    if (useStream)
                    {
                        return await StreamAnthropicResponse(client, url, requestBody, progress!);
                    }
                    else
                    {
                        var anthropicResponse = await client.PostAsync(url, new StringContent(requestBody, Encoding.UTF8, "application/json"));
                        responseContent = await anthropicResponse.Content.ReadAsStringAsync();
                        var anthropicResult = JsonSerializer.Deserialize<AnthropicResponse>(responseContent);
                        return anthropicResult?.content?[0]?.text ?? "无有效返回";
                    }

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
                            maxOutputTokens = 16384
                        }
                    };
                    requestBody = JsonSerializer.Serialize(googleRequest);
                    var googleResponse = await client.PostAsync(url, new StringContent(requestBody, Encoding.UTF8, "application/json"));
                    responseContent = await googleResponse.Content.ReadAsStringAsync();

                    var googleResult = JsonSerializer.Deserialize<GoogleResponse>(responseContent);
                    var text = googleResult?.candidates?[0]?.content?.parts?[0]?.text ?? "无有效返回";
                    if (text.Length > 0) progress?.Report(text.Length / 4);
                    return text;

                default:
                    return "错误：不支持的API风格";
            }
        }
        catch (Exception ex)
        {
            return $"调用异常：{ex.Message}";
        }
    }

    // ===== SSE 流式响应解析 =====

    private const int MaxOutputTokens = 16384;

    /// <summary>
    /// 流式读取 OpenAI 兼容 SSE 响应，实时报告 token 数
    /// </summary>
    protected static async Task<string> StreamOpenAiResponse(HttpClient client, string url, string requestBody, IProgress<int> progress)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
        };
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            return $"调用异常：HTTP {(int)response.StatusCode} - {error}";
        }

        using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        var sb = new StringBuilder();
        var tokenCount = 0;
        string? line;

        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (!line.StartsWith("data: ") || line == "data: [DONE]")
                continue;

            var data = line[6..];
            try
            {
                using var doc = JsonDocument.Parse(data);
                var delta = doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("delta");

                if (delta.TryGetProperty("content", out var content))
                {
                    var text = content.GetString();
                    if (text != null)
                    {
                        sb.Append(text);
                        tokenCount++;
                        progress.Report(tokenCount);
                    }
                }
            }
            catch { /* 跳过格式异常的 chunk */ }
        }

        return sb.Length > 0 ? sb.ToString() : "无有效返回";
    }

    /// <summary>
    /// 流式读取 Anthropic SSE 响应，实时报告 token 数
    /// </summary>
    protected static async Task<string> StreamAnthropicResponse(HttpClient client, string url, string requestBody, IProgress<int> progress)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
        };
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            return $"调用异常：HTTP {(int)response.StatusCode} - {error}";
        }

        using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        var sb = new StringBuilder();
        var tokenCount = 0;
        string? line;

        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (!line.StartsWith("data: "))
                continue;

            var data = line[6..];
            try
            {
                using var doc = JsonDocument.Parse(data);
                var root = doc.RootElement;

                if (root.TryGetProperty("type", out var type) && type.GetString() == "content_block_delta")
                {
                    var text = root.GetProperty("delta").GetProperty("text").GetString();
                    if (text != null)
                    {
                        sb.Append(text);
                        tokenCount++;
                        progress.Report(tokenCount);
                    }
                }
            }
            catch { /* 跳过非 delta 事件 */ }
        }

        return sb.Length > 0 ? sb.ToString() : "无有效返回";
    }

    // ===== 携带文件的问答（三层降级）=====

    /// <summary>
    /// 携带 docx 文件的问答
    /// 按优先级使用最佳方式：文件上传 → base64 inline → 文本提取
    /// </summary>
    public async Task<string> AskWithFileAsync(
        HttpClient client, string model, string prompt,
        string docxPath, string? textContent = null, IProgress<int>? progress = null)
    {
        // Tier 1: 独立文件上传
        if (SupportsDocxFileUpload)
        {
            var result = await UploadFileAsync(client, docxPath);
            return await AskWithFileIdAsync(client, model, prompt, result, progress);
        }

        // Tier 2: base64 inline
        if (SupportsDocxBase64Inline)
        {
            var base64 = Convert.ToBase64String(await File.ReadAllBytesAsync(docxPath));
            return await AskWithBase64Async(client, model, prompt, base64, progress);
        }

        // Tier 3: 文本提取
        var text = textContent ?? ExtractTextFromDocx(docxPath);
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException($"无法从文档中提取文本: {Path.GetFileName(docxPath)}");
        return await AskAsync(client, model, prompt, text, progress);
    }

    /// <summary>
    /// base64 inline 方式调用（Tier 2）
    /// </summary>
    protected async Task<string> AskWithBase64Async(HttpClient client, string model, string prompt, string base64, IProgress<int>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(Key))
            throw new InvalidDataException("错误：API密钥未配置");

        var useStream = progress is not null;

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
                    max_completion_tokens = 16384,
                    temperature = 0.1,
                    stream = useStream
                };
                requestBody = JsonSerializer.Serialize(openAiRequest);
                if (useStream)
                    return await StreamOpenAiResponse(client, Url!, requestBody, progress!);
                var openAiResp = await client.PostAsync(Url, new StringContent(requestBody, Encoding.UTF8, "application/json"));
                responseContent = await openAiResp.Content.ReadAsStringAsync();
                var openAiResult = JsonSerializer.Deserialize<OpenAIResponse>(responseContent);
                return openAiResult?.choices?[0]?.message?.content ?? "无有效返回";

            case TokenProviderStyle.Anthropic:
                client.DefaultRequestHeaders.Add("x-api-key", Key);
                client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
                var anthropicRequest = new
                {
                    model = model,
                    max_tokens = 16384,
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
                    stream = useStream
                };
                requestBody = JsonSerializer.Serialize(anthropicRequest);
                if (useStream)
                    return await StreamAnthropicResponse(client, Url!, requestBody, progress!);
                var anthropicResp = await client.PostAsync(Url, new StringContent(requestBody, Encoding.UTF8, "application/json"));
                responseContent = await anthropicResp.Content.ReadAsStringAsync();
                var anthropicResult = JsonSerializer.Deserialize<AnthropicResponse>(responseContent);
                return anthropicResult?.content?[0]?.text ?? "无有效返回";

            case TokenProviderStyle.Google:
                var url = Url!.Replace("{model}", model) + "?key=" + Key;
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
                    generationConfig = new { temperature = 0.1, maxOutputTokens = 16384 }
                };
                requestBody = JsonSerializer.Serialize(googleRequest);
                var googleResp = await client.PostAsync(url, new StringContent(requestBody, Encoding.UTF8, "application/json"));
                responseContent = await googleResp.Content.ReadAsStringAsync();
                var googleResult = JsonSerializer.Deserialize<GoogleResponse>(responseContent);
                var text = googleResult?.candidates?[0]?.content?.parts?[0]?.text ?? "无有效返回";
                if (text.Length > 0) progress?.Report(text.Length / 4);
                return text;

            default:
                return "错误：不支持的API风格";
        }
    }

    /// <summary>
    /// 从 docx 文件提取纯文本（Tier 3 降级方案）
    /// 使用 ZipFile + XmlDocument 直接读取，容错性好
    /// 正确处理表格：按行提取，单元格用 " | " 分隔
    /// 公式转为 LaTeX 格式
    /// </summary>
    internal static string ExtractTextFromDocx(string docxPath)
    {
        try
        {
            using var zip = ZipFile.OpenRead(docxPath);
            var entry = zip.GetEntry("word/document.xml")
                ?? throw new FileNotFoundException("docx 文件中缺少 word/document.xml");

            using var entryStream = entry.Open();
            var xmlDoc = new XmlDocument();
            xmlDoc.Load(entryStream);

            var nsMgr = new XmlNamespaceManager(xmlDoc.NameTable);
            nsMgr.AddNamespace("w", "http://schemas.openxmlformats.org/wordprocessingml/2006/main");
            nsMgr.AddNamespace("m", "http://schemas.openxmlformats.org/officeDocument/2006/math");

            var body = xmlDoc.SelectSingleNode("//w:body", nsMgr);
            if (body == null) return "";

            var sb = new StringBuilder();
            foreach (XmlNode child in body.ChildNodes)
            {
                if (child.LocalName == "p")
                {
                    var text = ExtractXmlNodeText(child, nsMgr);
                    if (!string.IsNullOrEmpty(text))
                        sb.AppendLine(text);
                }
                else if (child.LocalName == "tbl")
                {
                    var rows = child.SelectNodes("w:tr", nsMgr);
                    if (rows == null) continue;
                    foreach (XmlNode row in rows)
                    {
                        var cells = row.SelectNodes("w:tc", nsMgr);
                        if (cells == null) continue;
                        var cellTexts = new List<string>();
                        foreach (XmlNode cell in cells)
                            cellTexts.Add(ExtractXmlNodeText(cell, nsMgr));
                        sb.AppendLine(string.Join(" | ", cellTexts));
                    }
                }
            }
            return sb.Length > 0 ? sb.ToString() : "";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AI] docx 文本提取失败: {ex.Message}");
            return "";
        }
    }

    /// <summary>
    /// 提取 XML 节点下所有 w:t 和 m:t（公式）文本，按文档顺序拼接
    /// </summary>
    private static string ExtractXmlNodeText(XmlNode node, XmlNamespaceManager nsMgr)
    {
        var sb = new StringBuilder();
        CollectTextInOrder(node, nsMgr, sb);
        return sb.ToString().Trim();
    }

    /// <summary>
    /// 按文档顺序递归收集文本，公式转为 LaTeX
    /// </summary>
    private static void CollectTextInOrder(XmlNode node, XmlNamespaceManager nsMgr, StringBuilder sb)
    {
        foreach (XmlNode child in node.ChildNodes)
        {
            // OMML 公式容器 → LaTeX
            if ((child.LocalName == "oMath" || child.LocalName == "oMathPara")
                && child.NamespaceURI == "http://schemas.openxmlformats.org/officeDocument/2006/math")
            {
                var latex = OmmlNodeToLatex(child, nsMgr);
                sb.Append(latex);
                continue;
            }

            if (child.NodeType == XmlNodeType.Text)
                sb.Append(child.Value);
            else if (child.LocalName == "t" && child.NamespaceURI == "http://schemas.openxmlformats.org/wordprocessingml/2006/main")
                sb.Append(child.InnerText);
            else if (child.HasChildNodes)
                CollectTextInOrder(child, nsMgr, sb);
        }
    }

    // ===== OMML → LaTeX 转换器 =====

    /// <summary>
    /// 将 OMML XmlElement 转为 LaTeX 字符串（ZipFile 方案）
    /// </summary>
    private static string OmmlNodeToLatex(XmlNode node, XmlNamespaceManager nsMgr)
    {
        var sb = new StringBuilder();
        foreach (XmlNode child in node.ChildNodes)
        {
            if (child.NamespaceURI != "http://schemas.openxmlformats.org/officeDocument/2006/math")
            {
                // 非 math 命名空间的元素（如 w:r），用 CollectTextInOrder 提取 w:t 文本
                CollectTextInOrder(child, nsMgr, sb);
                continue;
            }

            switch (child.LocalName)
            {
                case "f":
                    var num = child.SelectSingleNode("m:num", nsMgr);
                    var den = child.SelectSingleNode("m:den", nsMgr);
                    sb.Append($"\\frac{{{OmmlNodeToLatex(num!, nsMgr)}}}{{{OmmlNodeToLatex(den!, nsMgr)}}}");
                    break;

                case "sSup":
                    var supE = child.SelectSingleNode("m:e", nsMgr);
                    var supS = child.SelectSingleNode("m:sup", nsMgr);
                    sb.Append($"{{{OmmlNodeToLatex(supE!, nsMgr)}}}^{{{OmmlNodeToLatex(supS!, nsMgr)}}}");
                    break;

                case "sSub":
                    var subE = child.SelectSingleNode("m:e", nsMgr);
                    var subS = child.SelectSingleNode("m:sub", nsMgr);
                    sb.Append($"{{{OmmlNodeToLatex(subE!, nsMgr)}}}_{{{OmmlNodeToLatex(subS!, nsMgr)}}}");
                    break;

                case "sSubSup":
                    var ssE = child.SelectSingleNode("m:e", nsMgr);
                    var ssSub = child.SelectSingleNode("m:sub", nsMgr);
                    var ssSup = child.SelectSingleNode("m:sup", nsMgr);
                    sb.Append($"{{{OmmlNodeToLatex(ssE!, nsMgr)}}}_{{{OmmlNodeToLatex(ssSub!, nsMgr)}}}^{{{OmmlNodeToLatex(ssSup!, nsMgr)}}}");
                    break;

                case "rad":
                    var radE = child.SelectSingleNode("m:e", nsMgr);
                    var radDeg = child.SelectSingleNode("m:deg", nsMgr);
                    var degText = radDeg != null ? OmmlNodeToLatex(radDeg, nsMgr) : "";
                    if (string.IsNullOrEmpty(degText))
                        sb.Append($"\\sqrt{{{OmmlNodeToLatex(radE!, nsMgr)}}}");
                    else
                        sb.Append($"\\sqrt[{degText}]{{{OmmlNodeToLatex(radE!, nsMgr)}}}");
                    break;

                case "d":
                    var dPr = child.SelectSingleNode("m:dPr", nsMgr);
                    var beg = dPr?.SelectSingleNode("m:begChr/@m:val", nsMgr)?.Value ?? "(";
                    var end = dPr?.SelectSingleNode("m:endChr/@m:val", nsMgr)?.Value ?? ")";
                    var dContent = new List<string>();
                    foreach (XmlNode de in child.SelectNodes("m:e", nsMgr)!)
                        dContent.Add(OmmlNodeToLatex(de, nsMgr));
                    sb.Append($"\\left{beg}{string.Join(", ", dContent)}\\right{end}");
                    break;

                case "nary":
                    var naryChr = child.SelectSingleNode("m:naryPr/m:chr/@m:val", nsMgr)?.Value ?? "∑";
                    var naryOp = naryChr switch
                    {
                        "∑" => "\\sum",
                        "∏" => "\\prod",
                        "∫" => "\\int",
                        "∮" => "\\oint",
                        _ => naryChr
                    };
                    var narySub = child.SelectSingleNode("m:sub", nsMgr);
                    var narySup = child.SelectSingleNode("m:sup", nsMgr);
                    var naryE = child.SelectSingleNode("m:e", nsMgr);
                    sb.Append(naryOp);
                    if (narySub != null) sb.Append($"_{{{OmmlNodeToLatex(narySub, nsMgr)}}}");
                    if (narySup != null) sb.Append($"^{{{OmmlNodeToLatex(narySup, nsMgr)}}}");
                    if (naryE != null) sb.Append($" {OmmlNodeToLatex(naryE, nsMgr)}");
                    break;

                case "limLow":
                    var llE = child.SelectSingleNode("m:e", nsMgr);
                    var llLim = child.SelectSingleNode("m:lim", nsMgr);
                    sb.Append($"{{{OmmlNodeToLatex(llE!, nsMgr)}}}_{{{OmmlNodeToLatex(llLim!, nsMgr)}}}");
                    break;

                case "limUpp":
                    var luE = child.SelectSingleNode("m:e", nsMgr);
                    var luLim = child.SelectSingleNode("m:lim", nsMgr);
                    sb.Append($"{{{OmmlNodeToLatex(luE!, nsMgr)}}}^{{{OmmlNodeToLatex(luLim!, nsMgr)}}}");
                    break;

                case "func":
                    var funcF = child.SelectSingleNode("m:fName", nsMgr);
                    var funcE = child.SelectSingleNode("m:e", nsMgr);
                    sb.Append($"{OmmlNodeToLatex(funcF!, nsMgr)}\\left({OmmlNodeToLatex(funcE!, nsMgr)}\\right)");
                    break;

                case "bar":
                    var barE = child.SelectSingleNode("m:e", nsMgr);
                    sb.Append($"\\overline{{{OmmlNodeToLatex(barE!, nsMgr)}}}");
                    break;

                case "acc":
                    var accE = child.SelectSingleNode("m:e", nsMgr);
                    sb.Append($"\\hat{{{OmmlNodeToLatex(accE!, nsMgr)}}}");
                    break;

                case "groupChr":
                    var gcE = child.SelectSingleNode("m:e", nsMgr);
                    sb.Append($"\\underbrace{{{OmmlNodeToLatex(gcE!, nsMgr)}}}");
                    break;

                case "eqArr":
                    foreach (XmlNode eqE in child.SelectNodes("m:e", nsMgr)!)
                        sb.AppendLine(OmmlNodeToLatex(eqE, nsMgr) + " \\\\");
                    break;

                case "oMath":
                case "oMathPara":
                    sb.Append(OmmlNodeToLatex(child, nsMgr));
                    break;

                default:
                    // 未知 math 元素（m:r、m:e、m:sub 等）→ 收集所有子文本
                    CollectTextInOrder(child, nsMgr, sb);
                    break;
            }
        }
        return sb.ToString();
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
