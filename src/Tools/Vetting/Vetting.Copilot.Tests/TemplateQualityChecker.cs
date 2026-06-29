using System.Text.Json;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Vetting.Copilot.Tests;

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
    public List<int> ExcludedTables { get; init; } = [];
    public Dictionary<string, int> TypeCounts { get; init; } = [];
    public List<string> Warnings { get; init; } = [];
    public List<string> Errors { get; init; } = [];
    public List<RequiredFile> Files { get; init; } = [];
    public List<string> FileWarnings { get; init; } = [];
    public double Score { get; init; }
    public bool Passed => Errors.Count == 0 && MissingTables.Count == 0;
}

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

public static class TemplateQualityChecker
{
    public static QualityCheckResult Check(string jsonFilePath, string sourceFilePath)
    {
        var result = new QualityCheckResult { FileName = Path.GetFileName(sourceFilePath) };
        if (!File.Exists(jsonFilePath)) return result with { Errors = new List<string> { "JSON file not found" } };

        List<int> excludedTables = new();
        try
        {
            using var doc = WordprocessingDocument.Open(sourceFilePath, false);
            var body = doc.MainDocumentPart!.Document.Body!;
            var tables = body.Elements<Table>().ToList();
            for (int i = 0; i < tables.Count; i++) if (IsSkippableTable(tables[i])) excludedTables.Add(i);
            result = result with { TableCount = tables.Count, ParagraphCount = body.Elements<Paragraph>().Count(), ExcludedTables = excludedTables };
        }
        catch (Exception ex) { return result with { Errors = new List<string> { "Read source failed: " + ex.Message } }; }

        JsonDocument? jsonDoc = null;
        try { jsonDoc = JsonDocument.Parse(File.ReadAllText(jsonFilePath)); result = result with { JsonValid = true }; }
        catch (JsonException ex) { return result with { Errors = new List<string> { "JSON parse failed: " + ex.Message } }; }

        using (jsonDoc)
        {
            var root = jsonDoc!.RootElement;
            var errors = new List<string>(result.Errors);
            var warnings = new List<string>();
            if (!root.TryGetProperty("operations", out var opsEl) || opsEl.ValueKind != JsonValueKind.Array) return result with { Errors = new List<string> { "Missing operations array" } };

            var (operators, opWarnings) = OperatorParser.ParseWithWarnings(opsEl);
            warnings.AddRange(opWarnings);

            List<RequiredFile> files = new(); List<string> fileWarnings = new();
            if (root.TryGetProperty("files", out var filesEl))
            {
                var available = new HashSet<string>(PredFiles.ListNames());
                var (fs, fw) = OperatorParser.ParseFiles(filesEl, available);
                files = fs; fileWarnings = fw;
            }
            else fileWarnings.Add("Missing files array");

            var typeCounts = operators.GroupBy(o => o.GetType().Name).ToDictionary(g => g.Key, g => g.Count());
            var covered = new HashSet<int>();
            foreach (var op in operators)
            {
                int? ti = op switch
                {
                    ScalarOp s => s.Location.Table,
                    RecommendOp r => r.Range.Table,
                    ListExpandOp c => c.Range.Table,
                    GridOp g => g.Range.Table,
                    ParagraphOp z when z.Location.IsCell => z.Location.Table,
                    UnknownTableOp ug => ug.Range.Table,
                    _ => null
                };
                if (ti.HasValue && ti.Value >= 0) covered.Add(ti.Value);
            }

            var missing = Enumerable.Range(0, result.TableCount).Except(covered).Except(result.ExcludedTables).OrderBy(x => x).ToList();
            foreach (var op in operators) CheckBounds(op, result.TableCount, result.ParagraphCount, warnings);

            int requiredTables = result.TableCount - result.ExcludedTables.Count;
            double coverage = requiredTables <= 0 ? 1.0 : (double)(requiredTables - missing.Count) / requiredTables;
            double fileScore = files.Count > 0 ? 1.0 : 0.0;
            double warnScore = opWarnings.Count == 0 ? 1.0 : Math.Max(0, 1.0 - opWarnings.Count * 0.05);
            var score = (coverage * 0.6 + fileScore * 0.15 + warnScore * 0.25) * 100;

            return result with { OperationCount = operators.Count, FileCount = files.Count, CoveredTables = covered.OrderBy(x => x).ToList(), MissingTables = missing, TypeCounts = typeCounts, Warnings = warnings, Errors = errors, Files = files, FileWarnings = fileWarnings, Score = Math.Round(score, 1) };
        }
    }

    private static bool IsSkippableTable(Table t)
    {
        var txt = t.InnerText;
        if (txt.Contains('\u25a1') || txt.Contains('\u2610') || txt.Contains('\u2611') || txt.Contains('\u2612')) return true;
        var firstRow = t.Elements<TableRow>().FirstOrDefault()?.InnerText ?? "";
        if (firstRow.Contains("资料清单") || firstRow.Contains("附件清单") || firstRow.Contains("是否已提供") || firstRow.Contains("是否适用")) return true;

        // 单行标题表：只有 1 行（分区标题，无实际填写内容）
        if (t.Elements<TableRow>().Count() <= 1) return true;

        // 静态参考/映射表：策略分类标准、风控要求
        if (firstRow.Contains("一级分类") && firstRow.Contains("二级分类")) return true;
        if (firstRow.Contains("风控项目") || firstRow.Contains("风控要求")) return true;

        return false;
    }

    private static void CheckBounds(FillOperator op, int tableCount, int paraCount, List<string> warnings)
    {
        void Loc(Location loc, string label)
        {
            if (loc.IsCell && loc.Table >= tableCount) warnings.Add(label + ": table=" + loc.Table + " exceeds " + tableCount);
            if (loc.IsParagraph && loc.Para >= paraCount) warnings.Add(label + ": para=" + loc.Para + " exceeds " + paraCount);
        }
        switch (op)
        {
            case ScalarOp s: Loc(s.Location, "type=a"); break;
            case RecommendOp r: Loc(r.Range.Start, "type=b"); break;
            case ParagraphOp z: Loc(z.Location, "type=z"); break;
            case ListExpandOp c: Loc(c.Range.Start, "type=c start"); Loc(c.Range.End, "type=c end"); break;
            case GridOp g: Loc(g.Range.Start, "type=d/e start"); Loc(g.Range.End, "type=d/e end"); break;
            case UnknownTableOp ug: Loc(ug.Range.Start, "type=g start"); Loc(ug.Range.End, "type=g end"); break;
        }
    }

    public static QualityReport GenerateReport(List<QualityCheckResult> results)
    {
        var success = results.Count(r => r.Passed);
        var failure = results.Count - success;
        var suggestions = new List<string>();
        if (results.Any(r => r.MissingTables.Count > 0)) suggestions.Add("Tables not covered");
        if (results.Any(r => r.FileCount == 0)) suggestions.Add("Files not output");
        if (results.SelectMany(r => r.Warnings).Any(w => w.Contains("exceeds"))) suggestions.Add("Index out of bounds");
        if (failure > results.Count / 2) suggestions.Add("More than half failed");
        return new QualityReport { TotalFiles = results.Count, SuccessCount = success, FailureCount = failure, AverageScore = results.Count > 0 ? Math.Round(results.Average(r => r.Score), 1) : 0, Results = results, OptimizationSuggestions = suggestions };
    }

    public static string FormatReport(QualityReport report)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Total: " + report.TotalFiles + ", Pass: " + report.SuccessCount + ", Fail: " + report.FailureCount + ", Avg: " + report.AverageScore + "%");
        foreach (var r in report.Results)
        {
            sb.AppendLine("  [" + (r.Passed ? "OK" : "FAIL") + "] " + r.FileName + ": ops=" + r.OperationCount + ", files=" + r.FileCount + ", tables=" + r.TableCount + "(covered=" + r.CoveredTables.Count + "), score=" + r.Score + "%");
            if (r.Errors.Count > 0) sb.AppendLine("    Errors: " + string.Join("; ", r.Errors));
        }
        return sb.ToString();
    }
}
