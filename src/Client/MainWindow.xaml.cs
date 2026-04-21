using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FMO.Models;
using FMO.Utilities;
using Serilog;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FMO;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : HandyControl.Controls.Window
{
    public MainWindow()
    {
        InitializeComponent();

#if DEBUG
        Title = "调试模式";
#endif

        Width = SystemParameters.FullPrimaryScreenWidth * 0.9;
        Height = SystemParameters.FullPrimaryScreenHeight * 0.9;

        PreviewKeyDown += MainWindow_PreviewKeyDown;

        HelpService.Initialize(this);
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F7)
        {
            Window window = new Window
            {
                Title = "Log",
                Content = new LogView(),
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Owner = this
            };
            window.ShowDialog();
        }
    }

    private void DockPanel_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            this.DragMove();
    }


}


public partial class TabItemInfo : ObservableObject
{
    public required string Header { get; set; }

    public Brush? Background { get; set; }

    public Brush? HeaderBrush { get; set; } = Brushes.Black;

    public FrameworkElement? Content { get; set; }

    public bool IsCloseable { get; set; } = true;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public bool IsFund { get; set; }
}

public partial class MainWindowViewModel : ObservableRecipient, IRecipient<string>, IRecipient<OpenFundMessage>, IRecipient<OpenPageMessage>, IRecipient<ToastMessage>, IRecipient<VerifyMessage>, IRecipient<VerifyResultMessage>
{

    private PlatformPageViewModel? PlatformDataContext { get; set; }


    [ObservableProperty]
    public partial string? Title { get; set; }


    /// <summary>
    /// 通知
    /// </summary>
    [ObservableProperty]
    public partial string? Toast { get; set; }


    [ObservableProperty]
    public partial ObservableCollection<TabItemInfo> Pages { get; private set; }


    public ObservableCollection<MainMenu> MenuItems { get; }


    public Version? Version { get; set; }

    public string? OrgId { get; set; }

    public MainWindowViewModel()
    {
        IsActive = true;
        Version = Assembly.GetExecutingAssembly().GetName().Version;

        MenuItems = [new MainMenu { Title = "管理人", IconBrush = Brushes.BlueViolet, Command = OpenPageCommand, Parameter = "ManagerPage", Icon = GetGeometry("f.house") },
                    new MainMenu { Title = "基金", IconBrush = Brushes.Violet, Command = OpenPageCommand, Parameter = "FundsPage", Icon = GetGeometry("f.fire")},
                    new MainMenu { Title = "客户", IconBrush = Brushes.ForestGreen, Command = OpenPageCommand, Parameter = "Customer", Icon = GetGeometry("f.user")},
                    new MainMenu { Title = "TA", IconBrush = Brushes.Orange, Command = OpenPageCommand, Parameter = "TA", Icon = GetGeometry("f.calendar-days")},
                    new MainMenu { Title = "信批", IconBrush = Brushes.OliveDrab, Command = OpenPageCommand, Parameter = "Disclosure", Icon = GetGeometry("f.disclosure")},
                    new MainMenu { Title = "平台", IconBrush = Brushes.Brown, Command = OpenPageCommand, Parameter = "Trustee", Icon = GetGeometry("f.infinity")},
                    new MainMenu { Title = "任务", IconBrush = Brushes.DarkOrchid, Command = OpenPageCommand, Parameter = "Task", Icon = GetGeometry("f.bolt")},
                    new MainMenu { Title = "报表", IconBrush = Brushes.RoyalBlue, Command = OpenPageCommand, Parameter = "Statement", Icon = GetGeometry("f.square-poll-vertical")},
               /*     new MainMenu { Title = "法规", IconBrush = Brushes.OrangeRed, Command = OpenPageCommand, Parameter = "Law", Icon = GetGeometry("f.scale-balanced")},  */];


        Pages = new ObservableCollection<TabItemInfo>([new TabItemInfo { Header = "首页", IsCloseable = false, Content = new HomePage() }]);

        // 管理人名称
        using var db = DbHelper.Base();// DbHelper.Base();
        Title = db.GetCollection<Manager>().FindOne(x => x.IsMaster)?.Name;

        OrgId = CalcOrgId(Title);

        if (db.FileStorage.Exists("icon.main"))
        {
            try
            {
                using var ms = new MemoryStream();
                db.FileStorage.Download("icon.main", ms);
                ms.Seek(0, SeekOrigin.Begin);
                BitmapImage bitmapSource = new BitmapImage();
                bitmapSource.BeginInit();
                bitmapSource.CacheOption = BitmapCacheOption.OnLoad;
                bitmapSource.StreamSource = ms;
                bitmapSource.EndInit();
                App.Current.MainWindow.Icon = bitmapSource;
            }
            catch (Exception e)
            {
            }
        }
    }

    private string? CalcOrgId(string? input)
    {
        // 检查输入是否为空
        if (string.IsNullOrWhiteSpace(input))
            return null;

        using (MD5 md5 = MD5.Create())
        {
            // 将字符串转换为字节数组
            byte[] inputBytes = Encoding.UTF8.GetBytes(input);

            // 计算哈希值
            byte[] hashBytes = md5.ComputeHash(inputBytes);

            // 将字节数组转换为十六进制字符串
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < hashBytes.Length; i++)
                sb.Append(hashBytes[i].ToString("x2"));

            return sb.ToString();
        }
    }


    public void Receive(string message)
    {
        HandyControl.Controls.Growl.Info(message);
    }

    protected override void OnActivated()
    {
        WeakReferenceMessenger.Default.RegisterAll(this);
        //WeakReferenceMessenger.Default.Register<string, string>(this, "toast");
    }

    public void Receive(OpenFundMessage message)
    {
        var db = DbHelper.Base();
        var fund = db.GetCollection<Fund>().FindById(message.Id);
        //var ele = db.GetCollection<FundElements>().FindById(message.Id);
        //if (ele is null)
        //{
        //    ele = FundElements.Create(message.Id);
        //    db.GetCollection<FundElements>().Insert(ele);
        //}

        // 检查要求
        //var flows = db.GetCollection<FundFlow>().Find(x => x.FundId == fund.Id).Select(x => x.Id).ToArray();


        //if (ele.Init()) db.GetCollection<FundElements>().Update(ele);
        db.Dispose();
        if (fund is null) return;

        var page = Pages.FirstOrDefault(x => x.Content is FundInfoPage p && p.Tag.ToString() == fund.Name);
        if (page is null)
        {
            var obj = new FundInfoPage() { Tag = fund.Name, DataContext = new FundInfoPageViewModel(fund) };
            page = new TabItemInfo { Header = fund.ShortName ?? fund.Name ?? "Fund", IsFund = true, Content = obj, };//new TabItem { Header = GenerateHeader(fund.ShortName ?? fund.Name ?? "Fund"), Content = obj };
            Pages.Add(page);
        }

        page.IsSelected = true;
    }

    public void Receive(OpenPageMessage message)
    {
        OpenPage(message.Page);
    }


    public void Receive(VerifyResultMessage message)
    {
        if (!message.IsSuccessed)
            HandyControl.Controls.Growl.Warning(message.Error);
    }

    public void Receive(VerifyMessage message)
    {
        var wnd = new VerifyWindow();
        wnd.DataContext = new VerifyWindowViewModel(message);
        wnd.Owner = App.Current.MainWindow;
        wnd.ShowDialog();
    }

    [RelayCommand]
    public void OpenPage(string id)
    {
        switch (id)
        {
            case "Trustee":
                {
                    var page = Pages.FirstOrDefault(x => x.Content is PlatformPage);
                    if (page is null)
                    {
                        PlatformDataContext = PlatformDataContext ?? new PlatformPageViewModel();
                        page = new TabItemInfo { Header = "外部平台", Background = Brushes.Brown, HeaderBrush = Brushes.White, Content = new PlatformPage { DataContext = PlatformDataContext } };
                        Pages.Add(page);
                    }

                    page.IsSelected = true;
                    break;
                }

            case "FundsPage":
                {
                    var page = Pages.FirstOrDefault(x => x.Content is FundsPage);
                    if (page is null)
                    {
                        page = new TabItemInfo { Header = "基金总览", Background = Brushes.Violet, Content = new FundsPage() };
                        Pages.Add(page);
                    }

                    page.IsSelected = true;
                    break;
                }

            case "ManagerPage":
                {
                    var page = Pages.FirstOrDefault(x => x.Content is ManagerPage);
                    if (page is null)
                    {
                        page = new TabItemInfo { Header = "管理人", HeaderBrush = Brushes.White, Background = Brushes.BlueViolet, Content = new ManagerPage() };// new TabItem { Header = GenerateHeader("管理人"), Background = Brushes.BlueViolet, Foreground = Brushes.White, Content = new ManagerPage() { Foreground = Brushes.Black } };
                        Pages.Add(page);
                    }

                    page.IsSelected = true;
                    break;
                }
            case "Task":
                {
                    var page = Pages.FirstOrDefault(x => x.Content is TaskPage);
                    if (page is null)
                    {
                        page = new TabItemInfo { Header = "任务", HeaderBrush = Brushes.White, Background = Brushes.DarkOrchid, Content = new TaskPage() };
                        Pages.Add(page);
                    }

                    page.IsSelected = true;
                    break;
                }

            case "Statement":
                {
                    var page = Pages.FirstOrDefault(x => x.Content is StatementPage);
                    if (page is null)
                    {
                        page = new TabItemInfo { Header = "报表", Background = Brushes.RoyalBlue, HeaderBrush = Brushes.White, Content = new StatementPage() };
                        Pages.Add(page);
                    }

                    page.IsSelected = true;
                    break;
                }

            case "Customer":
                {
                    var page = Pages.FirstOrDefault(x => x.Content is CustomerPage);
                    if (page is null)
                    {
                        page = new TabItemInfo
                        {
                            Header = ("投资人"),
                            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#49bc69")),
                            Content = new CustomerPage()
                        };
                        Pages.Add(page);
                    }

                    page.IsSelected = true;
                    break;
                }

            case "TA":
                {
                    var page = Pages.FirstOrDefault(x => x.Content is TransferRecordPage);
                    if (page is null)
                    {
                        page = new TabItemInfo { Header = "  TA  ", Background = Brushes.Orange, Content = new TransferRecordPage() };
                        Pages.Add(page);
                    }

                    page.IsSelected = true;
                    break;
                }


            case "Disclosure":
                {
                    var page = Pages.FirstOrDefault(x => x.Content is DisclosurePage);
                    if (page is null)
                    {
                        page = new TabItemInfo { Header = "信批", Background = Brushes.OliveDrab, HeaderBrush = Brushes.White, Content = new DisclosurePage() };
                        Pages.Add(page);
                    }

                    page.IsSelected = true;
                    break;
                }

            case "Law":
                {
                    var page = Pages.FirstOrDefault(x => x.Content is LawPage);
                    if (page is null)
                    {
                        page = new TabItemInfo { Header = "法律法规", Background = Brushes.OrangeRed, HeaderBrush = Brushes.White, Content = new LawPage() };
                        Pages.Add(page);
                    }

                    page.IsSelected = true;
                    break;
                }


            default:
                //{
                //    Type? type = Type.GetType($"FMO.{id}");
                //    if (type is null) break;

                //    var page = Pages.FirstOrDefault(x => x.Content?.GetType() == type);
                //    if (page is null)
                //    {
                //        var obj = Activator.CreateInstance(type) as UserControl;
                //        page = new TabItem { Header = GenerateHeader(obj?.Tag as string ?? "新标签"), Content = obj };
                //        Pages.Add(page);
                //    }

                //    page.IsSelected = true;
                //}
                break;
        }

    }

    [RelayCommand]
    public void ClosePage(TabItemInfo tabItem)
    {
        if (tabItem is not null)
        {
            if (tabItem.Content is FrameworkElement e && e.DataContext is not null)
                e.DataContext = null;

            if (tabItem.Content is not null)
                tabItem.Content.DataContext = null;
            tabItem.Content = null;
            Pages.Remove(tabItem);

        }
    }

    [RelayCommand]
    public void CloseWindow()
    {
        App.Current.MainWindow.Close();
    }


    [RelayCommand]
    public void PinWindow()
    {
        App.Current.MainWindow.Topmost = !App.Current.MainWindow.Topmost;
    }


    [RelayCommand]
    public void test()
    {
        Log.Warning(DateTime.Now.ToString());
    }

    public void Receive(ToastMessage message)
    {
        switch (message.Level)
        {
            case LogLevel.Info:
                HandyControl.Controls.Growl.Info(message.Message);
                break;
            case LogLevel.Warning:
                HandyControl.Controls.Growl.Warning(message.Message);
                break;
            case LogLevel.Error:
                HandyControl.Controls.Growl.Error(message.Message);
                break;
            case LogLevel.Success:
                HandyControl.Controls.Growl.Success(message.Message);
                break;
            default:
                break;
        }
    }

    /// <summary>
    /// 从指定资源字典中获取Geometry对象
    /// </summary>
    /// <param name="resourceKey">Geometry资源的键名</param>
    /// <param name="resourceDictionaryPath">资源字典路径，默认为"/icon.xaml"</param>
    /// <returns>Geometry对象</returns>
    public static Geometry? GetGeometry(string resourceKey, string resourceDictionaryPath = "/Icons.xaml")
    {
        // 加载资源字典
        ResourceDictionary resourceDictionary = new ResourceDictionary
        {
            Source = new System.Uri(resourceDictionaryPath, System.UriKind.Relative)
        };

        // 从资源字典中获取Geometry
        if (resourceDictionary.Contains(resourceKey) && resourceDictionary[resourceKey] is Geometry geometry)
            return geometry;

        return null;
    }


}


public partial class MainMenu : ObservableObject, IRecipient<UniformTip>
{
    public MainMenu()
    {
        WeakReferenceMessenger.Default.RegisterAll(this);
    }

    public required string Title { get; set; }

    public required Brush IconBrush { get; set; }


    public Geometry? Icon { get; set; }


    public ICommand? Command { get; set; }


    public object? Parameter { get; set; }

    [ObservableProperty]
    public partial string? Tip { get; set; }

    [ObservableProperty]
    public partial bool HasTip { get; set; }

    public void Receive(UniformTip message)
    {
        switch (message.Type)
        {
            case TipType.TANoOwner:
                if (Title == "TA")
                {
                    if (message.Tip is null)
                    {
                        HasTip = false;
                        break;
                    }
                    HasTip = true;
                    Tip = message.Tip.ToString();
                }
                break;
            default:
                break;
        }
    }
}
