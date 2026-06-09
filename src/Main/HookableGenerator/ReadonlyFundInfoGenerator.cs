using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;

namespace SG;

[Generator]
public class ReadonlyFundInfoGenerator : IIncrementalGenerator
{
    private const string FundFactorsClass = "FundFactors";
    private const string ReadonlyInfoClass = "ReadonlyFundInfo";
    private const string SingletonMetadataName = "FMO.Models.SingletonFactorItem`1";
    private const string SingletonValueMetadataName = "FMO.Models.SingletonValueFactorItem`1";
    private const string FactorItemMetadataName = "FMO.Models.FactorItem`1";
    private const string ValueFactorItemMetadataName = "FMO.Models.ValueFactorItem`1";
    private const string FactFieldAttrName = "FactFieldAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var fundFactorsPropsProvider = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax c && c.Identifier.ValueText == FundFactorsClass,
                transform: static (ctx, ct) => ExtractFactorProperties(ctx, ct))
            .Where(static props => !props.IsEmpty)
            .Collect();

        var readonlyInfoPropsProvider = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax c && c.Identifier.ValueText == ReadonlyInfoClass,
                transform: static (ctx, ct) => ExtractExistingMemberNames(ctx, ct))
            .Collect();

        var combined = fundFactorsPropsProvider.Combine(readonlyInfoPropsProvider);

        context.RegisterSourceOutput(combined, static (spc, source) =>
        {
            var fundFactorsProps = source.Left
                .SelectMany(p => p)
                .GroupBy(p => p.Name)
                .Select(g => g.First())
                .ToImmutableArray();

            var existingNames = source.Right
                .SelectMany(p => p)
                .ToImmutableHashSet();

            var missingProps = fundFactorsProps
                .Where(p => !existingNames.Contains(p.Name))
                .ToImmutableArray();

            GenerateCode(spc, missingProps);
        });
    }

    private static ImmutableArray<PropertyMeta> ExtractFactorProperties(GeneratorSyntaxContext ctx, CancellationToken _)
    {
        if (ctx.Node is not ClassDeclarationSyntax classDecl)
            return ImmutableArray<PropertyMeta>.Empty;

        if (ctx.SemanticModel.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol classSymbol)
            return ImmutableArray<PropertyMeta>.Empty;

        var compilation = ctx.SemanticModel.Compilation;
        var singletonDef = compilation.GetTypeByMetadataName(SingletonMetadataName);
        var singletonValueDef = compilation.GetTypeByMetadataName(SingletonValueMetadataName);
        var factorItemDef = compilation.GetTypeByMetadataName(FactorItemMetadataName);
        var valueFactorItemDef = compilation.GetTypeByMetadataName(ValueFactorItemMetadataName);

        var builder = ImmutableArray.CreateBuilder<PropertyMeta>();
        var seen = new HashSet<string>();

        var currentType = classSymbol;
        while (currentType != null)
        {
            foreach (var prop in currentType.GetMembers().OfType<IPropertySymbol>())
            {
                if (prop.IsStatic || prop.IsIndexer || prop.Type.TypeKind == TypeKind.Error) continue;
                if (!seen.Add(prop.Name)) continue;

                if (TryExtract(prop, singletonDef, singletonValueDef, factorItemDef, valueFactorItemDef, out var meta))
                    builder.Add(meta);
            }
            currentType = currentType.BaseType;
        }

        return builder.ToImmutable();
    }

    private static ImmutableArray<string> ExtractExistingMemberNames(GeneratorSyntaxContext ctx, CancellationToken _)
    {
        if (ctx.Node is not ClassDeclarationSyntax classDecl)
            return ImmutableArray<string>.Empty;

        if (ctx.SemanticModel.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol classSymbol)
            return ImmutableArray<string>.Empty;

        var builder = ImmutableArray.CreateBuilder<string>();
        var seen = new HashSet<string>();

        var currentType = classSymbol;
        while (currentType != null)
        {
            foreach (var member in currentType.GetMembers())
            {
                var name = member switch
                {
                    IPropertySymbol p => p.Name,
                    IFieldSymbol f => f.Name,
                    _ => null
                };
                if (name != null && seen.Add(name))
                    builder.Add(name);
            }
            currentType = currentType.BaseType;
        }

        return builder.ToImmutable();
    }

    private static bool TryExtract(
        IPropertySymbol prop,
        INamedTypeSymbol? singletonDef,
        INamedTypeSymbol? singletonValueDef,
        INamedTypeSymbol? factorItemDef,
        INamedTypeSymbol? valueFactorItemDef,
        out PropertyMeta meta)
    {
        meta = default;

        if (prop.Type is not INamedTypeSymbol namedType || namedType.TypeArguments.Length != 1)
            return false;

        bool isFactorItem = false;
        bool isMatch = false;

        var currentType = namedType;
        while (currentType != null)
        {
            var def = currentType.OriginalDefinition;

            if (singletonDef != null && def.Equals(singletonDef, SymbolEqualityComparer.Default))
            { isMatch = true; isFactorItem = false; break; }

            if (singletonValueDef != null && def.Equals(singletonValueDef, SymbolEqualityComparer.Default))
            { isMatch = true; isFactorItem = false; break; }

            if (factorItemDef != null && def.Equals(factorItemDef, SymbolEqualityComparer.Default))
            { isMatch = true; isFactorItem = true; break; }

            if (valueFactorItemDef != null && def.Equals(valueFactorItemDef, SymbolEqualityComparer.Default))
            { isMatch = true; isFactorItem = true; break; }

            currentType = currentType.BaseType;
        }

        if (!isMatch) return false;

        var genericArg = namedType.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        string? factFieldName = null;
        foreach (var attr in prop.GetAttributes())
        {
            if (attr.AttributeClass?.Name == FactFieldAttrName &&
                attr.ConstructorArguments.Length > 0 &&
                attr.ConstructorArguments[0].Value is string name)
            {
                factFieldName = name;
                break;
            }
        }

        var isValueType = namedType.TypeArguments[0].IsValueType;
        meta = new PropertyMeta(prop.Name, genericArg, isFactorItem, factFieldName, isValueType);
        return true;
    }

    private static void GenerateCode(SourceProductionContext ctx, ImmutableArray<PropertyMeta> props)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("namespace FMO.Models;");
        sb.AppendLine();
        sb.AppendLine($"public partial class {ReadonlyInfoClass}");
        sb.AppendLine("{");

        if (props.IsEmpty)
        {
            // 即使没有需要生成的属性，也输出注释，证明生成器正在工作
            sb.AppendLine("    // [Generator Active] No missing properties found to generate.");
        }
        else
        {
            foreach (var p in props)
            {
                // Singleton 保持 T，FactorItem 改为 T[]
                var typeName = p.IsFactorItem ? $"{p.GenericArg}[]?" : p.IsValueType ? p.GenericArg : $"{p.GenericArg}?";
                sb.AppendLine($"    public {typeName} {p.Name} {{ get; init; }}");
            }
        }

        sb.AppendLine("}");

        // 必须使用固定的 hintName，绝不能包含随机数或时间！
        ctx.AddSource("ReadonlyFundInfo.Generated.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }
}