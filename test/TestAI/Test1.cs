using FMO.AI;
using FMO.Utilities;
using Initial;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace TestAI;

[TestClass]
public sealed class Test1
{
    [TestInitialize]
    public void TestInit() => DataInject.SetAsDebug();

    [TestMethod]
    public async Task ExtractFactsFromAllDocx()
    {
        // 1. 取出所有 TokenProvider，筛选可用的
        using var db = DbHelper.Base();
        var providers = db.GetCollection<TokenProvider>().FindAll().Where(x=> x.GetType() != typeof(TokenProvider) )
            .Where(p => !string.IsNullOrWhiteSpace(p.Key)
                     && !string.IsNullOrWhiteSpace(p.Model))
            .ToList();

        Assert.IsNotEmpty(providers);

        // 2. 定位 docx 目录
        var docxDir = Path.Combine(AppContext.BaseDirectory, @"..\..\..\docx");
        var factsRoot = Path.Combine(AppContext.BaseDirectory, @"..\..\..\facts");
        var docxFiles = Directory.GetFiles(docxDir, "*.docx", SearchOption.TopDirectoryOnly);
        Assert.IsNotEmpty(docxFiles);

        // 3. 对每个可用 Provider 逐一测试
        foreach (var provider in providers)
        {
            var providerDir = Path.Combine(factsRoot, provider.Company);
            Directory.CreateDirectory(providerDir);
            Console.WriteLine($"=== 测试 Provider: {provider.Company} (Model: {provider.Model}) ===");

            foreach (var docxPath in docxFiles)
            {
                var fileName = Path.GetFileNameWithoutExtension(docxPath);
                Console.WriteLine($"  --- 文件: {fileName} ---");
                Console.WriteLine($"  路径: {docxPath}");
                Console.WriteLine($"  大小: {new FileInfo(docxPath).Length / 1024} KB");

                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromMinutes(5);

                var response = await provider.AskWithFileAsync(
                    client, provider.Model, FundDocxPrompt.Build(), docxPath);

                var json = TokenProvider.ExtractJson(response);

                // 格式化 JSON
                using var doc = JsonDocument.Parse(json);
                var pretty = JsonSerializer.Serialize(doc.RootElement,
                    new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

                // 输出到 facts/{Provider公司名}/
                await File.WriteAllTextAsync(
                    Path.Combine(providerDir, $"{fileName}.json"), pretty);

                Console.WriteLine($"  [OK] {fileName}");
            }
        }
    }

    [TestMethod]
    public void ExtractTextFromAllDocx()
    {
        var docxDir = Path.Combine(AppContext.BaseDirectory, @"..\..\..\docx");
        var docxFiles = Directory.GetFiles(docxDir, "*.docx", SearchOption.TopDirectoryOnly);
        Assert.IsNotEmpty(docxFiles);

        foreach (var docxPath in docxFiles)
        {
            var fileName = Path.GetFileName(docxPath);
            var fileSizeKB = new FileInfo(docxPath).Length / 1024;

            var text = TokenProvider.ExtractTextFromDocx(docxPath);

            Assert.IsFalse(string.IsNullOrWhiteSpace(text),
                $"ExtractTextFromDocx 返回空文本: {fileName}");

            var charCount = text.Length;
            var lineCount = text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;

            Console.WriteLine($"[OK] {fileName}: {fileSizeKB}KB, {charCount} 字符, {lineCount} 行");
        }
    }
}
