using System.Text.Json;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Vetting.Copilot.Tests;

/// <summary>
/// 模板质量检查结果
/// </summary>
public record QualityCheckResult
{
    public string FileName { get; init; } = "";
    public bool JsonValid { get; init; }
    public bool StructureValid { get; init; }
    public int OperationCount { get; init; }
    public int PlaceholderCount { get; init; }
    public int ValidOperations { get; init; }
    public int InvalidOperations { get; init; }
    public List<string> Errors { get; init; } = [];
    public List<string> Warnings { get; init; } = [];
    public List<string> PlaceholderNames { get; init; } = [];
    public double Score => OperationCount > 0 ? (double)ValidOperations / OperationCount * 100 : 0;
}

/// <summary>
/// 模板生成综合报告
/// </summary>
public record QualityReport
{
    public DateTime GeneratedAt { get; init; } = DateTime.Now;
    public int TotalFiles { get; init; }
    public int SuccessCount { get; init; }
    public int FailureCount { get; init; }
    public double AverageScore { get; init; }
    public List<QualityCheckResult> Results { get; init; } = [];
    public List<string> OptimizationSuggestions { get; init; } = [];
}

/// <summary>
/// 模板质量检查器
/// </summary>
public static class TemplateQualityChecker
{
    /// <summary>
    /// 检查生成的 JSON 和模板文件质量
    /// </summary>
    public static QualityCheckResult Check(string jsonFilePath, string templateFilePath)
    {
        var result = new QualityCheckResult { FileName = Path.GetFileName(templateFilePath) };

        // 1. 检查 JSON 文件
        if (!File.Exists(jsonFilePath))
        {
            result.Errors.Add("JSON 文件不存在");
            return result;
        }

        var json = File.ReadAllText(jsonFilePath);
        JsonDocument? doc = null;
        try
        {
            doc = JsonDocument.Parse(json);
            result = result with { JsonValid = true };
        }
        catch (JsonException ex)
        {
            result.Errors.Add($"JSON 解析失败: {ex.Message}");
            return result;
        }

        using (doc)
        {
            var root = doc!.RootElement;

            // 2. 检查 operations 结构
            if (!root.TryGetProperty("operations", out var ops) || ops.ValueKind != JsonValueKind.Array)
            {
                result.Errors.Add("缺少 operations 数组");
                return result;
            }

            var opList = ops.EnumerateArray().ToList();
            result = result with { OperationCount = opList.Count };

            int valid = 0, invalid = 0;
            var placeholders = new HashSet<string>();

            for (int i = 0; i < opList.Count; i++)
            {
                var op = opList[i];
                var opResult = CheckOperation(op, i);
                if (opResult.isValid)
                {
                    valid++;
                    if (opResult.placeholder != null)
                        placeholders.Add(opResult.placeholder);
                }
                else
                {
                    invalid++;
                    result.Errors.AddRange(opResult.errors);
                }
            }

            // 3. 检查 placeholders 对象
            if (root.TryGetProperty("placeholders", out var ph) && ph.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in ph.EnumerateObject())
                {
                    placeholders.Add($"{{{{{prop.Name}}}}}");
                }
            }

            result = result with
            {
                StructureValid = invalid == 0,
                ValidOperations = valid,
                InvalidOperations = invalid,
                PlaceholderCount = placeholders.Count,
                PlaceholderNames = placeholders.OrderBy(p => p).ToList()
            };

            // 4. 检查模板文件
            if (File.Exists(templateFilePath))
            {
                var tplWarnings = CheckTemplateFile(templateFilePath);
                result.Warnings.AddRange(tplWarnings);
            }

            // 5. 检查占位符命名规范
            var namingErrors = CheckPlaceholderNaming(placeholders);
            result.Errors.AddRange(namingErrors);
        }

        return result;
    }

    /// <summary>
    /// 检查单个操作
    /// </summary>
    private static (bool isValid, string? placeholder, List<string> errors) CheckOperation(JsonElement op, int index)
    {
        var errors = new List<string>();

        if (op.ValueKind != JsonValueKind.Object)
        {
            errors.Add($"operations[{index}]: 不是对象");
            return (false, null, errors);
        }

        // 检查 tool 字段
        if (!op.TryGetProperty("tool", out var tool))
        {
            errors.Add($"operations[{index}]: 缺少 tool 字段");
            return (false, null, errors);
        }

        var toolName = tool.GetString();
        if (toolName is not ("set_cell" or "set_paragraph"))
        {
            errors.Add($"operations[{index}]: 未知工具 '{toolName}'");
            return (false, null, errors);
        }

        // 检查 text 字段
        if (!op.TryGetProperty("text", out var textEl) || textEl.ValueKind != JsonValueKind.String)
        {
            errors.Add($"operations[{index}]: 缺少 text 字段");
            return (false, null, errors);
        }

        var text = textEl.GetString() ?? "";

        // 检查索引字段
        if (toolName == "set_cell")
        {
            CheckIntField(op, "table_index", index, errors);
            CheckIntField(op, "row_index", index, errors);
            CheckIntField(op, "col_index", index, errors);
        }
        else if (toolName == "set_paragraph")
        {
            CheckIntField(op, "para_index", index, errors);
        }

        // 提取占位符
        string? placeholder = null;
        var match = Regex.Match(text, @"\{\{([^}]+)\}\}");
        if (match.Success)
            placeholder = match.Value;

        // 检查散装占位符是否带 question
        if (placeholder != null && Regex.IsMatch(placeholder, @"\{\{a\d+\}\}"))
        {
            if (!op.TryGetProperty("question", out _))
            {
                errors.Add($"operations[{index}]: 散装占位符 {placeholder} 缺少 question 参数");
            }
        }

        return (errors.Count == 0, placeholder, errors);
    }

    private static void CheckIntField(JsonElement op, string field, int index, List<string> errors)
    {
        // 检查 snake_case 和 camelCase
        var snake = field;
        var camel = string.Concat(field.Split('_').Select((s, i) => i == 0 ? s : char.ToUpper(s[0]) + s[1..]));

        if (!op.TryGetProperty(snake, out _) && !op.TryGetProperty(camel, out _))
        {
            errors.Add($"operations[{index}]: 缺少 {field} 字段");
        }
    }

    /// <summary>
    /// 检查模板文件中的占位符是否被正确写入
    /// </summary>
    private static List<string> CheckTemplateFile(string templatePath)
    {
        var warnings = new List<string>();
        try
        {
            using var doc = WordprocessingDocument.Open(templatePath, false);
            var body = doc.MainDocumentPart!.Document.Body!;

            int placeholderCount = 0;
            foreach (var para in body.Descendants<Paragraph>())
            {
                var text = para.InnerText;
                if (Regex.IsMatch(text, @"\{\{[^}]+\}\}"))
                    placeholderCount += Regex.Matches(text, @"\{\{[^}]+\}\}").Count;
            }

            foreach (var table in body.Descendants<Table>())
            {
                foreach (var cell in table.Descendants<TableCell>())
                {
                    var text = cell.InnerText;
                    if (Regex.IsMatch(text, @"\{\{[^}]+\}\}"))
                        placeholderCount += Regex.Matches(text, @"\{\{[^}]+\}\}").Count;
                }
            }

            if (placeholderCount == 0)
                warnings.Add("模板文件中未找到任何占位符 {{...}}，可能写入失败");
        }
        catch (Exception ex)
        {
            warnings.Add($"读取模板文件失败: {ex.Message}");
        }
        return warnings;
    }

    /// <summary>
    /// 检查占位符命名是否符合规范
    /// </summary>
    private static List<string> CheckPlaceholderNaming(HashSet<string> placeholders)
    {
        var errors = new List<string>();
        var validPrefixes = new[]
        {
            "manager_", "product.", "recommend", "shareholder.", "actualcontroller.",
            "department.", "strategy.", "award.", "credit_", "invest_", "risk_",
            "financialstatement", "drawdownrecord", "aum",
            "executive.", "researcher.", "riskctrl.", "pm.", "contact.", "compliance.",
            "a"
        };

        foreach (var ph in placeholders)
        {
            var name = ph.Trim('{', '}');
            var isValid = validPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            if (!isValid && !Regex.IsMatch(name, @"^a\d+$"))
            {
                errors.Add($"占位符 {ph} 命名不符合规范（不在已知前缀列表中）");
            }

            // 检查 product 不能用下划线
            if (Regex.IsMatch(name, @"^product_", RegexOptions.IgnoreCase))
            {
                errors.Add($"占位符 {ph} 错误：product 必须用点号（.），不能用下划线（_）");
            }
        }
        return errors;
    }

    /// <summary>
    /// 生成综合报告
    /// </summary>
    public static QualityReport GenerateReport(List<QualityCheckResult> results)
    {
        var success = results.Where(r => r.JsonValid && r.StructureValid).ToList();
        var failure = results.Where(r => !r.JsonValid || !r.StructureValid).ToList();

        var suggestions = new List<string>();

        // 分析常见问题
        var allErrors = results.SelectMany(r => r.Errors).ToList();

        if (allErrors.Any(e => e.Contains("缺少 question 参数")))
            suggestions.Add("[syspt] 散装占位符 {{aN}} 缺少 question 参数 — 建议在 syspt.md 中加强强调此规则");

        if (allErrors.Any(e => e.Contains("product_")))
            suggestions.Add("[syspt] AI 使用了 product_XXX 而非 product.XXX — 代码已自动修复，但建议在 syspt 中更突出此规则");

        if (allErrors.Any(e => e.Contains("col_index") || e.Contains("row_index")))
            suggestions.Add("[syspt] 索引字段缺失或命名不一致 — 建议在 syspt 中明确要求 snake_case 索引名");

        if (failure.Count > results.Count / 2)
            suggestions.Add("[model] 超过半数文件生成失败 — 考虑换用更强模型或拆分长文档");

        if (results.Any(r => r.OperationCount == 0))
            suggestions.Add("[syspt] 有文件 operations 为空 — 可能文档结构过于复杂，需要分段解析");

        return new QualityReport
        {
            TotalFiles = results.Count,
            SuccessCount = success.Count,
            FailureCount = failure.Count,
            AverageScore = results.Count > 0 ? results.Average(r => r.Score) : 0,
            Results = results,
            OptimizationSuggestions = suggestions
        };
    }

    /// <summary>
    /// 格式化报告为可读文本
    /// </summary>
    public static string FormatReport(QualityReport report)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("═══════════════════════════════════════════");
        sb.AppendLine("  Vetting.Copilot 模板生成质量报告");
        sb.AppendLine($"  生成时间: {report.GeneratedAt:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine("═══════════════════════════════════════════");
        sb.AppendLine();
        sb.AppendLine($"总文件数:   {report.TotalFiles}");
        sb.AppendLine($"成功:       {report.SuccessCount}");
        sb.AppendLine($"失败:       {report.FailureCount}");
        sb.AppendLine($"平均质量分: {report.AverageScore:F1}%");
        sb.AppendLine();

        sb.AppendLine("─── 各文件详情 ───");
        foreach (var r in report.Results)
        {
            sb.AppendLine();
            sb.AppendLine($"  [{(r.JsonValid && r.StructureValid ? "OK" : "FAIL")}] {r.FileName}");
            sb.AppendLine($"      操作数: {r.OperationCount} (有效: {r.ValidOperations}, 无效: {r.InvalidOperations})");
            sb.AppendLine($"      占位符: {r.PlaceholderCount} 个");
            sb.AppendLine($"      质量分: {r.Score:F1}%");

            if (r.Errors.Count > 0)
            {
                sb.AppendLine("      错误:");
                foreach (var e in r.Errors)
                    sb.AppendLine($"        - {e}");
            }
            if (r.Warnings.Count > 0)
            {
                sb.AppendLine("      警告:");
                foreach (var w in r.Warnings)
                    sb.AppendLine($"        - {w}");
            }
        }

        if (report.OptimizationSuggestions.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("─── 优化建议 ───");
            foreach (var s in report.OptimizationSuggestions)
                sb.AppendLine($"  {s}");
        }

        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════════");
        return sb.ToString();
    }
}
