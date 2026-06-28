using System.Text.Json;
using DocumentFormat.OpenXml.Packaging;
using Vetting.Copilot.Models.Info;
using Xunit;
using Xunit.Abstractions;

namespace Vetting.Copilot.Tests;

public class RealDocPromptTest
{
    private readonly ITestOutputHelper _out;
    private const string TestFilesDir = @"D:\Projects\FundOffice\src\Tools\Vetting\test-files";

    public RealDocPromptTest(ITestOutputHelper @out) => _out = @out;

    [Fact]
    public void CheckTestFilesExist()
    {
        var files = Directory.GetFiles(TestFilesDir, "*.docx")
            .Where(f => !Path.GetFileName(f).StartsWith("~$")).ToArray();
        Assert.True(files.Length > 0, "No test files found");
        foreach (var f in files) _out.WriteLine($"  - {Path.GetFileName(f)}");
    }

    [Fact]
    public void ParseAndValidateJson()
    {
        var jsonPath = Directory.GetFiles(@"D:\Projects\FundOffice\src\Tools\Vetting\test-output", "*.json").FirstOrDefault();
        if (jsonPath == null) { _out.WriteLine("No JSON files found"); return; }

        var json = File.ReadAllText(jsonPath);
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("operations", out _), "Missing operations");
        _out.WriteLine($"Parsed {jsonPath}");
    }

    [Fact]
    public void CheckTableCoverage()
    {
        var jsonPath = Directory.GetFiles(@"D:\Projects\FundOffice\src\Tools\Vetting\test-output", "*.json").FirstOrDefault();
        if (jsonPath == null) { _out.WriteLine("No JSON files found"); return; }

        var sourceName = Path.GetFileNameWithoutExtension(jsonPath).Split("_by")[0] + ".docx";
        var sourcePath = Directory.GetFiles(TestFilesDir, sourceName).FirstOrDefault();
        if (sourcePath == null) { _out.WriteLine($"Source not found: {sourceName}"); return; }

        var result = TemplateQualityChecker.Check(jsonPath, sourcePath);
        _out.WriteLine($"File: {result.FileName}");
        _out.WriteLine($"Tables: {result.TableCount}, Covered: {result.CoveredTables.Count}, Missing: [{string.Join(",", result.MissingTables)}]");
        _out.WriteLine($"Score: {result.Score}%");
        Assert.True(result.JsonValid, "JSON invalid");
    }
}
