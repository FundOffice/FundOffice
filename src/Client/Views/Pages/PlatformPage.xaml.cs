using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FMO.ESigning;

using FMO.Models;
using FMO.Shared;
using FMO.Trustee;
using FMO.Utilities;
using MoT;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Utilities;

namespace FMO;

/// <summary>
/// PlatformPage.xaml 的交互逻辑
/// </summary>
public partial class PlatformPage : UserControl
{
    private Queue<Key> queue = new();


    public PlatformPage()
    {
        InitializeComponent();

    }


}


public partial class ProxyViewModel : ObservableObject
{
    public ProxyViewModel()
    {
        debouncer = new(CheckAccessImpl, 500);
    }

    [ObservableProperty]
    public partial string? Address { get; set; }



    [ObservableProperty]
    public partial string? User { get; set; }

    [ObservableProperty]
    public partial string? Password { get; set; }

    public bool IsAvailiable { get; private set; }

    AsyncDebouncer debouncer { get; }


    [RelayCommand]
    public async Task CheckAccess()
    {
        await debouncer.InvokeAsync();
    }

    public async Task CheckAccessImpl()
    {
        try
        {
            using var client = new HttpClient(new HttpClientHandler
            {
                UseProxy = true,
                Proxy = new WebProxy(Address)
                {
                    Credentials = string.IsNullOrWhiteSpace(User) ? null : new NetworkCredential(User, Password)
                }
            });

            var resp = await client.GetAsync("https://www.baidu.com");
            //var cont = await resp.Content.ReadAsStringAsync();

            IsAvailiable = resp.StatusCode == HttpStatusCode.OK;
            ProxyChecked?.Invoke(IsAvailiable);

            WeakReferenceMessenger.Default.Send(new ToastMessage(ToastLevel.Success, $"代理连接成功"));
        }
        catch (Exception e)
        {
            WeakReferenceMessenger.Default.Send(new ToastMessage(ToastLevel.Warning, $"连接失败，请检查端口、用户名密码是否正确"));
            Logg.Error($"连接Proxy  {e}");
        }
    }


    public delegate void ProxyCheckedHandlder(bool valid);

    public ProxyCheckedHandlder? ProxyChecked;
}

/// <summary>
/// Page的vm
/// </summary>
public partial class PlatformPageViewModel : ObservableObject, IRecipient<TrusteeRunMessage>, IRecipient<SigningRunMessage>
{

    private static bool _firstLoad = true;


    public TrusteeViewModelBase[] Trustees2 { get; }

    //public ObservableCollection<PlatformPageViewModelDigital> Digitals { get; } = new();

    public AmacAccountViewModel[] AmacAccounts { get; set; }

    public AmacDirectViewModel[] DirectAccounts { get; set; }


    [ObservableProperty]
    public partial bool UseProxyForTrustee { get; set; }

    [ObservableProperty]
    public partial bool IsTrusteeProxyAvailiable { get; set; } = true;


    [ObservableProperty]
    public partial bool ShowProxyConfig { get; set; }

    public ProxyViewModel ProxyViewModel { get; } = new();


    public SyncButtonInfo[] TrusteeAPIButtons { get; set; }


    [ObservableProperty]
    public partial string? LocalIP { get; set; }


    [ObservableProperty]
    public partial bool IsTrusteeReportVisible { get; set; }



    [ObservableProperty]
    public partial bool IsTrusteeRebuildVisible { get; set; }




    [ObservableProperty]
    public partial IEnumerable<TrusteeApiBase.LogInfo>? TrusteeWorkLogs { get; set; }

    [ObservableProperty]
    public partial bool AllowWorkReport { get; set; }


    public CollectionViewSource TrusteeWorkLogSource { get; } = new();

    [ObservableProperty]
    public partial ESignViewModelBase[] ESignViewModels { get; set; }


    public CollectionViewSource ESignSource { get; } = new();

    /// <summary>
    /// 启用/禁用电签
    /// </summary>
    [ObservableProperty]
    public partial bool SetESigningState { get; set; }



    public SyncButtonInfo[] ESigningButtons { get; set; }


    [ObservableProperty]
    public partial ObservableCollection<ModifiableViewModel<TokenProvider, TokenProviderViewModel>> AIProviders { get; set; }




    public PlatformPageViewModel()
    {
        WeakReferenceMessenger.Default.RegisterAll(this);

        using var db = DbHelper.Base();
        AIProviders = [.. db.GetCollection<TokenProvider>().FindAll().Select(x => new ModifiableViewModel<TokenProvider, TokenProviderViewModel>() { OldValue = x, NewValue = new(x) })];

        foreach (var item in AIProviders)
            item.Changed += AI_Changed;
        AIProviders.CollectionChanged += (s, e) =>
        {
            if (e.NewItems is not null)
                foreach (ModifiableViewModel<TokenProvider, TokenProviderViewModel> item in e.NewItems)
                    item.Changed += AI_Changed;

            if (e.OldItems is not null)
                foreach (ModifiableViewModel<TokenProvider, TokenProviderViewModel> item in e.OldItems)
                    item.Changed -= AI_Changed;
        };


        ESignViewModels = SigningGalley.ViewModels;
        ESignSource.Source = ESignViewModels;
        //ESignSource.Filter += ESignSource_Filter;

        ESigningButtons = [
            new((Geometry)App.Current.Resources["f.user-group"], SyncSigningCustmersOnceCommand, nameof(ESigningWorker.SyncCustmersOnce), "同步投资人信息"),
            new((Geometry)App.Current.Resources["f.file-shield"], SyncSigningQualificationsOnceCommand, nameof(ESigningWorker.SyncQualificationsOnce), "同步合格投资人认定"),
            new((Geometry)App.Current.Resources["f.file-signature"], SyncSigningOrdersOnceCommand, nameof(ESigningWorker.SyncOrdersOnce), "同步交易订单"),  ];



        ///// 协会平台账号
        var acc = db.GetCollection<AmacAccount>().FindAll().ToList();

        string[] ids = ["ambers", "human", "peixun", "xinpi"];

        acc = acc.Where(x => ids.Contains(x.Id)).ToList();

        foreach (var item in ids)
        {
            if (acc.All(x => x.Id != item))
                acc.Add(new AmacAccount(item, "", "", false));
        }

        AmacAccounts = acc.Select(x => new AmacAccountViewModel(x)).ToArray();

        ids = ["pmg", "pof"];
        var dacc = db.GetCollection<AmacReportAccount>().FindAll().ToList();
        foreach (var item in ids)
        {
            if (dacc.All(x => x.Id != item))
                dacc.Add(new AmacReportAccount(item, "", "", "", false));
        }

        DirectAccounts = dacc.Select(x => new AmacDirectViewModel(x)).ToArray();


        Trustees2 = TrusteeGallay.TrusteeViewModels;
        var work = TrusteeGallay.Worker;
        TrusteeAPIButtons = [
            new((Geometry)App.Current.Resources["f.table-cells"], QueryNetValueOnceCommand, nameof(TrusteeWorker.QueryNetValueOnce), "同步净值"),
            new((Geometry)App.Current.Resources["f.hand-holding-dollar"], QueryRaisingBalanceOnceCommand, nameof(TrusteeWorker.QueryRaisingBalanceOnce), "同步募集户余额"),
            new((Geometry)App.Current.Resources["f.tornado"], QueryRaisingAccountTransctionOnceCommand,  nameof(TrusteeWorker.QueryRaisingAccountTransctionOnce),"同步募集户流水"),
            new((Geometry)App.Current.Resources["f.file-circle-plus"], QueryTransferRequestOnceCommand, nameof(TrusteeWorker.QueryTransferRequestOnce), "同步交易申请"),
            new((Geometry)App.Current.Resources["f.clipboard-check"], QueryTransferRecordOnceCommand, nameof(QueryTransferRecordOnce), "同步交易确认"),
            new((Geometry)App.Current.Resources["f.file-invoice-dollar"], QueryDailyFeeOnceCommand, nameof(QueryDailyFeeOnce), "同步每日计提费用"), ];






        using var pdb = DbHelper.Platform();
        var config = pdb.GetCollection<TrusteeUnifiedConfig>().FindOne(_ => true);
        if (config is not null)
        {
            ProxyViewModel.User = config.ProxyUser;
            ProxyViewModel.Password = config.ProxyPassword;
            ProxyViewModel.Address = config.ProxyUrl;

            UseProxyForTrustee = config.UseProxy;
        }
        ProxyViewModel.ProxyChecked += (e) =>
        {
            IsTrusteeProxyAvailiable = e;
            if (e) ShowProxyConfig = false;

            if (e) UpdateProxy();
        };

        Task.Run(async () => await UpdateLocalIP());



        TrusteeWorkLogSource.GroupDescriptions.Add(new PropertyGroupDescription("Time.Date"));
    }

    private void AI_Changed(ValueChangeEventArgs<TokenProvider> args)
    {
        if (args.NewValue is null) return;
        using var db = DbHelper.Base();
        db.GetCollection<TokenProvider>().Upsert(args.NewValue);
    }

    private void ESignSource_Filter(object sender, FilterEventArgs e)
    {
        e.Accepted = SetESigningState ? true : (e.Item as ESignViewModelBase)!.IsEnable ?? false;
    }


    /// <summary>
    /// 查看api 运行报告
    /// </summary>
    [RelayCommand]
    public void ViewTrusteeWorkReport()
    {
        var wnd = new TrusteeLogViewerWindow();
        wnd.Owner = App.Current.MainWindow;
        wnd.ShowDialog();
        return;

        //只看3天内的
        //TrusteeWorkLogs = TrusteeApiBase.GetLogs();//?.OrderByDescending(x => x.Time).Take(100);//.Where(x => (DateTime.Today - x.Time).Days < 3);
        //TrusteeWorkLogSource.Source = TrusteeWorkLogs;
    }


    [RelayCommand]
    public void ViewTrusteeConfig()
    {
        Window window = new Window();
        window.Content = new TrusteeWorkerSettingView();
        window.DataContext = new TrusteeWorkerSettingViewModel();
        window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        window.Owner = App.Current.MainWindow;
        window.ShowDialog();
    }

    #region Trustee Buttons

    [RelayCommand]
    public async Task QueryNetValueOnce()
    {
        await Task.Run(() => TrusteeGallay.Worker.QueryNetValueOnce());
    }

    [RelayCommand]
    public async Task QueryRaisingAccountTransctionOnce()
    {
        await Task.Run(() => TrusteeGallay.Worker.QueryRaisingAccountTransctionOnce());
    }

    [RelayCommand]
    public async Task QueryTransferRecordOnce()
    {
        await Task.Run(() => TrusteeGallay.Worker.QueryTransferRecordOnce());
    }

    [RelayCommand]
    public async Task QueryDailyFeeOnce()
    {
        await Task.Run(() => TrusteeGallay.Worker.QueryDailyFeeOnce());
    }

    [RelayCommand]
    public async Task QueryTransferRequestOnce()
    {
        await Task.Run(() => TrusteeGallay.Worker.QueryTransferRequestOnce());
    }

    [RelayCommand]
    public async Task QueryRaisingBalanceOnce()
    {
        await Task.Run(() => TrusteeGallay.Worker.QueryRaisingBalanceOnce());
    }

    #endregion


    [RelayCommand]
    public async Task SyncSigningCustmersOnce()
    {
        await Task.Run(() => SigningGalley.Worker.SyncCustmersOnce());
    }

    [RelayCommand]
    public async Task SyncSigningQualificationsOnce()
    {
        await Task.Run(() => SigningGalley.Worker.SyncQualificationsOnce());
    }

    [RelayCommand]
    public async Task SyncSigningOrdersOnce()
    {
        await Task.Run(() => SigningGalley.Worker.SyncOrdersOnce());
    }

    [RelayCommand]
    public void AddTokenProvider()
    {
        AIProviders.Add(new ModifiableViewModel<TokenProvider, TokenProviderViewModel>() { OldValue = null, NewValue = new TokenProviderViewModel { Url = "", Key = "", Style = TokenProviderStyle.None } });
    }


    private void UpdateProxy()
    {
        LocalIP = null;

        var obj = ProxyViewModel;

        Task.Run(async () => await UpdateLocalIP());

        using var pdb = DbHelper.Platform();
        var config = pdb.GetCollection<TrusteeUnifiedConfig>().FindOne(_ => true) ?? new();
        config.ProxyUrl = obj.Address;
        config.ProxyUser = obj.User;
        config.ProxyPassword = obj.Password;
        config.UseProxy = UseProxyForTrustee;
        pdb.GetCollection<TrusteeUnifiedConfig>().Upsert(config);

        //更新到trustee
        TrusteeApiBase.SetProxy(config.UseProxy ? new WebProxy(config.ProxyUrl) { Credentials = string.IsNullOrWhiteSpace(config.ProxyUser) ? null : new NetworkCredential(config.ProxyUser, config.ProxyPassword) } : null);
    }

    /// <summary>
    /// 加载托管插件，一个dll只加载一个
    /// </summary>
    /// <param name="assembly"></param>
    //void TryAddTrustee(Assembly assembly)
    //{
    //    var type = assembly.GetTypes().FirstOrDefault(x => x.GetInterface(typeof(ITrusteeAssist).FullName!) is not null);
    //    if (type is null) return;

    //    ITrusteeAssist trusteeAssist = (ITrusteeAssist)Activator.CreateInstance(type)!;

    //    Stream? iconStream = null;
    //    var res = assembly.GetManifestResourceNames();
    //    var name = res.FirstOrDefault(x => x.Contains(".logo."));
    //    if (name is not null)
    //        iconStream = assembly.GetManifestResourceStream(name);

    //    var icon = new BitmapImage();
    //    icon.BeginInit();
    //    icon.StreamSource = iconStream;
    //    icon.EndInit();

    //    Trustees.Add(new PlatformPageViewModelTrustee(trusteeAssist, trusteeAssist.Name, icon));
    //}

    /// <summary>
    /// 
    /// </summary>
    /// <param name="assembly"></param>
    //void TryAddSignature(Assembly assembly)
    //{
    //    var type = assembly.GetTypes().FirstOrDefault(x => x.GetInterface(typeof(IDigitalSignature).FullName!) is not null);
    //    if (type is null) return;

    //    IDigitalSignature assist = (IDigitalSignature)Activator.CreateInstance(type)!;

    //    Stream? iconStream = null;
    //    var res = assembly.GetManifestResourceNames();
    //    var name = res.FirstOrDefault(x => x.Contains(".logo."));
    //    if (name is not null)
    //        iconStream = assembly.GetManifestResourceStream(name);

    //    using var db = DbHelper.Platform();
    //    var acc = db.GetCollection<PlatformAccount>().FindById(assist.Identifier);

    //    assist.UserID = acc?.UserId;
    //    assist.Password = acc?.Password;

    //    var icon = new BitmapImage();
    //    icon.BeginInit();
    //    icon.StreamSource = iconStream;
    //    icon.EndInit();

    //    //Digitals.Add(new PlatformPageViewModelDigital(assist, assist.Name, icon));
    //}


    partial void OnUseProxyForTrusteeChanged(bool value)
    {
        UpdateProxy();

    }

    partial void OnSetESigningStateChanged(bool value)
    {
        ESignSource.View.Refresh();
    }

    private async Task UpdateLocalIP()
    {
        var value = UseProxyForTrustee;

        try
        {
            using var client = new HttpClient(new HttpClientHandler
            {
                UseProxy = value,
                Proxy = new WebProxy(ProxyViewModel.Address) { Credentials = string.IsNullOrWhiteSpace(ProxyViewModel.User) ? null : new NetworkCredential(ProxyViewModel.User, ProxyViewModel.Password) }
            });
            try { LocalIP = (await client.GetStringAsync("https://api-ipv4.ip.sb/ip")).Trim(); }
            catch { LocalIP = await client.GetStringAsync("https://ifconfig.me/ip"); }
        }
        catch (HttpRequestException e) when (e.Message.Contains("积极拒绝"))
        {
            Toast.Error("代理不可用");
            UseProxyForTrustee = false;
        }
        catch
        {
            LocalIP = "Unknown";
        }

    }

    internal void OpenDebug()
    {
        AllowWorkReport = true;
    }

    public void Receive(TrusteeRunMessage message)
    {
        if (TrusteeAPIButtons.FirstOrDefault(x => x.Method == message.Name) is SyncButtonInfo btn)
            btn.IsRunning = message.IsRunning;
    }


    public void Receive(SigningRunMessage message)
    {
        if (ESigningButtons.FirstOrDefault(x => x.Method == message.Name) is SyncButtonInfo btn)
            btn.IsRunning = message.IsRunning;
    }


}





public partial class SyncButtonData(Geometry Icon, ICommand Command, SyncButtonData.SyncProcess SyncProcess, string Description) : ObservableObject
{
    [ObservableProperty]
    public partial bool IsRunning { get; set; }

    public Geometry Icon { get; } = Icon;

    public ICommand Command { get; } = Command;

    public SyncProcess SyncProcesser { get; } = SyncProcess;


    public string Description { get; } = Description;

    public delegate Task SyncProcess();
}


public partial class SyncButtonInfo(Geometry Icon, IAsyncRelayCommand Command, string Method, string ToolTip) : ObservableObject
{
    public Geometry Icon { get; } = Icon;
    public IAsyncRelayCommand Command { get; } = Command;

    public string Method { get; } = Method;

    [ObservableProperty]
    public partial bool IsRunning { get; set; }

    public string ToolTip { get; } = ToolTip;
}



public partial class AmacAccountViewModel : ObservableObject
{
    public string Identifier { get; }

    public string? Url { get; set; }

    public string? Title { get; set; }

    private AmacAccount _account;

    public virtual bool IsChanged => Name != _account.Name || Password != _account.Password;

    public virtual bool CanSave => !string.IsNullOrWhiteSpace(Name) && !string.IsNullOrWhiteSpace(Password) && IsChanged;

    [ObservableProperty]
    public partial bool IsReadOnly { get; set; } = true;


    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsChanged))]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial string? Name { get; set; }


    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsChanged))]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial string? Password { get; set; }


    [SetsRequiredMembers]
    public AmacAccountViewModel(AmacAccount account)
    {
        _account = account;
        Identifier = account.Id;
        Name = account.Name;
        Password = account.Password;

        switch (Identifier)
        {
            case "ambers":
                Url = "https://ambers.amac.org.cn/";
                Title = "Ambers";
                break;
            case "human":
                Url = "https://human.amac.org.cn/";
                Title = "从业人员";
                break;
            case "peixun":
                Url = "https://peixun.amac.org.cn/";
                Title = "培训平台";
                break;
            case "xinpi":
                Url = "https://pfid.amac.org.cn/";
                Title = "信批平台";
                break;
            default:
                break;
        }
    }


    [RelayCommand(CanExecute = nameof(CanSave))]
    public void Save()
    {
        _account = _account with { Name = Name!, Password = Password! };
        using var db = DbHelper.Base();
        db.GetCollection<AmacAccount>().Upsert(_account);

        OnPropertyChanged(nameof(IsChanged));
        OnPropertyChanged(nameof(CanSave));
    }

    [RelayCommand]
    public void GoTo()
    {
        if (Url?.Length > 10)
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Url) { UseShellExecute = true });
    }
}


public partial class AmacDirectViewModel : ObservableObject
{
    public string Identifier { get; }

    public string? Title { get; set; }

    private AmacReportAccount _account;

    public virtual bool IsChanged => Name != _account.Name || Password != _account.Password || Key != _account.Key;

    public virtual bool CanSave => !string.IsNullOrWhiteSpace(Name) && !string.IsNullOrWhiteSpace(Password) && IsChanged;

    [ObservableProperty]
    public partial bool IsReadOnly { get; set; } = true;


    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsChanged))]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial string? Name { get; set; }


    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsChanged))]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial string? Password { get; set; }


    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsChanged))]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial string? Key { get; set; }

    [SetsRequiredMembers]
    public AmacDirectViewModel(AmacReportAccount account)
    {
        _account = account;

        Title = account.Id switch { "pmg" => "运营系统", "pof" => "信披系统", _ => "" };

        Identifier = account.Id;
        Name = account.Name;
        Password = account.Password;
        Key = account.Key;
    }


    [RelayCommand(CanExecute = nameof(CanSave))]
    public void Save()
    {
        _account = _account with { Name = Name!, Password = Password!, Key = Key! };
        using var db = DbHelper.Base();
        db.GetCollection<AmacReportAccount>().Upsert(_account);

        OnPropertyChanged(nameof(IsChanged));
        OnPropertyChanged(nameof(CanSave));
    }
}


public partial class TokenProviderViewModel : IViewModel<TokenProvider, TokenProviderViewModel>, IDataValidation
{
    public static TokenProviderStyle[] Styles { get; } = [TokenProviderStyle.OpenAI, TokenProviderStyle.Anthropic];

    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(Id) && !string.IsNullOrWhiteSpace(Url) && !string.IsNullOrWhiteSpace(Key) && Style != TokenProviderStyle.None;
    }
}