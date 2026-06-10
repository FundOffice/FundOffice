using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia;

namespace Launcher
{
    internal sealed class Program
    {
        public static bool AutoInstall { get; private set; }

        [STAThread]
        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Dynamic assembly loading from app\\ directory is intentional.")]
        public static void Main(string[] args)
        {
            AutoInstall = args.Contains("--install", StringComparer.OrdinalIgnoreCase);

            // Launcher.exe 在根目录，所有 DLL 在 app\ 子目录
            // 将 app\ 加入搜索路径，让运行时能找到原生库和托管程序集
            var appDir = Path.Combine(AppContext.BaseDirectory, "app");
            if (Directory.Exists(appDir))
            {
                // 原生 DLL 搜索路径（P/Invoke 用）
                var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
                Environment.SetEnvironmentVariable("PATH", $"{appDir};{pathEnv}");

                // 托管程序集解析（反射/动态加载用）
                System.Runtime.Loader.AssemblyLoadContext.Default.Resolving += (ctx, name) =>
                {
                    var dllPath = Path.Combine(appDir, name.Name + ".dll");
                    return File.Exists(dllPath) ? ctx.LoadFromAssemblyPath(dllPath) : null;
                };
            }

            // 全局异常捕获，避免静默闪退
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                ShowFatalError("未处理异常", e.ExceptionObject as Exception);

            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                ShowFatalError("异步任务异常", e.Exception);
                e.SetObserved();
            };

            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }

        private static void ShowFatalError(string title, Exception? ex)
        {
            var msg = ex?.Message ?? "未知错误";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Win32 MessageBox，AOT 下可用
                _ = MessageBox(new IntPtr(0), $"{title}\n\n{msg}", "Thor Launcher 错误", 0x10);
            }
            else
            {
                Console.Error.WriteLine($"[{title}] {msg}");
            }
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}
