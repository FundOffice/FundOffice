using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FundOffice.Copilot.Configuration;
using FundOffice.Copilot.Providers;
using Vetting.Data;
using Vetting.Entity;

namespace Vetting.ViewModel;
public partial class AIProviderItemViewModel : ObservableObject
{
    public int Id { get; set; }
    [ObservableProperty] public partial string Name { get; set; } = "";
    public string[] ProviderTypes { get; } = ["OpenAI", "Anthropic"];
    [ObservableProperty] public partial string ProviderType { get; set; } = "OpenAI";
    [ObservableProperty] public partial string ApiKey { get; set; } = "";
    [ObservableProperty] public partial string BaseUrl { get; set; } = "";
    [ObservableProperty] public partial string Model { get; set; } = "";
    [ObservableProperty] public partial bool Tested { get; set; }
    [ObservableProperty] public partial string StatusMessage { get; set; } = "";
    [ObservableProperty] public partial bool IsBusy { get; set; }
    public ObservableCollection<string> AvailableModels { get; } = [];

    public AIProviderItemViewModel() { }

    public AIProviderItemViewModel(AIProviderConfig config)
    {
        Id = config.Id;
        Name = config.Name;
        ProviderType = config.ProviderType;
        ApiKey = config.ApiKey;
        BaseUrl = config.BaseUrl;
        Model = config.Model;
    }

    partial void OnProviderTypeChanged(string value)
    {
        //BaseUrl = value == "Anthropic" ? "https://api.anthropic.com" : "https://api.openai.com";
        Tested = false;
        AvailableModels.Clear();
        Model = "";
    }

    [RelayCommand]
    private async Task FetchModelsAsync()
    {
        if (string.IsNullOrWhiteSpace(BaseUrl)) { StatusMessage = "请输入 Url"; return; }
        if (string.IsNullOrWhiteSpace(ApiKey)) { StatusMessage = "请输入 API Key"; return; }

        IsBusy = true; StatusMessage = "正在获取模型..."; Tested = false;
        try
        {
            var provider = CreateProvider();
            var models = await provider.GetModelsAsync();
            AvailableModels.Clear();
            foreach (var m in models) AvailableModels.Add(m.Id);
            Model = AvailableModels.FirstOrDefault() ?? "";
            Tested = true;
            StatusMessage = $"连通成功，获取到 {models.Count} 个模型";
        }
        catch (Exception ex) { StatusMessage = $"失败: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save(Window window)
    {
        using var db = new VettingDbContext();
        db.AIProviderConfigs.Upsert(new AIProviderConfig(Id, Name, ProviderType, ApiKey, BaseUrl, Model));
        StatusMessage = "已保存";
        window.Close();
    }
    private bool CanSave() => !string.IsNullOrWhiteSpace(Name) && Tested && !string.IsNullOrWhiteSpace(Model);

    partial void OnTestedChanged(bool value) => SaveCommand.NotifyCanExecuteChanged();

    private ITokenProvider CreateProvider() => ProviderType switch
    {
        "Anthropic" => new AnthropicTokenProvider(new AnthropicOptions { ApiKey = ApiKey, BaseUrl = BaseUrl }),
        _ => new OpenAITokenProvider(new OpenAIOptions { ApiKey = ApiKey, BaseUrl = BaseUrl }),
    };
}
