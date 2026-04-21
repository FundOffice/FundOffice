using FMO.Logging;
using FMO.Models;
using FMO.Plugin;
using FMO.Utilities;
using Microsoft.Win32;
using Serilog;
using System.IO;
using System.Windows;

namespace FMO;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
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

#if RELEASE
        // 设置工作目录
        
        //if (!string.IsNullOrWhiteSpace(Config.Default.WorkFolder))
        //{
        //    var di = new DirectoryInfo(Config.Default.WorkFolder);
        //    if (di.Exists)
        //        Directory.SetCurrentDirectory(di.FullName);
        //}
      // 从注册表读取
        using (var key = Registry.CurrentUser.OpenSubKey(@$"Software\Nexus"))       
#else
        using (var key = Registry.CurrentUser.OpenSubKey(@$"Software\Nexus\Debug"))
#endif        
        {
            if (key != null)
            {
                var workFolder = key.GetValue("WorkingFolder") as string;
                if (!string.IsNullOrWhiteSpace(workFolder))
                {
                    var di = new DirectoryInfo(workFolder);
                    if (di.Exists)
                        Directory.SetCurrentDirectory(di.FullName);
                }
            }
        }

        //Task.Run(async () =>
        //{
        //    await Task.Delay(1000 * 10);

        //    DataTracker.OnDailyValue([new DailyValue { FundId = 5 }]);
        //});


        Log.Logger = new LoggerConfiguration().WriteTo.LiteDB(@"logs.db", "logex").CreateLogger();
        //Log.Logger = new LoggerConfiguration().WriteTo.LiteDB(@"logs.db", formatter:new MessageTemplateTextFormatter("{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {ClassName}.{MethodName}({LineNumber}) - {Message:lj}{NewLine}{Exception}")).CreateLogger();
        LogEx.Information($"System Start {DateTime.Now}");
        // 处理所有 AppDomain 的未处理异常（包括非 UI 线程）
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            var exception = (Exception)args.ExceptionObject;
            Log.Error($"{exception}");
        };

        // 处理 Task 内部未处理的异常
        TaskScheduler.UnobservedTaskException += (s, args) =>
        {
            Log.Error($"{s}");
            args.SetObserved(); // 避免后续崩溃
        };



        //DbHelper.initpassword();


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


        //Log.Logger = new LoggerConfiguration().WriteTo.File("log.txt", outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level}] ({SourceContext}.{Method}) {Message}{NewLine}{Exception}").CreateLogger();


        if (CheckIsFirstRun())
            StartupUri = new Uri("InitWindow.xaml", UriKind.Relative);
        else
            StartupUri = new Uri("MainWindow.xaml", UriKind.Relative);

        //加载插件
        PluginManager.Init();
    }


    private bool CheckIsFirstRun()
    {
        using var db = DbHelper.Base();
        var manager = db.GetCollection<Manager>().FindOne(x => x.IsMaster);

        return (manager is null);
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
        Log.Error(e.Exception.Message);
#if !DEBUG
        e.Handled = true;
#endif
        //MessageBox.Show("出错了，请查看Log");
    }

}
