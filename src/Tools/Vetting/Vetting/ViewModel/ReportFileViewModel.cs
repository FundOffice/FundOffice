using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using Vetting.Copilot;
using Vetting.Copilot.Data;
using Vetting.Copilot.Models.Entities;
using Vetting.Data;
using Vetting.Entity;

namespace Vetting.ViewModel;

public partial class ReportFileViewModel : ObservableObject, IRecipient<RunModeChanged>, IRecipient<ProviderSelectionChanged>
{
    public string FileName { get; set; }
    public string AbsolutePath { get; set; }
    public string VettingId { get; set; } = "";
    [ObservableProperty] public partial bool IsExpanded { get; set; }
    [ObservableProperty] public partial bool IsRecommendOpen { get; set; }
    [ObservableProperty] public partial bool IsAutoMode { get; set; }

    public ObservableCollection<ProviderRunViewModel> Providers { get; } = [];
    [ObservableProperty] public partial string Output { get; set; } = "";

    // 推荐产品（文件级，所有 provider 共用）
    public ObservableCollection<FundInfoVM> AvailableFunds { get; } = [];
    public ObservableCollection<FundInfoVM> RecommendedFunds { get; } = [];
    [ObservableProperty] public partial FundInfoVM? SelectedAvailable { get; set; }
    [ObservableProperty] public partial FundInfoVM? SelectedRecommended { get; set; }

    public ReportFileViewModel(FileInfo fileInfo, string vettingId,
        AIProviderItemViewModel[] selectedProviders, string answerMode, string runMode)
    {
        FileName = fileInfo.Name;
        AbsolutePath = fileInfo.FullName;
        VettingId = vettingId;

        IsAutoMode = runMode == MainWindowViewModel.RunModeAuto;
        WeakReferenceMessenger.Default.Register<RunModeChanged>(this);
        WeakReferenceMessenger.Default.Register<ProviderSelectionChanged>(this);

        using var db = new VettingDbContext();
        foreach (var f in db.FundInfos.FindAll())
            AvailableFunds.Add(new FundInfoVM(f));

        var rec = db.TemplateRecommends.FindOne(r => r.FileName == FileName);
        if (rec?.FundIds != null)
        {
            var ids = rec.FundIds.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
            foreach (var id in ids)
            {
                var fund = AvailableFunds.FirstOrDefault(f => f.Entity.Id == id);
                if (fund != null) RecommendedFunds.Add(fund);
            }
        }

        // 在构造时创建 Providers
        foreach (var p in selectedProviders)
        {
            var provider = CustomQuestionAnswerer.CreateProvider(p.ProviderId, p.ProviderType, p.ApiKey, p.BaseUrl, p.Model, p.ApiVersion);
            var vm = new ProviderRunViewModel(p.Name, p.ProviderId, provider, FileName, VettingId, AbsolutePath)
            {
                IsFullMode = answerMode == MainWindowViewModel.AnswerModeFull,
            };
            Providers.Add(vm);
        }
    }

    private void Log(string message) => Output += message + Environment.NewLine;

    public void Receive(RunModeChanged message) =>
        IsAutoMode = message.RunMode == MainWindowViewModel.RunModeAuto;

    public void Receive(ProviderSelectionChanged message)
    {
        var providerId = message.Identifier;
        if (message.IsSelected)
        {
            // 添加新 provider
            using var db = new VettingAppDbContext();
            var config = db.AIProviderConfigs.Query().ToEnumerable().FirstOrDefault(c => $"{c.Id.GetHashCode():x}" == providerId);
            if (config == null) return;

            var provider = CustomQuestionAnswerer.CreateProvider(message.Identifier, config.ProviderType, config.ApiKey, config.BaseUrl, config.Model, config.ApiVersion);
            var vm = new ProviderRunViewModel(config.Name, message.Identifier, provider, FileName, VettingId, AbsolutePath)
            {
                IsFullMode = db.GetSettings().AnswerMode == MainWindowViewModel.AnswerModeFull,
            };
            Providers.Add(vm);
        }
        else
        {
            // 移除 provider
            var existing = Providers.FirstOrDefault(p => p.ProviderId == providerId);
            if (existing != null)
                Providers.Remove(existing);
        }
    }

    // ── 推荐产品（文件级）──────────────────────────────

    public void SaveRecommend()
    {
        using var db = new VettingDbContext();
        var ids = string.Join(",", RecommendedFunds.Select(f => f.Entity.Id));
        var existing = db.TemplateRecommends.FindOne(r => r.FileName == FileName);
        if (existing != null)
        {
            existing.FundIds = ids;
            db.TemplateRecommends.Update(existing);
        }
        else if (RecommendedFunds.Count > 0)
        {
            db.TemplateRecommends.Insert(new TemplateRecommend { FileName = FileName, ProviderId = "", FundIds = ids });
        }
    }

    [RelayCommand]
    private void OpenFile() => Process.Start(new ProcessStartInfo(AbsolutePath) { UseShellExecute = true });

    // ── 自动运行（解析 → AI回答 → 填充）──────────────────

    [RelayCommand]
    private async Task RunAutoAsync()
    {
        IsExpanded = true;
        Log("═══ 自动模式开始 ═══");

        try
        {
            // 1. 解析
            Log("── 步骤 1/3：解析 ──");
            await GenerateTemplatesAsync();
            if (GenerateTemplatesCommand.IsRunning) return;

            // 2. AI 回答
            Log("── 步骤 2/3：AI 回答 ──");
            await AIAnswerCustomQuestionsAsync();
            if (AIAnswerCustomQuestionsCommand.IsRunning) return;

            // 3. 填充
            Log("── 步骤 3/3：填充生成 ──");
            await FillTemplateAsync();
        }
        catch (Exception ex)
        {
            Log($"自动运行错误: {ex.Message}");
        }

        Log("═══ 自动模式完成 ═══");
    }

    // ── 解析（调用所有选中 provider）──────────────────

    [RelayCommand]
    private async Task GenerateTemplatesAsync()
    {
        if (Providers.Count == 0) { HandyControl.Controls.Growl.Warning("请先选择 AI 接口"); return; }

        IsExpanded = true;
        // 并行解析
        await Task.WhenAll(Providers.Select(p => p.RunParseAsync()));
    }



    // ── AI 回答（一次调用所有 provider）────────────────

    [RelayCommand]
    private async Task AIAnswerCustomQuestionsAsync()
    {
        if (Providers.Count == 0) { HandyControl.Controls.Growl.Warning("请先解析文档"); return; }
        IsExpanded = true;
        await Task.WhenAll(Providers.Select(p => p.RunAnswerAsync()));
    }

    // ── 填充（一次调用所有 provider 的 JSON）──────────

    [RelayCommand]
    private async Task FillTemplateAsync()
    {
        if (Providers.Count == 0) { HandyControl.Controls.Growl.Warning("请先解析文档"); return; }
        if (!IsAutoMode) Output = "";
        IsExpanded = true;
        await Task.WhenAll(Providers.Select(p => p.RunFillAsync()));
    }

    /// <summary>推荐产品 Id：文件级优先，回退全局推荐</summary>
    private static int[] GetRecommendIds(ObservableCollection<FundInfoVM> recommendedFunds)
    {
        var ids = recommendedFunds.Select(f => f.Entity.Id).ToArray();
        if (ids.Length > 0) return ids;
        using var db = new VettingDbContext();
        var rec = db.TemplateRecommends.FindOne(r => r.FileName == "__global__");
        return rec?.FundIds != null
            ? rec.FundIds.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray()
            : [];
    }
}
