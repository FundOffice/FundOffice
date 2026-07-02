using FundOffice.Vetting.Services;

namespace TestVetting;

[TestClass]
public class DocOpsTests
{
    private static readonly string TestDocx = @"F:\vettin\1.尽职调查报告模板-2024.08.20.docx";

    [TestMethod]
    public void ParseDocument_ReturnsContent()
    {
        if (!File.Exists(TestDocx))
        {
            Console.WriteLine($"测试文件不存在: {TestDocx}");
            return;
        }

        var result = DocOps.ParseDocument(TestDocx);

        Console.WriteLine(result[..Math.Min(3000, result.Length)]);

        Assert.IsFalse(string.IsNullOrWhiteSpace(result), "ParseDocument 不应返回空");
        Assert.IsTrue(result.Contains("P["), "应包含段落索引");
    }

    [TestMethod]
    public void ParseDocument_HasCorrectFormat()
    {
        if (!File.Exists(TestDocx)) return;

        var result = DocOps.ParseDocument(TestDocx);
        var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // 至少应该有段落或表格输出
        Assert.IsTrue(lines.Length > 0, "应有输出行");

        // 检查格式: P[N] 或 T[N]
        var hasParagraph = lines.Any(l => l.StartsWith("P["));
        var hasTable = lines.Any(l => l.StartsWith("T["));
        Assert.IsTrue(hasParagraph || hasTable, "应包含 P[N] 或 T[N] 格式");
    }

    [TestMethod]
    public void ParseDocument_WriteOutput()
    {
        if (!File.Exists(TestDocx)) return;

        var result = DocOps.ParseDocument(TestDocx);
        var outPath = Path.Combine(AppContext.BaseDirectory, "parse_output.txt");
        File.WriteAllText(outPath, result);
        Console.WriteLine($"输出已写入: {outPath}");
        Console.WriteLine($"总长度: {result.Length} 字符");
        Console.WriteLine("=== 前3000字 ===");
        Console.WriteLine(result[..Math.Min(3000, result.Length)]);
    }
}
