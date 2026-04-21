using Serilog;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;

namespace FMO.Plugin;

public static class PluginManager
{
    private static List<IPlugin> Plugins { get; } = new();

    private static List<PluginLoadContext> PluginsLoadContexts { get; } = new();

    static PluginManager()
    {
        LoadAll();
    }

    private static void LoadAll()
    {
        var folder = AppDomain.CurrentDomain.BaseDirectory;

        var directoryInfos = new DirectoryInfo ("plugins").GetDirectories();


        foreach (var di in directoryInfos)
        {
            try
            {
                // 先找def.json
                var fi = new FileInfo(Path.Combine(di.FullName, "def.json"));
                if (!fi.Exists) continue;

                using var sr = new StreamReader(fi.FullName);

                var def = JsonSerializer.Deserialize<PluginDefinition>(sr.ReadToEnd());
                if (def is null)
                {
                    Log.Error($"Load Plugin [{di.Name}] Error: 无法解析配置");
                    continue;
                }

                def.Folder = di.FullName;

                Load(def);

                //var context = new PluginLoadContext(def);

                //PluginsLoadContexts.Add(context);
                //Plugins.AddRange(context.Plugins);
            }
            catch (Exception e)
            {
                Log.Error($"Load Plugin [{di.Name}] Error: {e}");
            }
        }

    }

    public static void Init()
    {

    }

    private static void Load(PluginDefinition def)
    {
        string pluginPath = Path.Combine(def.Folder, def.EndPoint);
        //_resolver = new AssemblyDependencyResolver(pluginPath);

        var list = new List<IPlugin>();


        var di = new DirectoryInfo(def.Folder);
        Func<AssemblyLoadContext, AssemblyName, Assembly?> fun = (context, name) =>
        {
            string assemblyFileName = name.Name + ".dll";
            var path = Path.Combine(di.FullName, assemblyFileName);
            if(File.Exists(path)) return context.LoadFromAssemblyPath(path);
            else return null;
        };
        AssemblyLoadContext.Default.Resolving += fun;
        foreach (var item in di.GetFiles("*.dll"))
        {
            if (item.Name == def.EndPoint) continue;
            AssemblyLoadContext.Default.LoadFromAssemblyPath(item.FullName);
        }
        AssemblyLoadContext.Default.Resolving -= fun;

        var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(pluginPath);

        try
        {
            foreach (Type type in assembly.GetTypes())
            {
                // 检查是否实现了 IPlugin 接口
                if (typeof(IPlugin).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                {
                    try
                    {
                        // 创建实例并调用 OnLoaded
                        var plugin = (IPlugin)Activator.CreateInstance(type)!;
                        list.Add(plugin);
                        plugin.OnLoad();
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"创建 {type.FullName} 失败：{ex.Message}");
                    }
                }
            }
        }
        catch (ReflectionTypeLoadException ex)
        {
            Log.Error($"加载 {def.EndPoint} 失败：{string.Join('\n', ex.LoaderExceptions.Select(x => x?.Message))}");
        }
        catch (Exception ex)
        {
            Log.Error($"加载 {def.EndPoint} 失败：{ex.Message}");
        }
    }

}
