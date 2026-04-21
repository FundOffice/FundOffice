
using Serilog;
using System.Reflection;
using System.Runtime.Loader;

namespace FMO.Plugin;

public class PluginLoadContext : AssemblyLoadContext
{
    private PluginDefinition _def;

    public IReadOnlyList<IPlugin> Plugins { get; private set; }

    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(PluginDefinition def) : base(isCollectible: true) // 设置为可收集，才能卸载
    {
        _def = def;

        string pluginPath = Path.Combine(def.Folder, def.EndPoint);
        _resolver = new AssemblyDependencyResolver(pluginPath);

        var list = new List<IPlugin>();

        var assembly = LoadFromAssemblyPath(pluginPath);

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
        Plugins = list.ToArray();
    }


    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null)
            return LoadFromAssemblyPath(assemblyPath);

        // 回退到默认上下文（用于加载主程序中的 dll）
        return null;
    }
}