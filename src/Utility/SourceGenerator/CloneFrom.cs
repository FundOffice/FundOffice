// File: CloneFromGenerator.cs
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace SourceGenerators;

[Generator]
public class CloneFromGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1. 找到所有 .CloneFrom(...) 调用
        var cloneFromCalls = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) =>
                {
                    if (node is InvocationExpressionSyntax invocation)
                    {
                        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                            memberAccess.Name.Identifier.Text == "CloneFrom")
                        {
                            return true;
                        }
                    }
                    return false;
                },
                transform: static (ctx, _) =>
                {
                    var invocation = (InvocationExpressionSyntax)ctx.Node;
                    var semanticModel = ctx.SemanticModel;

                    var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
                    var thisArg = memberAccess.Expression;
                    var sourceArg = invocation.ArgumentList.Arguments.FirstOrDefault();

                    if (sourceArg == null) return default; // 返回 null 值

                    var thisType = semanticModel.GetTypeInfo(thisArg).Type;
                    var sourceType = semanticModel.GetTypeInfo(sourceArg.Expression).Type;

                    if (thisType == null || sourceType == null) return default;

                    // 检查是否有现成的实现（避免重复生成）
                    var symbolInfo = semanticModel.GetSymbolInfo(invocation);
                    if (symbolInfo.Symbol is IMethodSymbol method && !IsPartialDefinition(method))
                        return default; // 已有实现，不生成

                    return new CallSite
                    {
                        ThisType = thisType,
                        SourceType = sourceType,
                        Syntax = invocation
                    };
                })
            .Where(call => call != null); // 过滤掉 null

        // 2. 结合 Compilation
        var compilationAndCalls = context.CompilationProvider.Combine(cloneFromCalls.Collect());

        // 3. 生成代码
        context.RegisterSourceOutput(compilationAndCalls, Execute);
    }

    private static void Execute(
        SourceProductionContext context,
        (Compilation, ImmutableArray<CallSite>) source)
    {
        var (compilation, calls) = source;
        if (calls.IsDefaultOrEmpty) return;

        var uniquePairs = new HashSet<(INamedTypeSymbol To, INamedTypeSymbol From)>(SymbolPairComparer.Instance);

        foreach (var call in calls)
        {
            if (call.ThisType is not INamedTypeSymbol toType ||
                call.SourceType is not INamedTypeSymbol fromType)
                continue;

            if (!IsValidCandidate(toType) || !IsValidCandidate(fromType))
                continue;

            uniquePairs.Add((toType, fromType));
        }

        if (uniquePairs.Count == 0) return;

        var methodsByNamespace = new Dictionary<string, List<(INamedTypeSymbol From, INamedTypeSymbol To, List<(IPropertySymbol, IPropertySymbol)> Props)>>();

        var typeComparer = SymbolEqualityComparer.IncludeNullability;

        foreach (var (toType, fromType) in uniquePairs)
        {
            var fromProps = GetReadableProperties(fromType);
            var toProps = GetWritableProperties(toType);

            var matched = new List<(IPropertySymbol, IPropertySymbol)>();
            foreach (var fromProp in fromProps)
            {
                var toProp = toProps.FirstOrDefault(p => p.Name == fromProp.Name);
                if (toProp == null) continue;
                if (typeComparer.Equals(fromProp.Type, toProp.Type))
                {
                    matched.Add((fromProp, toProp));
                }
            }

            if (matched.Count == 0) continue;

            var ns = toType.ContainingNamespace?.ToDisplayString() ?? "Generated";

            if (!methodsByNamespace.TryGetValue(ns, out var list))
            {
                list = new();
                methodsByNamespace[ns] = list;
            }

            list.Add((fromType, toType, matched));
        }

        foreach (var kvp in methodsByNamespace)
        {
            var ns = kvp.Key;
            var methods = kvp.Value;
            var code = GenerateCodeForNamespace(ns, methods);
            var safeNs = ns.Replace(".", "_").Replace("::", "_");
            context.AddSource($"CloneExtensions.{safeNs}.g.cs", code);
        }
    }

    private static List<IPropertySymbol> GetReadableProperties(INamedTypeSymbol type) =>
        type.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(p => p.DeclaredAccessibility == Accessibility.Public)
            .Where(p => p.GetMethod?.DeclaredAccessibility == Accessibility.Public)
            .Where(p => !p.IsStatic)
            .ToList();

    private static List<IPropertySymbol> GetWritableProperties(INamedTypeSymbol type) =>
        type.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(p => p.DeclaredAccessibility == Accessibility.Public)
            .Where(p => p.SetMethod?.DeclaredAccessibility == Accessibility.Public)
            .Where(p => !p.IsStatic)
            .ToList();

    private static bool IsValidCandidate(INamedTypeSymbol type)
    {
        if (type.IsStatic ||
            type.TypeKind is not TypeKind.Class and not TypeKind.Struct ||
            type is { IsImplicitlyDeclared: true } ||
            type.Name.StartsWith("<"))
            return false;

        if (type.DeclaredAccessibility is not Accessibility.Public and not Accessibility.Internal)
            return false;

        var typeName = type.ToDisplayString();
        if (typeName.StartsWith("System.") ||
            typeName.StartsWith("Microsoft.") ||
            typeName is "string" or "object" or "decimal" or "DateTime" or "TimeSpan" or "Guid")
            return false;

        if (ImplementsGenericInterface(type, "System.Collections.Generic.IEnumerable`1") ||
            ImplementsGenericInterface(type, "System.Collections.IEnumerable"))
            return false;

        return true;
    }

    private static bool ImplementsGenericInterface(INamedTypeSymbol type, string fullyQualifiedInterface)
    {
        return type.AllInterfaces.Any(i =>
            i.ToDisplayString().StartsWith(fullyQualifiedInterface));
    }

    private static bool IsPartialDefinition(IMethodSymbol method)
    {
        var sourceMethod = method?.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() as MethodDeclarationSyntax;
        return sourceMethod?.Modifiers.Any(SyntaxKind.PartialKeyword) == true;
    }

    private static SourceText GenerateCodeForNamespace(
        string namespaceName,
        List<(INamedTypeSymbol From, INamedTypeSymbol To, List<(IPropertySymbol, IPropertySymbol)> Props)> methods)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#pragma warning disable");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine();
        sb.AppendLine($"namespace {namespaceName}");
        sb.AppendLine("{{");

        var isInternal = methods.Any(m =>
            m.From.DeclaredAccessibility == Accessibility.Internal ||
            m.To.DeclaredAccessibility == Accessibility.Internal);

        var accessibility = isInternal ? "internal" : "public";

        sb.AppendLine($"    {accessibility} static partial class CloneExtensions");
        sb.AppendLine("    {{");

        foreach (var (fromType, toType, props) in methods)
        {
            var fromDisplay = fromType.ToDisplayString();
            var toDisplay = toType.ToDisplayString();

            sb.AppendLine($"        /// <summary>Auto-generated from '{toDisplay}.CloneFrom({fromDisplay})' call</summary>");
            sb.AppendLine($"        {accessibility} static void CloneFrom(this {toDisplay} to, {fromDisplay} from)");
            sb.AppendLine("        {{");
            sb.AppendLine("            if (to is null) return;");
            sb.AppendLine("            if (from is null) return;");

            foreach (var (src, _) in props)
            {
                sb.AppendLine($"            to.{src.Name} = from.{src.Name};");
            }

            //sb.AppendLine("            return to;");
            sb.AppendLine("        }}");
            sb.AppendLine();
        }

        sb.AppendLine("    }}");
        sb.AppendLine("}}");

        // 修复：双大括号转义
        var code = sb.ToString().Replace("{{", "{").Replace("}}", "}");
        return SourceText.From(code, System.Text.Encoding.UTF8);
    }

    // 用于存储调用点信息
    private sealed class CallSite
    {
        public ITypeSymbol ThisType { get; set; } = null!;
        public ITypeSymbol SourceType { get; set; } = null!;
        public SyntaxNode Syntax { get; set; } = null!;
    }

    // 自定义比较器
    private sealed class SymbolPairComparer : IEqualityComparer<(INamedTypeSymbol, INamedTypeSymbol)>
    {
        public static readonly SymbolPairComparer Instance = new();
        private static readonly SymbolEqualityComparer _equality = SymbolEqualityComparer.Default;

        public bool Equals((INamedTypeSymbol, INamedTypeSymbol) x, (INamedTypeSymbol, INamedTypeSymbol) y)
        {
            return _equality.Equals(x.Item1, y.Item1) && _equality.Equals(x.Item2, y.Item2);
        }

        public int GetHashCode((INamedTypeSymbol, INamedTypeSymbol) obj)
        {
            // 手动实现哈希（兼容 .NET Standard 2.0）
            var h1 = _equality.GetHashCode(obj.Item1);
            var h2 = _equality.GetHashCode(obj.Item2);
            return ((h1 << 5) + h1) ^ h2; // 简单哈希组合
        }
    }
}