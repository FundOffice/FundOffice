using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FundOffice.Copilot.Configuration;
using FundOffice.Copilot.Providers;
using Vetting.Copilot;
using Vetting.Copilot.Data;
using Vetting.Copilot.Models;
using Xunit;
using Xunit.Abstractions;

namespace Vetting.Copilot.Tests;

/// <summary>
/// 对真实文档跑完整 AI 解析流程，验证新 prompt 效果
/// </summary>
public class RealDocPromptTest
{
    private readonly ITestOutputHelper _out;

    private const string DocPath = @"D:\fundd\files\vetting\2c51ac7ce6424e0fa06738dbb29e2704\【中金财富】证券私募尽调问题清单-公司情况（202506）(1).docx";
    private const string DbPath = @"D:\fundd\data\vetting.db";

    public RealDocPromptTest(ITestOutputHelper @out) => _out = @out;

    private static ITokenProvider CreateProvider(AIProviderConfig c) => c.ProviderType switch
    {
        "Anthropic" => new AnthropicTokenProvider(new AnthropicOptions
        { Identifier = c.Name, ApiKey = c.ApiKey, BaseUrl = c.BaseUrl, Model = c.Model }),
        _ => new OpenAITokenProvider(new OpenAIOptions
        { Identifier = c.Name, ApiKey = c.ApiKey, BaseUrl = c.BaseUrl, Model = c.Model }),
    };

    [Fact]
    public async Task GenerateAndVerifyAllTablesCovered()
    {
        if (!File.Exists(DocPath)) { _out.WriteLine("文档不存在，跳过"); return; }

        // 1. 解析文档结构 + 统计表格数
        var structure = DocOps.ParseDocument(DocPath);
        int tableCount;
        using (var doc = WordprocessingDocument.Open(DocPath, false))
            tableCount = doc.MainDocumentPart!.Document.Body!.Elements<Table>().Count();
        _out.WriteLine($"文档共 {tableCount} 个表格");

        // 2. 加载 syspt
        var sysPrompt = await TemplateGenerator.LoadSysptAsync();

        // 3. 从数据库读 provider
        List<AIProviderConfig> providers;
        using (var db = new VettingDbContext(DbPath))
            providers = db.AIProviderConfigs.FindAll().ToList();
        if (providers.Count == 0) { _out.WriteLine("数据库无 provider，跳过"); return; }
        foreach (var p in providers) _out.WriteLine($"provider: {p.Name} ({p.ProviderType}) model={p.Model}");

        // 4. 选第一个 provider 跑
        var provider = CreateProvider(providers[0]);
        var messages = new[]
        {
            FundOffice.Copilot.Models.ChatMessage.System(sysPrompt),
            FundOffice.Copilot.Models.ChatMessage.User(structure + PredFiles.BuildPromptSection())
        };
        var options = new FundOffice.Copilot.Models.ChatOptions
        {
            AdditionalProperties = new Dictionary<string, object>
            {
                ["response_format"] = new { type = "json_object" }
            }
        };

        var sb = new System.Text.StringBuilder();
        await foreach (var token in provider.ChatCompletionStreamAsync(messages, options: options))
        {
            if (token is FundOffice.Copilot.Models.TextDelta td) sb.Append(td.Text);
        }
        var json = sb.ToString().Trim();
        _out.WriteLine($"AI 返回长度: {json.Length} 字符");

        // 5. 解析
        using var jsonDoc = System.Text.Json.JsonDocument.Parse(json);
        var (operators, warnings) = OperatorParser.ParseWithWarnings(jsonDoc.RootElement.GetProperty("operations"));
        _out.WriteLine($"解析出 {operators.Count} 个操作，{warnings.Count} 个警告");
        foreach (var w in warnings) _out.WriteLine($"  ⚠ {w}");

        // 6. 验证：每个表格至少被一个操作覆盖
        var coveredTables = new HashSet<int>();
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
            if (ti.HasValue && ti.Value >= 0) coveredTables.Add(ti.Value);
        }

        _out.WriteLine($"\n=== 表格覆盖情况 ===");
        _out.WriteLine($"文档表格: 0..{tableCount - 1}");
        _out.WriteLine($"已覆盖: {string.Join(",", coveredTables.OrderBy(x => x))}");
        var missing = Enumerable.Range(0, tableCount).Except(coveredTables).ToArray();
        _out.WriteLine($"缺失: {(missing.Length == 0 ? "无" : string.Join(",", missing))}");

        // 7. 统计各类型数量
        var byType = operators.GroupBy(o => o.GetType().Name)
            .ToDictionary(g => g.Key, g => g.Count());
        _out.WriteLine($"\n=== 操作类型统计 ===");
        foreach (var kv in byType) _out.WriteLine($"  {kv.Key}: {kv.Value}");

        // 8. 列出所有 Type g
        var unknowns = operators.OfType<UnknownTableOp>().ToArray();
        _out.WriteLine($"\n=== Type g (未知表格) 共 {unknowns.Length} 个 ===");
        foreach (var u in unknowns)
            _out.WriteLine($"  T[{u.Ts.TableIndex}] {u.Description}");

        // 9. 保存结果供人工查看
        var outPath = Path.Combine(Path.GetTempPath(), "real_doc_parsed.json");
        File.WriteAllText(outPath, json);
        _out.WriteLine($"\n完整 JSON 已保存: {outPath}");

        // 断言：不能漏表格
        Assert.True(missing.Length == 0,
            $"遗漏表格: {string.Join(",", missing)}。共 {tableCount} 个表格，仅覆盖 {coveredTables.Count} 个");

        Assert.True(operators.Count > 0, "未解析出任何操作");
    }
}
