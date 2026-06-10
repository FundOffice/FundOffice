using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiteDB;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Launcher.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private const string DesktopRuntimeName = "Microsoft.WindowsDesktop.App";
    private const string CoreRuntimeName = "Microsoft.NETCore.App";
#if RELEASE
    private const string RegistryPath = @"Software\Nexus";
#else
    private const string RegistryPath = @"Software\Nexus\Debug";
#endif

    private readonly CancellationTokenSource _cts = new();
    private readonly bool _autoInstall;
    private readonly Window? _window;

    // --- 首次运行状态 ---
    [ObservableProperty] private bool _needWorkingFolder;
    [ObservableProperty] private bool _canChangeWorkingFolder;  // 工作目录已存在，允许更换
    [ObservableProperty] private bool _firstRun;
    [ObservableProperty] private bool _firstRunDone;
    [ObservableProperty] private bool _databaseCorrupted;  // 数据库损坏标志
    [ObservableProperty] private string? _managerName;
    [ObservableProperty] private ManagerInfo[]? _managerOptions;
    [ObservableProperty] private bool _isValidManager;
    [ObservableProperty] private bool _isSearching;
    [ObservableProperty] private string? _repairManagerName;  // 数据库损坏修复时输入的管理人名称

    /// <summary>
    /// 目录是否可用（首次运行且数据库未损坏）
    /// </summary>
    public bool IsDirectoryAvailable => FirstRun && !DatabaseCorrupted;

    /// <summary>
    /// 是否显示"更换目录"按钮（首次运行且数据库未损坏）
    /// </summary>
    public bool CanChangeWorkingFolderVisible => FirstRun && !DatabaseCorrupted;

    private string? _initManagerId;      // 验证通过的管理人ID（启动时传参用）
    private string? _initManagerName;    // 验证通过的管理人名称（启动时传参用）
    private string? _initManagerCode;    // 验证通过的管理人RegisterNo（启动时传参用）

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
    [ObservableProperty] private bool _readyToLaunch;
    [ObservableProperty] private string _statusHint = "正在检查...";

    private string? _requiredVersion;
    private string? _latestVersion;
    private bool _isUpdating;
    private UpdatePlan? _updatePlan;

    private static string CacheDir => Path.Combine(AppContext.BaseDirectory, ".update-cache");

    /// <summary>
    /// 设计器用无参构造函数。
    /// </summary>
    public MainWindowViewModel() : this(false, null) { }

    public MainWindowViewModel(bool autoInstall, Window? window = null)
    {
        _autoInstall = autoInstall;
        _window = window;
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

        // ── 1. 检查运行时 ──
        await CheckRuntimeAsync();
        if (RuntimeMissing)
        {
            StatusHint = "请安装缺少的运行时后启动";
            CanLaunch = false;
            return;
        }

        // ── 2. 检查工作目录（注册表 WorkingFolder） ──
        if (CheckWorkingFolder())
        {
            // 工作目录不存在，需要用户选择
            NeedWorkingFolder = true;
            StatusHint = "请先设置工作目录";

            // 等待用户选择目录
            while (NeedWorkingFolder && !_cts.IsCancellationRequested)
                await Task.Delay(200, _cts.Token);
            if (_cts.IsCancellationRequested) return;
        }

        // ── 3. 检查首次运行（data\base.db 是否存在） ──
        DatabaseCorrupted = false;
        if (CheckFirstRun())
        {
            // 数据库不存在或没有主管理人，进入首次运行流程
            FirstRun = true;

            if (DatabaseCorrupted)
            {
                StatusHint = "此目录数据库已损坏，请更换目录";
                CanLaunch = false;     // 隐藏启动按钮
                ReadyToLaunch = false; // 不允许启动，必须更换目录
            }
            else
            {
                StatusHint = "首次运行，请输入管理人名称";
                CanLaunch = false;     // 隐藏启动按钮
                ReadyToLaunch = false;
            }
        }
        else
        {
            // 数据库已有主管理人，跳过首次运行流程
            FirstRun = false;
            FirstRunDone = true;
            StatusHint = "准备就绪";
        }

        // ── 4. 检查更新（5 秒超时） ──
        var updateTask = CheckUpdateAsync();
        var completed = await Task.WhenAny(updateTask, Task.Delay(TimeSpan.FromSeconds(5)));
        if (completed == updateTask)
        {
            try { await updateTask; } catch { }
        }

        // ── 5. 根据情况决定是否自动启动 ──
        if (UpdateAvailable && !DatabaseCorrupted && !FirstRun)
        {
            // 有更新且数据库正常且非首次运行：显示更新按钮，让用户选择
            CanLaunch = true;
            ReadyToLaunch = true;
            CanUpdate = true;
            StatusHint = UpdateText;
        }
        else if (!FirstRun && !DatabaseCorrupted)
        {
            // 非首次运行、无更新、数据库正常：直接自动启动
            CanLaunch = true;
            ReadyToLaunch = true;
            StatusHint = "正在启动 Thor...";
            await DoLaunchAsync();
        }
        else if (DatabaseCorrupted)
        {
            // 数据库损坏：隐藏启动按钮，等待修复或更换目录
            CanLaunch = false;
            ReadyToLaunch = false;
        }
        else
        {
            // 首次运行：隐藏启动按钮，等待用户输入管理人信息
            CanLaunch = false;
            ReadyToLaunch = false;
            StatusHint = "请输入管理人名称后点击启动";
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

    // ═══════════════════════════════════════════════
    //  首次运行检查与工作目录初始化
    // ═══════════════════════════════════════════════

    /// <summary>
    /// 检查注册表中是否已设置工作目录（WorkingFolder）。
    /// 检查 Software\Nexus（发布版）和 Software\Nexus\Debug（调试版）两个路径。
    /// 返回 true 表示需要用户选择工作目录。
    /// </summary>
    private bool CheckWorkingFolder()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
        var dir = key?.GetValue("WorkingFolder") as string;

        if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
        {
            Directory.SetCurrentDirectory(dir);
            _existingWorkingFolder = dir;
            return false;
        }

        return true;
    }



    /// <summary>
    /// 检查工作目录下是否已有有效的数据库和主管理人。
    /// 参考 InitWindow.ChooseFolder 的逻辑（第 193-213 行）。
    /// 返回 true 表示需要首次运行（数据库不存在或无效）。
    /// </summary>
    private bool CheckFirstRun()
    {
        var dbPath = @"data\base.db";
        if (!File.Exists(dbPath))
            return true;  // 数据库不存在，需要首次运行

        try
        {
            // 计算数据库密码
            var password = GetDatabasePassword();

            // 使用 LiteDB 直接打开数据库
            using var db = new LiteDatabase($"FileName={dbPath};Password={password};Connection=Shared");

            // 查找 IsMaster = true 的文档
            var collection = db.GetCollection("Manager");
            var manager = collection.FindOne(Query.EQ("IsMaster", true));

            // 找到主管理人，说明数据库完整可用，不需要首次运行
            // 没有找到主管理人，需要首次运行
            return manager == null;
        }
        catch
        {
            // 数据库损坏、无法打开，需要重新初始化（首次运行）
            // 标记此目录不可用，需要用户重新选择
            DatabaseCorrupted = true;
            return true;
        }
    }

    /// <summary>
    /// 计算数据库密码。从注册表读取 Code，解密后加上后缀，然后计算 MD5。
    /// </summary>
    private string GetDatabasePassword()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
        return key?.GetValue("Code") is string code ?  ComputeDatabasePassword(Decrypt(code)) : "";
    }

    /// <summary>
    /// 根据原始 code 计算数据库密码（不依赖注册表）。
    /// </summary>
    private static string ComputeDatabasePassword(string rawCode)
    {
        var password = string.IsNullOrEmpty(rawCode) ? "" : rawCode;
        password += "jgkfld9024039284jrwe";

        using var md5 = MD5.Create();
        var bytes = Encoding.UTF8.GetBytes(password);
        var hashBytes = md5.ComputeHash(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// AES 解密（简化版，复制自 AesHelper.Decrypt）。
    /// </summary>
    private static string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
            return string.Empty;

        // AesHelper 使用的硬编码密钥
        var secret = "OHM5a0YycFI3eFEzZEc1akwxekM0dkI2bk0wYVM3d0V0TjViUjhnSzJtUDRxWDd6";
        var bytes = Convert.FromBase64String(secret);
        byte[] cipherBytes = Convert.FromBase64String(cipherText);

        using (Aes aes = Aes.Create())
        {
            aes.Key = bytes[..32];
            aes.IV = bytes[32..];
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using (MemoryStream ms = new MemoryStream())
            using (CryptoStream cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write))
            {
                cs.Write(cipherBytes, 0, cipherBytes.Length);
                cs.FlushFinalBlock();
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }
    }

    public static string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        var bytes = Convert.FromBase64String("OHM5a0YycFI3eFEzZEc1akwxekM0dkI2bk0wYVM3d0V0TjViUjhnSzJtUDRxWDd6");

        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);

        using (Aes aes = Aes.Create())
        {
            aes.Key = bytes[..32];
            aes.IV = bytes[32..];
            // 加密模式 & 填充模式（通用标准）
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using (MemoryStream ms = new MemoryStream())
            using (CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
            {
                cs.Write(plainBytes, 0, plainBytes.Length);
                cs.FlushFinalBlock();
                // 转 Base64 方便传输/存储
                return Convert.ToBase64String(ms.ToArray());
            }
        }
    }

    private string? _existingWorkingFolder;

    /// <summary>
    /// 用户点击"选择目录"：打开系统文件夹选择对话框，写入注册表并创建工作目录结构。
    /// </summary>
    [RelayCommand]
    private async Task ChooseFolder()
    {
        if (_window == null) return;

        try
        {
            var options = new FolderPickerOpenOptions
            {
                Title = "选择工作目录",
                AllowMultiple = false,
            };

            // 建议起始位置
            var startPath = !string.IsNullOrEmpty(_existingWorkingFolder)
                ? _existingWorkingFolder
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (Directory.Exists(startPath))
            {
                try
                {
                    options.SuggestedStartLocation =
                        await _window.StorageProvider.TryGetFolderFromPathAsync(
                            new Uri($"file:///{startPath.Replace('\\', '/')}"));
                }
                catch { /* 路径转换失败时忽略，使用系统默认位置 */ }
            }

            var result = await _window.StorageProvider.OpenFolderPickerAsync(options);
            if (result.Count == 0) return;

            var folderPath = result[0].Path.LocalPath;

            // 写入注册表（与 Thor 保持一致）
            using var regKey = Registry.CurrentUser.CreateSubKey(RegistryPath);
            regKey.SetValue("WorkingFolder", folderPath);

            // 设为当前工作目录
            Directory.SetCurrentDirectory(folderPath);

            // 预创建工作目录结构（与 App.xaml.cs OnStartup 保持一致）
            foreach (var sub in new[]
            {
                "data", "config",
                "files/funds", "files/evaluation", "files/tac",
                "files/accounts", "files/accounts/security",
                "files/accounts/stock", "files/accounts/future",
                "files/accounts/fund", "files/accounts/other",
                "plugins"
            })
                Directory.CreateDirectory(Path.Combine(folderPath, sub));

            NeedWorkingFolder = false;

            // 关键：重新检查数据库状态（参考 InitWindow 第 193-213 行）
            DatabaseCorrupted = false;
            if (!CheckFirstRun())
            {
                // 数据库已有主管理人，允许直接启动
                StatusHint = "工作目录已设置";
                CanLaunch = true;
                ReadyToLaunch = true;
                FirstRun = false;  // 确保不显示管理人输入框
                FirstRunDone = true;

                // 检查更新，无更新则自动启动
                _ = CheckUpdateAsync();
                if (!UpdateAvailable)
                {
                    StatusHint = "正在启动 Thor...";
                    await DoLaunchAsync();
                }
            }
            else
            {
                // 没有数据库或没有主管理人，进入首次运行流程
                FirstRun = true;
                FirstRunDone = false;

                if (DatabaseCorrupted)
                {
                    StatusHint = "此目录数据库已损坏，请更换目录或重新初始化";
                    CanLaunch = false;     // 隐藏启动按钮
                    ReadyToLaunch = false; // 不允许启动
                }
                else
                {
                    StatusHint = "首次运行，请输入管理人名称";
                    CanLaunch = false;     // 隐藏启动按钮
                    ReadyToLaunch = false;
                }
            }
        }
        catch (Exception ex)
        {
            StatusHint = $"设置工作目录失败: {ex.Message}";
        }
    }

    // ═══════════════════════════════════════════════
    //  AMAC 管理人搜索（首次运行）
    // ═══════════════════════════════════════════════

    private CancellationTokenSource? _searchCts;

    /// <summary>
    /// 用户从搜索结果列表中点击选择一个管理人。
    /// 这是唯一确认管理人的方式（强制从列表选择）。
    /// </summary>
    [RelayCommand]
    private void ConfirmManagerSelection(ManagerInfo? manager)
    {
        if (manager == null || string.IsNullOrEmpty(manager.Id)) return;

        ManagerName = manager.Name;  // 回填到输入框
        IsValidManager = true;
        _initManagerId = manager.Id;
        _initManagerName = manager.Name;
        _initManagerCode = manager.RegisterNo;  // 保存 registerNo 作为 code
        FirstRunDone = false;      // 不标记完成，等待点击启动按钮
        FirstRun = true;           // 保持显示输入框（允许修改）
        ManagerOptions = null;     // 隐藏搜索结果列表
        CanLaunch = true;          // 显示启动按钮
        ReadyToLaunch = true;      // 启用启动按钮
        StatusHint = $"已确认管理人：{manager.Name}，可修改或点击启动";
    }

    /// <summary>
    /// 数据库损坏时，输入管理人名称尝试修复。
    /// 通过 AMAC 搜索获取管理人信息，爬取详情页获取组织机构代码，
    /// 再与数据库中存储的 Manager.Identity.Id 比对，一致则修复成功并直接启动。
    /// </summary>
    [RelayCommand]
    private async Task TryRepairAsync()
    {
        var name = RepairManagerName?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            StatusHint = "请输入管理人名称";
            return;
        }

        IsWorking = true;
        StatusHint = "正在搜索管理人...";

        try
        {
            // 1. 搜索 AMAC 获取管理人信息
            var results = await SearchManagersAsync(name, _cts.Token);
            if (results == null || results.Length == 0)
            {
                StatusHint = "未找到匹配的管理人";
                IsWorking = false;
                return;
            }

            // 优先精确匹配，否则用第一个结果
            var managerInfo = results.FirstOrDefault(m => m.Name == name) ?? results[0];

            // 2. 爬取管理人详情页获取组织机构代码（Identity.Id）
            StatusHint = "正在获取管理人详细信息...";
            var orgCode = await FetchOrganizationCodeAsync(managerInfo.Id!, _cts.Token);
            if (string.IsNullOrEmpty(orgCode))
            {
                StatusHint = "无法获取管理人组织机构代码，请检查网络";
                IsWorking = false;
                return;
            }

            // 3. 尝试读取数据库（用 orgCode 作为 code 计算密码）
            StatusHint = "正在验证数据库...";
            var dbPath = @"data\base.db";
            var password = ComputeDatabasePassword(orgCode);

            using var db = new LiteDatabase($"FileName={dbPath};Password={password};Connection=Shared");
            var collection = db.GetCollection("Manager");
            var dbManager = collection.FindOne(Query.EQ("IsMaster", true));

            if (dbManager == null)
            {
                StatusHint = "数据库中未找到管理人信息";
                IsWorking = false;
                return;
            }

            // 4. 读取数据库中的 Manager.Identity.Id
            string? dbIdentityId = null;
            if (dbManager.TryGetValue("Identity", out var identityValue) && identityValue.IsDocument)
            {
                var identityDoc = identityValue.AsDocument;
                if (identityDoc.TryGetValue("_id", out var idValue))
                    dbIdentityId = idValue.AsString;
            }

            // 5. 比对：一致则修复成功，直接启动
            if (dbIdentityId == orgCode)
            {
                DatabaseCorrupted = false;
                FirstRun = false;
                FirstRunDone = true;
                StatusHint = "修复成功，正在启动 Thor...";
                IsWorking = false;

                using (var key = Registry.CurrentUser.CreateSubKey(RegistryPath))
                {
                    key.SetValue("Cap", Encrypt(name));
                    key.SetValue("Code", Encrypt(orgCode));
                }


                CanLaunch = true;
                ReadyToLaunch = true;
                await DoLaunchAsync();
            }
            else
            {
                StatusHint = $"修复失败：数据库中的身份代码({dbIdentityId ?? "无"})与输入的管理人不一致({orgCode})";
                IsWorking = false;
            }
        }
        catch (Exception ex)
        {
            StatusHint = $"修复失败: {ex.Message}";
            IsWorking = false;
        }
    }

    /// <summary>
    /// 爬取 AMAC 管理人详情页，提取组织机构代码。
    /// </summary>
    private static async Task<string?> FetchOrganizationCodeAsync(string amacId, CancellationToken token)
    {
        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(15);
            client.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            var url = $"https://gs.amac.org.cn/amac-infodisc/res/pof/manager/{amacId}.html";
            var html = await client.GetStringAsync(url, token);

            // 检查页面是否有效
            if (!html.Contains("机构信息"))
                return null;

            // 提取组织机构代码：匹配 <td class="title">组织机构代码</td> 后的 <td>值</td>
            var match = Regex.Match(html,
                @"<td[^>]*class=[""']title[""'][^>]*>\s*组织机构代码\s*</td>\s*<td[^>]*>([^<]+)</td>",
                RegexOptions.IgnoreCase);
            if (match.Success)
                return match.Groups[1].Value.Trim();

            // 备用匹配（更宽松）
            match = Regex.Match(html,
                @"组织机构代码\s*</td>\s*<td[^>]*>([^<]+)</td>",
                RegexOptions.IgnoreCase);
            if (match.Success)
                return match.Groups[1].Value.Trim();

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 管理人名称变更时触发 AMAC 搜索（防抖 500ms）。
    /// 注意：这里仅执行搜索，不自动确认。用户必须从列表中选择。
    /// </summary>
    async partial void OnManagerNameChanged(string? value)
    {
        // Trim 输入，去除首尾空格
        value = value?.Trim();

        // 如果用户已经确认了管理人，不再触发搜索（避免回填时重复搜索）
        if (IsValidManager && ManagerOptions != null && ManagerOptions.Any(m => m.Name == value))
            return;

        IsValidManager = false;

        if (string.IsNullOrWhiteSpace(value))
        {
            ManagerOptions = null;
            return;
        }

        // 如果输入值与当前搜索结果中的某一项完全匹配，不需要重新搜索
        if (ManagerOptions != null && ManagerOptions.Any(m => m.Name == value))
        {
            // 精确匹配已存在，直接提示用户点击即可
            StatusHint = $"名称有效，请点击列表中的 \"{value}\" 确认";
            return;
        }

        // 防抖：取消上一次搜索
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        try { await Task.Delay(500, token); }
        catch { return; }

        if (token.IsCancellationRequested) return;

        IsSearching = true;
        try
        {
            var results = await SearchManagersAsync(value, token);
            if (token.IsCancellationRequested) return;

            ManagerOptions = results;

            // 精确匹配仅用于提示，不自动确认
            if (results != null && results.Any(m => m.Name == value))
            {
                // 用户输入了完整名称且在列表中，提示可以点击列表确认
                StatusHint = $"名称有效，请点击列表中的 \"{value}\" 确认";
            }
            else if (results != null && results.Length > 0)
            {
                StatusHint = $"找到 {results.Length} 个管理人，请点击选择";
            }
            else
            {
                StatusHint = "未找到匹配的管理人，请重新输入";
                ManagerOptions = null;  // 清空列表，防止无效选择
            }
        }
        catch (Exception ex)
        {
            // 网络错误时明确提示，不允许继续
            StatusHint = $"搜索失败：{ex.Message}。请检查网络连接后重试";
            ManagerOptions = null;  // 确保无法选择
            IsValidManager = false;
        }
        finally
        {
            IsSearching = false;
        }
    }

    partial void OnFirstRunChanged(bool value)
    {
        OnPropertyChanged(nameof(IsDirectoryAvailable));
        OnPropertyChanged(nameof(CanChangeWorkingFolderVisible));
    }

    partial void OnDatabaseCorruptedChanged(bool value)
    {
        OnPropertyChanged(nameof(IsDirectoryAvailable));
        OnPropertyChanged(nameof(CanChangeWorkingFolderVisible));
    }

    /// <summary>
    /// 调用 AMAC API 搜索管理人，返回匹配的管理人信息列表（包含 ID 和名称）。
    /// 参考 AmacHtml.GetInstitutionInfoFromAmac。
    /// </summary>
    private static async Task<ManagerInfo[]> SearchManagersAsync(string keyword, CancellationToken token)
    {
        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(10); // 设置超时时间
            client.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            var body = "{\"keyword\":\"KEYWORD\",\"regiProvinceFsc\":\"province\",\"offiProvinceFsc\":\"province\",".Replace("KEYWORD", keyword) +
                       "\"establishDate\":{\"from\":\"1900-01-01\",\"to\":\"9999-01-01\"}," +
                       "\"registerDate\":{\"from\":\"1900-01-01\",\"to\":\"9999-01-01\"}}";

            var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, "https://gs.amac.org.cn/amac-infodisc/api/pof/manager/query?&page=0&size=20")
            {
                Content = content,
            };
            request.Headers.Add("Accept", "application/json, text/javascript, */*; q=0.01");
            request.Headers.Add("Origin", "https://gs.amac.org.cn");
            request.Headers.Add("Referer", "https://gs.amac.org.cn/amac-infodisc/res/pof/manager/managerList.html");

            using var resp = await client.SendAsync(request, token);
            if (!resp.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"AMAC API 返回错误: {resp.StatusCode}");
            }

            var json = await resp.Content.ReadAsStringAsync(token);
            json = System.Text.RegularExpressions.Regex.Replace(json, "</*em>", "");

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("content", out var arr))
                return [];

            var managers = new List<ManagerInfo>();
            foreach (var item in arr.EnumerateArray())
            {
                if (item.TryGetProperty("id", out var idEl) &&
                    item.TryGetProperty("managerName", out var nameEl) &&
                    item.TryGetProperty("registerNo", out var registerNoEl))
                {
                    var id = idEl.GetString();
                    var name = nameEl.GetString();
                    var registerNo = registerNoEl.GetString();
                    if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(name))
                        managers.Add(new ManagerInfo(id, name, registerNo ?? ""));
                }
            }

            return managers.Take(8).ToArray();
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException || !token.IsCancellationRequested)
        {
            throw new TimeoutException("网络连接超时，请检查您的网络连接后重试");
        }
        catch (HttpRequestException ex)
        {
            throw new HttpRequestException($"网络请求失败: {ex.Message}", ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new Exception($"搜索管理人时发生错误: {ex.Message}", ex);
        }
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
    private async Task Launch()
    {
        // 首次运行时，如果用户还没有确认管理人，提示先选择
        if (FirstRun && string.IsNullOrEmpty(_initManagerId))
        {
            StatusHint = "请先从列表中选择管理人";
            return;
        }

        await DoLaunchAsync();
    }

    private async Task DoLaunchAsync()
    {
        // 首次运行：启动 initmanager.exe 并传递管理人信息
        if (FirstRun && _initManagerId != null && _initManagerName != null)
        {
            await LaunchInitManagerAsync();
            return;
        }

        // 非首次运行：直接启动 Thor.exe
        await LaunchThorAsync();
    }

    /// <summary>
    /// 首次运行时启动 initmanager.exe，传递管理人名、ID 和 code。
    /// 无窗口运行，监控其退出后启动 Thor。
    /// </summary>
    private async Task LaunchInitManagerAsync()
    {
        // 查找 initmanager.exe
        var appDir = Path.Combine(AppContext.BaseDirectory, "app");
        var exePath = Path.Combine(appDir, "InitManager.exe");

        // 开发时 fallback：同目录或上一级
        if (!File.Exists(exePath))
            exePath = Path.Combine(AppContext.BaseDirectory, "InitManager.exe");
        if (!File.Exists(exePath))
            exePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "InitManager.exe"));

        if (!File.Exists(exePath))
        {
            StatusHint = "未找到 InitManager.exe";
            return;
        }

        try
        {
            // 隐藏 UI（输入框、选择目录按钮、更换目录按钮）
            FirstRun = false;
            FirstRunDone = true;
            NeedWorkingFolder = false;
            CanChangeWorkingFolder = false;
            ReadyToLaunch = false;

            StatusHint = "正在初始化数据库...";
            IsWorking = true;
            WorkingText = "初始化中";
            Progress = 50;

            // 获取当前工作目录
            var currentDir = Directory.GetCurrentDirectory();

            // 构建参数：-name "名称" -id "ID" -code "RegisterNo" -dir "工作目录"
            var args = $"-name \"{_initManagerName}\" -id \"{_initManagerId}\" -code \"{_initManagerCode}\" -dir \"{currentDir}\"";

            // 启动 initmanager.exe（无窗口）
            var process = Process.Start(new ProcessStartInfo(exePath)
            {
                UseShellExecute = false,
                Arguments = args,
                CreateNoWindow = true,  // 不显示窗口
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });

            if (process != null)
            {
                // 等待 initmanager 完成
                await process.WaitForExitAsync(_cts.Token);

                // 检查退出码
                if (process.ExitCode == 0)
                {
                    StatusHint = "初始化完成，正在启动 Thor...";
                    Progress = 90;
                    await Task.Delay(1000);

                    // 启动 Thor
                    await LaunchThorAsync();
                }
                else
                {
                    var error = await process.StandardError.ReadToEndAsync();
                    StatusHint = $"初始化失败 (退出码 {process.ExitCode}): {error}";
                    IsWorking = false;
                }
            }
        }
        catch (Exception ex)
        {
            StatusHint = $"启动初始化失败: {ex.Message}";
            IsWorking = false;
        }
    }

    /// <summary>
    /// 启动 Thor.exe
    /// </summary>
    private async Task LaunchThorAsync()
    {
        var appDir = Path.Combine(AppContext.BaseDirectory, "app");
        var exePath = Path.Combine(appDir, "Thor.exe");

        // 开发时 fallback
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
            var thor = Process.Start(new ProcessStartInfo(exePath)
            {
                UseShellExecute = false,
            });

            if (thor != null)
            {
                try { thor.WaitForInputIdle(10_000); } catch { }
                await Task.Delay(500);
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
        _searchCts?.Cancel();  // 同时取消搜索
        _cts.Dispose();
        _searchCts?.Dispose(); // 释放资源
    }

    private static void Shutdown()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }

    // ═══════════════════════════════════════════════
    //  内部数据模型
    // ═══════════════════════════════════════════════

    public sealed record ManagerInfo(string Id, string Name, string RegisterNo);

    private sealed record ReleaseAsset(string Version, string? DeltaUrl, string? FullUrl, long DeltaSize, long FullSize);

    private sealed record UpdatePlan(bool UseDeltaChain, List<string> Urls, long TotalSize, List<string> DeltaVersions);
}
