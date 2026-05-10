using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FMO.Disclosure;
using FMO.ESigning;
using FMO.IO.AMAC;
using FMO.Logging;
using FMO.Models;
using FMO.Schedule;
using FMO.Trustee;
using FMO.Utilities;
using Microsoft.Win32;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using Serilog;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FMO;

/// <summary>
/// HomePage.xaml 的交互逻辑
/// </summary>
public partial class HomePage : UserControl
{
    public HomePage()
    {
        InitializeComponent();
    }

    private void UserControl_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.F5 && DataContext is HomePageViewModel vm)
            Task.Run(() => vm.InitPlot());
    }
}



public partial class HomePageViewModel : ObservableObject, IRecipient<FundTipMessage>, IRecipient<TrusteeWorkResult>
{
    /// <summary>
    /// 是否正在同步
    /// </summary>
    [ObservableProperty]
    public partial bool IsSynchronizing { get; set; }


    /// <summary>
    /// 是否正在自检
    /// </summary>
    [ObservableProperty]
    public partial bool IsSelfTesting { get; set; }



    [ObservableProperty]
    public partial double BackupProcess { get; set; }
    [ObservableProperty]
    public partial double BackupProcess2 { get; set; }

    [ObservableProperty]
    public partial IList<string>? FundTips { get; set; }


    [ObservableProperty]
    public partial PlotModel? ManageScaleContext { get; set; }

    [ObservableProperty]
    public partial PlotModel? BuySellIn7DaysContext { get; set; }


    [ObservableProperty]
    public partial bool IsInitializing { get; set; }


    [ObservableProperty]
    public partial string? InitializeMesage { get; set; }

    public Tool[] Tools { get; set; }


    /// <summary>
    /// 募集户余额报警
    /// </summary> 
    public RaisingAccountWarning RaisingAccountTip { get; } = new();


    public DateTime DailyUpdateTime { get; set; }

    private Timer _dailyTimer;

    private CancellationTokenSource _backupToken = new();

    public HomePageViewModel()
    {
        WeakReferenceMessenger.Default.RegisterAll(this);

        AssemblyLoadContext.Default.Resolving += Default_Resolving;

        Task.Run(() =>
        {
            IsInitializing = true;

            //DatabaseAssist.Miggrate();

            InitializeMesage = "数据库自检中";
            ///数据库自检等操作
            DatabaseAssist.SystemValidation();

            DataSelfTest();

            InitializeMesage = "初始化任务";
            InitMission();
            WeakReferenceMessenger.Default.Send(new MainMenuEnableMessage("Task", true));


            InitializeMesage = "每日数据更新中";
            OnNewDate();

            // 数据验证
            InitializeMesage = "数据检验中，请耐心等待";
            VerifyRules.InitAll();

            // 加载托管消息
            LoadTrusteeMessages();

            InitializeMesage = null;


            //启动api
            TrusteeGallay.Initialize();

            // 加载电签组件
            InitializeSignings();

            // 加载信批组件
            InitializeDisclosureChannels();


            // 加载托管组件
            InitializeTrustees();

            // 加载触发器
            InitializeTriggers();

            IsInitializing = false;
        });


        // 每小时运行一次，判断是不是新的一天
        _dailyTimer = new Timer(x => OnNewDate(), null, 60000, 1000 * 60 * 60);

        Tools = [new("DatabaseViewer", ""), new Tool("FeeCalc", "费用计算器"), new("LearnAssist", "学习助手"), new("TemplateManager", "模板管理器")];

        //Tools = [new Tool { ExeName = "FeeCalc", Icon = GetGeometry("f.sack-dollar"), Foreground = Brushes.MediumPurple },
        //         new Tool { ExeName = "LearnAssist", Icon = GetGeometry("f.youtube"), Foreground = Brushes.Red },
        //         new Tool { ExeName = "TemplateManager", Icon = GetGeometry("f.table-columns"), Foreground = new SolidColorBrush(Color.FromRgb(42,145,223)) },
        //        ];

    }

    private Assembly? Default_Resolving(AssemblyLoadContext ctx, AssemblyName name)
    {
        var file = name.Name + ".dll";
        var path = Directory.EnumerateFiles(AppContext.BaseDirectory, file, SearchOption.AllDirectories).FirstOrDefault();
        return string.IsNullOrWhiteSpace(path) ? null : ctx.LoadFromAssemblyPath(path);
    }

    private void InitMission()
    {
        // 1. 获取主程序目录 +   子文件夹
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string disDir = Path.Combine(baseDir, "mission");


        if (!Directory.Exists(disDir))
        {
            LogEx.Warning("mission 目录不存在，退出加载");

            return;
        }

        // 2. 获取所有 dll 文件
        string[] dllFiles = Directory.GetFiles(disDir, "*.dll", SearchOption.TopDirectoryOnly);

        foreach (var dllPath in dllFiles)
        {
            try
            {
                if (!VerifyDll(dllPath)) continue;

                // 3. 加载程序集
                Assembly assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(dllPath);
                foreach (Type type in assembly.GetTypes())
                {
                    if (type.IsClass && type.IsAbstract && type.IsSealed)
                    {
                        var method = type.GetMethod("RegisterMissionTemplate", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
                        if (method is not null) method.Invoke(null, null);
                    }
                }
            }
            catch (Exception ex)
            {
                LogEx.Error($"加载mission组件失败，错误：{ex.Message}");
            }
        }


        MissionSchedule.Init();
    }

    #region 组件
    private static void InitializeSignings()
    {
        SigningGalley.Initialize();


        // 1. 获取主程序目录 + esign 子文件夹
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string esignDir = Path.Combine(baseDir, "esign");


        if (!Directory.Exists(esignDir))
        {
            LogEx.Warning("esign 目录不存在，退出加载");
            return;
        }

        // 2. 获取所有 dll 文件
        string[] dllFiles = Directory.GetFiles(esignDir, "*.dll", SearchOption.TopDirectoryOnly);

        foreach (var dllPath in dllFiles)
        {
            try
            {
                if (!VerifyDll(dllPath)) continue;

                // 3. 加载程序集
                Assembly assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(dllPath);

                // 4. 遍历程序集中的所有公共类
                foreach (Type type in assembly.GetExportedTypes())
                {
                    LoadESign(type);
                    LoadDisclosure(type);
                }
            }
            catch (Exception ex)
            {
                LogEx.Error($"注册电子签名组件失败，错误：{ex.Message}");
            }
        }

    }

    private static bool LoadESign(Type type)
    {
        try
        {
            // 5. 过滤：必须是类 + 实现 ISigning + 标记 [ES]
            if (!type.IsClass || type.IsAbstract || !typeof(ISigning).IsAssignableFrom(type))
                return false;

            // 6. 获取 ES 特性
            var esAttr = type.GetCustomAttribute<ESignDefineAttribute>();
            if (esAttr == null) return false;

            // 7. 拿到泛型参数 
            Type vmType = esAttr.ViewModelType;
            if (vmType.BaseType == null || !vmType.IsSubclassOf(typeof(ESignViewModelBase)))
                return false;


            // 8. 创建实例
            var assistInstance = Activator.CreateInstance(type) as ISigning;
            var vmInstance = Activator.CreateInstance(vmType) as ESignViewModelBase;

            // 9. 自动注册
            SigningGalley.Register(assistInstance!, vmInstance!);

        }
        catch (Exception ex)
        {
            LogEx.Error($"注册电子签名组件失败：{type.Name}，错误：{ex.Message}");
        }

        return true;
    }

    private static void InitializeTrustees()
    {

        // 1. 获取主程序目录 + trustee 子文件夹
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string trusteeDir = Path.Combine(baseDir, "trustee");


        if (!Directory.Exists(trusteeDir))
        {
            LogEx.Warning("trustee 目录不存在，退出加载");
            WeakReferenceMessenger.Default.Send(new MainMenuEnableMessage("Trustee", true));
            return;
        }

        // 2. 获取所有 dll 文件
        string[] dllFiles = Directory.GetFiles(trusteeDir, "*.dll", SearchOption.TopDirectoryOnly);

        foreach (var dllPath in dllFiles)
        {
            try
            {
                if (!VerifyDll(dllPath)) continue;

                // 3. 加载程序集
                Assembly assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(dllPath);

                // 4. 遍历程序集中的所有公共类
                foreach (Type type in assembly.GetExportedTypes())
                {
                    LoadTrustee(type);
                }
            }
            catch (Exception ex)
            {
                LogEx.Error($"注册电子签名组件失败，错误：{ex.Message}");
            }
        }


        WeakReferenceMessenger.Default.Send(new MainMenuEnableMessage("Trustee", true));
    }

    private static bool LoadTrustee(Type type)
    {
        try
        {
            // 5. 过滤：必须是类 + 实现 ISigning + 标记 [ES]
            if (!type.IsClass || type.IsAbstract || !typeof(ITrustee).IsAssignableFrom(type))
                return false;

            // 6. 获取 ES 特性
            var esAttr = type.GetCustomAttribute<TrusteeDefineAttribute>();
            if (esAttr == null) return false;

            // 7. 拿到泛型参数 
            Type vmType = esAttr.ViewModelType;
            if (vmType.BaseType == null || !vmType.IsSubclassOf(typeof(TrusteeViewModelBase)))
                return false;


            // 8. 创建实例
            var assistInstance = Activator.CreateInstance(type) as ITrustee;
            var vmInstance = Activator.CreateInstance(vmType) as TrusteeViewModelBase;

            // 9. 自动注册
            TrusteeGallay.Register(assistInstance!, vmInstance!);

        }
        catch (Exception ex)
        {
            LogEx.Error($"注册电子签名组件失败：{type.Name}，错误：{ex.Message}");
        }

        return true;
    }

    private static void InitializeDisclosureChannels()
    {
        DisclosureChannelManager.Initialize();

        // 1. 获取主程序目录 + disclosure 子文件夹
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string disDir = Path.Combine(baseDir, "disclosure");


        if (!Directory.Exists(disDir))
        {
            LogEx.Warning("disclosure 目录不存在，退出加载");
            WeakReferenceMessenger.Default.Send(new MainMenuEnableMessage("Disclosure", true));

            return;
        }

        // 2. 获取所有 dll 文件
        string[] dllFiles = Directory.GetFiles(disDir, "*.dll", SearchOption.TopDirectoryOnly);

        foreach (var dllPath in dllFiles)
        {
            try
            {
                if (!VerifyDll(dllPath)) continue;

                // 3. 加载程序集
                Assembly assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(dllPath);

                // 4. 遍历程序集中的所有公共类
                foreach (Type type in assembly.GetExportedTypes())
                {
                    LoadDisclosure(type);
                }
            }
            catch (Exception ex)
            {
                LogEx.Error($"注册信批组件失败，错误：{ex.Message}");
            }
        }


        WeakReferenceMessenger.Default.Send(new MainMenuEnableMessage("Disclosure", true));
    }

    private static bool LoadDisclosure(Type type)
    {
        try
        {
            // 5. 过滤：必须是类 + 实现 IDisclosureChannel + 标记 [ES]
            if (!type.IsClass || type.IsAbstract || !typeof(IDisclosureChannel).IsAssignableFrom(type))
                return false;

            // 6. 获取 ES 特性
            var esAttr = type.GetCustomAttribute<DisclosureDefineAttribute>();
            if (esAttr == null) return false;

            // 7. 拿到泛型参数 
            Type cfType = esAttr.ConfigType;
            Type vmType = esAttr.ViewModelType;

            if (cfType.BaseType == null || !cfType.IsSubclassOf(typeof(DisclosureChannelConfig)))
                return false;
            if (vmType.BaseType == null || !vmType.IsSubclassOf(typeof(ChannelConfigViewModel)))
                return false;


            // 8. 创建实例
            var assistInstance = Activator.CreateInstance(type) as IDisclosureChannel;

            // 9. 自动注册
            DisclosureChannelManager.Register(assistInstance!, () => Activator.CreateInstance(vmType) as ChannelConfigViewModel);
        }
        catch (Exception ex)
        {
            LogEx.Error($"注册信批组件失败：{type.Name}，错误：{ex.Message}");
        }

        return true;
    }


    private static void InitializeTriggers()
    {
        // 1. 获取主程序目录 +   子文件夹
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string disDir = Path.Combine(baseDir, "trigger");


        if (!Directory.Exists(disDir))
        {
            LogEx.Warning("trigger 目录不存在，退出加载");

            return;
        }

        // 2. 获取所有 dll 文件
        string[] dllFiles = Directory.GetFiles(disDir, "*.dll", SearchOption.TopDirectoryOnly);

        foreach (var dllPath in dllFiles)
        {
            try
            {
                if (!VerifyDll(dllPath)) continue;

                // 3. 加载程序集
                Assembly assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(dllPath);
                foreach (Type type in assembly.GetTypes())
                {
                    if (type.IsClass && type.IsAbstract && type.IsSealed)
                    {
                        var method = type.GetMethod("InitHooks", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
                        if (method is not null) method.Invoke(null, null);
                    }
                }
            }
            catch (Exception ex)
            {
                LogEx.Error($"加载Trigger组件失败，错误：{ex.Message}");
            }
        }


        //WeakReferenceMessenger.Default.Send(new MainMenuEnableMessage("Disclosure", true));
    }
    #endregion

    private void LoadTrusteeMessages()
    {
        using var db = DbHelper.Base();
        var data = db.GetCollection<TrusteeWorker.WorkReturn>(TrusteeWorker.TableRaisingBalance).FindAll().ToArray();
        if (data.Length > 0) WeakReferenceMessenger.Default.Send(new TrusteeWorkResult(nameof(ITrustee.QueryRaisingBalance), data));


    }



    /// <summary>
    /// 数据自检
    /// </summary>
    public void DataSelfTest()
    {
        IsSelfTesting = true;
        var db = DbHelper.Base();
        var c = db.GetCollection<Fund>().FindAll().ToArray();
        db.Dispose();


        Task[] t = [Task.Run(async() => await SyncFundsFromAmac(c) ),
                    Task.Run(() => DataTracker.CheckFundFolder(c)),
                    Task.Run(()=> DataTracker.CheckTAMissOwner()),
        ];

        Task.WaitAll(t);
        IsSelfTesting = false;
    }

    private static async Task SyncFundsFromAmac(Fund[] c)
    {
        // 从未同步过的、正在清算的、备案的
        var ned = c.Where(x => x.PublicDisclosureSynchronizeTime == default || x.Status switch { FundStatus.StartLiquidation or FundStatus.Registration => true, _ => false }).ToArray();
        if (ned.Length > 0)
        {
            try
            {
                var db = DbHelper.Base();
                HandyControl.Controls.Growl.Warning($"发现{ned.Length}个基金待同步公示信息");

                using HttpClient client = new HttpClient();
                foreach (var (i, set) in ned.Chunk(50).Index())
                {
                    foreach (var (j, f) in set.Index())
                    {
                        var cleared = f.Status > FundStatus.StartLiquidation;

                        await AmacAssist.SyncFundInfoAsync(f, client);
                        DataTracker.CheckFundFolder([f]);

                        f.PublicDisclosureSynchronizeTime = DateTime.Now;
                        db.GetCollection<Fund>().Update(f);
                        WeakReferenceMessenger.Default.Send(f);

                        //
                        if (!cleared && f.Status > FundStatus.StartLiquidation)
                            DataTracker.OnFundCleared(f);

                        await Task.Delay(200);
                    }

                    int finished = i * 50 + set.Length;
                    if (finished < ned.Length)
                        HandyControl.Controls.Growl.Success($"已同步{finished}个基金，剩余{ned.Length - finished}个");
                }
                db.Dispose();
                HandyControl.Controls.Growl.Success($"基金同步公示信息完成");
            }
            catch (Exception ex)
            {
                Log.Error($"同步公示信息失败：{ex}");
                HandyControl.Controls.Growl.Error($"同步基金公示信息失败");
            }
        }
    }

    public void InitPlot()
    {
        using var db = DbHelper.Base();
        var status = db.GetCollection<Fund>().FindAll().Select(x => x.Status).ToList();

        PlotManageScale(db);

        PlotBuySellIn7Days(db);
    }

    private void PlotManageScale2(BaseDatabase db)
    {
        // 计算管理规模
        var fids = db.GetCollection<Fund>().FindAll().Select(x => x.Id).ToList();
        if (fids.Count > 0)
        {
            var sd = fids.Select(x => db.GetDailyCollection(x).FindAll().Where(x => x is not null).OrderBy(x => x.Date).ToList()).Where(x => x.Count > 0);
            if (!sd.Any()) return;
            var mindate = sd.Select(x => x[0].Date).Min(); var maxdate = sd.Select(x => x[^1].Date).Max();
            var tmpdate = mindate;
            var dates = new List<DateOnly>();
            dates.Add(mindate);
            while (tmpdate < maxdate)
            {
                tmpdate = tmpdate.AddDays(1);
                dates.Add(tmpdate);
            }

            var scale = new double[dates.Count];

            Parallel.ForEach(sd, f =>
            {
                // 对齐第一个
                var first = dates.IndexOf(f[0].Date);
                for (int i = 0, j = first; i < f.Count; i++)
                {
                    for (; j < dates.Count; j++)
                    {
                        if (f[i].Date == dates[j])
                        {
                            scale[j++] += (double)f[i].NetAsset;
                            break;
                        }
                    }

                }
            });

            // 计算月内最大规模
            var lastmon = dates[0];
            List<DateOnly> mons = [dates[0]];
            List<double> sc = [scale[0]];
            for (int i = 1; i < dates.Count; i++)
            {
                if (scale[i] <= 0) continue;

                if ((dates[i].Year > mons[^1].Year || dates[i].Month > mons[^1].Month))
                {
                    mons.Add(dates[i]);
                    sc.Add(scale[i]);
                }
                else if (scale[i] > sc[^1])
                {
                    mons[^1] = dates[i];
                    sc[^1] = scale[i];
                }

            }
            if (sc.Count > 1 && sc[^1] < sc[^2] / 2)
            {
                mons.RemoveAt(mons.Count - 1);
                sc.RemoveAt(sc.Count - 1);
            }

            App.Current.Dispatcher.BeginInvoke(() =>
            {

                ManageScaleContext = new PlotModel
                {
                    Title = "管理规模",
                    PlotType = PlotType.XY,
                    PlotAreaBorderThickness = new OxyThickness(1, 0, 0, 0)
                };
                // 添加坐标轴（关键步骤！）
                ManageScaleContext.Axes.Add(new DateTimeAxis  // X轴用日期轴
                {
                    Position = OxyPlot.Axes.AxisPosition.Bottom,
                    StringFormat = "yyyy-MM" // 日期格式
                });
                ManageScaleContext.Axes.Add(new LinearAxis  // Y轴
                {
                    Position = OxyPlot.Axes.AxisPosition.Left,
                    MaximumPadding = 0.1
                });

                ManageScaleContext.Series.Add(new OxyPlot.Series.AreaSeries
                {
                    ItemsSource = sc.Index().Select(x => new { Date = new DateTime(mons[x.Index], default), Value = x.Item / 10000 }).ToList(),
                    DataFieldX = "Date",          // 绑定到匿名类型的Date属性
                    DataFieldY = "Value",         // 绑定到Value属性
                    MarkerType = MarkerType.None,
                    LineStyle = LineStyle.Solid,
                    Color = OxyColors.RoyalBlue,
                    MarkerFill = OxyColors.RoyalBlue,
                    InterpolationAlgorithm = InterpolationAlgorithms.CanonicalSpline,
                    TrackerFormatString = "{2:yyyy-MM}         {4:N0} 万元",
                    StrokeThickness = 2,
                    TrackerKey = "CustomTracker"

                });

                // 自动调整坐标轴范围（关键步骤！）
                ManageScaleContext.InvalidatePlot(true);
            });


        }

    }

    private void PlotManageScale(BaseDatabase db)
    {
        var startdate = db.GetCollection<Fund>().Query().Where(x => x.SetupDate.Year > 1970).Select(x => x.SetupDate).ToList().Min();
        var scaledates = db.GetCollection<DailyManageSacle>().Query().Select(x => x.Date).ToList();
        var tdays = Days.TradingDaysFrom(startdate);

        // 生成缺失的日期
        var mis = tdays.Except(scaledates).ToList();
        DataTracker.UpdateManageSacle(mis);

        var scales = db.GetCollection<DailyManageSacle>().FindAll().ToList();
        var sc = scales.OrderBy(x => x.Date).GroupBy(x => new { x.Date.Year, x.Date.Month }).Select(x => new { Date = new DateTime(x.Key.Year, x.Key.Month, 1), Value = x.Max(y => y.Scale) / 10000 }).ToList();

        App.Current.Dispatcher.BeginInvoke(() =>
        {

            ManageScaleContext = new PlotModel
            {
                Title = "管理规模",
                PlotType = PlotType.XY,
                PlotAreaBorderThickness = new OxyThickness(1, 0, 0, 0)
            };
            // 添加坐标轴（关键步骤！）
            ManageScaleContext.Axes.Add(new DateTimeAxis  // X轴用日期轴
            {
                Position = OxyPlot.Axes.AxisPosition.Bottom,
                StringFormat = "yyyy-MM" // 日期格式
            });
            ManageScaleContext.Axes.Add(new LinearAxis  // Y轴
            {
                Position = OxyPlot.Axes.AxisPosition.Left,
                MaximumPadding = 0.1
            });

            ManageScaleContext.Series.Add(new OxyPlot.Series.AreaSeries
            {
                ItemsSource = sc,//sc.Index().Select(x => new { Date = new DateTime(mons[x.Index], default), Value = x.Item / 10000 }).ToList(),
                DataFieldX = "Date",          // 绑定到匿名类型的Date属性
                DataFieldY = "Value",         // 绑定到Value属性
                MarkerType = MarkerType.None,
                LineStyle = LineStyle.Solid,
                Color = OxyColors.RoyalBlue,
                MarkerFill = OxyColors.RoyalBlue,
                InterpolationAlgorithm = InterpolationAlgorithms.CanonicalSpline,
                TrackerFormatString = "{2:yyyy-MM}         {4:N0} 万元",
                StrokeThickness = 2,
                TrackerKey = "CustomTracker"

            });

            // 自动调整坐标轴范围（关键步骤！）
            ManageScaleContext.InvalidatePlot(true);
        });

    }

    private void PlotFundType(BaseDatabase db)
    {

    }


    private void PlotBuySellIn7Days(BaseDatabase db)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var data = db.GetCollection<TransferRequest>().Find(x => today.DayNumber - x.RequestDate.DayNumber < 7);


        var model = new PlotModel
        {

            PlotAreaBorderThickness = new OxyThickness(0) // 饼图通常不需要边框
        };

        // 创建 PieSeries
        var pieSeries = new PieSeries
        {
            StrokeThickness = 2.0,
            InsideLabelPosition = 0.8,
            AngleSpan = 360,
            StartAngle = 0
        };
        pieSeries.Slices.Add(new PieSlice("买入", 100) { Fill = OxyColors.Green, IsExploded = true });
        pieSeries.Slices.Add(new PieSlice("卖出", 50) { Fill = OxyColors.Red });
        ;

        model.Series.Add(pieSeries);

        BuySellIn7DaysContext = model;
        // 自动调整坐标轴范围（关键步骤！）
        BuySellIn7DaysContext.InvalidatePlot(true);
    }



    private static bool VerifyDll(string dll)
    {
#if DEBUG
       // return true;
//#else
        return SecurityHelper.IsAuthorSigned(dll);
//return AssemblyName.GetAssemblyName(dll).GetPublicKeyToken().SequenceEqual(new byte[] { 0xA9, 0x4A, 0x3A, 0xC4, 0x0B, 0x3F, 0xC1, 0xBE });
#endif
    }




    [RelayCommand]
    public void OpenDbViewer()
    {
        try
        {
            var di = new DirectoryInfo(System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName).Parent!;

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = Path.Combine(di.FullName, "DatabaseViewer.exe"), WorkingDirectory = Directory.GetCurrentDirectory() });
        }
        catch (Exception e)
        {

            HandyControl.Controls.Growl.Warning($"无法启动数据视图，{e.Message}");
        }
    }


    [RelayCommand]
    public void OpenDataFolder()
    {
        try { System.Diagnostics.Process.Start("explorer.exe", Path.Combine(Directory.GetCurrentDirectory(), "files")); } catch { }
    }


    [RelayCommand]
    public void OpenExe(string exeName)
    {
        try
        {
            var di = new DirectoryInfo(System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName).Parent!;

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = Path.Combine(di.FullName, $"{exeName}.exe"), WorkingDirectory = Directory.GetCurrentDirectory() });
        }
        catch (Exception e)
        {
            HandyControl.Controls.Growl.Warning($"无法启动应用，{e.Message}");
        }
    }


    [RelayCommand]
    public async Task Backup()
    {
        if (_backupToken.IsCancellationRequested)
        {
            _backupToken.Dispose();
            _backupToken = new();
        }
        await Task.Factory.StartNew(() =>
          {
              try
              {
                  // 检测备份大小
                  double bytes = GetFolderSize("data") + GetFolderSize("files") + GetFolderSize("config") + GetFolderSize("manager");
                  int fcnt = GetFileCount("data") + GetFileCount("config") + GetFileCount("files") + GetFileCount("manager");

                  // 选择保存位置
                  var fd = new OpenFolderDialog();
                  fd.Title = $"请选择全量备份存在文件夹，大约{bytes switch { > 1073741824 => $"{bytes / 1073741824:N2}G", > 1048576 => $"{bytes / 1048576:N2}G", _ => $"{bytes / 1024:N2}G" }}";
                  if (fd.ShowDialog() is not true)
                      return;

                  string path = Path.Combine(fd.FolderName, $"backup-{DateTime.Today:yyyy.MM.dd}.zip");
                  using var fs = new FileStream(path, FileMode.Create);
                  try
                  {
                      BackupProcess = 0;
                      int c = 0;

                      using ZipArchive archive = new ZipArchive(fs, ZipArchiveMode.Create);
                      AddDirectoryToZip(_backupToken.Token, archive, "data", "data", ref c, fcnt);
                      AddDirectoryToZip(_backupToken.Token, archive, "config", "config", ref c, fcnt);
                      AddDirectoryToZip(_backupToken.Token, archive, "files", "files", ref c, fcnt);
                      AddDirectoryToZip(_backupToken.Token, archive, "manager", "manager", ref c, fcnt);
                  }
                  catch (OperationCanceledException)
                  {
                      fs.Close();
                      File.Delete(path);
                      BackupProcess = 0;
                      HandyControl.Controls.Growl.Info("备份取消"); 
                  }
              }
              catch (Exception e)
              {
                  Log.Error($"备份失败 {e}");
                  HandyControl.Controls.Growl.Error("备份失败");
              }
          }, TaskCreationOptions.LongRunning);
    }

    [RelayCommand]
    public void StopBackup()
    {
        _backupToken.Cancel();
    }


    private long GetFolderSize(string dir)
    {
        var di = new DirectoryInfo(dir);
        return !di.Exists ? 0 : di.GetFiles("*.*", SearchOption.AllDirectories).Sum(x => x.Length);
    }

    private int GetFileCount(string dir) => Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories).Length;

    // 将文件夹及其内容添加到压缩包
    private void AddDirectoryToZip(CancellationToken backupToken, ZipArchive archive, string sourceDir, string entryBase, ref int c, int size)
    {
        // 添加文件夹条目
        archive.CreateEntry($"{entryBase}/");

        // 添加文件
        foreach (string filePath in Directory.GetFiles(sourceDir))
        {
            backupToken.ThrowIfCancellationRequested();
            string entryName = Path.Combine(entryBase, Path.GetFileName(filePath));
            archive.CreateEntryFromFile(filePath, entryName);

            BackupProcess = (double)(++c) / size * 100;
            BackupProcess2 = 100 * (BackupProcess - Math.Floor(BackupProcess));
        }

        // 递归添加子文件夹
        foreach (string subDir in Directory.GetDirectories(sourceDir))
        {
            backupToken.ThrowIfCancellationRequested();
            string subDirName = Path.GetFileName(subDir);
            string newEntryBase = Path.Combine(entryBase, subDirName);
            AddDirectoryToZip(backupToken, archive, subDir, newEntryBase, ref c, size);
        }
    }

    [RelayCommand]
    public void RefreshPlot() => InitPlot();

    public void Receive(FundTipMessage message)
    {
        App.Current.Dispatcher.BeginInvoke(() =>
          {
              FundTips = DataTracker.FundTips.Where(x => x.Tip is not null).Select(x => $"{x.FundName}  {x.Tip}").ToArray();
          });

    }

    public void Receive(TrusteeWorkResult message)
    {
        switch (message.Method)
        {
            case nameof(ITrustee.QueryRaisingBalance):
                OnHandleRaisingBalance(message.Returns);
                break;
            default:
                break;
        }

    }

    private void OnHandleRaisingBalance(IList<TrusteeWorker.WorkReturn> returns)
    {
        //
        using var db = DbHelper.Base();
        var funds = db.GetCollection<Fund>().FindAll().Where(x => x.Status >= FundStatus.ContractFinalized && x.Status <= FundStatus.StartLiquidation).ToArray();

        List<(string fc, ReturnCode rc, decimal v)> list = new();

        foreach (var r in returns)
        {
            // 失败
            if (r.Code != ReturnCode.Success)
            {
                // 获取对应的产品名



                continue;
            }

            // 成功 r.Data 可能是object[]
            if (r.Data is System.Collections.IEnumerable en && en.OfType<FundBankBalance>() is IEnumerable<FundBankBalance> bankBalances)
            {
                foreach (var b in bankBalances)
                {
                    list.Add((b.FundName!, r.Code, b.Balance));
                }
            }
        }

        // 没有记录的产品
        var failed = funds.Where(x => x.Status >= FundStatus.ContractFinalized && x.Status <= FundStatus.StartLiquidation).ExceptBy(list.Select(x => x.fc), x => x.Name).ToArray();

        RaisingAccountTip.HasFailed = failed.Length > 0;
        RaisingAccountTip.FailedFunds = failed.Select(x => x.Name).ToArray();
        RaisingAccountTip.TotalBalance = list.Sum(x => x.v);
        RaisingAccountTip.BalanceDetail = list.Where(x => x.v > 0).ToDictionary(x => x.fc, x => x.v);
    }



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

        return resourceDictionary["f.ghost"] as Geometry;
    }


    private void OnNewDate()
    {
        if (DailyUpdateTime.Date == DateTime.Today) return;

        try
        {
            var today = DateOnly.FromDateTime(DateTime.Now);

            // 新的一天
            DataTracker.OnNewDay(new NewDay(today));

            WeakReferenceMessenger.Default.Send(new NewDay(today));

            // 更新规模图
            InitPlot();


            DailyCheckRequestIsWell();






            WeakReferenceMessenger.Default.Send(new ToastMessage(LogLevel.Info, "更新每日数据完成"));
        }
        catch (Exception ex)
        {
            Log.Error($"HomePage, OnNewDate {ex}");
            WeakReferenceMessenger.Default.Send(new ToastMessage(LogLevel.Info, "更新每日数据失败"));
        }

        BackupDb();

        DailyUpdateTime = DateTime.Now;
    }

    private static void BackupDb()
    {
        try
        {
            Directory.CreateDirectory("backup");
            var ca = CultureInfo.CurrentCulture.Calendar;
            var path = @$"backup\bakdb_{DateTime.Now:yy}{ca.GetWeekOfYear(DateTime.Now, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday):D2}.zip";

            if (!File.Exists(path))
            {
                using ZipArchive zip = new ZipArchive(File.Create(path), ZipArchiveMode.Create);
                foreach (var file in Directory.GetFiles("data", "*.db"))
                {
                    if (file.Contains("log")) continue;

                    var entryName = Path.GetRelativePath("data", file);
                    zip.CreateEntryFromFile(file, entryName);
                }
            }
        }
        catch (Exception ex)
        {
            LogEx.Error(ex);
        }
    }

    public partial class RaisingAccountWarning : ObservableObject
    {
        [ObservableProperty]
        public partial string[]? FailedFunds { get; set; }

        [ObservableProperty]
        public partial bool HasFailed { get; set; }

        [ObservableProperty]
        public partial decimal? TotalBalance { get; set; }

        [ObservableProperty]
        public partial bool HasBalance { get; set; }

        [ObservableProperty]
        public partial Dictionary<string, decimal>? BalanceDetail { get; set; }



        [RelayCommand]
        public async Task Update()
        {
            /// 在WeakReferenceMessenger接收消息
            await TrusteeGallay.Worker.QueryRaisingBalanceOnce();
        }

    }


    public class Tool
    {
        [SetsRequiredMembers]
        public Tool(string exe, string? toolTip)
        {
            ExeName = exe;
            ToolTip = toolTip;
            var di = new FileInfo(Path.Combine(System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName, @$"..\{exe}.exe"));
            if (di.Exists)
                Icon = ExtractIcon(di.FullName);

        }


        public required string ExeName { get; set; }

        public ImageSource? Icon { get; set; }

        public string? ToolTip { get; set; }


    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern uint PrivateExtractIcons(
            string lpszFile, int nIconIndex, int cxIcon, int cyIcon,
            IntPtr[] phicon, uint[] piconid, uint nIcons, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    /// <summary>
    /// 从 EXE/DLL 文件中提取指定尺寸的图标，并转为 WPF ImageSource
    /// </summary>
    /// <param name="filePath">目标文件路径</param>
    /// <param name="size">期望提取的宽高（如 32, 48, 256）</param>
    public static BitmapSource? ExtractIcon(string filePath, int size = 32)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            throw new FileNotFoundException("找不到指定文件", filePath);

        IntPtr[] hIcons = new IntPtr[1];
        uint[] pIconIds = new uint[1];

        // 请求提取 1 个指定尺寸的图标
        uint extractedCount = PrivateExtractIcons(filePath, 0, size, size, hIcons, pIconIds, 1, 0);

        if (extractedCount == 0 || hIcons[0] == IntPtr.Zero)
            return null; // 文件无图标资源或提取失败

        try
        {
            // WPF 会深拷贝像素数据，之后可安全释放原生句柄
            return Imaging.CreateBitmapSourceFromHIcon(
                hIcons[0],
                new Int32Rect(0, 0, size, size),
                BitmapSizeOptions.FromEmptyOptions());
        }
        finally
        {
            // ⚠️ 必须释放 Win32 HICON，否则句柄泄漏
            DestroyIcon(hIcons[0]);
        }
    }
}
