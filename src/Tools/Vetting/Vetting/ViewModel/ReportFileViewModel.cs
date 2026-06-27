using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FundOffice.Copilot.Providers;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using Vetting.Copilot.Data;
using Vetting.Copilot.Models;
using Vetting.Copilot.Models.Entities;
using Vetting.Copilot.Models.Info;
using Vetting.Copilot;

namespace Vetting.ViewModel;

public partial class ReportFileViewModel : ObservableObject
{
    public required string FileName { get; set; }
    public required string AbsolutePath { get; set; }
    public string VettingId { get; set; } = "";
    [ObservableProperty] public partial bool IsExpanded { get; set; }
    [ObservableProperty] public partial bool IsRecommendOpen { get; set; }

    public ObservableCollection<VettingParseTaskViewModel> Tasks { get; } = [];
    [ObservableProperty] public partial string Output { get; set; } = "";
    public ObservableCollection<QuestionAnswerTaskViewModel> AIStatuses { get; } = [];

    // 推荐产品（文件级，所有 provider 共用）
    public ObservableCollection<FundInfoVM> AvailableFunds { get; } = [];
    public ObservableCollection<FundInfoVM> RecommendedFunds { get; } = [];
    [ObservableProperty] public partial FundInfoVM? SelectedAvailable { get; set; }
    [ObservableProperty] public partial FundInfoVM? SelectedRecommended { get; set; }

    private string _fileHash = "";

    [SetsRequiredMembers]
    public ReportFileViewModel(FileInfo fileInfo, string vettingId)
    {
        FileName = fileInfo.Name;
        AbsolutePath = fileInfo.FullName;
        VettingId = vettingId;
        _fileHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(AbsolutePath))).ToLowerInvariant();

        using var db = new VettingDbContext();
        foreach (var f in db.FundInfos.FindAll())
            AvailableFunds.Add(new FundInfoVM(f));

        var rec = db.TemplateRecommends.FindOne(r => r.FileHash == _fileHash);
        if (rec?.FundIds != null)
        {
            var ids = rec.FundIds.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
            foreach (var id in ids)
            {
                var fund = AvailableFunds.FirstOrDefault(f => f.Entity.Id == id);
                if (fund != null) RecommendedFunds.Add(fund);
            }
        }
    }

    private void Log(string message) => Output += message + Environment.NewLine;

    // ── 推荐产品（文件级）──────────────────────────────

    public void SaveRecommend()
    {
        using var db = new VettingDbContext();
        var ids = string.Join(",", RecommendedFunds.Select(f => f.Entity.Id));
        var existing = db.TemplateRecommends.FindOne(r => r.FileHash == _fileHash);
        if (existing != null)
        {
            existing.FundIds = ids;
            db.TemplateRecommends.Update(existing);
        }
        else if (RecommendedFunds.Count > 0)
        {
            db.TemplateRecommends.Insert(new TemplateRecommend { FileHash = _fileHash, ProviderId = "", FundIds = ids });
        }
    }

    [RelayCommand]
    private void OpenFile() => Process.Start(new ProcessStartInfo(AbsolutePath) { UseShellExecute = true });

    // ── 解析（调用所有选中 provider）──────────────────

    [RelayCommand]
    private async Task GenerateTemplatesAsync()
    {
        var sel = MainWindowViewModel.GlobalProviders.Where(x => x.IsSelected).ToArray();
        if (sel.Length == 0) { HandyControl.Controls.Growl.Warning("请先选择 AI 接口"); return; }

        var structure = FileRetry.Run(() => DocOps.ParseDocument(AbsolutePath), "解析文档");
        if (string.IsNullOrWhiteSpace(structure)) { HandyControl.Controls.Growl.Warning("无法解析文档"); return; }

        IsExpanded = true;
        var sysPrompt = await VettingParseTaskViewModel.LoadSysptAsync();
        Tasks.Clear();
        foreach (var provider in sel)
        {
            Tasks.Add(new VettingParseTaskViewModel
            {
                TaskName = provider.Name,
                Provider = CreateProvider(provider),
                VettingId = VettingId,
                FileName = FileName
            });
        }

        // 解析完成后立即填充模板（每个 provider 独立填一份），files 跨 provider 投票合并
        var recommendIds = GetRecommendIds();
        var fileVotes = new Dictionary<int, Dictionary<string, int>>();
        // 注意：async lambda 的 ContinueWith 返回 Task<Task>，必须 Unwrap 才能让 WhenAll 等到 FillAsync 完成
        var fillTasks = Tasks.Select(t => t.RunAsync(structure, sysPrompt)
            .ContinueWith(async _ =>
            {
                if (t.Status == TaskStatus.Done)
                    await FillAsync(t.Provider!.Identifier, recommendIds, fileVotes);
            })
            .Unwrap());
        await Task.WhenAll(fillTasks);

        // 复制已映射的附件到 final：文件名 {Index}.{Map}（按 Index 多数投票选 Map）
        CopyMappedFiles(fileVotes);
    }

    // ── 查看问题（合并所有 provider 去重）──────────────

    [RelayCommand]
    private void ViewCustomQuestions()
    {
        // 若数据库无问题，尝试从 tpl 下所有 provider 的 JSON 解析入库
        EnsureQuestionsFromJson();

        var vm = new CustomQuestionsViewModel(_fileHash, FileName);
        if (vm.Questions.Count == 0) { HandyControl.Controls.Growl.Warning("没有找到自定义问题"); return; }
        var win = new View.CustomQuestionsWindow { DataContext = vm, Owner = Application.Current.MainWindow };
        win.Show();
    }

    private void EnsureQuestionsFromJson()
    {
        using var db = new VettingDbContext();
        if (db.FileSpecialQuestions.Find(q => q.FileHash == _fileHash).Any()) return;

        var tplDir = Path.Combine("files", "vetting", VettingId, "tpl");
        if (!Directory.Exists(tplDir)) return;

        var safeName = Path.GetFileNameWithoutExtension(FileName);
        var jsonFiles = Directory.GetFiles(tplDir, $"{safeName}_by[*].json");
        foreach (var jsonPath in jsonFiles)
        {
            var m = Regex.Match(Path.GetFileNameWithoutExtension(jsonPath), @"_by\[(.+)\]$");
            var providerId = m.Success ? m.Groups[1].Value : "unknown";
            try
            {
                var json = File.ReadAllText(jsonPath);
                using var jsonDoc = JsonDocument.Parse(json);
                if (!jsonDoc.RootElement.TryGetProperty("operations", out var opsEl)) continue;
                var (operators, _) = OperatorParser.ParseWithWarnings(opsEl);

                int idx = 0;
                foreach (var op in operators)
                {
                    if (op is not ParagraphOp paraOp) continue;
                    if (string.IsNullOrWhiteSpace(paraOp.Question)) continue;
                    db.FileSpecialQuestions.Insert(new FileSpecialQuestion
                    {
                        FileHash = _fileHash,
                        Provider = providerId,
                        Index = idx,
                        Question = paraOp.Question,
                    });
                    idx++;
                }
            }
            catch { }
        }
    }

    // ── AI 回答（一次调用所有 provider）────────────────

    [RelayCommand]
    private async Task AIAnswerCustomQuestionsAsync()
    {
        var sel = MainWindowViewModel.GlobalProviders.Where(p => p.IsSelected).ToArray();
        if (sel.Length == 0) { HandyControl.Controls.Growl.Warning("请先选择 AI 接口"); return; }

        EnsureQuestionsFromJson();

        AIStatuses.Clear();
        IsExpanded = true;
        var tasks = sel.Select(p =>
        {
            var provider = CustomQuestionAnswerer.CreateProvider(p.Name, p.ProviderType, p.ApiKey, p.BaseUrl, p.Model);
            var vm = new QuestionAnswerTaskViewModel(provider, p.Name, _fileHash, p.Name);
            AIStatuses.Add(vm);
            return vm.RunAsync(line => Log(line));
        });
        var counts = await Task.WhenAll(tasks);
        Log($"AI 回答完成，共 {counts.Sum()} 条");
    }

    // ── 填充（一次调用所有 provider 的 JSON）──────────

    [RelayCommand]
    private async Task FillTemplateAsync()
    {
        Output = "";
        IsExpanded = true;

        var recommendIds = GetRecommendIds();
        var fileVotes = new Dictionary<int, Dictionary<string, int>>();

        var tplDir = Path.Combine("files", "vetting", VettingId, "tpl");
        var jsonFiles = Directory.Exists(tplDir)
            ? Directory.GetFiles(tplDir, $"{Path.GetFileNameWithoutExtension(FileName)}_by[*].json")
            : [];
        if (jsonFiles.Length == 0) { Log("没有找到解析结果 JSON（请先解析）"); return; }

        foreach (var jsonPath in jsonFiles)
        {
            var m = Regex.Match(Path.GetFileNameWithoutExtension(jsonPath), @"_by\[(.+)\]$");
            var providerId = m.Success ? m.Groups[1].Value : "unknown";
            await FillAsync(providerId, recommendIds, fileVotes);
        }

        CopyMappedFiles(fileVotes);
    }

    /// <summary>推荐产品 Id：文件级优先，回退全局推荐</summary>
    private int[] GetRecommendIds()
    {
        var ids = RecommendedFunds.Select(f => f.Entity.Id).ToArray();
        if (ids.Length > 0) return ids;
        using var db = new VettingDbContext();
        var rec = db.TemplateRecommends.FindOne(r => r.FileHash == "__global__");
        return rec?.FundIds != null
            ? rec.FundIds.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray()
            : [];
    }

    /// <summary>
    /// 单个 provider 的填充：读取其 JSON → 解析操作 → 收集 files 投票 → 生成填充文档。
    /// 多 provider 并发调用时，fileVotes 需加锁。
    /// </summary>
    private async Task FillAsync(string providerId, int[] recommendIds, Dictionary<int, Dictionary<string, int>> fileVotes)
    {
        var tplDir = Path.Combine("files", "vetting", VettingId, "tpl");
        var finalDir = Path.Combine("files", "vetting", VettingId, "final");
        Directory.CreateDirectory(finalDir);

        var safeName = Path.GetFileNameWithoutExtension(FileName);
        var ext = Path.GetExtension(FileName);
        var jsonPath = Path.Combine(tplDir, $"{safeName}_by[{providerId}].json");
        if (!File.Exists(jsonPath)) { Log($"[{providerId}] 无解析结果 JSON"); return; }

        try
        {
            var json = await File.ReadAllTextAsync(jsonPath);
            using var jsonDoc = JsonDocument.Parse(json);
            var operators = OperatorParser.Parse(jsonDoc.RootElement.GetProperty("operations"));
            Log($"[{providerId}] 已解析 {operators.Count} 个操作");

            // 收集 files 投票（并发安全）
            if (jsonDoc.RootElement.TryGetProperty("files", out var filesEl))
            {
                var availableNames = new HashSet<string>(PredFiles.ListNames());
                var (fs, _) = OperatorParser.ParseFiles(filesEl, availableNames);
                foreach (var f in fs)
                {
                    if (string.IsNullOrEmpty(f.Map)) continue;
                    lock (fileVotes)
                    {
                        if (!fileVotes.ContainsKey(f.Index)) fileVotes[f.Index] = new();
                        fileVotes[f.Index].TryGetValue(f.Map!, out var c);
                        fileVotes[f.Index][f.Map!] = c + 1;
                    }
                }
            }

            var resolver = await Task.Run(() => DataResolver.Load(_fileHash, providerId, recommendIds));
            var outPath = Path.Combine(finalDir, $"{safeName}_filled_by[{providerId}]{ext}");
            await Task.Run(() => FileRetry.Run(
                () => DocOps.Fill(AbsolutePath, outPath, operators, resolver),
                "填充文档",
                onRetry: msg => Log(msg)));
            Log($"[{providerId}] 已生成: {outPath}");
        }
        catch (Exception ex)
        {
            Log($"[{providerId}] 填充失败: {ex.Message}");
        }
    }

    /// <summary>按 Index 多数投票选 Map，把已映射附件复制到 final：{Index}.{Map}</summary>
    private void CopyMappedFiles(Dictionary<int, Dictionary<string, int>> fileVotes)
    {
        var finalDir = Path.Combine("files", "vetting", VettingId, "final");
        var winners = fileVotes.Select(kv => new KeyValuePair<int, string>(
            kv.Key, kv.Value.OrderByDescending(v => v.Value).First().Key));
        PredFiles.CopyMappedFiles(finalDir, winners, onLog: msg => Log(msg));
    }

    private static ITokenProvider CreateProvider(AIProviderItemViewModel vm) => vm.ProviderType switch
    {
        "Anthropic" => new AnthropicTokenProvider(
            new FundOffice.Copilot.Configuration.AnthropicOptions { Identifier = vm.Name, ApiKey = vm.ApiKey, BaseUrl = vm.BaseUrl, Model = vm.Model }),
        _ => new OpenAITokenProvider(
            new FundOffice.Copilot.Configuration.OpenAIOptions { Identifier = vm.Name, ApiKey = vm.ApiKey, BaseUrl = vm.BaseUrl, Model = vm.Model }),
    };
}
