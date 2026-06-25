using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FundOffice.Copilot.Models;
using FundOffice.Copilot.Providers;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using Vetting.Data;
using Vetting.Entity;
using Vetting.Models.Entities;

namespace Vetting.ViewModel;

public partial class TemplateFileViewModel : ObservableObject
{
    public required string FileName { get; set; }
    public required string AbsolutePath { get; set; }
    public string VettingId { get; set; } = "";
    [ObservableProperty] public partial bool IsExpanded { get; set; }
    public ObservableCollection<string> Output { get; } = [];

    [SetsRequiredMembers]
    public TemplateFileViewModel(FileInfo fileInfo, string vettingId)
    {
        FileName = fileInfo.Name;
        AbsolutePath = fileInfo.FullName;
        VettingId = vettingId;
    }

    [RelayCommand]
    private void ViewCustomQuestions()
    {
        var m = Regex.Match(FileName, @"(.+)_by\[(.+)\](.*)");
        if(!m.Success)
        {
            HandyControl.Controls.Growl.Warning("文件名不合法");
            return;
        }

        var safeName = m.Groups[1].Value;
        var providerId = m.Groups[2].Value;
        var ext = m.Groups[3].Value;
        var srcPath = Path.Combine("files", "vetting", VettingId, $"{safeName}{ext}");

        if (!File.Exists(srcPath))
        {
            HandyControl.Controls.Growl.Warning("找不到原始尽调文件");
            return;
        }
        var fileHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(srcPath))).ToLowerInvariant();

        var vm = new CustomQuestionsViewModel(fileHash, providerId, FileName);
        if (vm.Questions.Count == 0)
        {
            HandyControl.Controls.Growl.Warning("没有找到自定义问题");
            return;
        }

        var win = new Vetting.View.CustomQuestionsWindow { DataContext = vm, Owner = Application.Current.MainWindow };
        win.Show();
    }

    [RelayCommand]
    private async Task AIAnswerCustomQuestionsAsync()
    {
        var m = Regex.Match(FileName, @"(.+)_by\[(.+)\](.*)");
        if (!m.Success) { HandyControl.Controls.Growl.Warning("文件名不合法"); return; }
        var providerId = m.Groups[2].Value;
        var srcPath = Path.Combine("files", "vetting", VettingId, $"{m.Groups[1].Value}{m.Groups[3].Value}");
        if (!File.Exists(srcPath)) { HandyControl.Controls.Growl.Warning("找不到原始尽调文件"); return; }
        var fileHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(srcPath))).ToLowerInvariant();

        using var db = new VettingDbContext();
        var questions = db.FileSpecialQuestions
            .Find(q => q.FileHash == fileHash && q.Provider == providerId)
            .OrderBy(q => q.Index)
            .ToArray();
        if (questions.Length == 0) { HandyControl.Controls.Growl.Warning("没有自定义问题"); return; }

        // 从数据库读取 QA 作为上下文
        var qaList = db.QA.FindAll().ToArray();

        // 选择 AI Provider
        var config = db.AIProviderConfigs.FindAll().FirstOrDefault();
        if (config == null) { HandyControl.Controls.Growl.Warning("请先配置 AI 接口"); return; }
        var provider = CreateProvider(config);

        Output.Clear();
        IsExpanded = true;

        var prompt = BuildAnswerPrompt(qaList, questions);
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
        await foreach (var token in provider.ChatCompletionStreamAsync(messages, options: options))
        {
            if (token is TextDelta td) sb.Append(td.Text);
        }

        var json = sb.ToString().Trim();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // 解析结果并保存到 SpecialAnswer
        var answersProp = root.TryGetProperty("answers", out var a) ? a : root;
        var answerIdentifier = config.Name;
        var count = 0;
        foreach (var prop in answersProp.EnumerateObject())
        {
            var key = prop.Name; // "a1", "a2" ...
            if (!key.StartsWith('a') || !int.TryParse(key.TrimStart('a'), out var idx)) continue;
            var q = questions.FirstOrDefault(x => x.Index == idx);
            if (q == null) continue;
            var answer = prop.Value.GetString();
            var existing = db.SpecialAnswers.FindOne(sa => sa.QuestionId == q.Id && sa.Identifier == answerIdentifier);
            if (existing != null)
            {
                existing.Value = answer;
                db.SpecialAnswers.Update(existing);
            }
            else
            {
                db.SpecialAnswers.Insert(new SpecialAnswer { QuestionId = q.Id, Identifier = answerIdentifier, Value = answer });
            }
            Output.Add($"{{{{{key}}}}}  {q.Question}\n    → {answer}");
            count++;
        }
        Output.Add($"AI 回答完成，共 {count} 条");
    }

    private static string BuildAnswerPrompt(QA[] qaList, FileSpecialQuestion[] questions)
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

    private static ITokenProvider CreateProvider(AIProviderConfig config) => config.ProviderType switch
    {
        "Anthropic" => new AnthropicTokenProvider(
            new FundOffice.Copilot.Configuration.AnthropicOptions { Identifier = config.Name, ApiKey = config.ApiKey, BaseUrl = config.BaseUrl, Model = config.Model }),
        _ => new OpenAITokenProvider(
            new FundOffice.Copilot.Configuration.OpenAIOptions { Identifier = config.Name, ApiKey = config.ApiKey, BaseUrl = config.BaseUrl, Model = config.Model }),
    };
}
