using FMO.Logging;
using FMO.Models;
using FMO.Plugin;
using FMO.Utilities;
using Microsoft.Win32;
using Serilog;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Windows;

namespace FMO;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private bool _firstRun = false;

#if RELEASE
    Mutex mutex;
#endif

    public App()
    {
#if RELEASE
        // 单例模式
        string mutexName = "FundMiddleOfficeSingleton";

        // 尝试创建一个Mutex
        bool createdNew = false;
        mutex = new Mutex(false, mutexName, out createdNew);

        // 如果Mutex已经存在，说明程序已经在运行
        if (!createdNew)
            this.Shutdown();
#endif

        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        Log.Logger = new LoggerConfiguration().WriteTo.LiteDB(@"logs.db", "logex").CreateLogger();
        LogEx.Information($"System Start {DateTime.Now}");

        if (CheckIsFirstRun())
        {
            _firstRun = true;
            StartupUri = new Uri("InitWindow.xaml", UriKind.Relative);
            return;
        }


    }



    protected override void OnStartup(StartupEventArgs e)
    {
        AssemblyLoadContext.Default.Resolving += Default_Resolving;

        ResourceModuleInitializer.Initialize();

        // 处理所有 AppDomain 的未处理异常（包括非 UI 线程）
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            var exception = (Exception)args.ExceptionObject;
            LogEx.Error($"{exception}");
        };

        // 处理 Task 内部未处理的异常
        TaskScheduler.UnobservedTaskException += (s, args) =>
        {
            LogEx.Error($"{s}");
            args.SetObserved(); // 避免后续崩溃
        };



        Directory.CreateDirectory("data");
        Directory.CreateDirectory("config");
        Directory.CreateDirectory("files\\funds");
        Directory.CreateDirectory("files\\evaluation");
        Directory.CreateDirectory("plugins");
        Directory.CreateDirectory("files\\tac");
        Directory.CreateDirectory("files\\accounts");
        Directory.CreateDirectory("files\\accounts\\security");
        Directory.CreateDirectory("files\\accounts\\stock");
        Directory.CreateDirectory("files\\accounts\\future");
        Directory.CreateDirectory("files\\accounts\\fund");
        Directory.CreateDirectory("files\\accounts\\other");

        if (_firstRun) return;

        StartupUri = new Uri("MainWindow.xaml", UriKind.Relative);

        DbHelper.Init();
        //加载插件
        PluginManager.Init();

        Task.Run(() => DelayLoader.Load());

    }


    private bool CheckIsFirstRun()
    {
#if RELEASE
        using (var key = Registry.CurrentUser.OpenSubKey(@$"Software\Nexus"))       
#else
        using (var key = Registry.CurrentUser.OpenSubKey(@$"Software\Nexus\Debug"))
#endif
        {
            if (key?.GetValue("WorkingFolder") is not string dir || !Directory.Exists(dir))
                return true;

            Directory.SetCurrentDirectory(dir);

            try
            {
                if (!File.Exists(@"data\base.db")) 
                    return true;

                DbHelper.Init();
                using var db = DbHelper.Base();
                var manager = db.GetCollection<Manager>().FindOne(x => x.IsMaster);

                return manager is null;
            }
            catch (Exception ex) { LogEx.Error(ex); return false; }
        }
    }


    protected override async void OnExit(ExitEventArgs e)
    {
        //await Automation.DisposeAsync();


        DirectoryInfo tmpd = new("temp");
        if (tmpd.Exists)
            foreach (var item in tmpd.GetDirectories())
                try { item.Delete(true); } catch { }

        base.OnExit(e);
    }

    private void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        LogEx.Error(e.Exception.Message);
#if !DEBUG
        e.Handled = true;
#endif
        //MessageBox.Show("出错了，请查看Log");
    }



    private static Assembly? Default_Resolving(AssemblyLoadContext ctx, AssemblyName name)
    {
        var file = name.Name + ".dll";
        var path = Directory.EnumerateFiles(AppContext.BaseDirectory, file, SearchOption.AllDirectories).FirstOrDefault();
        return string.IsNullOrWhiteSpace(path) ? null : ctx.LoadFromAssemblyPath(path);
    }
}
