using FundOffice.Copilot.Configuration;
using FundOffice.Copilot.Models;
using FundOffice.Copilot.Providers;
using System.Text;
using System.Text.Json;
using Vetting.Copilot.Data;
using Vetting.Copilot.Models.Entities;
using Vetting.Copilot.Models.Info;

namespace Vetting.Copilot;

/// <summary>
/// 散装问题 AI 回答结果
/// </summary>
public record QuestionAnswerResult
{
    public bool Success { get; init; }
    public int AnsweredCount { get; init; }
    public string? ErrorMessage { get; init; }
    public List<(int index, string question, string answer)> Answers { get; init; } = [];
    public List<string> Logs { get; init; } = [];
}

/// <summary>
/// AI 回答散装问题服务 — 从数据库加载问答资料，调用 AI 回答文件专属问题
/// </summary>
public class CustomQuestionAnswerer
{
    private readonly ITokenProvider _provider;

    public CustomQuestionAnswerer(ITokenProvider provider)
    {
        _provider = provider;
    }

    public string ProviderIdentifier => _provider.Identifier;

    /// <summary>
    /// 从配置创建 ITokenProvider 实例
    /// </summary>
    public static ITokenProvider CreateProvider(string idf, string providerType, string apiKey, string baseUrl, string model, OpenAIApiVersion apiVersion = OpenAIApiVersion.ChatCompletions)
    {
        return providerType switch
        {
            "Anthropic" => new AnthropicTokenProvider(
                new AnthropicOptions { Identifier = idf, ApiKey = apiKey, BaseUrl = baseUrl, Model = model }),
            _ => apiVersion switch
            {
                OpenAIApiVersion.Responses => new OpenAIResponsesProvider(
                    new OpenAIOptions { Identifier = idf, ApiKey = apiKey, BaseUrl = baseUrl, Model = model, ApiVersion = apiVersion }),
                _ => new OpenAITokenProvider(
                    new OpenAIOptions { Identifier = idf, ApiKey = apiKey, BaseUrl = baseUrl, Model = model, ApiVersion = apiVersion }),
            },
        };
    }

    /// <summary>
    /// 回答文件的散装问题（不写数据库，返回结果）
    /// </summary>
    public async Task<QuestionAnswerResult> AnswerAsync(
        string fileName,
        string providerId,
        bool isFullMode = false,
        Action<string>? output = null,
        CancellationToken ct = default)
    {
        var logs = new List<string>();
        try
        {
            using var db = new VettingDbContext();
            var questions = db.FileSpecialQuestions
                .Find(q => q.FileName == fileName && q.Provider == providerId)
                .OrderBy(q => q.Index)
                .ToArray();
            if (questions.Length == 0)
            {
                logs.Add("没有自定义问题");
                return new QuestionAnswerResult { Success = true, Logs = logs };
            }

            var qaList = db.QA.FindAll().ToArray();
            var prompt = BuildPrompt(qaList, questions, isFullMode);

            var systemPrompt = GetSystemPrompt(isFullMode);
            var messages = new[]
            {
                ChatMessage.System(systemPrompt),
                ChatMessage.User(prompt)
            };
            var options = new ChatOptions
            {
                AdditionalProperties = new Dictionary<string, object>
                {
                    ["response_format"] = new { type = "json_object" }
                }
            };

            var sb = new StringBuilder();
            await foreach (var token in _provider.ChatCompletionStreamAsync(messages, options: options, cancellationToken: ct))
            {
                if (token is TextDelta td) sb.Append(td.Text);
                // ReasoningDelta 故意忽略：推理内容不进入结果 JSON
            }

            var json = sb.ToString().Trim();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var answersProp = root.TryGetProperty("answers", out var a) ? a : root;

            var answers = new List<(int index, string question, string answer)>();
            foreach (var prop in answersProp.EnumerateObject())
            {
                var key = prop.Name;
                if (!key.StartsWith('a') || !int.TryParse(key.TrimStart('a'), out var idx)) continue;
                var q = questions.FirstOrDefault(x => x.Index == idx);
                if (q == null) continue;
                var answer = prop.Value.GetString() ?? "";
                var (processedAnswer, isInferred) = ProcessAnswer(answer, isFullMode);
                answer = processedAnswer;

                answers.Add((idx, q.Question ?? "", answer));
                output?.Invoke($"{{{key}}}  {q.Question}\n    → {answer}");
                logs.Add($"{{a{idx}}} → {answer}");
            }

            var exactCount = answers.Count(a => !string.IsNullOrEmpty(a.answer) && !a.answer.StartsWith("（ai）"));
            var inferredCount = answers.Count(a => a.answer.StartsWith("（ai）"));
            logs.Add($"精确 {exactCount} 条" + (isFullMode ? $"，推断 {inferredCount} 条" : "") + $"，共 {answers.Count} 条");

            return new QuestionAnswerResult
            {
                Success = true,
                AnsweredCount = answers.Count,
                Answers = answers,
                Logs = logs
            };
        }
        catch (Exception ex)
        {
            logs.Add($"错误: {ex.Message}");
            return new QuestionAnswerResult { Success = false, ErrorMessage = ex.Message, Logs = logs };
        }
    }

    /// <summary>
    /// 回答散装问题并保存到数据库
    /// </summary>
    public async Task<QuestionAnswerResult> AnswerAndSaveAsync(
        string fileName,
        string providerId,
        string providerName,
        bool isFullMode = false,
        Action<string>? output = null,
        CancellationToken ct = default)
    {
        var result = await AnswerAsync(fileName, providerId, isFullMode, output, ct);
        if (!result.Success || result.AnsweredCount == 0) return result;

        using var db = new VettingDbContext();
        var questions = db.FileSpecialQuestions
            .Find(q => q.FileName == fileName && q.Provider == providerId)
            .ToArray();

        foreach (var (index, _, answer) in result.Answers)
        {
            var q = questions.FirstOrDefault(x => x.Index == index);
            if (q == null) continue;
            var existing = db.SpecialAnswers.FindOne(sa => sa.QuestionId == q.Id && sa.Identifier == providerName);
            if (existing != null)
            {
                existing.Value = answer;
                db.SpecialAnswers.Update(existing);
            }
            else
            {
                db.SpecialAnswers.Insert(new SpecialAnswer { QuestionId = q.Id, Identifier = providerName, Value = answer });
            }
        }

        return result;
    }

    public static string GetSystemPrompt(bool isFullMode) => isFullMode
        ? "你是一名尽调报告填写者。优先根据提供的历史问答资料精确回答问题；如果资料中没有相关信息，请以该公司运营的实际情况为背景，用肯定的语气给出合理的推断回答，并在该回答开头加上【推断】标记。回答要像直接填写尽调表格一样简洁明确，不要出现「根据资料」「资料显示」等引用性表述。"
        : "你是一名尽职调查分析师，根据提供的历史问答资料，准确回答尽调问题。直接回答，不要废话。如果资料中没有相关信息，回答\"\"(空字符串)。";

    /// <summary>处理 AI 返回的答案：检测【推断】标记，full 模式加（ai）前缀，strict 模式置空</summary>
    public static (string answer, bool isInferred) ProcessAnswer(string answer, bool isFullMode)
    {
        if (answer.Contains("暂无相关信息")) answer = "";
        if (string.IsNullOrWhiteSpace(answer)) return (answer, false);

        var isInferred = answer.StartsWith("【推断】");
        if (isInferred)
        {
            answer = answer["【推断】".Length..];
            if (isFullMode) answer = $"（ai）{answer}";
            else answer = "";
        }
        return (answer, isInferred);
    }

    public static string BuildPrompt(QA[] qaList, FileSpecialQuestion[] questions, bool isFullMode)
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
        if (isFullMode)
            sb.AppendLine(@"如果某问题的回答不是来自上面的历史问答资料，而是你根据专业判断得出的，请在该回答开头加上【推断】标记。例如: ""【推断】这是推断的回答内容""");
        return sb.ToString();
    }
}
