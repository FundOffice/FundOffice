using System.Text;
using System.Text.Json;
using FundOffice.Copilot.Configuration;
using FundOffice.Copilot.Models;
using FundOffice.Copilot.Providers;
using Vetting.Copilot;
using Vetting.Copilot.Data;
using Xunit;
using Xunit.Abstractions;

namespace Vetting.Copilot.Tests;

public record ProviderConfig(string Name, string ProviderType, string ApiKey, string BaseUrl, string Model, OpenAIApiVersion? ApiVersion = null);

public class IntegrationTests
{
    private readonly ITestOutputHelper _out;
    private const string TestFilesDir = @"D:\Projects\FundOffice\src\Tools\Vetting\test-files";
    private const string OutputDir = @"D:\Projects\FundOffice\src\Tools\Vetting\test-output";

    public IntegrationTests(ITestOutputHelper @out) => _out = @out;

    private static List<ProviderConfig> LoadProviders()
    {
        var jsonConfigPath = Path.Combine(TestFilesDir, "providers.json");
        if (File.Exists(jsonConfigPath))
        {
            var json = File.ReadAllText(jsonConfigPath);
            var configs = JsonSerializer.Deserialize<List<ProviderConfig>>(json);
            if (configs is { Count: > 0 }) return configs;
        }
        return new();
    }

    private static ITokenProvider CreateProvider(ProviderConfig c) => c.ProviderType switch
    {
        "Anthropic" => new AnthropicTokenProvider(new AnthropicOptions
        { Identifier = c.Name, ApiKey = c.ApiKey, BaseUrl = c.BaseUrl, Model = c.Model }),
        _ => (c.ApiVersion ?? OpenAIApiVersion.ChatCompletions) switch
        {
            OpenAIApiVersion.Responses => new OpenAIResponsesProvider(new OpenAIOptions
            { Identifier = c.Name, ApiKey = c.ApiKey, BaseUrl = c.BaseUrl, Model = c.Model, ApiVersion = OpenAIApiVersion.Responses }),
            _ => new OpenAITokenProvider(new OpenAIOptions
            { Identifier = c.Name, ApiKey = c.ApiKey, BaseUrl = c.BaseUrl, Model = c.Model }),
        },
    };

    private static string[] GetTestFiles() =>
        Directory.Exists(TestFilesDir)
            ? Directory.GetFiles(TestFilesDir, "*.docx").Where(f => !Path.GetFileName(f).StartsWith("~$")).ToArray()
            : Array.Empty<string>();

    [Fact]
    public void CheckTestFilesExist()
    {
        var files = GetTestFiles();
        Assert.True(files.Length > 0, $"Test files dir empty: {TestFilesDir}");
        _out.WriteLine($"Found {files.Length} test files");
        foreach (var f in files) _out.WriteLine($"  - {Path.GetFileName(f)}");
    }

    [Fact]
    public void CheckProvidersConfigured()
    {
        var providers = LoadProviders();
        Assert.True(providers.Count > 0, "No AI providers configured");
        foreach (var p in providers) _out.WriteLine($"provider: {p.Name} ({p.ProviderType}) model={p.Model}");
    }

    [Fact]
    public async Task GenerateAndCheckAllTemplates()
    {
        var files = GetTestFiles();
        Assert.True(files.Length > 0, $"Test files dir empty: {TestFilesDir}");

        var providers = LoadProviders();
        Assert.True(providers.Count > 0, "No AI providers configured");
        var provider = CreateProvider(providers[0]);
        var sysPrompt = await PromptService.LoadSysptAsync();

        Directory.CreateDirectory(OutputDir);
        var results = new List<QualityCheckResult>();

        foreach (var file in files)
        {
            var result = await GenerateOne(provider, sysPrompt, file);
            results.Add(result);
        }

        var report = TemplateQualityChecker.GenerateReport(results);
        var reportText = TemplateQualityChecker.FormatReport(report);
        var reportPath = Path.Combine(OutputDir, "quality-report.txt");
        await File.WriteAllTextAsync(reportPath, reportText);

        _out.WriteLine("");
        _out.WriteLine(reportText);

        Assert.True(report.SuccessCount >= 1, $"All files failed, see {reportPath}");
    }

    [Fact]
    public void CheckExistingTemplates()
    {
        if (!Directory.Exists(OutputDir)) { _out.WriteLine("No output dir"); return; }

        var results = new List<QualityCheckResult>();
        foreach (var jsonFile in Directory.GetFiles(OutputDir, "*_by[*].json"))
        {
            var name = Path.GetFileNameWithoutExtension(jsonFile);
            var m = System.Text.RegularExpressions.Regex.Match(name, @"_by\[(.+)\]$");
            if (!m.Success) continue;
            var baseName = name[..^m.Length];
            var source = Directory.GetFiles(TestFilesDir, baseName + ".*").FirstOrDefault();
            if (source == null) continue;
            results.Add(TemplateQualityChecker.Check(jsonFile, source));
        }

        if (results.Count == 0) { _out.WriteLine("No generated JSON found"); return; }
        _out.WriteLine(TemplateQualityChecker.FormatReport(TemplateQualityChecker.GenerateReport(results)));
    }

    private async Task<QualityCheckResult> GenerateOne(ITokenProvider provider, string sysPrompt, string file)
    {
        var fileName = Path.GetFileName(file);
        _out.WriteLine($"\n>>> Processing: {fileName}");

        try
        {
            var structure = DocOps.ParseDocument(file);
            _out.WriteLine($"    Structure: {structure.Length} chars, {DocOps.GetTableCount(file)} tables, {DocOps.GetParagraphCount(file)} paragraphs");

            var messages = new[] { ChatMessage.System(sysPrompt), ChatMessage.User(structure + PredFiles.BuildPromptSection()) };
            var options = new ChatOptions
            {
                AdditionalProperties = new Dictionary<string, object> { ["response_format"] = new { type = "json_object" } }
            };

            string json = "";
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                var sb = new StringBuilder();
                await foreach (var token in provider.ChatCompletionStreamAsync(messages, options: options))
                {
                    if (token is TextDelta td) sb.Append(td.Text);
                }
                json = sb.ToString().Trim();
                _out.WriteLine($"    [Attempt {attempt}] AI returned: {json.Length} chars");

                try
                {
                    using var probe = JsonDocument.Parse(json);
                    if (probe.RootElement.TryGetProperty("operations", out _))
                        break;
                    _out.WriteLine($"    [Attempt {attempt}] Missing operations, retry");
                }
                catch (JsonException ex)
                {
                    _out.WriteLine($"    [Attempt {attempt}] Invalid JSON: {ex.Message}, retry");
                }
            }

            Directory.CreateDirectory(OutputDir);
            var safeName = Path.GetFileNameWithoutExtension(file);
            var jsonPath = Path.Combine(OutputDir, $"{safeName}_by[{provider.Identifier}].json");
            await File.WriteAllTextAsync(jsonPath, json);

            var result = TemplateQualityChecker.Check(jsonPath, file);
            _out.WriteLine($"    Score: {result.Score:F1}% | Ops: {result.OperationCount} | Files: {result.FileCount} | Missing tables: [{string.Join(",", result.MissingTables)}]");
            return result;
        }
        catch (Exception ex)
        {
            _out.WriteLine($"    Error: {ex.Message}");
            return new QualityCheckResult { FileName = fileName, Errors = new List<string> { ex.Message } };
        }
    }

    [Fact]
    public async Task GenerateSingleFile()
    {
        var providers = LoadProviders();
        Assert.True(providers.Count > 0, "No AI providers configured");
        var provider = CreateProvider(providers[0]);
        var sysPrompt = await PromptService.LoadSysptAsync();

        var target = Path.Combine(TestFilesDir, "附件二：私募管理人调查问卷.docx");
        Assert.True(File.Exists(target), $"Test file not found: {target}");

        var result = await GenerateOne(provider, sysPrompt, target);

        Assert.True(result.JsonValid, "JSON parse failed");
        Assert.True(result.OperationCount > 0, "No operations generated");
        _out.WriteLine($"\nSingle file test passed, score {result.Score:F1}%, files {result.FileCount}");
    }

    private const string TestFiles2Dir = @"D:\Projects\FundOffice\src\Tools\Vetting\test-files2";
    private const string Output2Dir = @"D:\Projects\FundOffice\src\Tools\Vetting\test-output2";

    private static string[] GetTestFiles2() =>
        Directory.Exists(TestFiles2Dir)
            ? Directory.GetFiles(TestFiles2Dir, "*.docx").Where(f => !Path.GetFileName(f).StartsWith("~$")).ToArray()
            : Array.Empty<string>();

    [Fact]
    public async Task GenerateTestFiles2_AndCheckQuality()
    {
        var files = GetTestFiles2();
        Assert.True(files.Length > 0, $"Test files2 dir empty: {TestFiles2Dir}");

        var providers = LoadProviders();
        Assert.True(providers.Count > 0, "No AI providers configured");
        var provider = CreateProvider(providers[0]);
        var sysPrompt = await PromptService.LoadSysptAsync();

        Directory.CreateDirectory(Output2Dir);
        var results = new List<QualityCheckResult>();

        foreach (var file in files)
        {
            var result = await GenerateOne2(provider, sysPrompt, file);
            results.Add(result);
        }

        var report = TemplateQualityChecker.GenerateReport(results);
        var reportText = TemplateQualityChecker.FormatReport(report);

        // 详细分析 type g 和 prop=null
        var detailSb = new System.Text.StringBuilder();
        detailSb.AppendLine("========== test-files2 Quality Report ==========");
        detailSb.AppendLine(reportText);
        detailSb.AppendLine();

        int totalG = 0, totalNullProp = 0, totalOps = 0;
        foreach (var jsonFile in Directory.GetFiles(Output2Dir, "*.json"))
        {
            detailSb.AppendLine($"--- {Path.GetFileName(jsonFile)} ---");
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(jsonFile));
                if (!doc.RootElement.TryGetProperty("operations", out var opsEl)) continue;
                var (operators, warnings) = OperatorParser.ParseWithWarnings(opsEl);
                totalOps += operators.Count;

                // Count type g
                var typeGOps = operators.OfType<UnknownTableOp>().ToList();
                totalG += typeGOps.Count;
                detailSb.AppendLine($"  Operations: {operators.Count}, Type g: {typeGOps.Count}");
                foreach (var g in typeGOps)
                {
                    var nullProps = g.Properties.Where(p => p.Prop == null).ToList();
                    var mappedProps = g.Properties.Where(p => p.Prop != null).ToList();
                    detailSb.AppendLine($"  [g] {g.Description} | table={g.Range.Table} rows={g.Range.Start.Row}-{g.Range.End.Row} | props={g.Properties.Count} (mapped={mappedProps.Count}, null={nullProps.Count})");
                    if (nullProps.Count > 0)
                        detailSb.AppendLine($"       null props: {string.Join(", ", nullProps.Select(p => p.Header))}");
                }

                // Count prop=null in all non-g operations
                foreach (var op in operators.Where(o => o is not UnknownTableOp))
                {
                    List<PropItem>? props = op switch
                    {
                        ListExpandOp c => c.Properties,
                        GridOp ge => ge.Properties,
                        _ => null
                    };
                    if (props == null) continue;
                    var nulls = props.Where(p => p.Prop == null).ToList();
                    if (nulls.Count > 0)
                    {
                        totalNullProp += nulls.Count;
                        var entity = op switch { ListExpandOp c2 => c2.Entity, GridOp ge2 => ge2.Entity, _ => "?" };
                        detailSb.AppendLine($"  [prop=null] entity={entity}: {string.Join(", ", nulls.Select(p => p.Header))}");
                    }
                }

                // Warnings
                foreach (var w in warnings)
                    detailSb.AppendLine($"  [WARN] {w}");
            }
            catch (Exception ex)
            {
                detailSb.AppendLine($"  [ERROR] {ex.Message}");
            }
            detailSb.AppendLine();
        }

        detailSb.AppendLine("========== Summary ==========");
        detailSb.AppendLine($"Total files: {results.Count}, Total operations: {totalOps}");
        detailSb.AppendLine($"Type g count: {totalG} ({(totalOps > 0 ? (totalG * 100.0 / totalOps) : 0):F1}%)");
        detailSb.AppendLine($"Prop=null in mapped ops: {totalNullProp}");

        var detailPath = Path.Combine(Output2Dir, "quality-detail.txt");
        await File.WriteAllTextAsync(detailPath, detailSb.ToString());

        _out.WriteLine("");
        _out.WriteLine(detailSb.ToString());

        Assert.True(report.SuccessCount >= 1, $"All test-files2 failed, see {detailPath}");
    }

    [Fact]
    public void DumpMissingTables()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var file in GetTestFiles2())
        {
            sb.AppendLine($"========== {Path.GetFileName(file)} ==========");
            sb.AppendLine(DocOps.ParseDocument(file));
            sb.AppendLine();
        }
        var outPath = Path.Combine(Output2Dir, "all-structures.txt");
        File.WriteAllText(outPath, sb.ToString());
        _out.WriteLine($"Dumped to {outPath}");
    }

    private async Task<QualityCheckResult> GenerateOne2(ITokenProvider provider, string sysPrompt, string file)
    {
        var fileName = Path.GetFileName(file);
        _out.WriteLine($"\n>>> [test-files2] Processing: {fileName}");

        try
        {
            var structure = DocOps.ParseDocument(file);
            _out.WriteLine($"    Structure: {structure.Length} chars, {DocOps.GetTableCount(file)} tables, {DocOps.GetParagraphCount(file)} paragraphs");

            var messages = new[] { ChatMessage.System(sysPrompt), ChatMessage.User(structure + PredFiles.BuildPromptSection()) };
            var options = new ChatOptions
            {
                AdditionalProperties = new Dictionary<string, object> { ["response_format"] = new { type = "json_object" } }
            };

            string json = "";
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                var sb = new StringBuilder();
                await foreach (var token in provider.ChatCompletionStreamAsync(messages, options: options))
                {
                    if (token is TextDelta td) sb.Append(td.Text);
                }
                json = sb.ToString().Trim();
                _out.WriteLine($"    [Attempt {attempt}] AI returned: {json.Length} chars");

                try
                {
                    using var probe = JsonDocument.Parse(json);
                    if (probe.RootElement.TryGetProperty("operations", out _))
                        break;
                    _out.WriteLine($"    [Attempt {attempt}] Missing operations, retry");
                }
                catch (JsonException ex)
                {
                    _out.WriteLine($"    [Attempt {attempt}] Invalid JSON: {ex.Message}, retry");
                }
            }

            Directory.CreateDirectory(Output2Dir);
            var safeName = Path.GetFileNameWithoutExtension(file);
            var jsonPath = Path.Combine(Output2Dir, $"{safeName}_by[{provider.Identifier}].json");
            await File.WriteAllTextAsync(jsonPath, json);

            var result = TemplateQualityChecker.Check(jsonPath, file);
            _out.WriteLine($"    Score: {result.Score:F1}% | Ops: {result.OperationCount} | Type g: {result.TypeCounts.GetValueOrDefault("UnknownTableOp", 0)} | Missing tables: [{string.Join(",", result.MissingTables)}]");
            return result;
        }
        catch (Exception ex)
        {
            _out.WriteLine($"    Error: {ex.Message}");
            return new QualityCheckResult { FileName = fileName, Errors = new List<string> { ex.Message } };
        }
    }
}
