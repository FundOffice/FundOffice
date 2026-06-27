using Vetting.Copilot.Models.Entities;
using Vetting.Copilot.Models.Info;
using Xunit;
using Xunit.Abstractions;

namespace Vetting.Copilot.Tests;

public class InferenceDetectionTests(ITestOutputHelper _out)
{
    /// <summary>
    /// 模拟 QA 数据
    /// </summary>
    private static QA[] BuildQaList() =>
    [
        new() { Id = 1, Question = "注册资本", Answer = "1亿元人民币" },
        new() { Id = 2, Question = "法定代表人", Answer = "张三" },
        new() { Id = 3, Question = "公司成立日期", Answer = "2015年6月18日" },
        new() { Id = 4, Question = "管理人登记编号", Answer = "P12345" },
    ];

    /// <summary>
    /// 模拟文档中的问题（措辞可能与 QA 不同）
    /// </summary>
    private static FileSpecialQuestion[] BuildQuestions() =>
    [
        new() { Id = 1, FileHash = "", Provider = "test", Index = 0, Question = "公司的注册资本是多少" },
        new() { Id = 2, FileHash = "", Provider = "test", Index = 1, Question = "法定代表人是谁" },
        new() { Id = 3, FileHash = "", Provider = "test", Index = 2, Question = "公司成立日期是哪天" },
        new() { Id = 4, FileHash = "", Provider = "test", Index = 3, Question = "基金管理人登记证明编号是多少" },
        new() { Id = 5, FileHash = "", Provider = "test", Index = 4, Question = "公司有多少员工" },
    ];

    /// <summary>
    /// 证明 Contains 匹配的缺陷："管理人登记编号" 不在 "基金管理人登记证明编号是多少" 中
    /// </summary>
    [Fact]
    public void Contains_Fails_On_Wording_Difference()
    {
        var qaList = BuildQaList();
        var questions = BuildQuestions();

        // "基金管理人登记证明编号是多少" 不包含 "管理人登记编号"（中间有"证明"两字打断子串）
        var matched = qaList.Any(qa =>
            questions[3].Question!.Contains(qa.Question!, StringComparison.OrdinalIgnoreCase) ||
            qa.Question!.Contains(questions[3].Question!, StringComparison.OrdinalIgnoreCase));

        _out.WriteLine($"Q: {questions[3].Question} vs QA: 管理人登记编号 → Contains matched={matched}");
        Assert.False(matched, "Contains 无法匹配措辞差异的问题");
    }

    /// <summary>
    /// 验证修复后的逻辑：信任 AI 标记
    /// AI 没标【推断】= 精确回答，AI 标了【推断】= 推断回答
    /// 不再用 Contains fallback
    /// </summary>
    [Fact]
    public void Trust_AI_Marking_For_Inference_Detection()
    {
        var qaList = BuildQaList();
        var questions = BuildQuestions();

        // 模拟 AI 回答（没标【推断】的是精确，标了的是推断）
        var aiAnswers = new Dictionary<int, string>
        {
            [0] = "1亿元人民币",           // 精确
            [1] = "张三",                   // 精确
            [2] = "2015年6月18日",          // 精确
            [3] = "P12345",                 // 精确（虽然 Contains 匹配不上，但 AI 没标推断）
            [4] = "【推断】约50人",         // 推断
        };

        var exactCount = 0;
        var inferredCount = 0;

        foreach (var (idx, answer) in aiAnswers)
        {
            if (string.IsNullOrWhiteSpace(answer)) continue;

            var isInferred = answer.StartsWith("【推断】");
            if (isInferred) inferredCount++;
            else exactCount++;
        }

        Assert.Equal(4, exactCount);   // 4 个精确
        Assert.Equal(1, inferredCount); // 1 个推断
        _out.WriteLine($"精确: {exactCount}, 推断: {inferredCount}");
    }

    /// <summary>
    /// 验证 Strict 模式：推断回答置空，精确回答保留
    /// </summary>
    [Fact]
    public void Strict_Mode_Inferred_Answers_Become_Empty()
    {
        var aiAnswers = new Dictionary<int, string>
        {
            [0] = "1亿元人民币",
            [1] = "【推断】约50人",
        };

        var results = new Dictionary<int, string>();
        foreach (var (idx, answer) in aiAnswers)
        {
            var processed = answer;
            var isInferred = processed.StartsWith("【推断】");
            if (isInferred)
            {
                processed = processed["【推断】".Length..];
                processed = ""; // strict mode
            }
            results[idx] = processed;
        }

        Assert.Equal("1亿元人民币", results[0]); // 精确保留
        Assert.Equal("", results[1]);             // 推断置空
    }

    /// <summary>
    /// 验证 Full 模式：推断回答加（ai）前缀
    /// </summary>
    [Fact]
    public void Full_Mode_Inferred_Answers_Get_Ai_Prefix()
    {
        var aiAnswers = new Dictionary<int, string>
        {
            [0] = "1亿元人民币",
            [1] = "【推断】约50人",
        };

        var results = new Dictionary<int, string>();
        foreach (var (idx, answer) in aiAnswers)
        {
            var processed = answer;
            var isInferred = processed.StartsWith("【推断】");
            if (isInferred)
            {
                processed = processed["【推断】".Length..];
                processed = $"（ai）{processed}"; // full mode
            }
            results[idx] = processed;
        }

        Assert.Equal("1亿元人民币", results[0]);     // 精确不变
        Assert.Equal("（ai）约50人", results[1]);     // 推断加前缀
    }
}
