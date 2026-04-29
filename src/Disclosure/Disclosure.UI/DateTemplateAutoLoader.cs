using FMO.Logging;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
 

namespace FMO.Disclosure;


/// <summary>
/// DLL 被加载时 自动执行
/// 无需任何调用！自动注册插件资源
/// </summary>
internal static class DateTemplateAutoLoader
{
    [ModuleInitializer]
    public static void Initialize()
    {
        // 延迟到 Application 就绪时执行
        if (Application.Current == null)
        {
            // WPF 应用还没启动好，等它启动
            Dispatcher.CurrentDispatcher.BeginInvoke(new Action(RegisterResources));
            return;
        }

        RegisterResources();
    }

    /// <summary>
    /// 自动把插件 XAML 资源合并到主程序资源字典
    /// </summary>
    private static void RegisterResources()
    {
        try
        {
            var assemblyName = "Disclosure.UI";
            var targetFolder = "templates";



            string[] xamlFiles = ["WorkConfigTemplates.xaml", "ChannelConfigTemplates.xaml",];

            foreach (var file in xamlFiles)
            {
                var uri = new Uri($"pack://application:,,,/{assemblyName};component/{targetFolder}/{file}", UriKind.Absolute);

                Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = uri });
            }
        }
        catch (Exception ex)
        {
            LogEx.Error(ex);
        }
    }
}
