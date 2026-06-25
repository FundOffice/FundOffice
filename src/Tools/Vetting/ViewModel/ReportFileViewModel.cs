using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Vetting.Data;
using Vetting.Entity;

namespace Vetting.ViewModel;

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

        // Step 1: 解析文档完整内容
        var structure = Vetting.Services.DocOps.ParseDocument(AbsolutePath);

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
            new FundOffice.Copilot.Configuration.OpenAIOptions { Identifier = vm.Name, ApiKey = vm.ApiKey, BaseUrl = vm.BaseUrl,Model = vm.Model }),
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
