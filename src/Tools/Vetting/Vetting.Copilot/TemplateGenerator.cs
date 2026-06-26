using FundOffice.Copilot.Models;
using FundOffice.Copilot.Providers;
using System.Text;
using System.Text.Json;

namespace Vetting.Copilot;

/// <summary>
/// 模板生成结果
/// </summary>
public record TemplateGenerationResult
{
    public bool Success { get; init; }
    public string? Json { get; init; }
    public string? TemplatePath { get; init; }
    public int OperationCount { get; init; }
    public int PlaceholderCount { get; init; }
    public int TokenUsage { get; init; }
    public string? ErrorMessage { get; init; }
    public List<string> Logs { get; init; } = [];
}

/// <summary>
/// AI 模板生成器 — 解析尽调文档结构，调用 AI 生成模板操作列表，写入 docx 模板文件
/// </summary>
public class TemplateGenerator
{
    private readonly ITokenProvider _provider;

    public TemplateGenerator(ITokenProvider provider)
    {
        _provider = provider;
    }

    public string ProviderIdentifier => _provider.Identifier;

    /// <summary>
    /// 加载系统提示词。优先使用本地 files/vetting/syspt.md，否则使用嵌入资源。
    /// </summary>
    public static async Task<string> LoadSysptAsync()
    {
        var localPath = Path.Combine("files", "vetting", "syspt.md");
        var asm = typeof(TemplateGenerator).Assembly;
        using var embeddedSr = new StreamReader(asm.GetManifestResourceStream("Vetting.Copilot.syspt.md")!);
        var embedded = await embeddedSr.ReadToEndAsync();
        var embeddedVer = ExtractVersion(embedded);

        if (File.Exists(localPath))
        {
            var local = await File.ReadAllTextAsync(localPath);
            var localVer = ExtractVersion(local);
            if (localVer >= embeddedVer) return local;
        }
        return embedded;
    }

    /// <summary>
    /// 生成模板：解析文档结构 → 调用 AI → 写入模板文件
    /// </summary>
    /// <param name="sourceFilePath">原始尽调文件路径</param>
    /// <param name="outputDir">模板输出目录</param>
    /// <param name="sysPrompt">系统提示词（可选，默认自动加载）</param>
    /// <param name="progress">进度回调</param>
    /// <param name="ct">取消令牌</param>
    public async Task<TemplateGenerationResult> GenerateAsync(
        string sourceFilePath,
        string outputDir,
        string? sysPrompt = null,
        Action<string>? progress = null,
        CancellationToken ct = default)
    {
        var logs = new List<string>();
        try
        {
            sysPrompt ??= await LoadSysptAsync();

            var structure = DocOps.ParseDocument(sourceFilePath);
            logs.Add($"已解析文档结构 ({structure.Length} 字符)");

            var messages = new[]
            {
                ChatMessage.System(sysPrompt),
                ChatMessage.User(structure)
            };
            var options = new ChatOptions
            {
                AdditionalProperties = new Dictionary<string, object>
                {
                    ["response_format"] = new { type = "json_object" }
                }
            };

            var sb = new StringBuilder();
            int usage = 0;
            await foreach (var token in _provider.ChatCompletionStreamAsync(messages, options: options, cancellationToken: ct))
            {
                switch (token)
                {
                    case TextDelta td:
                        sb.Append(td.Text);
                        usage = sb.Length / 4;
                        break;
                    case UsageUpdate u:
                        usage = (u.PromptTokens ?? 0) + (u.CompletionTokens ?? 0);
                        break;
                }
            }

            var json = sb.ToString().Trim();
            using var jsonDoc = JsonDocument.Parse(json);
            var root = jsonDoc.RootElement;

            Directory.CreateDirectory(outputDir);
            var safeName = Path.GetFileNameWithoutExtension(sourceFilePath);
            var ext = Path.GetExtension(sourceFilePath);
            var tplPath = Path.Combine(outputDir, $"{safeName}_by[{_provider.Identifier}]{ext}");

            FileRetry.Run(() => File.Copy(sourceFilePath, tplPath, overwrite: true), "复制源文件", onRetry: m => { logs.Add(m); progress?.Invoke(m); });
            FileRetry.Run(() => File.WriteAllText(Path.Combine(outputDir, $"{safeName}_by[{_provider.Identifier}].json"), json), "保存JSON", onRetry: m => { logs.Add(m); progress?.Invoke(m); });

            var ops = new List<(string tool, Dictionary<string, JsonElement> input)>();
            foreach (var op in root.GetProperty("operations").EnumerateArray())
            {
                var tool = op.GetProperty("tool").GetString()!;
                var input = new Dictionary<string, JsonElement>();
                foreach (var prop in op.EnumerateObject())
                {
                    if (prop.Name == "text" && prop.Value.ValueKind == JsonValueKind.String)
                    {
                        var fixedText = System.Text.RegularExpressions.Regex.Replace(
                            prop.Value.GetString()!, @"\{\{product_", "{{product.");
                        input[prop.Name] = JsonSerializer.SerializeToElement(fixedText);
                    }
                    else
                        input[prop.Name] = prop.Value.Clone();
                }
                ops.Add((tool, input));
            }

            FileRetry.Run(() => DocOps.BatchWrite(tplPath, ops), "生成模板", onRetry: m => { logs.Add(m); progress?.Invoke(m); });

            var placeholders = root.TryGetProperty("placeholders", out var ph) ? ph.EnumerateObject().Count() : 0;
            var logMsg = $"模板已生成: {tplPath} ({ops.Count} 操作, {placeholders} 占位符)";
            logs.Add(logMsg);
            progress?.Invoke(logMsg);

            // 提取 FileSpecialQuestion 数据
            var specialQuestions = new List<(int index, string question)>();
            if (root.TryGetProperty("placeholders", out var phEl) && phEl.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in phEl.EnumerateObject())
                {
                    if (p.Name.StartsWith('a') && int.TryParse(p.Name.TrimStart('a'), out var idx))
                        specialQuestions.Add((idx, p.Value.GetString() ?? ""));
                }
            }

            return new TemplateGenerationResult
            {
                Success = true,
                Json = json,
                TemplatePath = tplPath,
                OperationCount = ops.Count,
                PlaceholderCount = placeholders,
                TokenUsage = usage,
                Logs = logs,
            };
        }
        catch (Exception ex)
        {
            logs.Add($"错误: {ex.Message}");
            return new TemplateGenerationResult { Success = false, ErrorMessage = ex.Message, TokenUsage = 0, Logs = logs };
        }
    }

    private static int ExtractVersion(string content)
    {
        var match = System.Text.RegularExpressions.Regex.Match(content, @"<!--\s*version:(\d+)\s*-->");
        return match.Success ? int.Parse(match.Groups[1].Value) : 0;
    }
}
