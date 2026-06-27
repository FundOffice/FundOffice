using System.Text.Json;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Vetting.Copilot.Tests;

/// <summary>
/// 模板质量检查结果（基于新的 operations/files 格式）
/// </summary>
public record QualityCheckResult
{
    public string FileName { get; init; } = "";
    public bool JsonValid { get; init; }
    public int OperationCount { get; init; }
    public int FileCount { get; init; }
    public int TableCount { get; init; }
    public int ParagraphCount { get; init; }
    public List<int> CoveredTables { get; init; } = [];
    public List<int> MissingTables { get; init; } = [];
    /// <summary>被排除的表格（勾选表 + 资料清单表），AI 可不覆盖</summary>
    public List<int> ExcludedTables { get; init; } = [];
    public Dictionary<string, int> TypeCounts { get; init; } = [];
    public List<string> Warnings { get; init; } = [];
    public List<string> Errors { get; init; } = [];
    public List<RequiredFile> Files { get; init; } = [];
    public List<string> FileWarnings { get; init; } = [];
    /// <summary>0~100 质量分</summary>
    public double Score { get; init; }

    public bool Passed => Errors.Count == 0 && MissingTables.Count == 0;
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
/// 模板质量检查器 — 解析 AI 返回的 JSON，对照源文档检查覆盖率和结构合法性
/// </summary>
public static class TemplateQualityChecker
{
    /// <summary>
    /// 检查生成的 JSON 质量。
    /// </summary>
    /// <param name="jsonFilePath">AI 输出的 JSON 文件路径</param>
    /// <param name="sourceFilePath">源 docx 文件路径（用于统计表格/段落数）</param>
    public static QualityCheckResult Check(string jsonFilePath, string sourceFilePath)
    {
        var result = new QualityCheckResult { FileName = Path.GetFileName(sourceFilePath) };

        if (!File.Exists(jsonFilePath))
        {
            return result with { Errors = new List<string> { "JSON 文件不存在" } };
        }

        // 统计源文档表格/段落数，并识别可跳过表格（勾选表 + 资料清单表）
        List<int> excludedTables = new();
        try
        {
            using var doc = WordprocessingDocument.Open(sourceFilePath, false);
            var body = doc.MainDocumentPart!.Document.Body!;
            var tables = body.Elements<Table>().ToList();
            for (int i = 0; i < tables.Count; i++)
            {
                if (IsSkippableTable(tables[i])) excludedTables.Add(i);
            }
            result = result with
            {
                TableCount = tables.Count,
                ParagraphCount = body.Elements<Paragraph>().Count(),
                ExcludedTables = excludedTables,
            };
        }
        catch (Exception ex)
        {
            result = result with { Errors = new List<string> { $"读取源文档失败: {ex.Message}" } };
            return result;
        }

        // 解析 JSON
        JsonDocument? jsonDoc = null;
        try
        {
            jsonDoc = JsonDocument.Parse(File.ReadAllText(jsonFilePath));
            result = result with { JsonValid = true };
        }
        catch (JsonException ex)
        {
            return result with { Errors = new List<string> { $"JSON 解析失败: {ex.Message}" } };
        }

        using (jsonDoc)
        {
            var root = jsonDoc!.RootElement;
            var errors = new List<string>(result.Errors);
            var warnings = new List<string>();

            if (!root.TryGetProperty("operations", out var opsEl) || opsEl.ValueKind != JsonValueKind.Array)
            {
                errors.Add("缺少 operations 数组");
                return result with { Errors = errors };
            }

            var (operators, opWarnings) = OperatorParser.ParseWithWarnings(opsEl);
            warnings.AddRange(opWarnings);

            // files
            List<RequiredFile> files = new();
            List<string> fileWarnings = new();
            if (root.TryGetProperty("files", out var filesEl))
            {
                var available = new HashSet<string>(PredFiles.ListNames());
                var (fs, fw) = OperatorParser.ParseFiles(filesEl, available);
                files = fs;
                fileWarnings = fw;
            }
            else
            {
                fileWarnings.Add("缺少 files 数组（附件清单）");
            }

            // 类型统计
            var typeCounts = operators
                .GroupBy(o => o.GetType().Name)
                .ToDictionary(g => g.Key, g => g.Count());

            // 表格覆盖
            var covered = new HashSet<int>();
            foreach (var op in operators)
            {
                int? ti = op switch
                {
                    ScalarOp s => s.Location.TableIndex,
                    RecommendOp r => r.Location.TableIndex,
                    ListExpandOp c => c.Ts.TableIndex,
                    GridOp g => g.Ts.TableIndex,
                    ParagraphOp z when z.Location.IsCell => z.Location.TableIndex,
                    UnknownTableOp ug => ug.Ts.TableIndex,
                    _ => null
                };
                if (ti.HasValue && ti.Value >= 0) covered.Add(ti.Value);
            }

            var missing = Enumerable.Range(0, result.TableCount)
                .Except(covered).Except(result.ExcludedTables).OrderBy(x => x).ToList();

            // 索引越界检查
            foreach (var op in operators)
            {
                CheckBounds(op, result.TableCount, result.ParagraphCount, warnings);
            }

            // 评分：覆盖率 60% + files 完整 15% + 无解析警告 25%
            int requiredTables = result.TableCount - result.ExcludedTables.Count;
            double coverage = requiredTables <= 0 ? 1.0
                : (double)(requiredTables - missing.Count) / requiredTables;
            double fileScore = files.Count > 0 ? 1.0 : 0.0;
            double warnScore = opWarnings.Count == 0 ? 1.0
                : Math.Max(0, 1.0 - opWarnings.Count * 0.05);
            var score = (coverage * 0.6 + fileScore * 0.15 + warnScore * 0.25) * 100;

            return result with
            {
                OperationCount = operators.Count,
                FileCount = files.Count,
                CoveredTables = covered.OrderBy(x => x).ToList(),
                MissingTables = missing,
                TypeCounts = typeCounts,
                Warnings = warnings,
                Errors = errors,
                Files = files,
                FileWarnings = fileWarnings,
                Score = Math.Round(score, 1),
            };
        }
    }

    /// <summary>识别可跳过的表格：勾选表（含 ☑/□/☐）或资料清单表（表头含"资料清单""是否已提供""是否适用""附件清单"）</summary>
    private static bool IsSkippableTable(Table t)
    {
        var txt = t.InnerText;
        if (txt.Contains('☑') || txt.Contains('□') || txt.Contains('☐')) return true;
        var firstRow = t.Elements<TableRow>().FirstOrDefault()?.InnerText ?? "";
        return firstRow.Contains("资料清单") || firstRow.Contains("附件清单")
            || firstRow.Contains("是否已提供") || firstRow.Contains("是否适用");
    }

    private static void CheckBounds(FillOperator op, int tableCount, int paraCount, List<string> warnings)
    {
        void Loc(DocLocation loc, string label)
        {
            if (loc.IsCell && loc.TableIndex >= tableCount)
                warnings.Add($"{label}: table_index={loc.TableIndex} 超出表格总数 {tableCount}");
            if (loc.IsParagraph && loc.ParaIndex >= paraCount)
                warnings.Add($"{label}: para_index={loc.ParaIndex} 超出段落总数 {paraCount}");
        }
        switch (op)
        {
            case ScalarOp s: Loc(s.Location, $"type=a ({s.Question})"); break;
            case RecommendOp r: Loc(r.Location, $"type=b ({r.Question})"); break;
            case ParagraphOp z: Loc(z.Location, $"type=z ({z.Question})"); break;
            case ListExpandOp c:
                Loc(c.Ts, $"type=c ts ({c.Entity})");
                Loc(c.Te, $"type=c te ({c.Entity})");
                if (c.Ts.TableIndex != c.Te.TableIndex)
                    warnings.Add($"type=c ({c.Entity}): ts/te 不在同一表格");
                break;
            case GridOp g:
                Loc(g.Ts, $"type=d/e ts ({g.Entity})");
                Loc(g.Te, $"type=d/e te ({g.Entity})");
                break;
            case UnknownTableOp ug:
                Loc(ug.Ts, $"type=g ts ({ug.Description})");
                Loc(ug.Te, $"type=g te ({ug.Description})");
                break;
        }
    }

    /// <summary>
    /// 生成综合报告
    /// </summary>
    public static QualityReport GenerateReport(List<QualityCheckResult> results)
    {
        var success = results.Count(r => r.Passed);
        var failure = results.Count - success;
        var suggestions = new List<string>();

        var missingAny = results.Any(r => r.MissingTables.Count > 0);
        if (missingAny)
            suggestions.Add("[syspt] 存在表格未覆盖 — 加强「表格全覆盖」要求，无对应实体的表用 Type g 补齐");

        if (results.Any(r => r.FileCount == 0))
            suggestions.Add("[syspt] 有文件未输出 files 附件清单 — 确认 syspt 已要求输出 files 数组");

        if (results.SelectMany(r => r.Warnings).Any(w => w.Contains("超出表格总数") || w.Contains("超出段落总数")))
            suggestions.Add("[syspt] 存在坐标越界 — 强调索引从 0 开始且不得超出结构范围");

        if (results.SelectMany(r => r.FileWarnings).Any(w => w.Contains("不在已有文件列表中")))
            suggestions.Add("[syspt] files.map 出现了不在已有文件列表中的文件名 — 强调 map 必须逐字一致或填 null");

        if (failure > results.Count / 2)
            suggestions.Add("[model] 超过半数文件检查失败 — 考虑换用更强模型或拆分长文档");

        return new QualityReport
        {
            TotalFiles = results.Count,
            SuccessCount = success,
            FailureCount = failure,
            AverageScore = results.Count > 0 ? Math.Round(results.Average(r => r.Score), 1) : 0,
            Results = results,
            OptimizationSuggestions = suggestions,
        };
    }

    public static string FormatReport(QualityReport report)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("═══════════════════════════════════════════");
        sb.AppendLine("  Vetting.Copilot 模板生成质量报告");
        sb.AppendLine($"  生成时间: {report.GeneratedAt:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine("═══════════════════════════════════════════");
        sb.AppendLine();
        sb.AppendLine($"总文件数:   {report.TotalFiles}");
        sb.AppendLine($"通过:       {report.SuccessCount}");
        sb.AppendLine($"失败:       {report.FailureCount}");
        sb.AppendLine($"平均质量分: {report.AverageScore:F1}%");
        sb.AppendLine();

        sb.AppendLine("─── 各文件详情 ───");
        foreach (var r in report.Results)
        {
            sb.AppendLine();
            sb.AppendLine($"  [{(r.Passed ? "OK " : "FAIL")}] {r.FileName}");
            sb.AppendLine($"      操作数: {r.OperationCount}, 附件数: {r.FileCount}");
            sb.AppendLine($"      表格: {r.TableCount} (覆盖 {r.CoveredTables.Count}, 可跳过 {r.ExcludedTables.Count}, 缺失 [{string.Join(",", r.MissingTables)}])");
            sb.AppendLine($"      类型: {string.Join(", ", r.TypeCounts.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}"))}");
            sb.AppendLine($"      质量分: {r.Score:F1}%");

            if (r.Files.Count > 0)
            {
                sb.AppendLine("      附件:");
                foreach (var f in r.Files)
                    sb.AppendLine($"        - {f.Raw} → {(f.Map ?? "（未映射）")}{(f.Stamped ? " [盖公章]" : "")}");
            }

            if (r.Warnings.Count > 0)
            {
                sb.AppendLine("      警告:");
                foreach (var w in r.Warnings.Distinct().Take(20))
                    sb.AppendLine($"        - {w}");
                if (r.Warnings.Count > 20) sb.AppendLine($"        ... 共 {r.Warnings.Count} 条");
            }
            if (r.FileWarnings.Count > 0)
            {
                sb.AppendLine("      附件警告:");
                foreach (var w in r.FileWarnings)
                    sb.AppendLine($"        - {w}");
            }
            if (r.Errors.Count > 0)
            {
                sb.AppendLine("      错误:");
                foreach (var e in r.Errors)
                    sb.AppendLine($"        - {e}");
            }
        }

        if (report.OptimizationSuggestions.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("─── 优化建议 ───");
            foreach (var s in report.OptimizationSuggestions.Distinct())
                sb.AppendLine($"  {s}");
        }

        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════════");
        return sb.ToString();
    }
}
