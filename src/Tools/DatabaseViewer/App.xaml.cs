using Microsoft.Win32;
using System.IO;
using System.Windows;

namespace DatabaseViewer;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public App()
    {
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
    }
}
