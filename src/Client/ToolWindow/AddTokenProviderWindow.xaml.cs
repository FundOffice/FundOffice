using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FMO.AI;
using FundOffice.Copilot.Providers;
using FMO.Utilities;
using System.Collections.ObjectModel;
using System.Windows;

namespace FMO;

public partial class AddTokenProviderWindowViewModel : ObservableObject
{
    /// <summary>
    /// 可用的 Provider 类型
    /// </summary>
    public AIProviderType[] ProviderTypes { get; } = Enum.GetValues<AIProviderType>();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial string? Name { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial string? BaseUrl { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial string? ApiKey { get; set; }

    [ObservableProperty]
    public partial bool HasKey { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial AIProviderType ProviderType { get; set; } = AIProviderType.OpenAI;

    [ObservableProperty]
    public partial ObservableCollection<string> AvailableModels { get; set; } = [];

    [ObservableProperty]
    public partial string? SelectedModel { get; set; }

    [ObservableProperty]
    public partial bool HasModels { get; set; }

    [ObservableProperty]
    public partial string? Tip { get; set; }

    [ObservableProperty]
    public partial bool HasTip { get; set; }

    /// <summary>
    /// 确认添加后创建的配置
    /// </summary>
    public TokenProviderConfig? Result { get; private set; }

    /// <summary>
    /// 是否确认
    /// </summary>
    public bool IsConfirmed { get; private set; }

    /// <summary>
    /// 是否为编辑模式
    /// </summary>
    public bool IsEditMode { get; set; }

    /// <summary>
    /// 编辑模式下的配置 ID
    /// </summary>
    public int EditConfigId { get; set; }

    partial void OnApiKeyChanged(string? value)
    {
        HasKey = !string.IsNullOrWhiteSpace(value);
        ConfirmCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedModelChanged(string? value) => ConfirmCommand.NotifyCanExecuteChanged();

    [RelayCommand]
    public async Task FetchModels()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            Tip = "请先输入 API 密钥";
            HasTip = true;
            return;
        }

        if (string.IsNullOrWhiteSpace(BaseUrl))
        {
            Tip = "请先输入 API 地址";
            HasTip = true;
            return;
        }

        HasTip = false;
        Tip = "正在验证...";
        HasTip = true;

        try
        {
            // 创建临时配置获取模型列表
            var tempConfig = new TokenProviderConfig
            {
                Name = Name ?? "",
                BaseUrl = BaseUrl!,
                ApiKey = ApiKey!,
                Model = "fetch-model",
                ProviderType = ProviderType
            };

            var provider = tempConfig.CreateProvider();
            var models = await provider.GetModelsAsync();

            AvailableModels.Clear();
            foreach (var m in models)
                AvailableModels.Add(m.Id);

            SelectedModel = AvailableModels.FirstOrDefault();
            HasModels = true;

            Tip = $"验证成功，获取到 {models.Count} 个模型";
            HasTip = true;
        }
        catch (TokenProviderException ex)
        {
            HasModels = false;
            Tip = $"验证失败: {ex.Kind} - {ex.Message}";
            HasTip = true;
        }
        catch (Exception ex)
        {
            HasModels = false;
            Tip = $"验证失败: {ex.Message}";
            HasTip = true;
        }
    }

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    public void Confirm()
    {
        var config = new TokenProviderConfig
        {
            Id = IsEditMode ? EditConfigId : 0,
            Name = Name ?? "",
            BaseUrl = BaseUrl ?? "",
            ApiKey = ApiKey ?? "",
            Model = SelectedModel ?? "",
            ProviderType = ProviderType
        };

        using var db = DbHelper.Base();
        if (IsEditMode)
        {
            db.GetCollection<TokenProviderConfig>().Update(config);
        }
        else
        {
            db.GetCollection<TokenProviderConfig>().Insert(config);
        }

        Result = config;
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
        !string.IsNullOrWhiteSpace(Name) &&
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.IsNullOrWhiteSpace(BaseUrl) &&
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