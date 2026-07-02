using System.Text.Json;
using Microsoft.Extensions.Logging;
using Vetting.Copilot.Data;

namespace Vetting.Copilot;

/// <summary>
/// 模板填充结果
/// </summary>
public record TemplateFillResult
{
    public bool Success { get; init; }
    public string? OutputPath { get; init; }
    public string? ErrorMessage { get; init; }
    public List<string> Logs { get; init; } = [];
}

/// <summary>
/// 模板填充器 — 从 AI JSON 解析 operators，用 DocOps.Fill 直接写值到文档
/// </summary>
public class TemplateFiller
{
    /// <summary>
    /// 填充模板生成最终文件
    /// </summary>
    public TemplateFillResult Fill(
        string sourcePath,
        string jsonPath,
        string outputPath,
        string fileHash,
        string providerId,
        int[]? recommendIds = null,
        Action<string>? progress = null,
        ILogger? logger = null)
    {
        var logs = new List<string>();
        try
        {
            if (!File.Exists(sourcePath))
                return new TemplateFillResult { Success = false, ErrorMessage = "源文件不存在", Logs = logs };
            if (!File.Exists(jsonPath))
                return new TemplateFillResult { Success = false, ErrorMessage = "AI JSON 文件不存在", Logs = logs };

            var json = File.ReadAllText(jsonPath);
            using var jsonDoc = JsonDocument.Parse(json);
            var root = jsonDoc.RootElement;

            var operators = OperatorParser.Parse(root.GetProperty("operations"));
            logs.Add($"已解析 {operators.Count} 个操作");
            progress?.Invoke($"已解析 {operators.Count} 个操作");

            var resolver = DataResolver.Load(fileHash, providerId, recommendIds);

            var outDir = Path.GetDirectoryName(outputPath)!;
            Directory.CreateDirectory(outDir);

            FileRetry.Run(() => DocOps.Fill(sourcePath, outputPath, operators, resolver, logger), "填充文档", onRetry: m => { logs.Add(m); progress?.Invoke(m); });

            var logMsg = $"已生成: {outputPath}";
            logs.Add(logMsg);
            progress?.Invoke(logMsg);

            return new TemplateFillResult { Success = true, OutputPath = outputPath, Logs = logs };
        }
        catch (Exception ex)
        {
            logs.Add($"填充失败: {ex.Message}");
            return new TemplateFillResult { Success = false, ErrorMessage = ex.Message, Logs = logs };
        }
    }

    /// <summary>
    /// 从模板路径推导对应的 JSON 路径
    /// </summary>
    public static string GetJsonPath(string templatePath)
    {
        return Path.ChangeExtension(templatePath, ".json");
    }

    /// <summary>
    /// 从模板路径推导对应的源文件路径（去掉 _by[provider] 后缀）
    /// </summary>
    public static string GetSourcePath(string templatePath)
    {
        var dir = Path.GetDirectoryName(templatePath) ?? "";
        var name = Path.GetFileNameWithoutExtension(templatePath);
        var ext = Path.GetExtension(templatePath);
        // 格式: {safeName}_by[{providerId}]{ext}
        var match = System.Text.RegularExpressions.Regex.Match(name, @"^(.+)_by\[(.+)\]$");
        if (match.Success)
        {
            var safeName = match.Groups[1].Value;
            // 源文件在上级目录
            var parentDir = Path.GetDirectoryName(dir) ?? dir;
            return Path.Combine(parentDir, $"{safeName}{ext}");
        }
        return Path.Combine(dir, $"{name}{ext}");
    }
}
