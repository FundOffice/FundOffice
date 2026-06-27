using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FundOffice.Copilot.Configuration;
using FundOffice.Copilot.Models;
using FundOffice.Copilot.Providers;
using Vetting.Copilot;
using Vetting.Copilot.Data;
using Xunit;
using Xunit.Abstractions;

namespace Vetting.Copilot.Tests;

/// <summary>
/// AI 提供者配置（providers.json 反序列化用）
/// </summary>
public record ProviderConfig(string Name, string ProviderType, string ApiKey, string BaseUrl, string Model);

/// <summary>
/// 集成测试：读取 AI 提供者 → 对 test-files 下所有 docx 依次生成 → 质量检查 → 综合报告
///
/// 使用方式：
///   dotnet test --filter "FullyQualifiedName~IntegrationTests.GenerateAndCheckAllTemplates"
/// </summary>
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

        // 回退：从 vetting.db 读取
        var dbPath = @"D:\fundd\data\vetting.db";
        if (File.Exists(dbPath))
        {
            try
            {
                using var db = new VettingDbContext(dbPath, noPassword: true);
                return db.AIProviderConfigs.FindAll()
                    .Select(c => new ProviderConfig(c.Name, c.ProviderType, c.ApiKey, c.BaseUrl, c.Model))
                    .ToList();
            }
            catch { /* 密码不对或库不存在，忽略 */ }
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
        Assert.True(files.Length > 0, $"测试文件目录为空: {TestFilesDir}");
        _out.WriteLine($"找到 {files.Length} 个测试文件:");
        foreach (var f in files) _out.WriteLine($"  - {Path.GetFileName(f)}");
    }

    [Fact]
    public void CheckProvidersConfigured()
    {
        var providers = LoadProviders();
        Assert.True(providers.Count > 0, "未找到 AI 提供者配置");
        foreach (var p in providers) _out.WriteLine($"provider: {p.Name} ({p.ProviderType}) model={p.Model}");
    }

    /// <summary>
    /// 对所有测试文件依次生成模板并检查质量，输出综合报告
    /// </summary>
    [Fact]
    public async Task GenerateAndCheckAllTemplates()
    {
        var files = GetTestFiles();
        Assert.True(files.Length > 0, $"测试文件目录为空: {TestFilesDir}");

        var providers = LoadProviders();
        Assert.True(providers.Count > 0, "未找到 AI 提供者配置");
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

        Assert.True(report.SuccessCount >= 1, $"全部文件检查失败，详见 {reportPath}");
    }

    /// <summary>
    /// 只检查已生成 JSON 的质量（不调用 AI）
    /// </summary>
    [Fact]
    public void CheckExistingTemplates()
    {
        if (!Directory.Exists(OutputDir)) { _out.WriteLine("无输出目录"); return; }

        var results = new List<QualityCheckResult>();
        foreach (var jsonFile in Directory.GetFiles(OutputDir, "*_by[*].json"))
        {
            var name = Path.GetFileNameWithoutExtension(jsonFile);
            var m = Regex.Match(name, @"_by\[(.+)\]$");
            if (!m.Success) continue;
            var baseName = name[..^m.Length];
            var source = Directory.GetFiles(TestFilesDir, baseName + ".*").FirstOrDefault();
            if (source == null) continue;
            results.Add(TemplateQualityChecker.Check(jsonFile, source));
        }

        if (results.Count == 0) { _out.WriteLine("未找到已生成的 JSON"); return; }
        _out.WriteLine(TemplateQualityChecker.FormatReport(TemplateQualityChecker.GenerateReport(results)));
    }

    /// <summary>
    /// 测试常用文件占位创建 + files map 解析 + 复制到 final 的全流程（不调用 AI）。
    /// 过滤：dotnet test --filter "MapAndCopyFlow"
    /// </summary>
    [Fact]
    public void MapAndCopyFlow()
    {
        // 1. 为常用文件名创建空占位
        var commonNames = new[] { "营业执照正本", "管理人登记证明", "审计报告_2024", "审计报告_2025", "审计报告_2026" };
        PredFiles.CreatePlaceholders(commonNames);
        var predNames = new HashSet<string>(PredFiles.ListNames());
        _out.WriteLine($"pred 文件数: {predNames.Count}");
        foreach (var n in predNames) _out.WriteLine($"  - {n}");

        // 扫描件 + 用印件都应存在
        Assert.Contains("营业执照正本.pdf", predNames);
        Assert.Contains("营业执照正本_用印.pdf", predNames);
        Assert.Contains("审计报告_2026.pdf", predNames);

        // 2. 模拟 AI 返回的 files JSON，map 指向 pred 文件名
        var json = """
        {
          "files": [
            {"index": 1, "raw": "营业执照正副本（盖公章）", "map": "营业执照正本.pdf", "stamped": true},
            {"index": 2, "raw": "管理人登记证明", "map": "管理人登记证明.pdf", "stamped": false},
            {"index": 3, "raw": "审计报告", "map": "审计报告_2026.pdf", "stamped": false},
            {"index": 4, "raw": "公司章程", "map": null, "stamped": false}
          ]
        }
        """;
        using var doc = JsonDocument.Parse(json);
        var (files, warnings) = OperatorParser.ParseFiles(doc.RootElement.GetProperty("files"), predNames);
        _out.WriteLine($"解析 files: {files.Count} 个，警告 {warnings.Count} 条");
        Assert.Equal(4, files.Count);
        Assert.Equal("营业执照正本.pdf", files[0].Map);
        Assert.Equal("审计报告_2026.pdf", files[2].Map);
        Assert.Null(files[3].Map);

        // 3. 复制已映射的附件到 final：{Index}.{Map}
        var finalDir = Path.Combine(OutputDir, "final-test");
        if (Directory.Exists(finalDir)) Directory.Delete(finalDir, true);
        var winners = files.Where(f => !string.IsNullOrEmpty(f.Map))
            .Select(f => new KeyValuePair<int, string>(f.Index, f.Map!));
        var logs = new List<string>();
        PredFiles.CopyMappedFiles(finalDir, winners, onLog: logs.Add);
        foreach (var l in logs) _out.WriteLine($"  copy: {l}");

        Assert.True(File.Exists(Path.Combine(finalDir, "附件", "1.营业执照正本.pdf")), "1.营业执照正本.pdf 未复制");
        Assert.True(File.Exists(Path.Combine(finalDir, "附件", "2.管理人登记证明.pdf")), "2.管理人登记证明.pdf 未复制");
        Assert.True(File.Exists(Path.Combine(finalDir, "附件", "3.审计报告_2026.pdf")), "3.审计报告_2026.pdf 未复制");
        Assert.False(File.Exists(Path.Combine(finalDir, "附件", "4.公司章程.pdf")), "map=null 不应复制");
        _out.WriteLine("map + copy 流程测试通过");
    }

    private static readonly Regex Regex = new(@"_by\[(.+)\]$", RegexOptions.Compiled);

    /// <summary>
    /// 对单个文件生成并检查（抽取的公共逻辑）。JSON 解析失败自动重试最多 3 次。
    /// </summary>
    private async Task<QualityCheckResult> GenerateOne(ITokenProvider provider, string sysPrompt, string file)
    {
        var fileName = Path.GetFileName(file);
        _out.WriteLine($"\n>>> 处理: {fileName}");

        try
        {
            var structure = DocOps.ParseDocument(file);
            _out.WriteLine($"    结构: {structure.Length} 字符, {DocOps.GetTableCount(file)} 表格, {DocOps.GetParagraphCount(file)} 段落");

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
                _out.WriteLine($"    [第{attempt}次] AI 返回: {json.Length} 字符");

                // 校验 JSON 可解析且含 operations
                try
                {
                    using var probe = JsonDocument.Parse(json);
                    if (probe.RootElement.TryGetProperty("operations", out _))
                        break; // 合法
                    _out.WriteLine($"    [第{attempt}次] 缺少 operations，重试");
                }
                catch (JsonException ex)
                {
                    _out.WriteLine($"    [第{attempt}次] JSON 无效: {ex.Message}，重试");
                }
                if (attempt == 3) _out.WriteLine("    已达最大重试次数，采用最后结果");
            }

            Directory.CreateDirectory(OutputDir);
            var safeName = Path.GetFileNameWithoutExtension(file);
            var jsonPath = Path.Combine(OutputDir, $"{safeName}_by[{provider.Identifier}].json");
            await File.WriteAllTextAsync(jsonPath, json);

            var result = TemplateQualityChecker.Check(jsonPath, file);
            _out.WriteLine($"    得分: {result.Score:F1}% | 操作: {result.OperationCount} | 附件: {result.FileCount} | 缺失表: [{string.Join(",", result.MissingTables)}]");
            foreach (var w in result.Warnings.Distinct().Take(10)) _out.WriteLine($"      ⚠ {w}");
            foreach (var e in result.Errors) _out.WriteLine($"      ✖ {e}");
            if (result.Files.Count > 0)
            {
                _out.WriteLine("    附件清单:");
                foreach (var f in result.Files)
                    _out.WriteLine($"      - {f.Raw} → {(f.Map ?? "（未映射）")}{(f.Stamped ? " [盖公章]" : "")}");
            }
            return result;
        }
        catch (Exception ex)
        {
            _out.WriteLine($"    异常: {ex.Message}");
            return new QualityCheckResult { FileName = fileName, Errors = new() { ex.Message } };
        }
    }

    /// <summary>
    /// 单文件测试：先验证一个文件能成功生成（含 files 附件清单），成功后再跑全部。
    /// 过滤：dotnet test --filter "GenerateSingleFile"
    /// </summary>
    [Fact]
    public async Task GenerateSingleFile()
    {
        var providers = LoadProviders();
        Assert.True(providers.Count > 0, "未找到 AI 提供者配置");
        var provider = CreateProvider(providers[0]);
        var sysPrompt = await TemplateGenerator.LoadSysptAsync();

        var target = Path.Combine(TestFilesDir, "【中金财富】证券私募尽调问题清单-公司情况（202506）(1).docx");
        Assert.True(File.Exists(target), $"测试文件不存在: {target}");

        var result = await GenerateOne(provider, sysPrompt, target);

        Assert.True(result.JsonValid, "JSON 解析失败");
        Assert.True(result.OperationCount > 0, "未生成任何操作");
        Assert.True(result.MissingTables.Count == 0, $"表格未覆盖: [{string.Join(",", result.MissingTables)}]");
        Assert.True(result.FileCount > 0, "未识别出附件清单（该文档含资料清单表，应输出 files）");
        _out.WriteLine($"\n单文件测试通过，质量分 {result.Score:F1}%，附件 {result.FileCount} 个");
    }
}
