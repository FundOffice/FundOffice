using FundOffice.Copilot.Models;
using FundOffice.Copilot.Providers;
using System.Text;
using System.Text.Json;
using Vetting.Data;
using Vetting.Entity;
using Vetting.Models.Entities;

namespace Vetting.Services;

public static class CustomQuestionAnswerService
{
    public static async Task<int> AnswerAsync(
        string fileHash,
        string providerId,
        ITokenProvider provider,
        string providerName,
        Action<string>? output = null,
        CancellationToken ct = default)
    {
        using var db = new VettingDbContext();
        var questions = db.FileSpecialQuestions
            .Find(q => q.FileHash == fileHash && q.Provider == providerId)
            .OrderBy(q => q.Index)
            .ToArray();
        if (questions.Length == 0) { output?.Invoke("没有自定义问题"); return 0; }

        var qaList = db.QA.FindAll().ToArray();

        var prompt = BuildPrompt(qaList, questions);
        var messages = new[]
        {
            ChatMessage.System("你是一名尽职调查分析师，根据提供的历史问答资料，准确回答尽调问题。直接回答，不要废话。如果资料中没有相关信息，回答\"暂无相关信息\"。"),
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
        await foreach (var token in provider.ChatCompletionStreamAsync(messages, options: options, cancellationToken: ct))
        {
            if (token is TextDelta td) sb.Append(td.Text);
        }

        var json = sb.ToString().Trim();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var answersProp = root.TryGetProperty("answers", out var a) ? a : root;

        var count = 0;
        foreach (var prop in answersProp.EnumerateObject())
        {
            var key = prop.Name;
            if (!key.StartsWith('a') || !int.TryParse(key.TrimStart('a'), out var idx)) continue;
            var q = questions.FirstOrDefault(x => x.Index == idx);
            if (q == null) continue;
            var answer = prop.Value.GetString();
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
            output?.Invoke($"{{{{{key}}}}}  {q.Question}\n    → {answer}");
            count++;
        }
        return count;
    }

    public static ITokenProvider CreateProvider(AIProviderConfig config) => config.ProviderType switch
    {
        "Anthropic" => new AnthropicTokenProvider(
            new FundOffice.Copilot.Configuration.AnthropicOptions { Identifier = config.Name, ApiKey = config.ApiKey, BaseUrl = config.BaseUrl, Model = config.Model }),
        _ => new OpenAITokenProvider(
            new FundOffice.Copilot.Configuration.OpenAIOptions { Identifier = config.Name, ApiKey = config.ApiKey, BaseUrl = config.BaseUrl, Model = config.Model }),
    };

    public static ITokenProvider CreateProvider(Vetting.ViewModel.AIProviderItemViewModel vm) => vm.ProviderType switch
    {
        "Anthropic" => new AnthropicTokenProvider(
            new FundOffice.Copilot.Configuration.AnthropicOptions { Identifier = vm.Name, ApiKey = vm.ApiKey, BaseUrl = vm.BaseUrl, Model = vm.Model }),
        _ => new OpenAITokenProvider(
            new FundOffice.Copilot.Configuration.OpenAIOptions { Identifier = vm.Name, ApiKey = vm.ApiKey, BaseUrl = vm.BaseUrl, Model = vm.Model }),
    };

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
