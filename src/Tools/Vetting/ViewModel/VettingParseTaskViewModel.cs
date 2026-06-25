using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FundOffice.Copilot.Providers;
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

            var tplDir = Path.Combine("files", "vetting", VettingId, "tpl");
            Directory.CreateDirectory(tplDir);
            var srcPath = Path.Combine("files", "vetting", VettingId, FileName);
            var tplPath = Path.Combine(tplDir, FileName);
            File.Copy(srcPath, tplPath, overwrite: true);

            // 保存返回json，用于调试
            File.WriteAllText(Path.Combine(tplDir, Path.GetFileNameWithoutExtension(FileName) + ".json"), json);

            var ops = new List<(string tool, Dictionary<string, System.Text.Json.JsonElement> input)>();
            foreach (var op in root.GetProperty("operations").EnumerateArray())
            {
                var tool = op.GetProperty("tool").GetString()!;
                var input = new Dictionary<string, System.Text.Json.JsonElement>();
                foreach (var prop in op.EnumerateObject())
                    input[prop.Name] = prop.Value.Clone();
                ops.Add((tool, input));
            }
            FundOffice.Vetting.Services.DocOps.BatchWrite(tplPath, ops);

            var placeholders = root.TryGetProperty("placeholders", out var ph) ? ph.EnumerateObject().Count() : 0;
            Output.Add($"模板已生成: {tplPath} ({ops.Count} 操作, {placeholders} 占位符)");
            Complete();
        }
        catch (Exception ex) { Fail(ex.Message); }
    }

    public static async Task<string> LoadSysptAsync()
    {
        var localPath = Path.Combine("files", "vetting", "syspt.md");
        var asm = typeof(VettingParseTaskViewModel).Assembly;
        using var embeddedSr = new StreamReader(asm.GetManifestResourceStream("Vetting.syspt.md")!);
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
        var match = System.Text.RegularExpressions.Regex.Match(content, @"<!--\s*version:(\d+)\s*-->");
        return match.Success ? int.Parse(match.Groups[1].Value) : 0;
    }

    private static string FormatElapsed(TimeSpan ts)
        => ts.TotalMinutes >= 1 ? $"{ts.Minutes}m{ts.Seconds:D2}s" : $"{ts.TotalSeconds:F1}s";
}
