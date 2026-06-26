using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Vetting.Copilot.Data;
using Vetting.Copilot.Models;

namespace Vetting.ViewModel;

public partial class ReportFileViewModel : ObservableObject
{
    public required string FileName { get; set; }
    public required string AbsolutePath { get; set; }
    public string VettingId { get; set; } = "";
    [ObservableProperty] public partial bool IsExpanded { get; set; }
    [ObservableProperty] public partial ObservableCollection<VettingParseTaskViewModel> Tasks { get; set; } = [];

    [SetsRequiredMembers]
    public ReportFileViewModel(FileInfo fileInfo, string vettingId)
    {
        FileName = fileInfo.Name;
        AbsolutePath = fileInfo.FullName;
        VettingId = vettingId;
    }

    [RelayCommand]
    private void OpenFile() => Process.Start(new ProcessStartInfo(AbsolutePath) { UseShellExecute = true });

    [RelayCommand]
    private async Task GenerateTemplatesAsync()
    {
        var sel = MainWindowViewModel.GlobalProviders.Where(x => x.IsSelected).ToArray();
        if (sel.Length == 0) { HandyControl.Controls.Growl.Warning("请先选择 AI 接口"); return; }


        var structure = Vetting.Copilot.FileRetry.Run(() => Vetting.Copilot.DocOps.ParseDocument(AbsolutePath), "解析文档");

        if (string.IsNullOrWhiteSpace(structure))
        {
            HandyControl.Controls.Growl.Warning("无法解析文档");
            return;
        }

        IsExpanded = true;
        var sysPrompt = await VettingParseTaskViewModel.LoadSysptAsync();
        Tasks = [.. sel.Select(provider => new VettingParseTaskViewModel
        {
            TaskName = provider.Name,
            Provider = CreateProvider(provider),
            VettingId = VettingId,
            FileName = FileName
        })];
        await Task.WhenAll(Tasks.Select(t => t.RunAsync(structure, sysPrompt)).ToArray());
    }

    private static FundOffice.Copilot.Providers.ITokenProvider CreateProvider(AIProviderItemViewModel vm) => vm.ProviderType switch
    {
        "Anthropic" => new FundOffice.Copilot.Providers.AnthropicTokenProvider(
            new FundOffice.Copilot.Configuration.AnthropicOptions { Identifier = vm.Name, ApiKey = vm.ApiKey, BaseUrl = vm.BaseUrl, Model = vm.Model }),
        _ => new FundOffice.Copilot.Providers.OpenAITokenProvider(
            new FundOffice.Copilot.Configuration.OpenAIOptions { Identifier = vm.Name, ApiKey = vm.ApiKey, BaseUrl = vm.BaseUrl, Model = vm.Model }),
    };
}
