using System.Text;
using System.Text.RegularExpressions;
using Vetting.Copilot.Data;
using Vetting.Copilot.Models.Entities;
using Vetting.Copilot.Models.Info;

namespace Vetting.Copilot;

/// <summary>
/// 提示词服务 — 加载系统提示词、构建用户提示词、处理 AI 回答
/// </summary>
public static class PromptService
{
    #region System Prompt (解析阶段)

    /// <summary>
    /// 加载系统提示词。优先使用本地 files/vetting/syspt.md，否则使用嵌入资源。
    /// </summary>
    public static async Task<string> LoadSysptAsync()
    {
        var localPath = Path.Combine("files", "vetting", "syspt.md");
        var asm = typeof(PromptService).Assembly;
        using var embeddedSr = new StreamReader(asm.GetManifestResourceStream("Vetting.Copilot.syspt.md")!);
        var embedded = await embeddedSr.ReadToEndAsync();
        var embeddedVer = ExtractVersion(embedded);

        if (File.Exists(localPath))
        {
            var local = await File.ReadAllTextAsync(localPath);
            var localVer = ExtractVersion(local);
            if (localVer >= embeddedVer) return local;
        }
        return embedded;
    }

    private static int ExtractVersion(string content)
    {
        var match = Regex.Match(content, @"<!--\s*version:(\d+)\s*-->");
        return match.Success ? int.Parse(match.Groups[1].Value) : 0;
    }

    #endregion

    #region QA Prompt (回答阶段)

    /// <summary>散装问题的系统提示词（始终使用完整模式，允许推断）</summary>
    public static string GetQASystemPrompt() =>
        "你是一名尽调报告填写者。优先根据提供的历史问答资料精确回答问题；如果资料中没有相关信息，请以该公司运营的实际情况为背景，用肯定的语气给出合理的推断回答，并在该回答开头加上【推断】标记。回答要像直接填写尽调表格一样简洁明确，不要出现「根据资料」「资料显示」等引用性表述。";

    /// <summary>构建散装问题的用户提示词（始终使用完整模式，允许推断）</summary>
    public static string BuildQAPrompt(QA[] qaList, FileSpecialQuestion[] questions)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## 历史问答资料");

        // 检测是否包含占位符
        bool hasPlaceholders = false;
        foreach (var qa in qaList)
        {
            sb.AppendLine($"问: {qa.Question}");
            sb.AppendLine($"答: {qa.Answer}");
            sb.AppendLine();
            if (!hasPlaceholders && (qa.Answer.Contains("{{") || qa.Answer.Contains("[img#")))
                hasPlaceholders = true;
        }

        if (hasPlaceholders)
            sb.AppendLine("⚠️ 注意: 上述资料中包含 `{{xx.}}` 模板占位符或 `[img#N]` 图片占位符，回答时必须原样保留这些占位符，不得替换、修改或省略。");

        sb.AppendLine("## 待回答问题");
        foreach (var q in questions)
            sb.AppendLine($"{{a{q.Index}}}: {q.Question}");
        sb.AppendLine();
        sb.AppendLine("请严格按以下 JSON 格式回答:");
        sb.AppendLine(@"{""answers"": {""a1"": ""回答内容"", ""a2"": ""回答内容""}}");
        sb.AppendLine(@"如果某问题的回答不是来自上面的历史问答资料，而是你根据专业判断得出的，请在该回答开头加上【推断】标记。例如: ""【推断】这是推断的回答内容""");
        return sb.ToString();
    }

    /// <summary>处理 AI 返回的答案：检测【推断】标记，加（ai）前缀标记推断内容</summary>
    public static (string answer, bool isInferred) ProcessAnswer(string answer)
    {
        if (answer.Contains("暂无相关信息")) answer = "";
        if (string.IsNullOrWhiteSpace(answer)) return (answer, false);

        var isInferred = answer.StartsWith("【推断】");
        if (isInferred)
        {
            answer = answer["【推断】".Length..];
            answer = $"（ai）{answer}";
        }
        return (answer, isInferred);
    }

    #endregion
}
