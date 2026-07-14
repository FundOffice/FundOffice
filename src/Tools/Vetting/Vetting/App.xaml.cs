using Microsoft.Win32;
using System.IO;
using System.Windows;

namespace Vetting;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

#if RELEASE
        using (var key = Registry.CurrentUser.CreateSubKey(@$"Software\Nexus"))
#else
        using (var key = Registry.CurrentUser.CreateSubKey(@$"Software\Nexus\Debug"))
#endif
        {
            if (key.GetValue("WorkingFolder") is string dir && Directory.Exists(dir))
                Directory.SetCurrentDirectory(dir);
        }
    }
}
