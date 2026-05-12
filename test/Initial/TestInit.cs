using FMO.Utilities;
using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.Intrinsics.X86;

namespace Initial;

#pragma warning disable CA1416 // 验证平台兼容性
public class TestInit
{
    public static void SetAsDebug()
    {

        using (var key = Registry.CurrentUser.CreateSubKey(@$"Software\Nexus\Debug"))
        {
            if (key.GetValue("WorkingFolder") is string dir)
            {
                Directory.SetCurrentDirectory(dir);

                Debug.WriteLine("=====================================================");

                Debug.WriteLine($"当前工作目录{dir}");
            }
            DbHelper.Init();
            Debug.WriteLine("=====================================================");
        }
    }

    public static void SetAsRelease()
    {

        using (var key = Registry.CurrentUser.CreateSubKey(@$"Software\Nexus"))
        {
            if (key.GetValue("WorkingFolder") is string dir)
            {
                Directory.SetCurrentDirectory(dir);

                Debug.WriteLine("=====================================================");

                Debug.WriteLine($"当前工作目录{dir}");
            }
            DbHelper.Init();
            Debug.WriteLine("=====================================================");
        }
    }
}
#pragma warning restore CA1416 // 验证平台兼容性
