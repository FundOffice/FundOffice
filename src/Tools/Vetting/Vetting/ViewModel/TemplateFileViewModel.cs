using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using Vetting.Copilot.Data;
using Vetting.Copilot.Models.Entities;
using Vetting.Copilot.Models.Info;
using Vetting.Copilot;

namespace Vetting.ViewModel;

public partial class TemplateFileViewModel : ObservableObject
{
    public required string FileName { get; set; }
    public required string AbsolutePath { get; set; }
    public string VettingId { get; set; } = "";
    [ObservableProperty] public partial bool IsExpanded { get; set; }
    [ObservableProperty] public partial bool IsRecommendOpen { get; set; }
    public ObservableCollection<string> Output { get; } = [];
    public ObservableCollection<QuestionAnswerTaskViewModel> AIStatuses { get; } = [];

    // 推荐产品
    public ObservableCollection<FundInfoVM> AvailableFunds { get; } = [];
    public ObservableCollection<FundInfoVM> RecommendedFunds { get; } = [];
    [ObservableProperty] public partial FundInfoVM? SelectedAvailable { get; set; }
    [ObservableProperty] public partial FundInfoVM? SelectedRecommended { get; set; }

    private string _fileHash = "";
    private string _providerId = "";

    [SetsRequiredMembers]
    public TemplateFileViewModel(FileInfo fileInfo, string vettingId)
    {
        FileName = fileInfo.Name;
        AbsolutePath = fileInfo.FullName;
        VettingId = vettingId;

        // 解析文件名获取 fileHash + providerId
        var m = Regex.Match(FileName, @"(.+)_by\[(.+)\](.*)");
        if (m.Success)
        {
            var srcPath = Path.Combine("files", "vetting", VettingId, $"{m.Groups[1].Value}{m.Groups[3].Value}");
            if (File.Exists(srcPath))
                _fileHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(srcPath))).ToLowerInvariant();
            _providerId = m.Groups[2].Value;
        }

        // 加载所有产品
        using var db = new VettingDbContext();
        foreach (var f in db.FundInfos.FindAll())
            AvailableFunds.Add(new FundInfoVM(f));

        // 加载推荐产品
        var rec = db.TemplateRecommends.FindOne(r => r.FileHash == _fileHash && r.ProviderId == _providerId);
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

    public void SaveRecommend()
    {
        using var db = new VettingDbContext();
        var existing = db.TemplateRecommends.FindOne(r => r.FileHash == _fileHash && r.ProviderId == _providerId);
        var ids = string.Join(",", RecommendedFunds.Select(f => f.Entity.Id));
        if (existing != null)
        {
            existing.FundIds = ids;
            db.TemplateRecommends.Update(existing);
        }
        else if (RecommendedFunds.Count > 0)
        {
            db.TemplateRecommends.Insert(new TemplateRecommend { FileHash = _fileHash, ProviderId = _providerId, FundIds = ids });
        }
    }

    [RelayCommand]
    private void OpenFile() => Process.Start(new ProcessStartInfo(AbsolutePath) { UseShellExecute = true });

    [RelayCommand]
    private void AddRecommend()
    {
        if (SelectedAvailable == null || RecommendedFunds.Contains(SelectedAvailable)) return;
        RecommendedFunds.Add(SelectedAvailable);
        SaveRecommend();
    }

    [RelayCommand]
    private void RemoveRecommend()
    {
        if (SelectedRecommended == null) return;
        RecommendedFunds.Remove(SelectedRecommended);
        SaveRecommend();
    }

    [RelayCommand]
    private void MoveUp()
    {
        var idx = SelectedRecommended != null ? RecommendedFunds.IndexOf(SelectedRecommended) : -1;
        if (idx <= 0) return;
        RecommendedFunds.Move(idx, idx - 1);
        SaveRecommend();
    }

    [RelayCommand]
    private void MoveDown()
    {
        var idx = SelectedRecommended != null ? RecommendedFunds.IndexOf(SelectedRecommended) : -1;
        if (idx < 0 || idx >= RecommendedFunds.Count - 1) return;
        RecommendedFunds.Move(idx, idx + 1);
        SaveRecommend();
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

        var sel = MainWindowViewModel.GlobalProviders.Where(p => p.IsSelected).ToArray();
        if (sel.Length == 0) { HandyControl.Controls.Growl.Warning("请先选择 AI 接口"); return; }

        AIStatuses.Clear();
        var tasks = sel.Select(p =>
        {
            var provider = CustomQuestionAnswerer.CreateProvider(p.Name, p.ProviderType, p.ApiKey, p.BaseUrl, p.Model);
            var vm = new QuestionAnswerTaskViewModel(provider, p.Name, fileHash, providerId);
            AIStatuses.Add(vm);
            return vm.RunAsync(line => Output.Add(line));
        });

        var counts = await Task.WhenAll(tasks);
        Output.Add($"AI 回答完成，共 {counts.Sum()} 条");
    }

    [RelayCommand]
    private async Task FillTemplateAsync()
    {
        Output.Clear();
        IsExpanded = true;

        var tplPath = AbsolutePath;
        if (!File.Exists(tplPath)) { Output.Add("模板文件不存在"); return; }

        // 解析文件名获取 safeName + providerId
        var m = Regex.Match(FileName, @"(.+)_by\[(.+)\](.*)");
        if (!m.Success) { Output.Add("文件名格式无法解析"); return; }
        var safeName = m.Groups[1].Value;
        var ext = m.Groups[3].Value;

        // 源文件（原始尽调文件，不是带占位符的模板）
        var srcPath = Path.Combine("files", "vetting", VettingId, $"{safeName}{ext}");
        if (!File.Exists(srcPath)) { Output.Add($"源文件不存在: {srcPath}"); return; }

        var fileHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(srcPath))).ToLowerInvariant();
        var providerId = m.Groups[2].Value;

        // 加载 AI JSON（operators）
        var jsonPath = Path.ChangeExtension(tplPath, ".json");
        if (!File.Exists(jsonPath)) { Output.Add($"AI JSON 文件不存在: {jsonPath}"); return; }

        var recommendIds = RecommendedFunds.Count > 0
            ? RecommendedFunds.Select(f => f.Entity.Id).ToArray()
            : LoadGlobalRecommendIds();

        var outDir = Path.Combine("files", "vetting", VettingId, "final");
        var outPath = Path.Combine(outDir, $"{safeName}_filled{ext}");

        try
        {
            var json = await File.ReadAllTextAsync(jsonPath);
            using var jsonDoc = JsonDocument.Parse(json);
            var operators = OperatorParser.Parse(jsonDoc.RootElement.GetProperty("operations"));
            Output.Add($"已解析 {operators.Count} 个操作");

            var resolver = await Task.Run(() => DataResolver.Load(fileHash, providerId, recommendIds));

            await Task.Run(() => FileRetry.Run(
                () => DocOps.Fill(srcPath, outPath, operators, resolver),
                "填充文档",
                onRetry: msg => Output.Add(msg)));

            Output.Add($"已生成: {outPath}");
        }
        catch (Exception ex)
        {
            Output.Add($"填充失败: {ex.Message}");
        }
    }

    private static int[] LoadGlobalRecommendIds()
    {
        using var db = new VettingDbContext();
        var rec = db.TemplateRecommends.FindOne(r => r.FileHash == "__global__");
        if (rec?.FundIds == null) return [];
        return rec.FundIds.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
    }
}
