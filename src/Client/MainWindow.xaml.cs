using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FMO.Logging;
using FMO.Models;
using FMO.Todo;
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

        Width = Math.Min(1920, SystemParameters.FullPrimaryScreenWidth * 0.9);
        Height = Math.Min(1080, SystemParameters.FullPrimaryScreenHeight * 0.9);

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

    private void TodoBorder_LostFocus(object sender, RoutedEventArgs e)
    {

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

public partial class MainWindowViewModel : ObservableRecipient, IRecipient<string>, IRecipient<OpenFundMessage>,
    IRecipient<OpenPageMessage>, IRecipient<ToastMessage>, IRecipient<VerifyMessage>, IRecipient<VerifyResultMessage>,
    IRecipient<TodoGroupStatusMessage>, IRecipient<TodoStatusMessage>, IRecipient<ITodo>, IRecipient<MissionFailedMessage>, IRecipient<AddNotifyTodoMessage>
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


    [ObservableProperty]
    public partial ObservableCollection<TodoViewModel>? TodoCollection { get; private set; }




    [ObservableProperty]
    public partial bool ShowTodoList { get; set; }

    public Version? Version { get; set; }

    public string? OrgId { get; set; }

    public MainWindowViewModel()
    {
        IsActive = true;
        Version = Assembly.GetExecutingAssembly().GetName().Version;

        MenuItems = [new MainMenu { IsEnabled = true, Title = "管理人", IconBrush = Brushes.BlueViolet, Command = OpenPageCommand, Parameter = "ManagerPage", Icon = GetGeometry("f.house") },
                     new MainMenu { IsEnabled = true, Title = "基金", IconBrush = Brushes.Violet, Command = OpenPageCommand, Parameter = "FundsPage", Icon = GetGeometry("f.fire")},
                     new MainMenu { IsEnabled = true, Title = "客户", IconBrush = Brushes.ForestGreen, Command = OpenPageCommand, Parameter = "Customer", Icon = GetGeometry("f.user")},
                     new MainMenu { IsEnabled = true, Title = "TA", IconBrush = Brushes.Orange, Command = OpenPageCommand, Parameter = "TA", Icon = GetGeometry("f.calendar-days")},
                     new MainMenu { IsEnabled = false, Title = "信批", IconBrush = Brushes.OliveDrab, Command = OpenPageCommand, Parameter = "Disclosure", Icon = GetGeometry("f.disclosure")},
                     new MainMenu { IsEnabled = false, Title = "平台", IconBrush = Brushes.Brown, Command = OpenPageCommand, Parameter = "Trustee", Icon = GetGeometry("f.infinity")},
                     new MainMenu { IsEnabled = false, Title = "任务", IconBrush = Brushes.DarkOrchid, Command = OpenPageCommand, Parameter = "Task", Icon = GetGeometry("f.bolt")},
                     new MainMenu { IsEnabled = true, Title = "报表", IconBrush = Brushes.RoyalBlue, Command = OpenPageCommand, Parameter = "Statement", Icon = GetGeometry("f.square-poll-vertical")},
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
                LogEx.Error($"Failed to load main icon: {e}");
            }
        }

        var all = TodoService.GetAll();
        if (all is not null)
            TodoCollection = [.. all.Select(x => TodoViewModelFactory.Create(x)).Where(x => x is not null).Select(x => x!)];


#if DEBUG
        EventManager.RegisterClassHandler(
            typeof(Window),
            Window.KeyDownEvent,
            new KeyEventHandler(MakeDebugData)
        );
#endif
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
        LogEx.Warning(DateTime.Now.ToString());
    }


    [RelayCommand]
    public void OpenTodo()
    {
        ShowTodoList = !ShowTodoList;
    }


    [RelayCommand]
    public void OpenSetting()
    {
        var wnd = new SettingsWindow();
        wnd.Owner = Application.Current.MainWindow;
        wnd.ShowDialog();
    }

#if DEBUG
    private void MakeDebugData(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.F3) return;

        //using var db = DbHelper.Base();
        //var fund = db.GetCollection<Fund>().FindOne(x => x.Name.Contains("5") && x.Status == FundStatus.Normal);
        //var req = db.GetCollection<TransferRequest>().FindOne(x => x.FundId == fund.Id && x.RequestType == TransferRequestType.Redemption);
        //req.RequestDate = new DateOnly(2026, 4, 1);
        //req.RequestAmount = 1000000;

        //TodoService.AutoHugeRedemption([req]);

        // TodoCollection?.Add(new HugeRedemptionTodoViewModel { OpenDay = DateOnly.FromDayNumber(Environment.TickCount%454)});

        //Schedule.MissionSchedule.Register(new Schedule. { Name="abc", Description = "ccd" });
        //TodoService.Register(new JustNotifyTodo { CreateTime = DateTime.Now, UniqueId = $"Settlement_{1}_{2}", Message = "msg" });

        //DataTracker.Notify([new TransferOrder { FundId = 9, FundName = "fd", InvestorIdentity = "ff", InvestorName = "fdsf", Type = TransferOrderType.Buy, Number = 100 }]);
    }
#endif


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

    public void Receive(TodoStatusMessage message)
    {
        if (TodoCollection is null) return;
        foreach (var t in TodoCollection.ToArray())
        {
            if (t.Id == message.Id && message.Status != TotoStatus.None)
                TodoCollection.Remove(t);
        }
    }

    public void Receive(ITodo message)
    {
        var vm = TodoViewModelFactory.Create(message);
        if (vm is null)
        {
            LogEx.Error($"{message.GetType()} 无法创建ViewModel");
            return;
        }

        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            if (TodoCollection is null)
                TodoCollection = [vm];
            else
            {
                if (message.UniqueId is not null)
                    TodoCollection.Where(x => x.UniqueId == message.UniqueId).ToList().ForEach(x => TodoCollection.Remove(x));
                TodoCollection.Add(vm);
            }
        });

    }

    public void Receive(TodoGroupStatusMessage message)
    {
        if (TodoCollection is null) return;
        foreach (var t in TodoCollection.ToArray())
        {
            if (t.UniqueId == message.UniqueId && message.Status != TotoStatus.None)
                TodoCollection.Remove(t);
        }
    }

    public void Receive(MissionFailedMessage message)
    {
        TodoService.Register(new JustNotifyTodo { CreateTime = DateTime.Now, UniqueId = $"MissionError_{message.Id}", Message = $"任务【{message.Id}】执行失败，请查看log" });
    }

    public void Receive(AddNotifyTodoMessage todo) => TodoService.Register(new JustNotifyTodo { CreateTime = DateTime.Now, UniqueId = todo.Unique, Message = todo.Message });
}


public partial class MainMenu : ObservableObject, IRecipient<UniformTip>, IRecipient<MainMenuEnableMessage>
{
    public MainMenu()
    {
        WeakReferenceMessenger.Default.RegisterAll(this);
    }

    public required string Title { get; set; }

    public required Brush IconBrush { get; set; }


    [ObservableProperty]
    public partial bool IsEnabled { get; set; }

    public Geometry? Icon { get; set; }


    public ICommand? Command { get; set; }


    public string? Parameter { get; set; }

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

    public void Receive(MainMenuEnableMessage message)
    {
        if (message.Key == Parameter)
            IsEnabled = message.IsEnabled;
    }
}
