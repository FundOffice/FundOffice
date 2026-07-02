using FundOffice.Copilot.Configuration;
using FundOffice.Copilot.Models;
using FundOffice.Copilot.Providers;
using System.Text;

namespace FMO.AI;

/// <summary>
/// AI 聊天适配器
/// 将 ITokenProvider 的 ChatCompletionAsync 接口适配为 AskAsync/AskWithFileAsync 接口
/// </summary>
public class AIChatAdapter
{
    private readonly ITokenProvider _provider;
    private readonly string _defaultModel;

    public AIChatAdapter(ITokenProvider provider, string defaultModel)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _defaultModel = defaultModel ?? throw new ArgumentNullException(nameof(defaultModel));
    }

    /// <summary>
    /// 简单文本问答
    /// </summary>
    /// <param name="prompt">系统提示词</param>
    /// <param name="message">用户消息</param>
    /// <param name="progress">进度报告（token 计数）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>AI 返回的文本</returns>
    public async Task<string> AskAsync(
        string prompt,
        string message,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        var messages = new List<ChatMessage>
        {
            ChatMessage.System(prompt),
            ChatMessage.User(message)
        };

        var options = new ChatOptions
        {
            Temperature = 0.1f,
            MaxTokens = 16384,
            TopP = 0.95f
        };

        // 如果需要进度报告，使用流式 API
        if (progress is not null)
        {
            return await StreamWithProgress(messages, options, progress, ct);
        }

        var result = await _provider.ChatCompletionAsync(messages, options: options, cancellationToken: ct);
        return ExtractText(result);
    }

    /// <summary>
    /// 从 docx 文件问答（Tier 2 → Tier 3 降级）
    /// Tier 2: base64 inline（通过 DocumentContent）
    /// Tier 3: 文本提取（通过 DocxTextExtractor）
    /// </summary>
    public async Task<string> AskWithFileAsync(
        string docxPath,
        string? textContent = null,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        string prompt = FundDocxPrompt.Build();

        // Tier 2: 尝试 base64 inline
        try
        {
            var base64 = Convert.ToBase64String(await File.ReadAllBytesAsync(docxPath, ct));
            return await AskWithBase64Async(prompt, base64, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", progress, ct);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AI] base64 inline 失败，降级到文本提取: {ex.Message}");
        }

        // Tier 3: 文本提取
        var text = textContent ?? DocxTextExtractor.ExtractTextFromDocx(docxPath);
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException($"无法从文档中提取文本: {Path.GetFileName(docxPath)}");

        return await AskAsync(prompt, text, progress, ct);
    }

    /// <summary>
    /// 从已提取的文本问答（跳过文件处理）
    /// </summary>
    public async Task<string> AskFromTextAsync(
        string textContent,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        string prompt = FundDocxPrompt.Build();
        return await AskAsync(prompt, textContent, progress, ct);
    }

    /// <summary>
    /// base64 inline 方式问答
    /// </summary>
    public async Task<string> AskWithBase64Async(
        string prompt,
        string base64,
        string mimeType,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        // 构建用户消息：文档 + 文本提示
        var userContent = new List<ContentPart>
        {
            new DocumentContent(mimeType, base64, "document.docx"),
            new TextContent("请从上面的文档中提取基金信息")
        };

        var messages = new List<ChatMessage>
        {
            ChatMessage.System(prompt),
            new ChatMessage { Role = MessageRole.User, Content = userContent }
        };

        var options = new ChatOptions
        {
            Temperature = 0.1f,
            MaxTokens = 16384
        };

        if (progress is not null)
        {
            return await StreamWithProgress(messages, options, progress, ct);
        }

        var result = await _provider.ChatCompletionAsync(messages, options: options, cancellationToken: ct);
        return ExtractText(result);
    }

    /// <summary>
    /// 流式调用并报告进度
    /// </summary>
    private async Task<string> StreamWithProgress(
        IReadOnlyList<ChatMessage> messages,
        ChatOptions options,
        IProgress<int> progress,
        CancellationToken ct)
    {
        var sb = new StringBuilder();
        var tokenCount = 0;

        await foreach (var token in _provider.ChatCompletionStreamAsync(messages, options: options, cancellationToken: ct))
        {
            if (token is TextDelta td)
            {
                sb.Append(td.Text);
                tokenCount++;
                progress.Report(tokenCount);
            }
        }

        return sb.Length > 0 ? sb.ToString() : "无有效返回";
    }

    /// <summary>
    /// 从 ChatResult 提取文本内容
    /// </summary>
    private static string ExtractText(ChatResult result)
    {
        var textContent = result.Messages?
            .SelectMany(m => m.Content)
            .OfType<TextContent>()
            .FirstOrDefault();

        return textContent?.Text ?? "无有效返回";
    }
}