using Microsoft.Win32;
using System.IO;
using System.Windows;

namespace FMO.FeeCalc;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public App()
    {
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
    }
}
