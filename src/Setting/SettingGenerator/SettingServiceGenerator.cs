#nullable enable
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml.Linq;

namespace VerifySettingGenerator;

[Generator]
public class VerifySettingGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1. 监听编译变化，提取所有符合条件的 Rule 元数据（含命名空间）
        var rulesProvider = context.CompilationProvider.Select((comp, ct) => ExtractRules(comp, ct));

        // 2. 注册源码输出
        context.RegisterSourceOutput(rulesProvider, (spc, rules) => GenerateSource(spc, rules));
    }

    /// <summary>
    /// 遍历当前工程及所有引用 DLL，提取标记了 VerifySettingUnit 且继承自 VerifyRule 的类型
    /// </summary>
    private static ImmutableArray<VerifyRuleInfo> ExtractRules(Compilation comp, CancellationToken ct)
    {
        var builder = ImmutableArray.CreateBuilder<VerifyRuleInfo>();
        var visitedNamespaces = new HashSet<INamespaceSymbol>(SymbolEqualityComparer.Default);

        // 获取当前程序集 + 所有引用程序集
        var assemblies = comp.References
            .Select(r => comp.GetAssemblyOrModuleSymbol(r) as IAssemblySymbol)
            .Where(a => a is not null)
            .Cast<IAssemblySymbol>()
            .Prepend(comp.Assembly);

        foreach (var asm in assemblies)
        {
            ct.ThrowIfCancellationRequested();
            TraverseNamespace(asm.GlobalNamespace, builder, visitedNamespaces, ct);
        }

        return builder.ToImmutable();
    }

    private static void TraverseNamespace(
        INamespaceSymbol ns,
        ImmutableArray<VerifyRuleInfo>.Builder builder,
        HashSet<INamespaceSymbol> visited,
        CancellationToken ct)
    {
        if (!visited.Add(ns)) return;
        ct.ThrowIfCancellationRequested();

        foreach (var member in ns.GetMembers())
        {
            if (member is INamespaceSymbol childNs)
            {
                TraverseNamespace(childNs, builder, visited, ct);
            }
            else if (member is INamedTypeSymbol type && type.TypeKind == TypeKind.Class && !type.IsAbstract)
            {
                // 检查是否继承自 VerifyRule
                if (!InheritsFromVerifyRule(type)) continue;

                // 检查是否包含 VerifySettingUnitAttribute
                var attr = type.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name == "VerifySettingUnitAttribute");
                if (attr is null) continue;

                // 安全提取特性参数（处理缺省值与命名参数）
                var title = attr.ConstructorArguments.Length > 0 ? attr.ConstructorArguments[0].Value as string ?? type.Name : type.Name;
                var desc = attr.ConstructorArguments.Length > 1 ? attr.ConstructorArguments[1].Value as string ?? string.Empty : string.Empty;
                var enable = attr.ConstructorArguments.Length > 2 ? attr.ConstructorArguments[2].Value is bool b && b : true;

                foreach (var named in attr.NamedArguments)
                {
                    if (named.Key == "Description") desc = named.Value.Value as string ?? desc;
                    if (named.Key == "Enable") enable = named.Value.Value is bool bb ? bb : enable;
                }

                // 记录完整命名空间，用于后续生成完全限定名实例化
                var nsName = type.ContainingNamespace?.ToString() ?? string.Empty;
                builder.Add(new VerifyRuleInfo(type.Name, nsName, title, desc, enable));
            }
        }
    }

    private static bool InheritsFromVerifyRule(INamedTypeSymbol type)
    {
        var current = type.BaseType;
        while (current is not null)
        {
            if (current.Name == "VerifyRule") return true;
            current = current.BaseType;
        }
        return false;
    }

    /// <summary>
    /// 生成 SettingService 分部类代码
    /// </summary>
    private static void GenerateSource(SourceProductionContext spc, ImmutableArray<VerifyRuleInfo> rules)
    {
        if (rules.IsDefaultOrEmpty) return;

        // 辅助：安全转义字符串字面量
        static string Esc(string? v) => v is null ? "\"\"" : $"@\"{v.Replace("\"", "\"\"")}\"";

        // 1. 初始化配置与实例创建代码块
        var initBlocks = string.Join("\n", rules.Select(r =>
        {
            var fqName = string.IsNullOrEmpty(r.Namespace) ? r.ClassName : $"{r.Namespace}.{r.ClassName}";
            return $$"""
                    InitVerifyUnit(builder, exist, "{{r.ClassName}}", {{Esc(r.Title)}}, 
                        {{Esc(r.Description)}}, {{r.IsEnabled.ToString().ToLowerInvariant()}}, new {{fqName}}());
            """;
        }));

        // 2. EnableVerify Switch 分支
        var enableCases = string.Join("\n", rules.Select(r =>
        {
            var fqName = string.IsNullOrEmpty(r.Namespace) ? r.ClassName : $"{r.Namespace}.{r.ClassName}";
            return $$"""
                                case "{{r.ClassName}}":            
                                    EnableVerifyInstance(name, new {{ fqName}}()); 
                                    break; 
             """;
        }));

        // 3. 组合最终源码 (全量使用 C#12 原始字面量)
        var source = $$"""
        #nullable enable
        using System.Collections.Generic;
        using System.Collections.Immutable;
        using FMO.Trigger;

        namespace FMO.Settings;

        public partial class SettingService
        {
            public ImmutableDictionary<string, SettingUnit> VerifyRuleSection { get; set; } = [];
            private Dictionary<string, VerifyRule> VerifyRuleObject { get; set; } = [];

            private void InitVerifySection()
            {
                // 安全加载已有配置，防止 Load 返回 null
                var exist = Load(SettingSections.VerifyRule)?.ToDictionary(x=>x.Name, x=>x) ?? [];
                var builder = ImmutableDictionary.CreateBuilder<string, SettingUnit>();

        {{initBlocks}}

                VerifyRuleSection = builder.ToImmutable();
            }


            private void InitVerifyUnit(ImmutableDictionary<string, SettingUnit>.Builder builder, Dictionary<string, SettingUnit> exist, string  key, string title, string desc, bool isEnable, VerifyRule instance)
            {
                var defaultUnit = new VerifyRuleUnit
                {
                    Name = key,
                    Section = SettingSections.VerifyRule,
                    Title = title,
                    Description = desc,
                    Data = new VerifyRuleUnitData { IsEnabled = isEnable }
                };
                builder[key] = exist.TryGetValue(key, out var ex) ? ex : defaultUnit;
        
                // 仅对启用状态的规则进行实例化与启动
                if (builder[key] is VerifyRuleUnit u && u.Data?.IsEnabled == true)
                    EnableVerifyInstance(key, instance); 
            }

            private void EnableVerifyInstance(string name, VerifyRule instance)
            {
                instance.Init();
                instance.Start();
                VerifyRuleObject[name] = instance;
            }

            public void EnableVerify(string name)
            {
                if (VerifyRuleSection.TryGetValue(name, out var unit) && unit is VerifyRuleUnit u)
                {
                    // Null 安全防御：确保 Data 对象存在
                    u.Data ??= new VerifyRuleUnitData();
                    u.Data.IsEnabled = true;

                    if (!VerifyRuleObject.ContainsKey(name))
                    {
                        switch (name)
                        {
         {{enableCases}}
                            default: break;
                        }
                    }
                    Save(u);
                }
            }

            public void DisableVerify(string name)
            {
                if (VerifyRuleSection.TryGetValue(name, out var unit) && unit is VerifyRuleUnit u)
                {
                    u.Data ??= new VerifyRuleUnitData();
                    u.Data.IsEnabled = false;

                    if (VerifyRuleObject.TryGetValue(name, out var obj))
                    {
                        obj.Stop();
                        VerifyRuleObject.Remove(name); // 清理实例引用，防止内存泄漏与重复启动
                    }

                    Save(u);
                }
            }
        }
        """;

    spc.AddSource("SettingService.VerifyRules.g.cs", SourceText.From(source, Encoding.UTF8));
    }
}

/// <summary>
/// 用于增量缓存的不可变数据模型
/// </summary>
internal sealed record VerifyRuleInfo(string ClassName, string Namespace, string Title, string Description, bool IsEnabled);