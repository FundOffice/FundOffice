using System.Text.Json;
using Vetting.Copilot;
using Vetting.Copilot.Data;
using Xunit;

namespace Vetting.Copilot.Tests;

/// <summary>
/// AI 提供者配置 — 从 Vetting 数据库读取
/// </summary>
public record ProviderConfig(string Name, string ProviderType, string ApiKey, string BaseUrl, string Model);

/// <summary>
/// 集成测试：读取 AI 提供者 → 生成模板 → 检查质量 → 输出报告
///
/// 使用方式：
///   1. 将尽调 .docx 文件放到 F:\Project\FundOffice\src\Tools\Vetting\test-files\ 目录
///   2. 确保 Vetting 应用已配置过 AI 提供者（data/vetting.db 中有数据）
///   3. 运行: dotnet test --filter "FullyQualifiedName~Vetting.Copilot.Tests.IntegrationTests"
/// </summary>
public class IntegrationTests
{
    private const string TestFilesDir = @"D:\Projects\FundOffice\src\Tools\Vetting\test-files";
    private const string OutputDir = @"D:\Projects\FundOffice\src\Tools\Vetting\test-output";

    /// <summary>
    /// 从 Vetting 数据库读取已配置的 AI 提供者
    /// </summary>
    private static List<ProviderConfig> LoadProviders()
    {
        var providers = new List<ProviderConfig>();

        // 尝试多个可能的数据库路径
        var testFilesDir = @"D:\Projects\FundOffice\src\Tools\Vetting\test-files";
        var possiblePaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "data", "vetting.db"),
            Path.Combine(Directory.GetCurrentDirectory(), "data", "vetting.db"),
            @"D:\Projects\FundOffice\src\Tools\Vetting\Vetting\bin\Debug\net10.0-windows\data\vetting.db",
            @"D:\Projects\FundOffice\src\Tools\Vetting\vetting.db",
        };

        foreach (var dbPath in possiblePaths)
        {
            if (!File.Exists(dbPath)) continue;

            try
            {
                using var db = new VettingDbContext(dbPath, noPassword: true);
                foreach (var config in db.AIProviderConfigs.FindAll())
                {
                    providers.Add(new ProviderConfig(config.Name, config.ProviderType, config.ApiKey, config.BaseUrl, config.Model));
                }
                if (providers.Count > 0) return providers;
            }
            catch
            {
                // 密码不对，尝试下一个路径
            }
        }

        // 如果数据库读取失败，尝试读取 JSON 配置文件
        var jsonConfigPath = Path.Combine(TestFilesDir, "providers.json");
        if (File.Exists(jsonConfigPath))
        {
            var json = File.ReadAllText(jsonConfigPath);
            var configs = JsonSerializer.Deserialize<List<ProviderConfig>>(json);
            if (configs != null) providers.AddRange(configs);
        }

        return providers;
    }

    /// <summary>
    /// 获取测试文件列表
    /// </summary>
    private static string[] GetTestFiles()
    {
        if (!Directory.Exists(TestFilesDir))
        {
            Directory.CreateDirectory(TestFilesDir);
            return [];
        }

        return Directory.GetFiles(TestFilesDir, "*.docx")
            .Where(f => !Path.GetFileName(f).StartsWith("~$"))
            .ToArray();
    }

    /// <summary>
    /// 创建 ITokenProvider 实例
    /// </summary>
    private static FundOffice.Copilot.Providers.ITokenProvider CreateProvider(ProviderConfig config)
    {
        return config.ProviderType switch
        {
            "Anthropic" => new FundOffice.Copilot.Providers.AnthropicTokenProvider(
                new FundOffice.Copilot.Configuration.AnthropicOptions
                {
                    Identifier = config.Name,
                    ApiKey = config.ApiKey,
                    BaseUrl = config.BaseUrl,
                    Model = config.Model
                }),
            _ => new FundOffice.Copilot.Providers.OpenAITokenProvider(
                new FundOffice.Copilot.Configuration.OpenAIOptions
                {
                    Identifier = config.Name,
                    ApiKey = config.ApiKey,
                    BaseUrl = config.BaseUrl,
                    Model = config.Model
                }),
        };
    }

    [Fact]
    public void CheckTestFilesExist()
    {
        var files = GetTestFiles();
        Assert.True(files.Length > 0,
            $"测试文件目录为空: {TestFilesDir}\n请将尽调 .docx 文件放到此目录");
    }

    [Fact]
    public void CheckProvidersConfigured()
    {
        var providers = LoadProviders();
        Assert.True(providers.Count > 0,
            "未找到 AI 提供者配置。请确保 Vetting 应用已配置 AI 提供者，或在 test-files/providers.json 中手动配置");
    }

    /// <summary>
    /// 主测试：对所有测试文件生成模板并检查质量，输出综合报告
    /// </summary>
    [Fact]
    public async Task GenerateAndCheckAllTemplates()
    {
        var files = GetTestFiles();
        Assert.True(files.Length > 0, $"测试文件目录为空: {TestFilesDir}");

        var providers = LoadProviders();
        Assert.True(providers.Count > 0, "未找到 AI 提供者配置");

        // 使用第一个可用的提供者
        var provider = providers[0];
        var tokenProvider = CreateProvider(provider);

        Directory.CreateDirectory(OutputDir);
        var results = new List<QualityCheckResult>();

        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            var safeName = Path.GetFileNameWithoutExtension(file);
            var tplDir = Path.Combine(OutputDir, safeName);
            Directory.CreateDirectory(tplDir);

            // 复制源文件到输出目录
            var srcCopy = Path.Combine(tplDir, fileName);
            File.Copy(file, srcCopy, overwrite: true);

            // 调用 TemplateGenerator
            var generator = new TemplateGenerator(tokenProvider);
            var genResult = await generator.GenerateAsync(
                srcCopy,
                tplDir,
                progress: line => Console.WriteLine($"  [{fileName}] {line}"));

            Assert.True(genResult.Success,
                $"文件 {fileName} 模板生成失败: {genResult.ErrorMessage}");

            // 质量检查
            var jsonPath = Path.Combine(tplDir,
                $"{safeName}_by[{provider.Name}].json");
            var tplPath = Path.Combine(tplDir,
                $"{safeName}_by[{provider.Name}].docx");

            var checkResult = TemplateQualityChecker.Check(jsonPath, tplPath);
            results.Add(checkResult);

            Console.WriteLine($"  [{fileName}] 得分: {checkResult.Score:F1}%, " +
                $"操作: {checkResult.OperationCount}, 占位符: {checkResult.PlaceholderCount}");
        }

        // 生成报告
        var report = TemplateQualityChecker.GenerateReport(results);
        var reportText = TemplateQualityChecker.FormatReport(report);
        var reportPath = Path.Combine(OutputDir, "quality-report.txt");
        File.WriteAllText(reportPath, reportText);

        Console.WriteLine();
        Console.WriteLine(reportText);

        // 至少一半文件应成功
        Assert.True(report.SuccessCount >= report.TotalFiles / 2,
            $"成功率过低: {report.SuccessCount}/{report.TotalFiles}");
    }

    /// <summary>
    /// 检查已生成模板的质量（不调用 AI，只分析已有结果）
    /// </summary>
    [Fact]
    public void CheckExistingTemplates()
    {
        if (!Directory.Exists(OutputDir))
        {
            Console.WriteLine($"输出目录不存在: {OutputDir}");
            return;
        }

        var results = new List<QualityCheckResult>();

        foreach (var dir in Directory.GetDirectories(OutputDir))
        {
            var jsonFiles = Directory.GetFiles(dir, "*.json");
            foreach (var jsonFile in jsonFiles)
            {
                var tplFile = Path.ChangeExtension(jsonFile, ".docx");
                if (File.Exists(tplFile))
                {
                    var result = TemplateQualityChecker.Check(jsonFile, tplFile);
                    results.Add(result);
                }
            }
        }

        if (results.Count == 0)
        {
            Console.WriteLine("未找到已生成的模板文件");
            return;
        }

        var report = TemplateQualityChecker.GenerateReport(results);
        Console.WriteLine(TemplateQualityChecker.FormatReport(report));
    }

    /// <summary>
    /// 单文件测试：测试指定文件的模板生成
    /// </summary>
    [Theory]
    [InlineData("test1.docx")]
    public async Task GenerateSingleTemplate(string fileName)
    {
        var filePath = Path.Combine(TestFilesDir, fileName);
        Assert.True(File.Exists(filePath), $"测试文件不存在: {filePath}");

        var providers = LoadProviders();
        Assert.True(providers.Count > 0, "未找到 AI 提供者配置");

        var provider = providers[0];
        var tokenProvider = CreateProvider(provider);

        var safeName = Path.GetFileNameWithoutExtension(fileName);
        var tplDir = Path.Combine(OutputDir, safeName);
        Directory.CreateDirectory(tplDir);

        var srcCopy = Path.Combine(tplDir, fileName);
        File.Copy(filePath, srcCopy, overwrite: true);

        var generator = new TemplateGenerator(tokenProvider);
        var result = await generator.GenerateAsync(srcCopy, tplDir,
            progress: line => Console.WriteLine(line));

        Assert.True(result.Success, $"生成失败: {result.ErrorMessage}");
        Assert.True(result.OperationCount > 0, "操作数为 0");
        Assert.NotNull(result.Json);

        // 质量检查
        var jsonPath = Path.Combine(tplDir, $"{safeName}_by[{provider.Name}].json");
        var tplPath = Path.Combine(tplDir, $"{safeName}_by[{provider.Name}].docx");
        var checkResult = TemplateQualityChecker.Check(jsonPath, tplPath);

        Console.WriteLine($"得分: {checkResult.Score:F1}%");
        Console.WriteLine($"操作: {checkResult.OperationCount}, 有效: {checkResult.ValidOperations}");
        Console.WriteLine($"占位符: {checkResult.PlaceholderCount}");

        if (checkResult.Errors.Count > 0)
        {
            Console.WriteLine("错误:");
            foreach (var e in checkResult.Errors)
                Console.WriteLine($"  - {e}");
        }
    }
}
