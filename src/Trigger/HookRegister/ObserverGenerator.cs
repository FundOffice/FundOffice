using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace TriggerGenerators;

[Generator(LanguageNames.CSharp)]
public sealed class ObserverGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var classDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsSyntaxTarget(node),
                transform: static (ctx, _) => GetSemanticTarget(ctx))
            .Where(static info => info is not null)
            .Collect();

        var compilationAndClasses = context.CompilationProvider.Combine(classDeclarations);
        context.RegisterSourceOutput(compilationAndClasses, static (spc, source) =>
            Execute(source.Left, source.Right, spc));
    }

    private static bool IsSyntaxTarget(SyntaxNode node) =>
        node is ClassDeclarationSyntax { Modifiers: var mods } cls
            && mods.Any(SyntaxKind.PartialKeyword)
            && cls.BaseList?.Types.Count > 0;

    private static ClassInfo? GetSemanticTarget(GeneratorSyntaxContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol classSymbol)
            return null;

        var trackerInterfaces = classSymbol.AllInterfaces
            .Where(i => i.Name == "ITracker" && i.Arity == 1)
            .ToArray();

        if (trackerInterfaces.Length == 0) return null;

        var genericTypes = new List<string>();
        var isEnumerableFlags = new List<bool>();

        foreach (var tracker in trackerInterfaces)
        {
            var typeArg = tracker.TypeArguments[0];
            genericTypes.Add(typeArg.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

            bool isEnumerable = ImplementsIEnumerable(typeArg, context.SemanticModel.Compilation);
            isEnumerableFlags.Add(isEnumerable);
        }

        var hasBaseClass = classSymbol.BaseType?.SpecialType != SpecialType.System_Object;

        // 🔹 检查当前类及继承链中是否存在 debouncer 字段
        var hasDebouncerField = HasFieldInHierarchy(classSymbol, "debouncer");

        return new ClassInfo(
            ClassName: classSymbol.Name,
            Namespace: classSymbol.ContainingNamespace.IsGlobalNamespace
                ? string.Empty
                : classSymbol.ContainingNamespace.ToDisplayString(),
            GenericTypes: genericTypes.ToArray(),
            IsEnumerableTypes: isEnumerableFlags.ToArray(),
            HasBaseClass: hasBaseClass,
            HasDebouncerField: hasDebouncerField);  // 🔹 新增
    }

    // 🔹 新增：检查类型及其基类中是否包含指定名称的字段
    private static bool HasFieldInHierarchy(INamedTypeSymbol typeSymbol, string fieldName)
    {
        var current = typeSymbol;
        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            // GetMembers 返回所有可访问的成员（包括 private/protected/public）
            var members = current.GetMembers(fieldName);
            if (members.Any(m => m.Kind == SymbolKind.Field))
            {
                return true;
            }
            current = current.BaseType;
        }
        return false;
    }

    private static bool ImplementsIEnumerable(ITypeSymbol typeSymbol, Compilation compilation)
    {
        var iEnumerableType = compilation.GetTypeByMetadataName("System.Collections.IEnumerable");
        var iEnumerableGenericType = compilation.GetTypeByMetadataName("System.Collections.Generic.IEnumerable`1");

        if (typeSymbol is INamedTypeSymbol namedType)
        {
            if (SymbolEqualityComparer.Default.Equals(typeSymbol.OriginalDefinition, iEnumerableGenericType) ||
                SymbolEqualityComparer.Default.Equals(typeSymbol, iEnumerableType))
            {
                return true;
            }
        }

        foreach (var iface in typeSymbol.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, iEnumerableGenericType) ||
                SymbolEqualityComparer.Default.Equals(iface, iEnumerableType))
            {
                return true;
            }
        }

        return false;
    }

    private static void Execute(
        Compilation compilation,
        ImmutableArray<ClassInfo?> classes,
        SourceProductionContext context)
    {
        foreach (var info in classes)
        {
            if (info is null) continue;
            var source = GenerateCode(info);
            var fileName = string.IsNullOrEmpty(info.Namespace)
                ? $"{info.ClassName}.g.cs"
                : $"{info.Namespace}.{info.ClassName}.g.cs";
            context.AddSource(fileName, SourceText.From(source, Encoding.UTF8));
        }
    }

    private static string GenerateCode(ClassInfo info)
    {
        var dataArrivalMethods = string.Join("\n\n",
            info.GenericTypes.Select((type, index) =>
            {
                var isEnumerable = info.IsEnumerableTypes[index];
                var validation = isEnumerable
                    ? "if (obj is null || !obj.Any()) return;"
                    : "if (obj is null) return;";

                // 🔹 根据 HasDebouncerField 生成条件调用代码
                var debouncerCall = info.HasDebouncerField
                    ? "debouncer?.Invoke();"
                    : string.Empty;

                return $$"""
    private partial void OnDataArrival({{type}} obj);

    public void DataArrival{{index + 1}}({{type}} obj)
    {
        try
        {
            // 检验数据
            {{validation}}

            semaphoreSlim.Wait();
            OnDataArrival(obj);
            {{debouncerCall}}
        }
        catch (Exception e) { Logg.Error(e); }
        finally { semaphoreSlim.Release(); }
    }
""";
            }));

        var registerItems = string.Join(", ",
            info.GenericTypes.Select((_, index) =>
                $"DataHub.Register(DataArrival{index + 1})"));

        var inheritanceClause = info.HasBaseClass ? string.Empty : " : DataObserver";

        // 🔹 如果父类有 debouncer，构造函数中不需要初始化
        var constructorBody = !info.HasDebouncerField
            ? string.Empty
            : "debouncer = new Debouncer(Verify, 1000);";

        return $$"""
// <auto-generated/>
#nullable enable

using System;
using System.Linq;
using System.Threading;

using FMO.Models;
using FMO.Utilities;
using MoT;

namespace {{info.Namespace}};

public partial class {{info.ClassName}}{{inheritanceClause}}
{

{{dataArrivalMethods}}

 
    /// <summary>
    /// 注册数据订阅
    /// </summary>
    protected override void RegisterHandler()
    {
        Dispose();
        disposables = [ {{registerItems}} ];
    }

   
}
""";
    }

    // 🔹 ClassInfo 添加 HasDebouncerField 字段
    private sealed record ClassInfo(
        string ClassName,
        string Namespace,
        string[] GenericTypes,
        bool[] IsEnumerableTypes,
        bool HasBaseClass,
        bool HasDebouncerField);  // 🔹 新增
}