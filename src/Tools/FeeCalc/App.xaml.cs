using FMO.Utilities;
using Microsoft.Win32;
using System.IO;
using System.Text;
using System.Windows;

namespace FMO.FeeCalc;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public App()
    {

        RegisterGlobalExceptionHandler();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
#if DEBUG
        using var key = Registry.CurrentUser.OpenSubKey(@$"Software\Nexus\Debug");
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
#endif

        DbHelper.Init(e.Args.FirstOrDefault());
    }

    #region 全局异常注册与处理
    private void RegisterGlobalExceptionHandler()
    {
        // UI线程异常
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        // 非UI线程异常
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        // Task异步未观测异常
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
    }

    /// <summary>
    /// UI线程未捕获异常
    /// </summary>
    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true; // 标记已处理，防止程序崩溃退出
        ShowExceptionMsgBox("UI线程发生未捕获异常", e.Exception);
    }

    /// <summary>
    /// 非UI线程异常（无法阻止进程崩溃，仅弹窗提示）
    /// </summary>
    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception;
        ShowExceptionMsgBox("非UI线程致命异常，程序即将退出", ex);
    }

    /// <summary>
    /// Task异步异常
    /// </summary>
    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved(); // 标记异常已观测，避免进程崩溃
        ShowExceptionMsgBox("异步Task发生未捕获异常", e.Exception);
    }

    /// <summary>
    /// 统一弹窗提示方法
    /// </summary>
    private void ShowExceptionMsgBox(string title, Exception? ex)
    {
        if (ex is null) return;
        string msg = $"【{title}】\r\n异常信息：{ex.Message}\r\n\r\n堆栈详情：{ex.StackTrace}";
        MessageBox.Show(msg, "程序异常", MessageBoxButton.OK, MessageBoxImage.Error);
    }
    #endregion

}
