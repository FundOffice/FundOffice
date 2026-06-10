using CommunityToolkit.Mvvm.Messaging;
using FMO.Disclosure;
using FMO.ESigning;
using FMO.IO.AMAC;

using FMO.Models;
using FMO.Schedule;
using FMO.Settings;
using FMO.Trustee;
using FMO.Utilities;
using LiteDB;
using MoT;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Loader;

namespace FMO;

internal class DelayLoader
{
    public static void Load()
    {
        Task.Run(DownloadFiles);


        WeakReferenceMessenger.Default.Send(new ToastMessage(ToastLevel.Information, "数据库自检中"));
        ///数据库自检等操作
        DatabaseAssist.SystemValidation();

        DataSelfTest();

        Task.Run(ReleaseFiles);

        Task.Run(() =>
        {

            WeakReferenceMessenger.Default.Send(new ToastMessage(ToastLevel.Information, "加载任务组件"));
            InitMission();
            WeakReferenceMessenger.Default.Send(new MainMenuEnableMessage("Task", true));

            // 加载触发器
            WeakReferenceMessenger.Default.Send(new ToastMessage(ToastLevel.Information, "加载触发器组件"));
            InitializeTriggers();

            // 加载配置
            WeakReferenceMessenger.Default.Send(new ToastMessage(ToastLevel.Information, "初始化配置"));
            SettingService.Initialize();
            SettingViewModelsInitializer.Initialize();

            SettingService.RegisterAbility("Basic", "AutoShowTodo", "自动显示待办事项", "在应用启动时自动显示待办事项", true, new AutoShowTodoFunction());


        });

        Task.Run(() =>
        {

            // 加载托管组件
            WeakReferenceMessenger.Default.Send(new ToastMessage(ToastLevel.Information, "加载托管平台组件"));
            InitializeTrustees();

            // 加载托管消息
            LoadTrusteeMessages();


            // 加载电签组件
            WeakReferenceMessenger.Default.Send(new ToastMessage(ToastLevel.Information, "加载电签组件"));
            InitializeSignings();

            // 加载信批组件
            WeakReferenceMessenger.Default.Send(new ToastMessage(ToastLevel.Information, "加载信批组件"));
            InitializeDisclosureChannels();


        });

        // 清理log
        DataHub.Register(ClearLog);
    }

    private static async Task DownloadFiles()
    {
        var basedir = AppDomain.CurrentDomain.BaseDirectory;
        var node = Path.Combine(basedir, ".playwright\\node\\win32_x64\\node.exe");
        if (!File.Exists(node))
        {
            try
            {
                using HttpClient client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                var resp = await client.GetAsync("https://gitee.com/iyu/ThorRef/releases/download/1.1/node.zip");
                var zipPath = Path.Combine(basedir, "node.zip");
                using (var fs = new FileStream(zipPath, FileMode.Create))
                    await resp.Content.CopyToAsync(fs);
                ZipFile.ExtractToDirectory(zipPath, Path.Combine(basedir, ".playwright"));
                File.Delete(zipPath);
            }
            catch (Exception e)
            {
                Logg.Error(e, "下载node失败");
            }
        }

        var dir = new DirectoryInfo(Path.Combine(basedir, "modelfiles"));
        if (!dir.Exists || dir.GetFiles("*.onnx").Length < 3)
        {
            try
            {
                using HttpClient client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                var resp = await client.GetAsync("https://gitee.com/iyu/ThorRef/releases/download/1.1/modelfiles.zip");
                var zipPath = Path.Combine(basedir, "modelfiles.zip");
                using (var fs = new FileStream(zipPath, FileMode.Create))
                    await resp.Content.CopyToAsync(fs);
                ZipFile.ExtractToDirectory(zipPath, basedir);
                File.Delete(zipPath);
            }
            catch (Exception e)
            {
                Logg.Error(e, "下载 onnx 失败");
            }
        }
    }

    private static Assembly? Default_Resolving(AssemblyLoadContext ctx, AssemblyName name)
    {
        var file = name.Name + ".dll";
        var path = Directory.EnumerateFiles(AppContext.BaseDirectory, file, SearchOption.AllDirectories).FirstOrDefault();
        return string.IsNullOrWhiteSpace(path) ? null : ctx.LoadFromAssemblyPath(path);
    }

    private static void LoadTrusteeMessages()
    {
        using var db = DbHelper.Base();
        var data = db.GetCollection<TrusteeWorker.WorkReturn>(TrusteeWorker.TableRaisingBalance).FindAll().ToArray();
        if (data.Length > 0) WeakReferenceMessenger.Default.Send(new TrusteeWorkResult(nameof(ITrustee.QueryRaisingBalance), data));


    }

    private static void InitMission()
    {
        // 1. 获取主程序目录 +   子文件夹
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string disDir = Path.Combine(baseDir, "mission");


        if (!Directory.Exists(disDir))
        {
            Logg.Warning("mission 目录不存在，退出加载");

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
                Logg.Error($"加载mission组件失败，错误：{ex.Message}");
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
            Logg.Warning("esign 目录不存在，退出加载");
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
                Logg.Error($"注册电子签名组件失败，错误：{ex.Message}");
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
            Logg.Error($"注册电子签名组件失败：{type.Name}，错误：{ex.Message}");
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
            Logg.Warning("trustee 目录不存在，退出加载");
            WeakReferenceMessenger.Default.Send(new MainMenuEnableMessage("Trustee", true));
            return;
        }

        // 2. 获取所有 dll 文件
        string[] dllFiles = Directory.GetFiles(trusteeDir, "*.dll", SearchOption.TopDirectoryOnly);

        foreach (var dllPath in dllFiles)
        {
            try
            {
                if (!VerifyDll(dllPath))
                {
                    Logg.Error($"验证托管组件失败，可能未签名或签名无效，路径：{dllPath}");
                    continue;
                }
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
                Logg.Error($"，错误：{ex.Message}");
            }
        }


        //启动api
        TrusteeGallay.Initialize();
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
            //var assistInstance = Activator.CreateInstance(type) as ITrustee;
            var vmInstance = Activator.CreateInstance(vmType) as TrusteeViewModelBase;

            // 9. 自动注册
            TrusteeGallay.Register(vmInstance!);

        }
        catch (Exception ex)
        {
            Logg.Error(ex, $"加载托管组件失败：{type.Name}");
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
            Logg.Warning("disclosure 目录不存在，退出加载");
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
                Logg.Error($"注册信批组件失败，错误：{ex.Message}");
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
            Logg.Error($"注册信批组件失败：{type.Name}，错误：{ex.Message}");
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
            Logg.Warning("trigger 目录不存在，退出加载");

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
                        var method = type.GetMethod("ModuleInitialize", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
                        if (method is not null) method.Invoke(null, null);
                    }
                }
            }
            catch (Exception ex)
            {
                Logg.Error($"加载Trigger组件失败，错误：{ex.Message}");
            }
        }


        //WeakReferenceMessenger.Default.Send(new MainMenuEnableMessage("Disclosure", true));
    }
    #endregion


    /// <summary>
    /// 数据自检
    /// </summary>
    public static void DataSelfTest()
    {
        var db = DbHelper.Base();
        var c = db.GetCollection<Fund>().FindAll().ToArray();
        db.Dispose();


        Task[] t = [Task.Run(async() => await SyncFundsFromAmac(c) ),
                    Task.Run(() => DataTracker.CheckFundFolder(c)),
                    Task.Run(()=> DataTracker.CheckTAMissOwner()),
        ];

        Task.WaitAll(t);
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

                        // 初始化flow和factor
                        try { FundHelper.InitNew(f); } catch (Exception e) { Logg.Error(e); }

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
                Logg.Error($"同步公示信息失败：{ex}");
                HandyControl.Controls.Growl.Error($"同步基金公示信息失败");
            }
        }
    }





    private static bool VerifyDll(string dll)
    {
#if DEBUG 
        return true;
#else
        return SecurityHelper.IsAuthorSigned(dll);
//return AssemblyName.GetAssemblyName(dll).GetPublicKeyToken().SequenceEqual(new byte[] { 0xA9, 0x4A, 0x3A, 0xC4, 0x0B, 0x3F, 0xC1, 0xBE });
#endif
    }


    private static void ClearLog(NewDay d)
    {
        if (!File.Exists("data\\platformlog.db"))
        {
            using var fs = new FileStream("data\\platformlog.db", FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (fs.Length > 1024 * 1024 * 500)
            {
                var db = new LiteDatabase(@$"FileName=data\platformlog.db;Connection=Shared");
                // 条目
                var total = db.GetCollection<TrusteeCallHistory>().Count();
                var mid = db.GetCollection<TrusteeCallHistory>().Query().Skip(total / 2).Limit(1).FirstOrDefault();
                if (mid is not null)
                    db.GetCollection<TrusteeCallHistory>().DeleteMany(x => x.Time.Date < mid.Time);
            }
        }
    }


    private static void ReleaseFiles()
    {
        if (!Directory.Exists(@"files\brochure"))
            Directory.CreateDirectory(@"files\brochure");

        {
            string target = @"files\brochure\.frame";
            if (!File.Exists(target) && Assembly.GetExecutingAssembly().GetManifestResourceStream("FMO.res.onepage.html") is Stream stream)
            {
                byte[] buffer = new byte[stream.Length];
                stream.ReadExactly(buffer, 0, buffer.Length);
                File.WriteAllBytes(target, buffer);
            }
        }

        {
            var target = @"files\brochure\default.html";
            if (!File.Exists(target) && Assembly.GetExecutingAssembly().GetManifestResourceStream("FMO.res.defaultBrochure.html") is Stream stream)
            {
                byte[] buffer = new byte[stream.Length];
                stream.ReadExactly(buffer, 0, buffer.Length);
                File.WriteAllBytes(target, buffer);
            }
        }



    }
}
