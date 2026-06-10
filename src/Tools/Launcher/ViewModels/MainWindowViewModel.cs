using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Launcher.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private const string DesktopRuntimeName = "Microsoft.WindowsDesktop.App";
    private const string CoreRuntimeName = "Microsoft.NETCore.App";
    private readonly CancellationTokenSource _cts = new();
    private readonly bool _autoInstall;

    // --- 运行时状态 ---
    [ObservableProperty] private bool _runtimeMissing;
    [ObservableProperty] private string _runtimeMissingText = string.Empty;

    // --- 更新状态 ---
    [ObservableProperty] private bool _updateAvailable;
    [ObservableProperty] private string _updateText = string.Empty;

    // --- 进度 ---
    [ObservableProperty] private bool _isWorking;
    [ObservableProperty] private string _workingText = string.Empty;
    [ObservableProperty] private string _progressDetail = string.Empty;
    [ObservableProperty] private int _progress;

    // --- 整体状态 ---
    [ObservableProperty] private bool _allReady;
    [ObservableProperty] private string _statusHint = "正在检查...";

    private string? _requiredVersion;

    /// <summary>
    /// 设计器用无参构造函数。
    /// </summary>
    public MainWindowViewModel() : this(false) { }

    public MainWindowViewModel(bool autoInstall)
    {
        _autoInstall = autoInstall;
        // 捕获 RunAsync 的所有异常，显示到 StatusHint，不闪退
        _ = RunAsync().ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                var msg = t.Exception?.InnerException?.Message ?? t.Exception?.Message ?? "未知错误";
                StatusHint = $"启动异常: {msg}";
            }
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    // ═══════════════════════════════════════════════
    //  主流程
    // ═══════════════════════════════════════════════

    private async Task RunAsync()
    {
        // 如果是管理员安装进程
        if (_autoInstall)
        {
            await RunAutoInstallAsync();
            return;
        }

        // ── 1. 并行检查运行时和更新 ──
        await Task.WhenAll(CheckRuntimeAsync(), CheckUpdateAsync());

        // ── 2. 判断是否可以直接启动 ──
        if (!RuntimeMissing && !UpdateAvailable)
        {
            AllReady = true;
            StatusHint = "正在启动 Thor...";
            await DoLaunchAsync();
        }
        else
        {
            StatusHint = "请处理下方问题后启动";
        }
    }

    // ═══════════════════════════════════════════════
    //  运行时检查与安装
    // ═══════════════════════════════════════════════

    private async Task CheckRuntimeAsync()
    {
        try
        {
            var targetVersion = RuntimeInformation.FrameworkDescription;
            var parts = targetVersion.Replace(".NET ", "").Split('.');
            _requiredVersion = parts.Length >= 2 ? $"{parts[0]}.{parts[1]}" : "10.0";

            var psi = new ProcessStartInfo("dotnet", "--list-runtimes")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process == null) { SetRuntimeMissing("无法启动 dotnet 进程"); return; }

            var output = await process.StandardOutput.ReadToEndAsync(_cts.Token);
            await process.WaitForExitAsync(_cts.Token);

            var hasCore = false;
            var hasDesktop = false;
            var versionPrefix = $"{_requiredVersion}.";

            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.Contains(versionPrefix))
                {
                    if (line.Contains(CoreRuntimeName)) hasCore = true;
                    if (line.Contains(DesktopRuntimeName)) hasDesktop = true;
                }
            }

            if (hasCore && hasDesktop)
            {
                RuntimeMissing = false;
                RuntimeMissingText = string.Empty;
            }
            else if (!hasCore && !hasDesktop)
            {
                SetRuntimeMissing($"缺少 .NET Runtime 和 Desktop Runtime {_requiredVersion}");
            }
            else if (!hasCore)
            {
                SetRuntimeMissing($"缺少 .NET Runtime {_requiredVersion} (基础运行时)");
            }
            else
            {
                SetRuntimeMissing($"缺少 .NET Desktop Runtime {_requiredVersion}");
            }
        }
        catch
        {
            SetRuntimeMissing($"缺少 .NET Desktop Runtime {_requiredVersion ?? "10.0"}");
        }
    }

    private void SetRuntimeMissing(string message)
    {
        RuntimeMissing = true;
        RuntimeMissingText = message;
    }

    /// <summary>
    /// 点击"安装"按钮：提权重启自身，触发 UAC。
    /// </summary>
    [RelayCommand]
    private void InstallRuntime()
    {
        try
        {
            var selfExe = Environment.ProcessPath
                ?? throw new InvalidOperationException("无法获取当前程序路径");

            Process.Start(new ProcessStartInfo(selfExe, "--install")
            {
                UseShellExecute = true,
                Verb = "runas",
            });

            // 提权进程启动成功后关闭当前窗口（延迟等待提权弹窗完成）
            _ = Task.Delay(800).ContinueWith(_ => Shutdown());
        }
        catch (Exception ex) when (
            ex.Message.Contains("canceled", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("取消"))
        {
            // 用户点击了"否"，不关闭，可以重试
            StatusHint = "已取消提权，可重新点击安装";
        }
        catch (Exception ex)
        {
            // 其他错误，不关闭，可以重试
            StatusHint = $"启动安装失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 管理员进程中的实际安装逻辑。
    /// </summary>
    private async Task RunAutoInstallAsync()
    {
        IsWorking = true;
        WorkingText = "正在准备下载...";
        Progress = 0;

        var installerPath = Path.Combine(Path.GetTempPath(), "dotnet-desktopruntime-install.exe");

        // 动画定时器：安装等待期间显示动态变化
        var animCts = new CancellationTokenSource();
        var animTask = RunAnimationAsync(animCts.Token);

        try
        {
            await DownloadWithRetryAsync(installerPath);

            Progress = 55;
            WorkingText = "正在安装";
            ProgressDetail = "";

            var psi = new ProcessStartInfo(installerPath, "/install /quiet /norestart")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            using var installProcess = Process.Start(psi)
                ?? throw new InvalidOperationException("无法启动安装程序");

            // 安装期间让进度条缓慢前进（55→85），给用户反馈
            var installProgressTask = SimulateInstallProgressAsync(installProcess, animCts.Token);
            await installProcess.WaitForExitAsync(_cts.Token);
            await installProgressTask;

            if (installProcess.ExitCode != 0)
            {
                var stderr = await installProcess.StandardError.ReadToEndAsync();
                throw new InvalidOperationException(
                    $"安装程序退出码 {installProcess.ExitCode}: {stderr.Trim()}");
            }

            Progress = 90;
            WorkingText = "验证安装结果";
            ProgressDetail = "";
            await CheckRuntimeAsync();

            Progress = 100;
            WorkingText = RuntimeMissing ? "安装可能未成功，请重试" : "安装完成";
            ProgressDetail = "";
            await Task.Delay(3000);
            Shutdown();
        }
        catch (OperationCanceledException)
        {
            WorkingText = "安装已取消";
            ProgressDetail = "";
            await Task.Delay(2000);
            Shutdown();
        }
        catch (Exception ex)
        {
            // 安装失败时不立即关闭，让用户看到错误
            WorkingText = $"安装失败: {ex.Message}";
            ProgressDetail = "";
            await Task.Delay(5000);
            Shutdown();
        }
        finally
        {
            await animCts.CancelAsync();
            try { await animTask; } catch { }
            animCts.Dispose();
            IsWorking = false;
            try { if (File.Exists(installerPath)) File.Delete(installerPath); } catch { }
        }
    }

    /// <summary>
    /// 带重试的下载逻辑（最多 2 次重试，间隔 2 秒）。
    /// </summary>
    private async Task DownloadWithRetryAsync(string installerPath, int maxRetries = 2)
    {
        Exception? lastEx = null;

        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                await DownloadAsync(installerPath);
                return;
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                lastEx = ex;
                WorkingText = $"下载失败（{ex.Message}），{2}秒后重试...";
                await Task.Delay(2000, _cts.Token);
                Progress = 0;
            }
        }

        throw lastEx ?? new InvalidOperationException("下载失败");
    }

    private async Task DownloadAsync(string installerPath)
    {
        WorkingText = "正在获取版本信息";
        ProgressDetail = "";

        // 动态获取最新版本号，避免硬编码
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

        var version = (await httpClient.GetStringAsync(
            "https://dotnetcli.blob.core.windows.net/dotnet/WindowsDesktop/10.0/latest.version",
            _cts.Token)).Trim();

        var url = $"https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/{version}/windowsdesktop-runtime-{version}-win-x64.exe";
        WorkingText = "正在下载";

        using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, _cts.Token);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
        var totalMB = totalBytes > 0 ? totalBytes / 1_048_576.0 : 0;

        await using var contentStream = await response.Content.ReadAsStreamAsync(_cts.Token);
        await using var fileStream = new FileStream(installerPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

        var buffer = new byte[65536];
        long totalRead = 0;
        int bytesRead;
        var sw = Stopwatch.StartNew();

        while ((bytesRead = await contentStream.ReadAsync(buffer, _cts.Token)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), _cts.Token);
            totalRead += bytesRead;

            if (totalBytes > 0)
            {
                Progress = (int)(totalRead * 50 / totalBytes);
                var readMB = totalRead / 1_048_576.0;
                var elapsed = sw.Elapsed.TotalSeconds;
                var speed = elapsed > 0 ? totalRead / elapsed / 1_048_576.0 : 0;
                ProgressDetail = $"{readMB:F1} / {totalMB:F1} MB ({speed:F1} MB/s)";
            }
            else
            {
                var readMB = totalRead / 1_048_576.0;
                ProgressDetail = $"{readMB:F1} MB";
            }
        }

        sw.Stop();
        ProgressDetail = $"{totalMB:F1} MB，下载完成";
    }

    // ═══════════════════════════════════════════════
    //  更新检查（留给你实现）
    // ═══════════════════════════════════════════════

    /// <summary>
    /// 检查更新。在此方法中实现你的更新检查逻辑。
    /// 如果检测到新版本，设置 UpdateAvailable = true 和 UpdateText。
    /// </summary>
    private async Task CheckUpdateAsync()
    {
        // ╔══════════════════════════════════════════════════╗
        // ║  TODO: 在此实现你的更新检查逻辑                    ║
        // ║                                                  ║
        // ║  示例:                                           ║
        // ║  var latest = await GetLatestVersion();           ║
        // ║  if (latest > currentVersion)                    ║
        // ║  {                                               ║
        // ║      UpdateAvailable = true;                     ║
        // ║      UpdateText = $"发现新版本 {latest}";          ║
        // ║  }                                               ║
        // ╚══════════════════════════════════════════════════╝

        await Task.CompletedTask;
    }

    /// <summary>
    /// 点击"更新"按钮的处理逻辑。
    /// </summary>
    [RelayCommand]
    private async Task UpdateAsync()
    {
        // ╔══════════════════════════════════════════════════╗
        // ║  TODO: 在此实现你的更新下载/安装逻辑               ║
        // ║                                                  ║
        // ║  示例:                                           ║
        // ║  IsWorking = true;                               ║
        // ║  WorkingText = "正在下载更新...";                  ║
        // ║  await DownloadAndApplyUpdate();                  ║
        // ║  UpdateAvailable = false;                        ║
        // ║  IsWorking = false;                              ║
        // ╚══════════════════════════════════════════════════╝

        await Task.CompletedTask;
    }

    // ═══════════════════════════════════════════════
    //  启动 Thor
    // ═══════════════════════════════════════════════

    [RelayCommand]
    private async Task Launch() => await DoLaunchAsync();

    private async Task DoLaunchAsync()
    {
        // 发布结构：Launcher.exe 在根目录，Thor.exe 在 app\ 子目录
        var appDir = Path.Combine(AppContext.BaseDirectory, "app");
        var exePath = Path.Combine(appDir, "Thor.exe");

        // 开发时 fallback：同目录或上一级
        if (!File.Exists(exePath))
            exePath = Path.Combine(AppContext.BaseDirectory, "Thor.exe");
        if (!File.Exists(exePath))
            exePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "Thor.exe"));

        if (!File.Exists(exePath))
        {
            StatusHint = "未找到 Thor.exe";
            return;
        }

        try
        {
            // 不使用 UseShellExecute，以便获取进程句柄等待窗口创建
            var thor = Process.Start(new ProcessStartInfo(exePath)
            {
                UseShellExecute = false,
            });

            if (thor != null)
            {
                // 等待 Thor 创建窗口（最多 10 秒），窗口就绪后再关闭 Launcher
                try { thor.WaitForInputIdle(10_000); } catch { }
                await Task.Delay(500); // 额外等待窗口渲染
            }

            Shutdown();
        }
        catch (Exception ex)
        {
            StatusHint = $"启动失败: {ex.Message}";
        }
    }

    // ═══════════════════════════════════════════════
    //  动画辅助
    // ═══════════════════════════════════════════════

    /// <summary>
    /// 文字动画：在工作文本后循环追加变化的点号，表示程序没有卡住。
    /// </summary>
    private async Task RunAnimationAsync(CancellationToken token)
    {
        var dots = new[] { "", ".", "..", "..." };
        var idx = 0;
        while (!token.IsCancellationRequested)
        {
            // 仅在安装阶段（WorkingText 不含 "下载" 时）追加动画
            if (!WorkingText.Contains("下载") && !WorkingText.Contains("获取"))
            {
                var baseText = WorkingText.TrimEnd('.');
                WorkingText = baseText + dots[idx % dots.Length];
            }
            idx++;
            try { await Task.Delay(500, token); } catch { break; }
        }
    }

    /// <summary>
    /// 安装等待期间让进度条从 55 缓慢前进到 85。
    /// </summary>
    private async Task SimulateInstallProgressAsync(Process process, CancellationToken token)
    {
        while (!process.HasExited && !token.IsCancellationRequested)
        {
            if (Progress < 85)
                Progress++;
            try { await Task.Delay(1500, token); } catch { break; }
        }
    }

    public void CancelIfRunning()
    {
        _cts.Cancel();
        _cts.Dispose();
    }

    private static void Shutdown()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }
}
