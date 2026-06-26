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
            // Step 2: 调用 AI
            var messages = new[]
            {
                FundOffice.Copilot.Models.ChatMessage.System(sysPrompt),
                FundOffice.Copilot.Models.ChatMessage.User(structure)
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

            // Step 3: 校验 JSON，调用 DocOps 生成模板文件
            var json = sb.ToString().Trim();
            using var jsonDoc = System.Text.Json.JsonDocument.Parse(json);
            var root = jsonDoc.RootElement;

            var safeName = Path.GetFileNameWithoutExtension(FileName);
            var ext = Path.GetExtension(FileName);
            var tplDir = Path.Combine("files", "vetting", VettingId, "tpl");
            Directory.CreateDirectory(tplDir);
            var srcPath = Path.Combine("files", "vetting", VettingId, FileName);
            var tplPath = Path.Combine(tplDir, $"{safeName}_by[{Provider.Identifier}]{ext}");
            FileRetry.Run(() => File.Copy(srcPath, tplPath, overwrite: true), "复制源文件", onRetry: m => Output.Add(m));


            // 保存返回json，用于调试
            FileRetry.Run(() => File.WriteAllText(Path.Combine(tplDir, $"{safeName}_by[{Provider.Identifier}].json"), json), "保存JSON", onRetry: m => Output.Add(m));

            var ops = new List<(string tool, Dictionary<string, System.Text.Json.JsonElement> input)>();
            foreach (var op in root.GetProperty("operations").EnumerateArray())
            {
                var tool = op.GetProperty("tool").GetString()!;
                var input = new Dictionary<string, System.Text.Json.JsonElement>();
                foreach (var prop in op.EnumerateObject())
                {
                    // 修复 AI 错误: {{product_XXX}} → {{product.XXX}}
                    if (prop.Name == "text" && prop.Value.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        var fixedText = System.Text.RegularExpressions.Regex.Replace(
                            prop.Value.GetString()!, @"\{\{product_", "{{product.");
                        input[prop.Name] = System.Text.Json.JsonSerializer.SerializeToElement(fixedText);
                    }
                    else
                        input[prop.Name] = prop.Value.Clone();
                }
                ops.Add((tool, input));
            }
            FileRetry.Run(() => Vetting.Copilot.DocOps.BatchWrite(tplPath, ops), "生成模板", onRetry: m => Output.Add(m));

            var placeholders = root.TryGetProperty("placeholders", out var ph) ? ph.EnumerateObject().Count() : 0;
            Output.Add($"模板已生成: {tplPath} ({ops.Count} 操作, {placeholders} 占位符)");
            Complete();


            // save FileSpecialQuestion
            if (root.TryGetProperty("placeholders", out var phEl) && phEl.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                var fileHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(srcPath))).ToLowerInvariant();
                var providerId = Provider!.Identifier;
                using var db = new Vetting.Copilot.Data.VettingDbContext();
                db.FileSpecialQuestions.DeleteMany(q => q.FileHash == fileHash && q.Provider == providerId);
                var questions = phEl.EnumerateObject()
                    .Where(p => p.Name.StartsWith('a') && int.TryParse(p.Name.TrimStart('a'), out _))
                    .Select(p => new FileSpecialQuestion
                    {
                        FileHash = fileHash,
                        Provider = providerId,
                        Index = int.Parse(p.Name.TrimStart('a')),
                        Question = p.Value.GetString()
                    }).ToArray();
                db.FileSpecialQuestions.InsertBulk(questions);
            }


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
