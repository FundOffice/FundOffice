using CommunityToolkit.Mvvm.ComponentModel;
using FundOffice.Copilot.Models;
using FundOffice.Copilot.Providers;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Vetting.Copilot;
using Vetting.Copilot.Data;
using Vetting.Copilot.Models.Entities;
using Vetting.Copilot.Models.Info;

namespace Vetting.ViewModel;

public partial class QuestionAnswerTaskViewModel : ObservableObject
{
    public ITokenProvider Provider { get; }
    public string ProviderName { get; }
    public string FileHash { get; }
    public string ProviderId { get; }

    [ObservableProperty] public partial string TaskName { get; set; } = "";
    [ObservableProperty] public partial TaskStatus Status { get; set; } = TaskStatus.Pending;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(UsageText))] public partial int Usage { get; set; }
    public string UsageText => Usage >= 1000 ? $"{Usage / 1000.0:F1}k tokens" : Usage > 0 ? $"{Usage} tokens" : "";
    [ObservableProperty] public partial string Elapsed { get; set; } = "";
    [ObservableProperty] public partial string? ErrorMessage { get; set; }
    [ObservableProperty] public partial bool IsExpanded { get; set; }
    public ObservableCollection<string> Output { get; } = [];
    private readonly Stopwatch _sw = new();

    public QuestionAnswerTaskViewModel(ITokenProvider provider, string providerName, string fileHash, string providerId)
    {
        Provider = provider;
        ProviderName = providerName;
        FileHash = fileHash;
        ProviderId = providerId;
        TaskName = providerName;
    }

    public async Task<int> RunAsync(Action<string>? onAnswered = null)
    {
        Status = TaskStatus.Running;
        _sw.Restart();
        try
        {
            using var db = new VettingDbContext();
            var questions = db.FileSpecialQuestions
                .Find(q => q.FileHash == FileHash && q.Provider == ProviderId)
                .OrderBy(q => q.Index).ToArray();

            if (questions.Length == 0)
            {
                Output.Add("没有自定义问题");
                _sw.Stop(); Status = TaskStatus.Done; Elapsed = FormatElapsed(_sw.Elapsed);
                return 0;
            }

            var qaList = db.QA.FindAll().ToArray();
            var prompt = BuildPrompt(qaList, questions);

            var messages = new[]
            {
                ChatMessage.System("你是一名尽职调查分析师，根据提供的历史问答资料，准确回答尽调问题。直接回答，不要废话。如果资料中没有相关信息，回答空字符串。"),
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
            await foreach (var token in Provider.ChatCompletionStreamAsync(messages, options: options))
            {
                switch (token)
                {
                    case TextDelta td:
                        sb.Append(td.Text);
                        Usage = sb.Length / 4;
                        break;
                    case UsageUpdate u:
                        Usage = (u.PromptTokens ?? 0) + (u.CompletionTokens ?? 0);
                        break;
                }
            }

            var json = sb.ToString().Trim();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var answersProp = root.TryGetProperty("answers", out var a) ? a : root;

            int count = 0;
            foreach (var prop in answersProp.EnumerateObject())
            {
                var key = prop.Name;
                if (!key.StartsWith('a') || !int.TryParse(key.TrimStart('a'), out var idx)) continue;
                var q = questions.FirstOrDefault(x => x.Index == idx);
                if (q == null) continue;
                var answer = prop.Value.GetString() ?? "";
                if (answer.Contains("暂无相关信息")) answer = "";

                var existing = db.SpecialAnswers.FindOne(sa => sa.QuestionId == q.Id && sa.Identifier == ProviderName);
                if (existing != null)
                {
                    existing.Value = answer;
                    db.SpecialAnswers.Update(existing);
                }
                else
                {
                    db.SpecialAnswers.Insert(new SpecialAnswer { QuestionId = q.Id, Identifier = ProviderName, Value = answer });
                }

                var line = $"{{{{a{idx}}}}}  {q.Question}\n    → {answer}";
                Output.Add(line);
                onAnswered?.Invoke(line);
                count++;
            }

            Output.Add($"回答完成，共 {count} 条");
            _sw.Stop(); Status = TaskStatus.Done; Elapsed = FormatElapsed(_sw.Elapsed);
            return count;
        }
        catch (Exception ex)
        {
            _sw.Stop(); Status = TaskStatus.Error;
            ErrorMessage = ex.Message; Elapsed = FormatElapsed(_sw.Elapsed);
            Output.Add($"错误: {ex.Message}");
            return 0;
        }
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

    private static string FormatElapsed(TimeSpan ts)
        => ts.TotalMinutes >= 1 ? $"{ts.Minutes}m{ts.Seconds:D2}s" : $"{ts.TotalSeconds:F1}s";
}
