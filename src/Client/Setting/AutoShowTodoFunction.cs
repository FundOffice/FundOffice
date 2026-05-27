using CommunityToolkit.Mvvm.Messaging;
using FMO.Models;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace FMO.Settings;

/// <summary>
/// 这是一个闲时自动显示待办事项的功能。当用户长时间没有操作时，会自动弹出待办事项窗口，提醒用户查看待办事项。
/// 关联到Setting Basic中
/// 在DelayLoader中会自动注册这个功能 后续迁移到其它类
///  SettingService.RegisterAbility( "Basic", "AutoShowTodo", "自动显示待办事项", "在应用启动时自动显示待办事项", true, new AutoShowTodoFunction());
/// </summary>
internal class AutoShowTodoFunction : ISettingFunction
{
    private long _lastTime;

    private CancellationTokenSource? _cancel;

    private nint mainHwnd;

    public void Dispose()
    {

    }

    public void Init()
    {
        // Initialization logic for AutoShowTodoFunction
       
       App.Current.Dispatcher.Invoke(() => mainHwnd = new WindowInteropHelper(App.Current.MainWindow).Handle);
    }

    private void MainWindow_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _lastTime = Stopwatch.GetTimestamp();
    }

    public void Start()
    {
        _cancel = new();
        App.Current.Dispatcher.Invoke(() => App.Current.MainWindow.PreviewMouseMove += MainWindow_PreviewMouseMove);
        _ = Task.Run(async () =>
        {
            PeriodicTimer timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            while (await timer.WaitForNextTickAsync())
            {
                _cancel?.Token.ThrowIfCancellationRequested();

                if (Stopwatch.GetTimestamp() - _lastTime > 60 * Stopwatch.Frequency)
                {
                    _lastTime = Stopwatch.GetTimestamp();
               
                    var foregroundHwnd = GetForegroundWindow();
                    if (foregroundHwnd == mainHwnd)
                        WeakReferenceMessenger.Default.Send(new ShowTodoMessage());
                }
            }
        });

    }

    public void Stop()
    {
        // Stop logic for AutoShowTodoFunction
        App.Current.MainWindow.PreviewMouseMove -= MainWindow_PreviewMouseMove;
        _cancel?.Cancel();
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

   
}

public class ShowTodoMessage;