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

public record ProviderConfig(string Name, string ProviderType, string ApiKey, string BaseUrl, string Model);

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
        _ => new OpenAITokenProvider(new OpenAIOptions
        { Identifier = c.Name, ApiKey = c.ApiKey, BaseUrl = c.BaseUrl, Model = c.Model }),
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
        var sysPrompt = await TemplateGenerator.LoadSysptAsync();

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
        var sysPrompt = await TemplateGenerator.LoadSysptAsync();

        var target = Path.Combine(TestFilesDir, "附件二：私募管理人调查问卷.docx");
        Assert.True(File.Exists(target), $"Test file not found: {target}");

        var result = await GenerateOne(provider, sysPrompt, target);

        Assert.True(result.JsonValid, "JSON parse failed");
        Assert.True(result.OperationCount > 0, "No operations generated");
        _out.WriteLine($"\nSingle file test passed, score {result.Score:F1}%, files {result.FileCount}");
    }
}
