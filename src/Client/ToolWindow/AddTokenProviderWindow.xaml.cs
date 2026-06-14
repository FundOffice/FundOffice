using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FMO.AI;
using FMO.Models;
using FMO.Utilities;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Windows;

namespace FMO;

public partial class AddTokenProviderWindowViewModel : ObservableObject
{
    public string[] Providers { get; } = TokenProviderViewModel.Providers;
    public TokenProviderStyle[] Styles { get; } = TokenProviderViewModel.Styles;

    /// <summary>
    /// 各厂商各风格的默认 API URL
    /// </summary>
    private static readonly Dictionary<(string Company, TokenProviderStyle Style), string> DefaultUrls = new()
    {
        [("OpenAI", TokenProviderStyle.OpenAI)] = "https://api.openai.com/v1/chat/completions",
        [("Anthropic", TokenProviderStyle.Anthropic)] = "https://api.anthropic.com/v1/messages",
        [("Google", TokenProviderStyle.Google)] = "https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent",
        [("DeepSeek", TokenProviderStyle.OpenAI)] = "https://api.deepseek.com/chat/completions",
        [("DeepSeek", TokenProviderStyle.Anthropic)] = "https://api.deepseek.com/anthropic/v1/messages",
        [("Qwen", TokenProviderStyle.OpenAI)] = "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions",
        [("Doubao", TokenProviderStyle.OpenAI)] = "https://ark.cn-beijing.volces.com/api/v3/chat/completions",
        [("Zhipu", TokenProviderStyle.OpenAI)] = "https://open.bigmodel.cn/api/paas/v4/chat/completions",
        [("Moonshot", TokenProviderStyle.OpenAI)] = "https://api.moonshot.cn/v1/chat/completions",
        [("Baichuan", TokenProviderStyle.OpenAI)] = "https://api.baichuan-ai.com/v1/chat/completions",
        [("XiaoMi", TokenProviderStyle.OpenAI)] = "https://api.xiaomimimo.com/v1/chat/completions",
        [("XiaoMi", TokenProviderStyle.Anthropic)] = "https://api.xiaomimimo.com/anthropic/v1/messages",
    };

    [ObservableProperty]
    public partial string? SelectedCompany { get; set; }

    [ObservableProperty]
    public partial string? Url { get; set; }

    [ObservableProperty]
    public partial string? Key { get; set; }

    [ObservableProperty]
    public partial bool HasKey { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<TokenProviderStyle> AvailableStyles { get; set; } = [];

    [ObservableProperty]
    public partial TokenProviderStyle Style { get; set; }

    [ObservableProperty]
    public partial string? Tip { get; set; }

    [ObservableProperty]
    public partial bool HasTip { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<string> AvailableModels { get; set; } = [];

    [ObservableProperty]
    public partial string? SelectedModel { get; set; }

    [ObservableProperty]
    public partial bool HasModels { get; set; }

    /// <summary>
    /// 确认添加后创建的 ViewModel
    /// </summary>
    public TokenProviderViewModel? Result { get; private set; }

    /// <summary>
    /// 是否确认
    /// </summary>
    public bool IsConfirmed { get; private set; }

    public AddTokenProviderWindowViewModel()
    {
        // 默认选中第一个公司
        if (Providers.Length > 0)
            SelectedCompany = Providers[0];
    }

    partial void OnSelectedCompanyChanged(string? value)
    {
        if (value is null) return;

        // 创建 ViewModel 实例获取该厂商支持的风格
        var vm = TokenProviderViewModel.Create(value);
        var supportedStyles = vm?.SupportedStyles ?? [TokenProviderStyle.OpenAI];

        // 先确定新的 Style（优先保留当前选择，否则取第一个）
        var newStyle = supportedStyles.Contains(Style) ? Style : supportedStyles[0];

        // 一次性替换整个集合，避免 Clear 导致 ListBox 闪烁/丢失选中
        AvailableStyles = new ObservableCollection<TokenProviderStyle>(supportedStyles);
        Style = newStyle;

        // 根据 (公司, 风格) 填充 URL
        Url = DefaultUrls.GetValueOrDefault((value, newStyle), "");

        // 切换厂商时清空密钥、模型，等重新输入 key 验证
        Key = null;
        HasKey = false;
        AvailableModels.Clear();
        SelectedModel = null;
        HasModels = false;

        ConfirmCommand.NotifyCanExecuteChanged();
    }

    partial void OnStyleChanged(TokenProviderStyle value)
    {
        // 风格变化时更新 URL
        if (SelectedCompany is not null)
            Url = DefaultUrls.GetValueOrDefault((SelectedCompany, value), "");
    }

    partial void OnKeyChanged(string? value)
    {
        HasKey = !string.IsNullOrWhiteSpace(value);
        ConfirmCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedModelChanged(string? value) => ConfirmCommand.NotifyCanExecuteChanged();

    [RelayCommand]
    public async Task FetchModels()
    {
        if (string.IsNullOrWhiteSpace(Key))
        {
            Tip = "请先输入 API 密钥";
            HasTip = true;
            return;
        }

        if (string.IsNullOrWhiteSpace(Url))
        {
            Tip = "请先输入 URL";
            HasTip = true;
            return;
        }

        HasTip = false;
        Tip = "正在验证...";
        HasTip = true;

        try
        {
            // 创建 VM 实例并设置 URL，供 ModelsUrl/UsageUrl 使用
            _vm = TokenProviderViewModel.Create(SelectedCompany!);
            if (_vm is not null)
                _vm.Url = Url ?? "";

            var fetchedModels = await FetchModelsFromApi();
            AvailableModels.Clear();
            foreach (var m in fetchedModels)
                AvailableModels.Add(m);

            SelectedModel = AvailableModels.FirstOrDefault();
            HasModels = true;

            Tip = $"验证成功，获取到 {fetchedModels.Length} 个模型";
            HasTip = true;
        }
        catch (Exception ex)
        {
            HasModels = false;
            Tip = $"验证失败: {ex.Message}";
            HasTip = true;
        }
    }

    private async Task<string[]> FetchModelsFromApi()
    {
        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(15);

        switch (Style)
        {
            case TokenProviderStyle.OpenAI:
                return await FetchOpenAiModels(client);

            case TokenProviderStyle.Anthropic:
                return await FetchAnthropicModels(client);

            case TokenProviderStyle.Google:
                return await FetchGoogleModels(client);

            default:
                throw new NotSupportedException($"不支持的 API 风格: {Style}");
        }
    }

    private async Task<string[]> FetchOpenAiModels(HttpClient client)
    {
        var modelsUrl = _vm!.ModelsUrl;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Key);

        var response = await client.GetAsync(modelsUrl);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);

        var models = new List<string>();
        if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in data.EnumerateArray())
            {
                if (item.TryGetProperty("id", out var id))
                    models.Add(id.GetString()!);
            }
        }

        return [.. models];
    }

    private async Task<string[]> FetchAnthropicModels(HttpClient client)
    {
        var modelsUrl = _vm!.ModelsUrl;

        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.Add("x-api-key", Key);
        client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

        var response = await client.GetAsync(modelsUrl);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);

        var models = new List<string>();
        if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in data.EnumerateArray())
            {
                if (item.TryGetProperty("id", out var id))
                    models.Add(id.GetString()!);
            }
        }

        return [.. models];
    }

    private async Task<string[]> FetchGoogleModels(HttpClient client)
    {
        var modelsUrl = _vm!.ModelsUrl + $"?key={Key}";

        var response = await client.GetAsync(modelsUrl);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);

        var models = new List<string>();
        if (doc.RootElement.TryGetProperty("models", out var modelsArr) && modelsArr.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in modelsArr.EnumerateArray())
            {
                if (item.TryGetProperty("name", out var name))
                {
                    var n = name.GetString()!;
                    // Google 返回 "models/gemini-2.5-pro" 格式，去掉前缀
                    if (n.StartsWith("models/"))
                        n = n.Substring("models/".Length);
                    models.Add(n);
                }
            }
        }

        return [.. models];
    }

    private TokenProviderViewModel? _vm;

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    public void Confirm()
    {
        var provider = TokenProviderViewModel.CreateProvider(SelectedCompany!);
        if (provider is null) return;

        provider.Style = Style;
        provider.Url = Url ?? "";
        provider.Key = Key ?? "";
        provider.Model = SelectedModel ?? "";

        using var db = DbHelper.Base();
        db.GetCollection<TokenProvider>().Insert(provider);

        Result = TokenProviderViewModel.Create(provider);
        IsConfirmed = true;

        foreach (Window window in App.Current.Windows)
        {
            if (window is AddTokenProviderWindow wnd && wnd.DataContext == this)
            {
                wnd.DialogResult = true;
                break;
            }
        }
    }

    private bool CanConfirm() =>
        !string.IsNullOrWhiteSpace(SelectedCompany) &&
        !string.IsNullOrWhiteSpace(Key) &&
        !string.IsNullOrWhiteSpace(SelectedModel);

    [RelayCommand]
    public void Cancel()
    {
        IsConfirmed = false;
        foreach (Window window in App.Current.Windows)
        {
            if (window is AddTokenProviderWindow wnd && wnd.DataContext == this)
            {
                wnd.DialogResult = false;
                break;
            }
        }
    }
}

public partial class AddTokenProviderWindow : Window
{
    public AddTokenProviderWindow()
    {
        InitializeComponent();
        DataContext = new AddTokenProviderWindowViewModel();
    }
}
