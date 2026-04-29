using System.Windows;


internal static class ResourceModuleInitializer
{

    public static void Initialize()
    {
        AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoaded;
    }

    /// <summary>
    /// 每当有程序集加载时触发
    /// </summary>
    public static void OnAssemblyLoaded(object? sender, AssemblyLoadEventArgs args)
    {
        var assembly = args.LoadedAssembly;

        try
        {
            // 跳过系统/动态程序集
            if (assembly.IsDynamic || string.IsNullOrEmpty(assembly.Location))
                return;
            if (assembly.FullName!.StartsWith("System.")
                || assembly.FullName.StartsWith("Microsoft.")
                || assembly.FullName.StartsWith("Windows."))
                return;

            var name = assembly.GetName().Name;


            var uri = new Uri($"pack://application:,,,/{name};component/dt.xaml", UriKind.Absolute);
            
            var dict = new ResourceDictionary { Source = uri };
            Application.Current.Resources.MergedDictionaries.Add(dict);

        }
        catch
        {
            // 不存在 dt.xaml 就静默跳过
        }
    }
    
}

