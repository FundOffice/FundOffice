using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace LearnAssist;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public App()
    {
        //if(!IsEnvironmentSafe()) this.Shutdown();

        if (DateTime.Now.Year + 1010 > 3036 || DateTime.Now.Month > 5) this.Shutdown();

#if RELEASE
    // 设置工作目录 
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

    }

    public static bool IsEnvironmentSafe()
    {
        if (Debugger.IsAttached) return false;
        if (Process.GetCurrentProcess().ProcessName.IndexOf("dbg", StringComparison.OrdinalIgnoreCase) >= 0) return false;

        //try
        //{
        //    using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystem");
        //    foreach (var obj in searcher.Get())
        //    {
        //        var model = obj["Model"]?.ToString() ?? "";
        //        var manufacturer = obj["Manufacturer"]?.ToString() ?? "";
        //        if (model.IndexOf("virtual", StringComparison.OrdinalIgnoreCase) >= 0 ||
        //            manufacturer.IndexOf("vmware", StringComparison.OrdinalIgnoreCase) >= 0 ||
        //            manufacturer.IndexOf("virtualbox", StringComparison.OrdinalIgnoreCase) >= 0)
        //            return false;
        //    }
        //}
        //catch { /* 忽略 WMI 异常 */ }

        // 时间回拨检测
        var now = DateTime.UtcNow;
        var fileTime = DateTime.FromFileTimeUtc(DateTime.UtcNow.ToFileTimeUtc());
        if (Math.Abs((now - fileTime).TotalSeconds) > 300) return false;

        return true;
    }
}
