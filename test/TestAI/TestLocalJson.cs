using FMO.AI;
using Initial;
using System.Text.Json;

namespace TestAI;

[TestClass]
public sealed class TestLocalJson
{
    [TestInitialize]
    public void TestInit() => DataInject.SetAsDebug();


    /// <summary>
    /// 读取 temp 目录下所有 JSON 文件，测试 AI 返回的 JSON 能否正确解析
    /// </summary>
    [TestMethod]
    public void ParseLocalJsonFiles()
    {
        var tempDir = "temp";

        Console.WriteLine($"查找目录: {tempDir}");
        Assert.IsTrue(Directory.Exists(tempDir), $"temp 目录不存在: {tempDir}");

        var jsonFiles = Directory.GetFiles(tempDir, "*.json");
        Assert.IsNotEmpty(jsonFiles, $"temp 目录下没有 JSON 文件: {tempDir}");

        Console.WriteLine($"找到 {jsonFiles.Length} 个 JSON 文件\n");

        var failed = 0;
        var succeeded = 0;

        foreach (var file in jsonFiles)
        {
            var fileName = Path.GetFileName(file);
            var content = File.ReadAllText(file);

            Console.WriteLine($"===== {fileName} =====");
            Console.WriteLine($"大小: {content.Length} 字符");

            try
            {
                // 1. 提取 JSON（处理可能包裹在 markdown code block 中的情况）
                var json = TokenProvider.ExtractJson(content);
                Console.WriteLine($"提取 JSON: {json.Length} 字符");

                // 2. 反序列化
                var dto = JsonSerializer.Deserialize<AiParsedFundInfo>(json, FundDocxAiParser.JsonOptions);
                if (dto == null)
                {
                    Console.WriteLine("[FAIL] 反序列化结果为 null");
                    failed++;
                    continue;
                }

                // 3. 转换为 FundFactor[]
                var factors = AiParsedFundInfoConverter.ToFactors(dto);
                Console.WriteLine($"[OK] 解析成功，提取 {factors.Length} 个 Factor");

                // 4. 列出有值的字段
                var nonNull = 0;
                foreach (var prop in typeof(AiParsedFundInfo).GetProperties())
                {
                    var val = prop.GetValue(dto);
                    if (val != null)
                    {
                        nonNull++;
                        var confProp = val.GetType().GetProperty("Confidence");
                        if (confProp != null)
                            Console.WriteLine($"  {prop.Name}: Confidence={confProp.GetValue(val):F2}");
                        else
                            Console.WriteLine($"  {prop.Name}: (has value)");
                    }
                }

                Console.WriteLine($"  非空字段: {nonNull}/{typeof(AiParsedFundInfo).GetProperties().Length}");

                // 5. 填充到 ReadonlyFundInfo 看看
                var fundInfo = new FMO.Models.ReadonlyFundInfo();
                fundInfo.FillBy(factors);
                Console.WriteLine($"  FullName: {fundInfo.FullName}");
                Console.WriteLine($"  ShortName: {fundInfo.ShortName}");

                succeeded++;
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"[FAIL] JSON 解析失败: {ex.Message}");
                Console.WriteLine($"  位置: Line {ex.LineNumber}, Position {ex.BytePositionInLine}");

                // 打印问题附近的文本
                if (ex.LineNumber.HasValue && ex.BytePositionInLine.HasValue)
                {
                    var lines = content.Split('\n');
                    var lineIdx = (int)ex.LineNumber.Value;
                    for (int i = Math.Max(0, lineIdx - 2); i < Math.Min(lines.Length, lineIdx + 3); i++)
                    {
                        var marker = i == lineIdx ? " >>>" : "    ";
                        Console.WriteLine($"{marker} {i + 1}: {lines[i]}");
                    }
                }
                failed++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FAIL] 异常: {ex.GetType().Name}: {ex.Message}");
                failed++;
            }

            Console.WriteLine();
        }

        Console.WriteLine($"\n===== 汇总 =====");
        Console.WriteLine($"成功: {succeeded}");
        Console.WriteLine($"失败: {failed}");

        Assert.AreEqual(0, failed, $"{failed} 个 JSON 文件解析失败");
    }
}
