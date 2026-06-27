using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FundOffice.Copilot.Providers;
using Vetting.Copilot.Models.Entities;
using Vetting.Copilot.Models.Info;
using Vetting.Copilot;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Vetting.ViewModel;

public partial class VettingParseTaskViewModel : ObservableObject
{
    public ITokenProvider? Provider { get; set; }
    public string VettingId { get; set; } = "";
    public string FileName { get; set; } = "";
    [ObservableProperty] public partial string TaskName { get; set; } = "";
    [ObservableProperty] public partial TaskStatus Status { get; set; } = TaskStatus.Pending;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(UsageText))] public partial int Usage { get; set; }
    public string UsageText => Usage >= 1000 ? $"{Usage / 1000.0:F1}k tokens" : Usage > 0 ? $"{Usage} tokens" : "";
    [ObservableProperty] public partial string Elapsed { get; set; } = "";
    [ObservableProperty] public partial string? ErrorMessage { get; set; }
    [ObservableProperty] public partial bool IsExpanded { get; set; }
    public ObservableCollection<string> Output { get; } = [];
    private readonly Stopwatch _sw = new();

    public void Start()
    {
        Status = TaskStatus.Running;
        _sw.Restart();
    }

    public void Complete()
    {
        _sw.Stop();
        Status = TaskStatus.Done;
        Elapsed = FormatElapsed(_sw.Elapsed);
    }

    public void Fail(string message)
    {
        _sw.Stop();
        Status = TaskStatus.Error;
        ErrorMessage = message;
        Elapsed = FormatElapsed(_sw.Elapsed);
    }

    public async Task RunAsync(string structure, string sysPrompt)
    {
        Start();
        try
        {
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

            var sb = new StringBuilder();
            await foreach (var token in Provider!.ChatCompletionStreamAsync(messages, options: options))
            {
                switch (token)
                {
                    case FundOffice.Copilot.Models.TextDelta td:
                        sb.Append(td.Text);
                        Usage = sb.Length / 4;
                        break;
                    case FundOffice.Copilot.Models.UsageUpdate u:
                        Usage = (u.PromptTokens ?? 0) + (u.CompletionTokens ?? 0);
                        break;
                }
            }

            var json = sb.ToString().Trim();

            // 校验 JSON 格式
            using var jsonDoc = System.Text.Json.JsonDocument.Parse(json);
            var root = jsonDoc.RootElement;
            if (!root.TryGetProperty("operations", out var opsEl) || opsEl.ValueKind != System.Text.Json.JsonValueKind.Array)
            {
                Fail("AI 返回的 JSON 缺少 operations 数组");
                return;
            }

            // 保存 JSON 文件
            var safeName = Path.GetFileNameWithoutExtension(FileName);
            var ext = Path.GetExtension(FileName);
            var tplDir = Path.Combine("files", "vetting", VettingId, "tpl");
            Directory.CreateDirectory(tplDir);
            var jsonPath = Path.Combine(tplDir, $"{safeName}_by[{Provider.Identifier}].json");
            FileRetry.Run(() => File.WriteAllText(jsonPath, json), "保存JSON", onRetry: m => Output.Add(m));

            // 计算源文件 hash
            var srcPath = Path.Combine("files", "vetting", VettingId, FileName);
            var fileHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(srcPath))).ToLowerInvariant();
            var providerId = Provider!.Identifier;

            // 用新解析器解析并收集警告
            var (operators, warnings) = Vetting.Copilot.OperatorParser.ParseWithWarnings(opsEl);
            foreach (var w in warnings) Output.Add($"⚠ {w}");

            // 提取 Type z 的 question 保存为 FileSpecialQuestion
            int questionCount = 0;
            using (var db = new Vetting.Copilot.Data.VettingDbContext())
            {
                var oldQuestions = db.FileSpecialQuestions.Find(q => q.FileHash == fileHash && q.Provider == providerId).ToArray();
                foreach (var old in oldQuestions)
                {
                    var oldAnswers = db.SpecialAnswers.Find(a => a.QuestionId == old.Id).ToArray();
                    foreach (var oa in oldAnswers) db.SpecialAnswers.Delete(oa.Id);
                    db.FileSpecialQuestions.Delete(old.Id);
                }

                int idx = 0;
                foreach (var op in operators)
                {
                    if (op is not ParagraphOp paraOp) continue;
                    if (string.IsNullOrWhiteSpace(paraOp.Question)) continue;

                    db.FileSpecialQuestions.Insert(new FileSpecialQuestion
                    {
                        FileHash = fileHash,
                        Provider = providerId,
                        Index = idx,
                        Question = paraOp.Question,
                    });
                    idx++;
                }
                questionCount = idx;
            }

            Output.Add($"已保存 {jsonPath} ({operators.Count} 操作, {questionCount} 个自定义问题)");
            foreach (var w in warnings) Output.Add($"  ⚠ {w}");
            Complete();
        }
        catch (Exception ex) { Fail(ex.Message); }
    }

    public static Task<string> LoadSysptAsync() => TemplateGenerator.LoadSysptAsync();

    private static int ExtractVersion(string content)
    {
        var match = System.Text.RegularExpressions.Regex.Match(content, @"<!--\s*version:(\d+)\s*-->");
        return match.Success ? int.Parse(match.Groups[1].Value) : 0;
    }

    private static string FormatElapsed(TimeSpan ts)
        => ts.TotalMinutes >= 1 ? $"{ts.Minutes}m{ts.Seconds:D2}s" : $"{ts.TotalSeconds:F1}s";
}
