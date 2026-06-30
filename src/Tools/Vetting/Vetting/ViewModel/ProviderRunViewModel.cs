using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FundOffice.Copilot.Models;
using FundOffice.Copilot.Providers;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using Vetting.Copilot;
using Vetting.Copilot.Data;
using Vetting.Copilot.Models.Entities;
using Vetting.Data;
using Vetting.Entity;

namespace Vetting.ViewModel;

/// <summary>
/// 单个 Provider 对单个文件的完整运行状态（解析 → AI回答 → 填充）
/// </summary>
public partial class ProviderRunViewModel : ObservableObject
{
    public string ProviderName { get; }
    public string ProviderId { get; }
    public ITokenProvider Provider { get; }
    public string FileName { get; }
    public string VettingId { get; }
    public string AbsolutePath { get; }
    public bool IsFullMode { get; set; }

    // ── 三阶段状态 ──
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    public partial TaskStatus ParseStatus { get; set; } = TaskStatus.Pending;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    public partial TaskStatus AnswerStatus { get; set; } = TaskStatus.Pending;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    public partial TaskStatus FillStatus { get; set; } = TaskStatus.Pending;

    public bool IsBusy => ParseStatus == TaskStatus.Running
                       || AnswerStatus == TaskStatus.Running
                       || FillStatus == TaskStatus.Running;

    // ── 共享进度 ──
    [ObservableProperty][NotifyPropertyChangedFor(nameof(UsageText))] public partial int Usage { get; set; }
    public string UsageText => Usage >= 1000 ? $"{Usage / 1000.0:F1}k tokens" : Usage > 0 ? $"{Usage} tokens" : "";
    [ObservableProperty] public partial string Elapsed { get; set; } = "";
    [ObservableProperty] public partial string? ErrorMessage { get; set; }
    [ObservableProperty] public partial string Tip { get; set; } = "";

    // ── 日志 ──
    public ObservableCollection<string> Logs { get; } = [];
    private readonly string _logPath;


    private readonly Stopwatch _sw = new();

    public ProviderRunViewModel(string providerName, string providerId, ITokenProvider provider,
        string fileName, string vettingId, string absolutePath)
    {
        ProviderName = providerName;
        ProviderId = providerId;
        Provider = provider;
        FileName = fileName;
        VettingId = vettingId;
        AbsolutePath = absolutePath;
        _logPath = Path.Combine("files", "vetting", "logs", $"{fileName}_{providerId}.txt");
    }

    private void Log(string message)
    {
        Logs.Add(message);
        // 追加写日志文件
        try
        {
            var dir = Path.GetDirectoryName(_logPath)!;
            Directory.CreateDirectory(dir);
            File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch { }
    }

    // ── 解析 ──────────────────────────────────────────

    [RelayCommand]
    public async Task RunParseAsync()
    {
        ParseStatus = TaskStatus.Running;
        ErrorMessage = null;
        _sw.Restart();
        try
        {
            // 记录解析前的文档结构
            var structure = FileRetry.Run(() => DocOps.ParseDocument(AbsolutePath), "解析文档");
            if (string.IsNullOrWhiteSpace(structure)) { Fail("无法解析文档"); return; }

            // 记录文档结构到日志
            Log("=== 文档结构 ===");
            Log(structure);

            var sysPrompt = await TemplateGenerator.LoadSysptAsync();
            var messages = new[]
            {
                ChatMessage.System(sysPrompt),
                ChatMessage.User(structure + PredFiles.BuildPromptSection())
            };
            var options = new ChatOptions
            {
                AdditionalProperties = new Dictionary<string, object>
                {
                    ["response_format"] = new { type = "json_object" }
                }
            };

            var sb = new StringBuilder();
            var reasoningSb = new StringBuilder();
            await foreach (var token in Provider.ChatCompletionStreamAsync(messages, options: options))
            {
                switch (token)
                {
                    case TextDelta td:
                        sb.Append(td.Text);
                        Usage = (sb.Length + reasoningSb.Length) / 4;
                        break;
                    case ReasoningDelta rd:
                        reasoningSb.Append(rd.Text);
                        Usage = (sb.Length + reasoningSb.Length) / 4;
                        break;
                    case UsageUpdate u:
                        Usage = (u.PromptTokens ?? 0) + (u.CompletionTokens ?? 0);
                        break;
                }
            }

            var json = sb.ToString().Trim();

            // 校验 JSON 格式
            using var jsonDoc = JsonDocument.Parse(json);
            var root = jsonDoc.RootElement;
            if (!root.TryGetProperty("operations", out var opsEl) || opsEl.ValueKind != JsonValueKind.Array)
            {
                Fail("AI 返回的 JSON 缺少 operations 数组");
                return;
            }

            // 保存到数据库
            using (var db = new VettingAppDbContext())
            {
                db.ParsedJsons.Insert(new ParsedJson
                {
                    FileName = FileName,
                    Provider = ProviderId,
                    Time = DateTime.Now,
                    Json = json,
                });
            }

            // 解析并收集警告
            var (operators, warnings) = OperatorParser.ParseWithWarnings(opsEl);
            foreach (var w in warnings) Log($"⚠ {w}");

            // 记录解析结果到日志
            Log("=== 解析结果 ===");
            foreach (var op in operators)
            {
                Log(FormatOperator(op));
            }

            // 提取 Type z 的 question 保存为 FileSpecialQuestion
            int questionCount = 0;
            using (var db = new VettingDbContext())
            {
                var oldQuestions = db.FileSpecialQuestions.Find(q => q.FileName == FileName && q.Provider == ProviderId).ToArray();
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
                        FileName = FileName,
                        Provider = ProviderId,
                        Index = idx,
                        Question = paraOp.Question,
                    });
                    idx++;
                }
                questionCount = idx;
            }

            Log($"已保存到数据库 ({operators.Count} 操作, {questionCount} 个自定义问题)");
            _sw.Stop();
            ParseStatus = TaskStatus.Done;
            Elapsed = FormatElapsed(_sw.Elapsed);
            Tip = $"解析完成: {operators.Count} 操作, {questionCount} 问题";
        }
        catch (Exception ex) { Fail(ex.Message); }
    }

    // ── AI 回答 ──────────────────────────────────────

    [RelayCommand]
    public async Task RunAnswerAsync()
    {
        AnswerStatus = TaskStatus.Running;
        ErrorMessage = null;
        _sw.Restart();
        try
        {
            using var db = new VettingDbContext();
            var questions = db.FileSpecialQuestions
                .Find(q => q.FileName == FileName && q.Provider == ProviderId)
                .OrderBy(q => q.Index).ToArray();

            if (questions.Length == 0)
            {
                Log("没有自定义问题");
                _sw.Stop(); AnswerStatus = TaskStatus.Done; Elapsed = FormatElapsed(_sw.Elapsed);
                Tip = "无需回答";
                return;
            }

            var qaList = db.QA.FindAll().ToArray();
            var prompt = CustomQuestionAnswerer.BuildPrompt(qaList, questions, IsFullMode);
            var systemPrompt = CustomQuestionAnswerer.GetSystemPrompt(IsFullMode);
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
            var reasoningSb = new StringBuilder();
            await foreach (var token in Provider.ChatCompletionStreamAsync(messages, options: options))
            {
                switch (token)
                {
                    case TextDelta td:
                        sb.Append(td.Text);
                        Usage = (sb.Length + reasoningSb.Length) / 4;
                        break;
                    case ReasoningDelta rd:
                        reasoningSb.Append(rd.Text);
                        Usage = (sb.Length + reasoningSb.Length) / 4;
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

            int count = 0, exactCount = 0, inferredCount = 0;
            foreach (var prop in answersProp.EnumerateObject())
            {
                var key = prop.Name;
                if (!key.StartsWith('a') || !int.TryParse(key.TrimStart('a'), out var idx)) continue;
                var q = questions.FirstOrDefault(x => x.Index == idx);
                if (q == null) continue;
                var answer = prop.Value.GetString() ?? "";
                var (processedAnswer, isInferred) = CustomQuestionAnswerer.ProcessAnswer(answer, IsFullMode);
                answer = processedAnswer;

                if (!string.IsNullOrWhiteSpace(answer))
                {
                    if (isInferred) inferredCount++;
                    else exactCount++;
                }

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

                Log($"{{{{a{idx}}}}}  {q.Question}\n    → {answer}");
                count++;
            }

            var tip = $"精确 {exactCount} 条" + (IsFullMode ? $"，推断 {inferredCount} 条" : "") + $"，共 {count} 条";
            Log($"回答完成：{tip}");
            _sw.Stop(); AnswerStatus = TaskStatus.Done; Elapsed = FormatElapsed(_sw.Elapsed);
            Tip = tip;
        }
        catch (Exception ex)
        {
            _sw.Stop(); AnswerStatus = TaskStatus.Error;
            ErrorMessage = ex.Message; Elapsed = FormatElapsed(_sw.Elapsed);
            Log($"错误: {ex.Message}");
        }
    }

    // ── 填充（导出报告）──────────────────────────────

    [RelayCommand]
    public async Task RunFillAsync()
    {
        FillStatus = TaskStatus.Running;
        ErrorMessage = null;
        _sw.Restart();
        try
        {
            var finalDir = Path.Combine("files", "vetting", VettingId, "final", ProviderId);
            Directory.CreateDirectory(finalDir);

            // 从数据库读取最新的解析 JSON 和推荐配置
            string? json = null;
            int[] recommanded;
            using (var db = new VettingAppDbContext())
            {
                var record = db.ParsedJsons.Query()
                    .Where(j => j.FileName == FileName && j.Provider == ProviderId)
                    .OrderByDescending(j => j.Time)
                    .FirstOrDefault();
                json = record?.Json;

                var existing = db.TemplateRecommends.FindOne(r => r.FileName == FileName);
                recommanded = existing?.FundIds?.Split(',').Select(x => int.TryParse(x, out var d) ? d : 0).Where(x => x > 0).ToArray() ?? [];

                if (recommanded.Length == 0)
                {
                    var rec = db.TemplateRecommends.FindOne(r => r.FileName == "__global__");
                    recommanded = rec?.FundIds?.Split(',').Select(x => int.TryParse(x, out var d) ? d : 0).Where(x => x > 0).ToArray() ?? [];
                }
            }
            if (json == null) { Log("无解析结果（请先解析）"); Fail("无解析结果"); return; }
            using var jsonDoc = JsonDocument.Parse(json);
            var operators = OperatorParser.Parse(jsonDoc.RootElement.GetProperty("operations"));
            Log($"已解析 {operators.Count} 个操作");

            // 收集 files 并直接映射（单 provider 无需投票）
            List<KeyValuePair<int, string>> fileMappings = [];
            if (jsonDoc.RootElement.TryGetProperty("files", out var filesEl))
            {
                var availableNames = new HashSet<string>(PredFiles.ListNames());
                var (fs, _) = OperatorParser.ParseFiles(filesEl, availableNames);
                foreach (var f in fs)
                {
                    if (!string.IsNullOrEmpty(f.Map))
                        fileMappings.Add(new KeyValuePair<int, string>(f.Index, f.Map));
                }
            }


            var resolver = await Task.Run(() => DataResolver.Load(FileName, ProviderId, recommanded));
            var outPath = Path.Combine(finalDir, $"{FileName}");
            await Task.Run(() => FileRetry.Run(
                () => DocOps.Fill(AbsolutePath, outPath, operators, resolver),
                "填充文档",
                onRetry: msg => Log(msg)));

            // 复制附件
            if (fileMappings.Count > 0)
                PredFiles.CopyMappedFiles(finalDir, fileMappings, onLog: msg => Log(msg));

            Log($"已生成: {outPath}");
            _sw.Stop(); FillStatus = TaskStatus.Done; Elapsed = FormatElapsed(_sw.Elapsed);
            Tip = "报告已导出";
        }
        catch (Exception ex)
        {
            Fail(ex.Message);
        }
    }

    // ── 查看解析结果 ──────────────────────────────────

    [RelayCommand]
    public void ViewParseResult()
    {
        var vm = new ParseResultViewModel(FileName, ProviderId, VettingId);
        var win = new View.ParseResultWindow { Owner = Application.Current.MainWindow, DataContext = vm };
        win.Show();
    }

    // ── 查看日志 ──────────────────────────────────────

    [RelayCommand]
    public void ViewLog()
    {
        if (!File.Exists(_logPath))
        {
            HandyControl.Controls.Growl.Info("暂无日志");
            return;
        }
        Process.Start(new ProcessStartInfo(_logPath) { UseShellExecute = true });
    }


    [RelayCommand]
    public void OpenFolder()
    {
        var finalDir = Path.Combine("files", "vetting", VettingId, "final", ProviderId);

        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe")
            {
                Arguments = finalDir,
                UseShellExecute = true
            });
        }
        catch { }
    }

    // ── 辅助方法 ──────────────────────────────────────

    private void Fail(string message)
    {
        _sw.Stop();
        // 设置当前正在运行的阶段为 Error
        if (ParseStatus == TaskStatus.Running) ParseStatus = TaskStatus.Error;
        if (AnswerStatus == TaskStatus.Running) AnswerStatus = TaskStatus.Error;
        if (FillStatus == TaskStatus.Running) FillStatus = TaskStatus.Error;
        ErrorMessage = message;
        Elapsed = FormatElapsed(_sw.Elapsed);
        Log($"错误: {message}");
    }

    private static string FormatElapsed(TimeSpan ts)
        => ts.TotalMinutes >= 1 ? $"{ts.Minutes}m{ts.Seconds:D2}s" : $"{ts.TotalSeconds:F1}s";

    private static string FormatOperator(FillOperator op)
    {
        return op switch
        {
            ScalarOp s => s.Location.IsParagraph
                ? $"[a] {s.Entity}.{s.Property} → P[{s.Location.Para}]"
                : $"[a] {s.Entity}.{s.Property} → T[{s.Location.Table}][{s.Location.Row},{s.Location.Col}]",
            RecommendOp r => $"[b] fund#{r.FundIndex} T[{r.Range.Table}] props={r.Props.Count}",
            ListExpandOp c => $"[c] {c.Entity} T[{c.Range.Table}] rows={c.Range.Start.Row}..{c.Range.End.Row} props={c.Properties.Count}",
            GridOp g when g.EntityPerRow => $"[d] {g.Entity} T[{g.Range.Table}] rows={g.Range.Start.Row}..{g.Range.End.Row} cols={g.Range.Start.Col}..{g.Range.End.Col} filter_by={g.FilterBy} props={g.Properties.Count}\n    {string.Join("\n    ", g.Properties.Select(p => $"prop={p.Prop} row={p.Row} col={p.Col}"))}",
            GridOp g => $"[e] {g.Entity} T[{g.Range.Table}] rows={g.Range.Start.Row}..{g.Range.End.Row} cols={g.Range.Start.Col}..{g.Range.End.Col} filter_by={g.FilterBy} props={g.Properties.Count}\n    {string.Join("\n    ", g.Properties.Select(p => $"prop={p.Prop} row={p.Row} col={p.Col}"))}",
            ParagraphOp z => $"[z] \"{z.Question}\" → P[{z.Location.Para}]",
            UnknownTableOp u => $"[g] T[{u.Range.Table}] {u.Description}",
            _ => $"[?] {op.GetType().Name}"
        };
    }
}
