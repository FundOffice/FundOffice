using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FMO.AMAC;
using FMO.Models;
using FMO.Trustee;
using FMO.Utilities;
using Microsoft.Win32;
using System.IO;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using Utilities;

namespace FMO;

/// <summary>
/// InitWindow.xaml 的交互逻辑
/// </summary>
public partial class InitWindow : Window
{
    public InitWindow()
    {
        InitializeComponent();
    }













}


public partial class InitWindowViewModel : ObservableRecipient
{
    [ObservableProperty]
    public partial ManagerInfo[]? ManagerOptions { get; set; }

    [ObservableProperty]
    public partial string? ManagerName { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SetUpCommand))]
    public partial Manager? Manager { get; set; }

    [ObservableProperty]
    public partial bool ShowProgress { get; set; }


    [ObservableProperty]
    public partial double InitProgress { get; set; }


    [ObservableProperty]
    public partial bool ShowSetManager { get; set; }


    [ObservableProperty]
    public partial bool ShowStep1 { get; set; }


    [ObservableProperty]
    public partial bool ShowStep2 { get; set; }


    [ObservableProperty]
    public partial string? CurrentScale { get; set; }


    [ObservableProperty]
    public partial bool IsNetworkDisconnected { get; set; }



    [ObservableProperty]
    public partial int PreNewRuleFundCount { get; set; }


    [ObservableProperty]
    public partial int NormalFundCount { get; set; }


    [ObservableProperty]
    public partial int AdviseFundCount { get; set; }



    [RelayCommand]

    public void Close()
    {
        App.Current.Windows[^1].Close();
        App.Current.Shutdown();
    }


    public bool CanInit => Manager is not null;

    /// <summary>
    /// 初始化进程
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanInit))]
    public async Task SetUpAsync()
    {
        if (Manager is null) return;

        ShowStep1 = false;
        ShowStep2 = true;
        ShowProgress = true;

        List<FundBasicInfo> funds = new List<FundBasicInfo>();
        await AmacHtml.CrawleManagerInfo(Manager, funds);
        Manager.Name = ManagerName!;

        CurrentScale = Manager.ScaleRange;
        PreNewRuleFundCount = funds.Count(x => x.IsPreRule);
        NormalFundCount = funds.Count(x => !x.IsPreRule && !x.IsAdvisor);
        AdviseFundCount = funds.Count(x => x.IsAdvisor);



        ///保存数据库
        Manager.IsMaster = true;

#if RELEASE
        using (var key = Registry.CurrentUser.CreateSubKey(@$"Software\Nexus"))       
#else
        using (var key = Registry.CurrentUser.CreateSubKey(@$"Software\Nexus\Debug"))
#endif        
        {
            key.SetValue("Cap", AesHelper.Encrypt(Manager.Name));
            key.SetValue("Code", AesHelper.Encrypt(Manager.Identity?.Id ?? ""));
        }

        //首次运行，记录Patch，以防错误运行

        DbHelper.Init();
        using var db = DbHelper.Base();
        db.GetCollection<Manager>().Insert(Manager);
        DatabaseAssist.InitPatch();

        //db.GetCollection<FundBasicInfo>().InsertBulk(funds);

        db.GetCollection<Fund>().InsertBulk(funds.Select(x => new Fund
        {
            Name = x.Name!,
            Code = $"unset.{x.Name!.GetHashCode()}",
            ShortName = Fund.GetDefaultShortName(x.Name!),
            Url = "https://gs.amac.org.cn/amac-infodisc/res/pof" + x.Url,
            AsAdvisor = x.IsAdvisor,
            AmacID = Regex.Match(x.Url!, @"\d{5,}").Value
        }));

        // 增加默认的platfrom proxy
        var b64 = "eyJJZCI6MCwiVXNlUHJveHkiOnRydWUsIlByb3h5VXJsIjoiaHR0cDovLzExNy43Mi41OS4xNDU6MTU3NyIsIlByb3h5VXNlciI6InRncCIsIlByb3h5UGFzc3dvcmQiOiJoNDMyMDlmd2o0MjA5NGhrMjM5ZiJ9";
        if (JsonSerializer.Deserialize(Encoding.UTF8.GetString(Convert.FromBase64String(b64)), typeof(TrusteeUnifiedConfig)) is TrusteeUnifiedConfig config)
            db.GetCollection<TrusteeUnifiedConfig>().Insert(config);

        Restart();

    }

    [RelayCommand]
    public void ChooseFolder()
    {
        OpenFolderDialog dialog = new OpenFolderDialog();
        var r = dialog.ShowDialog();
        if (r ?? false)
        {
            // 从注册表读取
#if RELEASE
            using (var key = Registry.CurrentUser.CreateSubKey(@$"Software\Nexus")) 
#else
            using (var key = Registry.CurrentUser.CreateSubKey(@$"Software\Nexus\Debug"))
#endif
            {
                key.SetValue("WorkingFolder", dialog.FolderName);

                // 设置工作目录
                Directory.SetCurrentDirectory(dialog.FolderName);

                // 检查是否有数据
                var hasdb = File.Exists(Path.Combine(dialog.FolderName, "data", "base.db"));
                if (hasdb)
                {
                    // 判断能否解开
                    try
                    {
                        DbHelper.Init();
                        using var db = DbHelper.Base();
                        var manager = db.GetCollection<Manager>().FindOne(x => x.IsMaster);

                        Restart();
                    }
                    catch
                    {
                        //出错的
                        Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
                        DbHelper.Init();

                        HandyControl.Controls.MessageBox.Warning("文件夹中存在数据，但无法打开，请重新选择文件夹");
                    }
                }
                else
                    ShowSetManager = true;
            }

        }
    }

    /// <summary>
    /// 重启程序
    /// </summary>
    public void Restart()
    {
#if !DEBUG

        var field = App.Current.GetType().GetField("mutex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var mutex = field!.GetValue(App.Current) as Mutex;
        mutex!.Close(); 
#endif

        System.Diagnostics.Process.Start(System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName);

        Close();
    }


    /// <summary>
    /// 根据关键字检索相关管理人
    /// </summary>
    /// <param name="value"></param>
    async partial void OnManagerNameChanged(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;

        ///选中一个名称
        var sel = ManagerOptions?.FirstOrDefault(x => x.ManagerName == value);

        if (sel is not null)
        {
            OnSelectManager(sel);
            return;
        }

        Manager = null;
        ManagerOptions = await AmacHtml.GetInstitutionInfoFromAmac(value);

        ///如果是复制的全称
        sel = ManagerOptions?.FirstOrDefault(x => x.ManagerName == value);

        if (sel is not null)
            OnSelectManager(sel);
    }

    private void OnSelectManager(ManagerInfo sel)
    {
        Manager = new Manager
        {
            Id = 1,
            Name = sel.ManagerName!,
            AmacId = sel.Id!,
            RegisterAddress = sel.RegisterAddress,
            RegisterDate = DateOnly.FromDateTime(new DateTime(1970, 1, 1).AddMilliseconds(sel.RegisterDate).ToLocalTime()),
            OfficeAddress = sel.OfficeAddress,
            SetupDate = DateOnly.FromDateTime(new DateTime(1970, 1, 1).AddMilliseconds(sel.EstablishDate).ToLocalTime()),
            RegisterNo = sel.RegisterNo!,
            ArtificialPerson = sel.ArtificialPersonName,
            FundCount = sel.FundCount,
            HasCreditTips = sel.HasCreditTips,
            HasSpecialTips = sel.HasSpecialTips,
            MemberType = sel.MemberType
        };

        ShowStep1 = true;
        ShowStep2 = false;
        ShowProgress = false;
        CurrentScale = null;
        PreNewRuleFundCount = 0;
        NormalFundCount = 0;
        AdviseFundCount = 0;
    }

    public void Receive(object message)
    {
        if (message is int i)
            InitProgress = i;
        else if (message is double d)
            InitProgress = d;

    }


    public InitWindowViewModel()
    {
        IsActive = true;

#if RELEASE
        using (var key = Registry.CurrentUser.OpenSubKey(@$"Software\Nexus"))       
#else
        using (var key = Registry.CurrentUser.OpenSubKey(@$"Software\Nexus\Debug"))
#endif
            ShowSetManager = key?.GetValue("WorkingFolder") is string dir && Directory.Exists(dir);


        IsNetworkDisconnected = !NetworkInterface.GetIsNetworkAvailable();
        NetworkChange.NetworkAvailabilityChanged += NetworkChange_NetworkAvailabilityChanged;
    }

    private void NetworkChange_NetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
    {

        IsNetworkDisconnected = !NetworkInterface.GetIsNetworkAvailable();
    }

 


}














