using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FundOffice.Copilot.Configuration;
using FundOffice.Copilot.Providers;
using System.Collections.ObjectModel;
using System.Windows;
using Vetting.Copilot.Data;
using Vetting.Copilot.Models;
using Vetting.Entity;

namespace Vetting.ViewModel;

public partial class AIProviderItemViewModel : ObservableObject
{
    public int Id { get; set; }
    public string ProviderId => $"{Id.GetHashCode():x}";

    [ObservableProperty] public partial string Name { get; set; } = "新AI";
    public string[] ProviderTypes { get; } = ["OpenAI", "Anthropic", "Google"];
    [ObservableProperty] public partial string ProviderType { get; set; } = "OpenAI";
    [ObservableProperty] public partial string ApiKey { get; set; } = "";
    [ObservableProperty] public partial string BaseUrl { get; set; } = "";
    [ObservableProperty] public partial string Model { get; set; } = "";
    [ObservableProperty] public partial bool Tested { get; set; }
    [ObservableProperty] public partial string StatusMessage { get; set; } = "";
    [ObservableProperty] public partial bool IsBusy { get; set; }

    public OpenAIApiVersion[] ApiVersions { get; } = [OpenAIApiVersion.ChatCompletions, OpenAIApiVersion.Responses];
    [ObservableProperty] public partial OpenAIApiVersion ApiVersion { get; set; } = OpenAIApiVersion.ChatCompletions;

    [ObservableProperty] public partial bool IsSelected { get; set; }

    public event Action? IsSelectedChanged;

    partial void OnIsSelectedChanged(bool value)
    {
        IsSelectedChanged?.Invoke();
        WeakReferenceMessenger.Default.Send(new ProviderSelectionChanged(ProviderId, value));
    }

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
        ApiVersion = config.ApiVersion;
    }

    partial void OnProviderTypeChanged(string value)
    {
        //BaseUrl = value == "Anthropic" ? "https://api.anthropic.com" : "https://api.openai.com";
        Tested = false;
        AvailableModels.Clear();
        Model = "";
    }

    [RelayCommand]
    private async Task TestConnectivityAsync()
    {
        if (string.IsNullOrWhiteSpace(BaseUrl)) { StatusMessage = "请输入 Url"; return; }
        if (string.IsNullOrWhiteSpace(ApiKey)) { StatusMessage = "请输入 API Key"; return; }
        if (string.IsNullOrWhiteSpace(Model)) { StatusMessage = "请先选择模型"; return; }

        IsBusy = true; StatusMessage = "正在测试连通性...";
        try
        {
            var provider = CreateProvider();
            await provider.TestConnectivityAsync();
            Tested = true;
            StatusMessage = "连通测试通过";
        }
        catch (Exception ex)
        {
            Tested = false;
            StatusMessage = $"连通失败({ex.GetType().Name}): {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task FetchModelsAsync()
    {
        if (string.IsNullOrWhiteSpace(BaseUrl)) { StatusMessage = "请输入 Url"; return; }
        if (string.IsNullOrWhiteSpace(ApiKey)) { StatusMessage = "请输入 API Key"; return; }

        IsBusy = true; StatusMessage = "正在获取模型..."; Tested = false;
        try
        {
            var provider = CreateProvider(overrideModel: string.IsNullOrWhiteSpace(Model) ? "gpt-4o-mini" : Model);
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
        AIProviderConfig entity = new(Id, Name, ProviderType, ApiKey, BaseUrl, Model, ApiVersion);
        db.AIProviderConfigs.Upsert(entity);
        StatusMessage = "已保存";

        if (Id == 0)
        {
            Id = entity.Id;
            WeakReferenceMessenger.Default.Send(new AIProviderChanged(Id, ChangedType.Add));
        }
        else
            WeakReferenceMessenger.Default.Send(new AIProviderChanged(Id, ChangedType.Update));

        window.Close();
    }
    private bool CanSave() => !string.IsNullOrWhiteSpace(Name) && Tested && !string.IsNullOrWhiteSpace(Model);

    partial void OnTestedChanged(bool value) => SaveCommand.NotifyCanExecuteChanged();

    private ITokenProvider CreateProvider(string? overrideModel = null) => ProviderType switch
    {
        "Anthropic" => new AnthropicTokenProvider(new AnthropicOptions { Identifier = ProviderId, ApiKey = ApiKey, BaseUrl = BaseUrl, Model = overrideModel ?? Model }),
        "Google" => new GoogleTokenProvider(new GoogleOptions { Identifier = ProviderId, ApiKey = ApiKey, BaseUrl = BaseUrl, Model = overrideModel ?? Model }),
        _ => ApiVersion switch
        {
            OpenAIApiVersion.Responses => new OpenAIResponsesProvider(new OpenAIOptions { Identifier = ProviderId, ApiKey = ApiKey, BaseUrl = BaseUrl, Model = overrideModel ?? Model, ApiVersion = ApiVersion }),
            _ => new OpenAITokenProvider(new OpenAIOptions { Identifier = ProviderId, ApiKey = ApiKey, BaseUrl = BaseUrl, Model = overrideModel ?? Model, ApiVersion = ApiVersion }),
        },
    };
}
