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

            // 1. 构造资源流名称：程序集名.g.resources
            var resourceName = $"{assembly.GetName().Name}.g.resources";

            var uri = new Uri($"pack://application:,,,/{name};component/dt.xaml", UriKind.Absolute);
            // 2. 尝试获取资源流 (如果返回 null，说明该 DLL 根本没嵌入任何 WPF 资源，直接跳过，无异常)
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null) return;

                try
                {
                    // 3. 读取资源列表
                    var reader = new System.Resources.ResourceReader(stream);
                    var targetBaml =  "dt.baml";

                    foreach (System.Collections.DictionaryEntry entry in reader)
                    {
                        if (entry.Key is string key)
                        {
                            // 资源在 DLL 内部通常叫 dt.baml 或 views/dt.baml
                            // 检查是否以目标文件名结尾
                            if (key.EndsWith(targetBaml, StringComparison.OrdinalIgnoreCase))
                            {
                                var dict = new ResourceDictionary { Source = uri };
                                Application.Current.Resources.MergedDictionaries.Add(dict);
                            }
                        }
                    }
                }
                catch
                {
                    // 读取流失败，视为不存在
                }
            }

        }
        catch
        {
            // 不存在 dt.xaml 就静默跳过
        }
    }

}

