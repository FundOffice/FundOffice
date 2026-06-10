using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;
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
    [ObservableProperty] private bool _canLaunch;
    [ObservableProperty] private bool _canUpdate;
    [ObservableProperty] private string _statusHint = "正在检查...";

    private string? _requiredVersion;
    private string? _latestVersion;
    private bool _isUpdating;
    private UpdatePlan? _updatePlan;

    private static string CacheDir => Path.Combine(AppContext.BaseDirectory, ".update-cache");

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

        // ── 1. 先检查运行时，完成后立即显示启动按钮 ──
        await CheckRuntimeAsync();
        CanLaunch = !RuntimeMissing;

        if (RuntimeMissing)
        {
            StatusHint = "请安装缺少的运行时后启动";
            return;
        }

        // ── 2. 检查更新（5 秒内必须返回，超时则继续启动） ──
        var updateTask = CheckUpdateAsync();
        var completed = await Task.WhenAny(updateTask, Task.Delay(TimeSpan.FromSeconds(5)));
        if (completed == updateTask)
        {
            // 更新检查在 5 秒内完成，吞掉异常
            try { await updateTask; } catch { }
        }
        // 否则超时了，CheckUpdateAsync 仍在后台跑，完成后会自动更新 UI

        // ── 3. 根据结果决定行为 ──
        if (!UpdateAvailable)
        {
            AllReady = true;
            StatusHint = "正在启动 Thor...";
            await DoLaunchAsync();
        }
        else
        {
            // 有更新但不强制，用户可以跳过更新直接启动
            CanUpdate = true;
            StatusHint = "可选择更新或直接启动";
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
    //  更新检查与下载
    // ═══════════════════════════════════════════════

    private const string GitHubReleasesApi =
        "https://api.github.com/repos/FundOffice/FundOffice/releases?per_page=100";

    /// <summary>内置默认代理列表，当 proxies.txt 不存在时兜底。</summary>
    private static readonly string[] DefaultProxies =
    [
        "https://ghfast.top/",
        "https://gh-proxy.com/",
        "https://ghproxy.net/",
    ];

    /// <summary>当前生效的代理列表（从 proxies.txt 加载或内置默认值）。</summary>
    private string[] _loadedProxies = DefaultProxies;

    /// <summary>按速度排序的可用代理列表（探测后填充，空 = 直连）。</summary>
    private List<string> _sortedProxies = [""];

    /// <summary>
    /// 从 proxies.txt 加载代理列表。文件不存在或解析失败时回退到内置默认列表。
    /// 只在首次调用时读取文件。
    /// </summary>
    private void LoadProxies()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "proxies.txt");
            if (!File.Exists(path)) return;

            var proxies = File.ReadAllLines(path)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrEmpty(l) && !l.StartsWith('#'))
                .ToArray();

            if (proxies.Length > 0)
                _loadedProxies = proxies;
        }
        catch { /* 读取失败保持默认值 */ }
    }

    /// <summary>
    /// 检查 GitHub Release 最新版本，与本地 Thor 版本对比。
    /// 获取所有 release 以构建增量链，智能选择增量包或全量包。
    /// </summary>
    private async Task CheckUpdateAsync()
    {
        try
        {
            var localVersion = GetLocalThorVersion();
            var releases = await FetchAllReleasesAsync();
            if (releases.Count == 0) return;

            // 最新版本排在第一位
            var latest = releases[0];
            _latestVersion = latest.Version;

            if (!IsNewerVersion(_latestVersion, localVersion)) return;

            // 构建最优更新计划（增量链 vs 全量包）
            _updatePlan = await BuildUpdatePlanAsync(localVersion, _latestVersion, releases);
            _updatePlan ??= new UpdatePlan(false,
                latest.FullUrl != null ? [latest.FullUrl] : [],
                latest.FullSize, []);

            if (_updatePlan.Urls.Count == 0) return;

            UpdateAvailable = true;

            if (_updatePlan.UseDeltaChain)
                UpdateText = $"发现新版本 v{_latestVersion}（当前 {localVersion}，增量更新）";
            else
                UpdateText = $"发现新版本 v{_latestVersion}（当前 {localVersion}）";
        }
        catch
        {
            // 网络错误等不影响主流程，静默跳过更新检查
        }
    }

    /// <summary>
    /// 获取本地 Thor.exe 的 FileVersion。
    /// </summary>
    private string GetLocalThorVersion()
    {
        var appDir = Path.Combine(AppContext.BaseDirectory, "app");
        var thorPath = Path.Combine(appDir, "Thor.exe");
        if (!File.Exists(thorPath))
            thorPath = Path.Combine(AppContext.BaseDirectory, "Thor.exe");
        if (!File.Exists(thorPath))
            thorPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "Thor.exe"));

        if (File.Exists(thorPath))
        {
            try
            {
                var fvi = FileVersionInfo.GetVersionInfo(thorPath);
                if (!string.IsNullOrEmpty(fvi.FileVersion))
                {
                    // 返回 Major.Minor.Patch 格式（去掉 Revision）
                    var parts = fvi.FileVersion.Split('.');
                    return parts.Length >= 3 ? $"{parts[0]}.{parts[1]}.{parts[2]}" : fvi.FileVersion;
                }
            }
            catch { }
        }

        return "0.0.0";
    }

    /// <summary>
    /// 获取 GitHub 所有 release，解析每个版本的增量包和全量包信息。
    /// 优先直连 GitHub（5秒超时），失败再依次尝试代理。
    /// </summary>
    private async Task<List<ReleaseAsset>> FetchAllReleasesAsync()
    {
        // 从外部配置文件加载代理列表
        LoadProxies();

        using var http = new HttpClient();
        http.Timeout = TimeSpan.FromSeconds(5); // 每个候选最多 5 秒
        http.DefaultRequestHeaders.Add("User-Agent", "Thor-Launcher");

        // 优先直连，失败再依次尝试代理
        var candidates = new List<string> { "" }; // 空 = 直连
        candidates.AddRange(_loadedProxies);

        string? json = null;
        string usedProxy = "";
        foreach (var proxy in candidates)
        {
            try
            {
                json = await http.GetStringAsync(proxy + GitHubReleasesApi, _cts.Token);
                usedProxy = proxy;
                break;
            }
            catch { /* 尝试下一个 */ }
        }

        if (json == null)
            return []; // 全部失败，静默返回空

        // 记录使用的代理，供后续下载参考
        _sortedProxies = [usedProxy];
        _sortedProxies.AddRange(candidates.Where(p => p != usedProxy));

        using var doc = JsonDocument.Parse(json);
        var releases = new List<ReleaseAsset>();

        foreach (var release in doc.RootElement.EnumerateArray())
        {
            var tagName = release.GetProperty("tag_name").GetString() ?? "";
            var version = tagName.TrimStart('v', 'V');
            if (!Version.TryParse(version, out _)) continue;

            string? deltaUrl = null, fullUrl = null;
            long deltaSize = 0, fullSize = 0;

            if (release.TryGetProperty("assets", out var assets))
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.GetProperty("name").GetString() ?? "";
                    var url = asset.GetProperty("browser_download_url").GetString() ?? "";
                    var size = asset.TryGetProperty("size", out var s) ? s.GetInt64() : 0;

                    if (name.Contains("Delta", StringComparison.Ordinal))
                    { deltaUrl = url; deltaSize = size; }
                    else if (name.Contains("Win-x64", StringComparison.Ordinal))
                    { fullUrl = url; fullSize = size; }
                }
            }

            releases.Add(new ReleaseAsset(version, deltaUrl, fullUrl, deltaSize, fullSize));
        }

        return releases;
    }

    /// <summary>
    /// 获取指定 release 中的资产信息。
    /// </summary>
    private static ReleaseAsset? GetReleaseAsset(List<ReleaseAsset> releases, string version)
        => releases.FirstOrDefault(r => r.Version == version);

    /// <summary>
    /// 构建最优更新计划：尝试构建从 localVersion 到 latestVersion 的增量链，
    /// 与全量包比较总大小，选择更优方案。
    /// 增量包是相邻版本间的差异，跨版本必须依次应用。
    /// </summary>
    private Task<UpdatePlan?> BuildUpdatePlanAsync(
        string localVersion, string latestVersion, List<ReleaseAsset> releases)
    {
        // releases 按版本号降序排列（GitHub API 默认按创建时间降序）
        // 构建版本链：localVersion → ... → latestVersion
        var versionsInOrder = releases
            .Select(r => r.Version)
            .Where(v =>
            {
                if (!Version.TryParse(v, out var ver)) return false;
                if (!Version.TryParse(localVersion, out var local)) return false;
                if (!Version.TryParse(latestVersion, out var latest)) return false;
                return ver > local && ver <= latest;
            })
            .OrderBy(v => Version.Parse(v))
            .ToList();

        if (versionsInOrder.Count == 0) return Task.FromResult<UpdatePlan?>(null);

        // 尝试构建增量链：每个中间版本都必须有增量包
        var deltaUrls = new List<string>();
        var deltaVersions = new List<string>();
        long totalDeltaSize = 0;
        var canUseDeltaChain = true;

        foreach (var ver in versionsInOrder)
        {
            var release = GetReleaseAsset(releases, ver);
            if (release?.DeltaUrl != null)
            {
                deltaUrls.Add(release.DeltaUrl);
                deltaVersions.Add(ver);
                totalDeltaSize += release.DeltaSize;
            }
            else
            {
                canUseDeltaChain = false;
                break;
            }
        }

        // 获取全量包信息
        var latestRelease = GetReleaseAsset(releases, latestVersion);
        var fullUrl = latestRelease?.FullUrl;
        var fullSize = latestRelease?.FullSize ?? 0;

        if (fullUrl == null) return Task.FromResult<UpdatePlan?>(null);

        // 决策逻辑：
        // 1. 增量链必须完整（每个中间版本都有 delta 包）
        // 2. 增量链总大小 < 全量包大小（delta size = 0 表示未知，视为更小）
        // 3. 增量链步数不超过 10（避免过多顺序下载）
        if (canUseDeltaChain && deltaVersions.Count <= 10)
        {
            var useDelta = totalDeltaSize == 0       // 大小未知，默认用增量
                         || totalDeltaSize < fullSize; // 增量链更小

            if (useDelta)
                return Task.FromResult<UpdatePlan?>(
                    new UpdatePlan(true, deltaUrls, totalDeltaSize, deltaVersions));
        }

        // 使用全量包
        return Task.FromResult<UpdatePlan?>(
            new UpdatePlan(false, [fullUrl], fullSize, []));
    }

    /// <summary>
    /// 比较版本号：remote 是否比 local 更新。
    /// </summary>
    private static bool IsNewerVersion(string remote, string local)
    {
        if (!Version.TryParse(remote, out var remoteVer)) return false;
        if (!Version.TryParse(local, out var localVer)) return true;
        return remoteVer > localVer;
    }

    /// <summary>
    /// 点击"更新"按钮：按计划下载更新包并替换本地文件。
    /// </summary>
    [RelayCommand]
    private async Task UpdateAsync()
    {
        if (_isUpdating || _updatePlan == null || _updatePlan.Urls.Count == 0) return;
        _isUpdating = true;
        CanUpdate = false;

        IsWorking = true;
        Progress = 0;
        ProgressDetail = "";

        var tempDirs = new List<string>();

        try
        {
            var urls = _updatePlan.Urls;
            var totalSteps = urls.Count;
            Directory.CreateDirectory(CacheDir);

            // ── 依次下载所有包 ──
            for (int i = 0; i < urls.Count; i++)
            {
                var prefix = totalSteps > 1 ? $"[{i + 1}/{totalSteps}] " : "";
                var zipPath = Path.Combine(CacheDir, $"pkg-{i:D3}.zip");
                await RacingDownloadAsync(urls[i], zipPath, prefix);
            }

            // ── 依次解压并应用 ──
            for (int i = 0; i < urls.Count; i++)
            {
                Progress = 75 + i * (20 / totalSteps);

                var zipPath = Path.Combine(CacheDir, $"pkg-{i:D3}.zip");
                var tempExtract = Path.Combine(Path.GetTempPath(), $"thor-extract-{Guid.NewGuid():N}");
                tempDirs.Add(tempExtract);

                WorkingText = totalSteps > 1
                    ? $"正在应用更新 [{i + 1}/{totalSteps}]"
                    : "正在解压";
                ProgressDetail = "";

                ZipFile.ExtractToDirectory(zipPath, tempExtract);
                await Task.Yield();
                ApplyUpdate(tempExtract);
            }

            Progress = 100;
            WorkingText = "更新完成";
            ProgressDetail = "";
            UpdateAvailable = false;

            // 清理缓存
            try { Directory.Delete(CacheDir, true); } catch { }

            await Task.Delay(1500);

            AllReady = true;
            StatusHint = "正在启动 Thor...";
            await DoLaunchAsync();
        }
        catch (OperationCanceledException)
        {
            WorkingText = "更新已取消";
            ProgressDetail = "";
            await Task.Delay(2000);
        }
        catch (Exception ex)
        {
            WorkingText = $"更新失败: {ex.Message}";
            ProgressDetail = "";
            await Task.Delay(3000);
        }
        finally
        {
            IsWorking = false;
            _isUpdating = false;
            CanUpdate = _updatePlan != null && UpdateAvailable;
            foreach (var d in tempDirs)
                try { if (Directory.Exists(d)) Directory.Delete(d, true); } catch { }
        }
    }

    /// <summary>
    /// 下载单个更新 zip 包。
    /// 按顺序尝试：API 成功的代理优先 → 其余代理 → 直连。失败自动降级。
    /// </summary>
    private async Task RacingDownloadAsync(string rawUrl, string destPath, string prefix = "")
    {
        WorkingText = $"{prefix}正在下载更新";
        ProgressDetail = "";

        // 按探测顺序构建候选：API 成功的代理排前面
        var urls = new List<string>();
        foreach (var proxy in _sortedProxies)
            urls.Add(proxy + rawUrl);
        // 补上没在 _sortedProxies 里的代理
        foreach (var proxy in _loadedProxies)
        {
            var url = proxy + rawUrl;
            if (!urls.Contains(url)) urls.Add(url);
        }
        // 直连兜底
        if (!urls.Contains(rawUrl)) urls.Insert(0, rawUrl);

        Exception? lastEx = null;
        foreach (var url in urls)
        {
            try
            {
                await DownloadFileAsync(url, destPath);
                return; // 成功
            }
            catch (Exception ex)
            {
                lastEx = ex;
                // 清理不完整的文件
                try { if (File.Exists(destPath)) File.Delete(destPath); } catch { }
            }
        }

        throw lastEx ?? new InvalidOperationException("所有下载通道均失败");
    }

    /// <summary>
    /// 从单个 URL 下载文件到指定路径。
    /// </summary>
    private async Task DownloadFileAsync(string url, string destPath)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        http.DefaultRequestHeaders.Add("User-Agent", "Thor-Launcher");

        using var resp = await http.GetAsync(url,
            HttpCompletionOption.ResponseHeadersRead, _cts.Token);
        resp.EnsureSuccessStatusCode();

        var total = resp.Content.Headers.ContentLength ?? -1L;

        using var netStream = await resp.Content.ReadAsStreamAsync(_cts.Token);
        using var fileStream = new FileStream(destPath, FileMode.Create,
            FileAccess.Write, FileShare.None, 65536);

        var buf = new byte[65536];
        long read = 0;
        int n;

        while ((n = await netStream.ReadAsync(buf, _cts.Token)) > 0)
        {
            await fileStream.WriteAsync(buf.AsMemory(0, n), _cts.Token);
            read += n;

            var readMB = read / 1_048_576.0;
            if (total > 0)
            {
                Progress = (int)(read * 70 / total);
                ProgressDetail = $"{readMB:F1} / {total / 1_048_576.0:F1} MB";
            }
            else
            {
                ProgressDetail = $"{readMB:F1} MB";
            }
        }

        Progress = 70;
        ProgressDetail = $"{read / 1_048_576.0:F1} MB，下载完成";
    }

    /// <summary>
    /// 将解压后的文件覆盖到安装目录。
    /// 发布结构：Launcher.exe 在根目录，Thor.exe 和其他文件在 app/ 子目录。
    /// </summary>
    private void ApplyUpdate(string extractDir)
    {
        var rootDir = AppContext.BaseDirectory.TrimEnd('\\', '/');
        var appDir = Path.Combine(rootDir, "app");

        // 如果 zip 包含 app/ 子目录，按原结构覆盖
        var hasAppFolder = Directory.Exists(Path.Combine(extractDir, "app"));

        foreach (var file in Directory.GetFiles(extractDir, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(extractDir, file);
            string targetPath;

            if (hasAppFolder)
            {
                // zip 内部已有 app/ 结构，直接映射到根目录
                targetPath = Path.Combine(rootDir, relativePath);
            }
            else
            {
                // zip 内文件平铺，全部放入 app/ 目录
                targetPath = Path.Combine(appDir, relativePath);
            }

            var targetDir = Path.GetDirectoryName(targetPath);
            if (targetDir != null && !Directory.Exists(targetDir))
                Directory.CreateDirectory(targetDir);

            // 不覆盖正在运行的 Launcher 自身
            if (Path.GetFullPath(targetPath).Equals(
                    Path.GetFullPath(Environment.ProcessPath ?? ""),
                    StringComparison.Ordinal))
                continue;

            try
            {
                File.Copy(file, targetPath, overwrite: true);
            }
            catch
            {
                // 单个文件覆盖失败不中断整体更新
            }
        }
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

    // ═══════════════════════════════════════════════
    //  内部数据模型
    // ═══════════════════════════════════════════════

    private sealed record ReleaseAsset(string Version, string? DeltaUrl, string? FullUrl, long DeltaSize, long FullSize);

    private sealed record UpdatePlan(bool UseDeltaChain, List<string> Urls, long TotalSize, List<string> DeltaVersions);
}
