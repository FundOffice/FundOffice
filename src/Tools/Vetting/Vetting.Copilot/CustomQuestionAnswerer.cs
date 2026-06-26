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
    public static ITokenProvider CreateProvider(string name, string providerType, string apiKey, string baseUrl, string model)
    {
        return providerType switch
        {
            "Anthropic" => new AnthropicTokenProvider(
                new AnthropicOptions { Identifier = name, ApiKey = apiKey, BaseUrl = baseUrl, Model = model }),
            _ => new OpenAITokenProvider(
                new OpenAIOptions { Identifier = name, ApiKey = apiKey, BaseUrl = baseUrl, Model = model }),
        };
    }

    /// <summary>
    /// 回答文件的散装问题（不写数据库，返回结果）
    /// </summary>
    public async Task<QuestionAnswerResult> AnswerAsync(
        string fileHash,
        string providerId,
        Action<string>? output = null,
        CancellationToken ct = default)
    {
        var logs = new List<string>();
        try
        {
            using var db = new VettingDbContext();
            var questions = db.FileSpecialQuestions
                .Find(q => q.FileHash == fileHash && q.Provider == providerId)
                .OrderBy(q => q.Index)
                .ToArray();
            if (questions.Length == 0)
            {
                logs.Add("没有自定义问题");
                return new QuestionAnswerResult { Success = true, Logs = logs };
            }

            var qaList = db.QA.FindAll().ToArray();
            var prompt = BuildPrompt(qaList, questions);
            var messages = new[]
            {
                ChatMessage.System("你是一名尽职调查分析师，根据提供的历史问答资料，准确回答尽调问题。直接回答，不要废话。如果资料中没有相关信息，回答\"\"(空字符串)。"),
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
                answers.Add((idx, q.Question ?? "", answer));
                output?.Invoke($"{{{{{key}}}}}  {q.Question}\n    → {answer}");
                logs.Add($"{{{{a{idx}}}}} → {answer}");
            }

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
        string fileHash,
        string providerId,
        string providerName,
        Action<string>? output = null,
        CancellationToken ct = default)
    {
        var result = await AnswerAsync(fileHash, providerId, output, ct);
        if (!result.Success || result.AnsweredCount == 0) return result;

        using var db = new VettingDbContext();
        var questions = db.FileSpecialQuestions
            .Find(q => q.FileHash == fileHash && q.Provider == providerId)
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

    private static string BuildPrompt(QA[] qaList, FileSpecialQuestion[] questions)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## 历史问答资料");
        foreach (var qa in qaList)
        {
            sb.AppendLine($"问: {qa.Question}");
            sb.AppendLine($"答: {qa.Answer}");
            sb.AppendLine();
        }
        sb.AppendLine("## 待回答问题");
        foreach (var q in questions)
            sb.AppendLine($"{{a{q.Index}}}: {q.Question}");
        sb.AppendLine();
        sb.AppendLine("请严格按以下 JSON 格式回答:");
        sb.AppendLine("{\"answers\": {\"a1\": \"回答内容\", \"a2\": \"回答内容\"}}");
        return sb.ToString();
    }
}
