using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using Vetting.Data;
using Vetting.Entity;

namespace Vetting.ViewModel;

public partial class VettingReportViewModel : ObservableObject
{
    public VettingReport Report { get; }
    public string Id => Report.Id;
    public string FolderPath => Path.Combine("files", "vetting", Id);
    public DateTime CreateTime => Report.CreateTime;
    [ObservableProperty] public partial string Name { get; set; }
    [ObservableProperty] public partial string NameEdit { get; set; }
    public ObservableCollection<ReportFileViewModel> OriginalFiles { get; } = [];

    private FileSystemWatcher? _watcher;

    public VettingReportViewModel(VettingReport report)
    {
        Report = report;
        Name = report.Name;
        NameEdit = report.Name;
    }

    public void StartWatching()
    {
        Directory.CreateDirectory(FolderPath);
        ReloadFiles();
        _watcher = new FileSystemWatcher(FolderPath) { NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size };
        _watcher.Created += (_, _) => ReloadFiles();
        _watcher.Deleted += (_, _) => ReloadFiles();
        _watcher.Renamed += (_, _) => ReloadFiles();
        _watcher.EnableRaisingEvents = true;
    }

    public void StopWatching()
    {
        if (_watcher is { } w) { w.EnableRaisingEvents = false; w.Dispose(); _watcher = null; }
    }

    private void ReloadFiles()
    {
        var files = new DirectoryInfo(FolderPath).GetFiles();
        App.Current.Dispatcher.Invoke(() =>
        {
            OriginalFiles.Clear();
            foreach (var f in files) OriginalFiles.Add(new ReportFileViewModel(f, Id));
        });
    }

    [RelayCommand]
    private void SaveName()
    {
        Name = NameEdit;
        SaveToDb();
    }

    public void SaveToDb()
    {
        using var db = new VettingDbContext();
        db.Reports.Upsert(new VettingReport(Id, Name, CreateTime));
    }
}

public partial class ReportFileViewModel : ObservableObject, IRecipient<AIProviderChanged>
{
    public required string FileName { get; set; }
    public required string AbsolutePath { get; set; }
    public string VettingId { get; set; } = "";
    [ObservableProperty] public partial bool IsExpanded { get; set; }
    [ObservableProperty] public partial ObservableCollection<VettingParseTaskViewModel> Tasks { get; set; } = [];
    public ObservableCollection<AIProviderItemViewModel> Providers { get; } = [];

    [SetsRequiredMembers]
    public ReportFileViewModel(FileInfo fileInfo, string vettingId)
    {
        FileName = fileInfo.Name;
        AbsolutePath = fileInfo.FullName;
        VettingId = vettingId;
        using var db = new VettingDbContext();
        foreach (var config in db.AIProviderConfigs.FindAll())
            Providers.Add(new AIProviderItemViewModel(config));
    }

    [RelayCommand]
    private async Task GenerateTemplatesAsync()
    {
        var sel = Providers.Where(x => x.IsSelected).ToArray();
        if (sel.Length == 0) return;

        IsExpanded = true;
        Tasks = [.. sel.Select(provider => new VettingParseTaskViewModel { TaskName = provider.Name, Provider = CreateProvider(provider) })];
        await Task.WhenAll(Tasks.Select(RunSingleTaskAsync).ToArray());
    }

    private async Task RunSingleTaskAsync(VettingParseTaskViewModel task)
    {
        task.Start();
        try
        {
            // Step 1: 解析文档结构
            var structure = FundOffice.Vetting.Services.DocOps.AnalyzeStructure(AbsolutePath);

            // Step 2: 读取 system prompt（本地版本优先），调用 AI
            var sysPrompt = await LoadSysptAsync();
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
            await foreach (var token in task.Provider!.ChatCompletionStreamAsync(messages, options: options))
            {
                switch (token)
                {
                    case FundOffice.Copilot.Models.TextDelta td:
                        sb.Append(td.Text);
                        task.Usage = sb.Length / 4; // 估算: ~4字符/token
                        break;
                    case FundOffice.Copilot.Models.UsageUpdate u:
                        task.Usage = (u.PromptTokens ?? 0) + (u.CompletionTokens ?? 0);
                        break;
                }
            }

            // Step 3: 校验 JSON，调用 DocOps 生成模板文件
            var json = sb.ToString().Trim();
            using var jsonDoc = System.Text.Json.JsonDocument.Parse(json);
            var root = jsonDoc.RootElement;

            var tplDir = Path.Combine("files", "vetting", VettingId, "tpl");
            Directory.CreateDirectory(tplDir);
            var tplPath = Path.Combine(tplDir, FileName);
            File.Copy(AbsolutePath, tplPath, overwrite: true);

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
            task.Output.Add($"模板已生成: {tplPath} ({ops.Count} 操作, {placeholders} 占位符)");
            task.Complete();
        }
        catch (Exception ex) { task.Fail(ex.Message); }
    }

    private static async Task<string> LoadSysptAsync()
    {
        var localPath = Path.Combine("files", "vetting", "syspt.md");
        var asm = typeof(ReportFileViewModel).Assembly;
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

    private static FundOffice.Copilot.Providers.ITokenProvider CreateProvider(AIProviderItemViewModel vm) => vm.ProviderType switch
    {
        "Anthropic" => new FundOffice.Copilot.Providers.AnthropicTokenProvider(
            new FundOffice.Copilot.Configuration.AnthropicOptions { ApiKey = vm.ApiKey, BaseUrl = vm.BaseUrl }),
        _ => new FundOffice.Copilot.Providers.OpenAITokenProvider(
            new FundOffice.Copilot.Configuration.OpenAIOptions { ApiKey = vm.ApiKey, BaseUrl = vm.BaseUrl }),
    };

    public void Receive(AIProviderChanged message)
    {
        switch (message.Type)
        {
            case ChangedType.Add:
                using (var db = new VettingDbContext())
                {
                    if (db.AIProviderConfigs.FindById(message.Id) is AIProviderConfig config)
                        Providers.Add(new AIProviderItemViewModel(config));
                }
                break;
            case ChangedType.Update:
                using (var db = new VettingDbContext())
                {
                    if (db.AIProviderConfigs.FindById(message.Id) is AIProviderConfig config)
                    {
                        var idx = Providers.IndexOf(Providers.FirstOrDefault(p => p.Id == message.Id)!);
                        if (idx >= 0) Providers[idx] = new AIProviderItemViewModel(config);
                    }
                }
                break;
            case ChangedType.Delete:
                if (Providers.FirstOrDefault(p => p.Id == message.Id) is { } toDelete)
                {
                    Providers.Remove(toDelete);
                }
                break;
            default:
                break;
        }
    }
}
