
using FMO.TPL;
using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;


Console.OutputEncoding = System.Text.Encoding.UTF8;

// ================== 路径配置 ==================
// 1. 获取程序基目录
var rootDir = AppContext.BaseDirectory;

// 2. 向上递归查找名为 FundOffice 的目录
var fundOfficeDir = FindParentDirectory(rootDir, "FundOffice");

// 3. 找不到则抛出明确异常，方便排查问题
if (fundOfficeDir is null)
{
    throw new DirectoryNotFoundException($"未找到名为 FundOffice 的上级目录，基目录：{rootDir}");
}

// 4. 拼接最终 Templates 目录
var templatesDir = Path.Combine(fundOfficeDir, "src", "Templates");


var deftplDir = Path.Combine(templatesDir, "Build");

if (!Directory.Exists(templatesDir))
{
    Console.WriteLine("❌ Templates 目录不存在。");
    return;
}

Directory.CreateDirectory(deftplDir);

// 清空旧 zip
foreach (var zip in Directory.GetFiles(deftplDir, "*.zip"))
{
    try { File.Delete(zip); }
    catch (Exception ex) { Console.WriteLine($"⚠️ 删除旧 zip 失败: {ex.Message}"); }
}

// ================== 打印标题 ==================
Console.WriteLine("🔍 扫描 Templates 子项目...");
Console.WriteLine();
Console.WriteLine("📦 模板插件构建状态 (实时更新)");
Console.WriteLine(new string('─', 70));

var subDirs = new DirectoryInfo(templatesDir)
    .GetDirectories()
    .Where(d => d.Name != "Build" && !d.Name.StartsWith("."))
    .OrderBy(d => d.Name)
    .ToArray();

if (subDirs.Length == 0)
{
    Console.WriteLine("📭 未找到任何子项目。");
    return;
}

// 创建状态对象
var statuses = subDirs.Select(dir =>
{
    var csproj = dir.GetFiles("*.csproj").FirstOrDefault();
    var name = csproj != null ? Path.GetFileNameWithoutExtension(csproj.Name) : dir.Name;
    return new ProjectStatus(name);
}).ToList();

// 初始化状态
foreach (var s in statuses)
{
    s.Update("🟡 等待中");
}

// 打印初始状态
PrintStatuses(statuses);

// ================== 并行构建 ==================
object consoleLock = new object();
int failedCount = 0;

var options = new ParallelOptions
{
    MaxDegreeOfParallelism = Environment.ProcessorCount
};

Parallel.ForEach(statuses, options, status =>
{
    var projDir = subDirs.FirstOrDefault(d =>
        d.GetFiles("*.csproj").Any(f => Path.GetFileNameWithoutExtension(f.Name) == status.ProjectName));

    if (projDir == null)
    {
        status.Update("🔴 项目目录丢失");
        Interlocked.Increment(ref failedCount);
        PrintStatuses(statuses);
        return;
    }

    var csprojFile = projDir.GetFiles("*.csproj").FirstOrDefault();
    if (csprojFile == null)
    {
        status.Update("🔴 无 .csproj 文件");
        Interlocked.Increment(ref failedCount);
        PrintStatuses(statuses);
        return;
    }

    var csprojPath = csprojFile.FullName;

    // 读取 AssemblyName
    string assemblyName;
    try
    {
        var csprojFileName = Path.GetFileNameWithoutExtension(csprojPath);
        assemblyName = ReadAssemblyNameFromCsproj(csprojPath) ?? csprojFileName;
    }
    catch
    {
        assemblyName = status.ProjectName;
    }

    var safeAssemblyName = SanitizeFileName(assemblyName);
    status.Update("🟡 正在构建...");
    PrintStatuses(statuses);

    // 构建
    if (!BuildProject(csprojPath))
    {
        status.Update("🔴 构建失败");
        Interlocked.Increment(ref failedCount);
        PrintStatuses(statuses);
        return;
    }

    var binDir = Path.Combine(projDir.FullName, "bin", "Debug", "net10.0");
    var dllPath = Path.Combine(binDir, $"{safeAssemblyName}.dll");
    var jsonPath = Path.Combine(binDir, "def.json");

    if (!File.Exists(dllPath))
    {
        status.Update("🔴 DLL 未生成");
        Interlocked.Increment(ref failedCount);
        PrintStatuses(statuses);
        return;
    }

    if (!GenerateTemplateInfo(dllPath, jsonPath))
    {
        status.Update("🔴 生成 JSON 失败");
        Interlocked.Increment(ref failedCount);
        PrintStatuses(statuses);
        return;
    }

    var xlsxFiles = Directory.GetFiles(projDir.FullName, "*.xlsx", SearchOption.TopDirectoryOnly);
    var zipPath = Path.Combine(deftplDir, $"{safeAssemblyName}.zip");
    var filesToZip = new List<string> { dllPath, jsonPath }.Concat(xlsxFiles).ToList();

    if (!CreateZip(filesToZip, zipPath))
    {
        status.Update("🔴 打包失败");
        Interlocked.Increment(ref failedCount);
        PrintStatuses(statuses);
        return;
    }

    var zipSize = new FileInfo(zipPath).Length;
    var sizeStr = FormatSize(zipSize);
    status.Update($"🟢 成功 ({sizeStr})");
    PrintStatuses(statuses);
});

// ================== 最终统计 ==================
Console.WriteLine();
Console.WriteLine(new string('─', 70));
Console.WriteLine($"🎉 构建完成！共 {statuses.Count} 个项目，失败 {failedCount} 个");
Console.WriteLine($"📂 输出目录: {deftplDir}");
Console.WriteLine("✅ 所有插件包已生成。");
Console.WriteLine("按任意键退出...");
Console.ReadKey();

// ================== 工具方法 ==================

/// <summary>
/// 向上递归查找指定名称的父级目录
/// </summary>
/// <param name="startPath">起始路径</param>
/// <param name="targetDirName">目标目录名称</param>
/// <returns>找到的目录完整路径，找不到返回null</returns>
static string? FindParentDirectory(string startPath, string targetDirName)
{
    var currentDir = new DirectoryInfo(startPath);

    // 循环向上遍历，直到到达磁盘根目录
    while (currentDir != null)
    {
        // 匹配目标目录名称（不区分大小写，更健壮）
        if (currentDir.Name.Equals(targetDirName, StringComparison.OrdinalIgnoreCase))
        {
            return currentDir.FullName;
        }

        currentDir = currentDir.Parent;
    }

    return null;
}

static string? ReadAssemblyNameFromCsproj(string csprojPath)
{
    if (!File.Exists(csprojPath)) return null;

    try
    {
        string content = File.ReadAllText(csprojPath);
        var match = Regex.Match(content,
            @"<AssemblyName\s*(?:[^>]*)?\s*>
                (?<name>[^<]+)
              </AssemblyName>",
            RegexOptions.IgnorePatternWhitespace | RegexOptions.Singleline | RegexOptions.IgnoreCase);

        return match.Success ? match.Groups["name"].Value.Trim() : null;
    }
    catch
    {
        return null;
    }
}

static bool BuildProject(string csprojPath)
{
    var psi = new ProcessStartInfo
    {
        FileName = "dotnet",
        Arguments = $"build \"{csprojPath}\" --nologo --verbosity minimal",
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true
    };

    using var process = Process.Start(psi);
    process?.WaitForExit();
    return process?.ExitCode == 0;
}

static bool GenerateTemplateInfo(string dllPath, string jsonPath)
{
    Assembly assembly;
    try
    {
        assembly = Assembly.LoadFrom(dllPath);
    }
    catch { return false; }

    var exporterType = assembly.GetTypes()
        .FirstOrDefault(t => typeof(IExporter).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract);

    if (exporterType == null) return false;

    IExporter instance;
    try
    {
        instance = (IExporter)Activator.CreateInstance(exporterType)!;
    }
    catch { return false; }

    var info = new TemplateInfo(
        instance.Id,
        instance.Name,
        instance.Description,
        exporterType.FullName!,
        instance.Suit,
        instance.Meta,
        Path.GetFileName(dllPath)
    );

    var json = JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true });
    try
    {
        File.WriteAllText(jsonPath, json);
        return true;
    }
    catch { return false; }
}

static bool CreateZip(List<string> files, string zipPath)
{
    try
    {
        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        foreach (var file in files.Distinct())
        {
            if (File.Exists(file))
            {
                archive.CreateEntryFromFile(file, Path.GetFileName(file));
            }
        }
        return true;
    }
    catch { return false; }
}

static string SanitizeFileName(string name)
{
    var invalid = Path.GetInvalidFileNameChars();
    return string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
}

static string FormatSize(long bytes)
{
    string[] s = { "B", "KB", "MB", "GB" };
    int i = 0;
    double size = bytes;
    while (size >= 1024 && i < s.Length - 1)
    {
        size /= 1024;
        i++;
    }
    return $"{size:0.##} {s[i]}";
}

// 打印所有状态（使用锁确保线程安全）
static void PrintStatuses(List<ProjectStatus> statuses)
{
    lock (statuses)
    {
        // 清空控制台状态区域（假设状态从第6行开始）
        int startLine = 5; // 标题占用了5行
        Console.SetCursorPosition(0, startLine);

        for (int i = 0; i < statuses.Count; i++)
        {
            Console.WriteLine(new string(' ', Console.WindowWidth - 1));
        }

        // 重新打印所有状态
        Console.SetCursorPosition(0, startLine);
        foreach (var status in statuses)
        {
            Console.WriteLine($"{status.ProjectName,-30} → {status.CurrentStatus,-40}");
        }

        // 将光标移到最后，避免干扰
        Console.SetCursorPosition(0, startLine + statuses.Count);
    }
}

// ================== 状态显示类 ==================
class ProjectStatus
{
    public string ProjectName { get; }
    public string CurrentStatus { get; private set; }

    public ProjectStatus(string projectName)
    {
        ProjectName = projectName;
        CurrentStatus = "等待中";
    }

    public void Update(string status)
    {
        CurrentStatus = status;
    }
}