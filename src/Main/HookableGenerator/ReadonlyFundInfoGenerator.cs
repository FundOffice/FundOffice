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
        var isValueType = namedType.TypeArguments[0].IsValueType;

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

        meta = new PropertyMeta(prop.Name, genericArg, isFactorItem, factFieldName, isValueType);
        return true;
    }

    private static void GenerateCode(SourceProductionContext ctx, ImmutableArray<PropertyMeta> props)
    {
        var singletons = props.Where(p => !p.IsFactorItem).ToImmutableArray();
        var factorItems = props.Where(p => p.IsFactorItem).ToImmutableArray();

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using System.Collections.Immutable;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine();
        sb.AppendLine("namespace FMO.Models;");
        sb.AppendLine();
        sb.AppendLine($"public partial class {ReadonlyInfoClass}");
        sb.AppendLine("{");

        if (props.IsEmpty)
        {
            sb.AppendLine("    // [Generator Active] No missing properties found to generate.");
        }
        else
        {
            foreach (var p in props)
            {
                var typeName = p.IsFactorItem
                    ? $"{p.GenericArg}?[]"
                    : $"{p.GenericArg}?";
                if (p.IsFactorItem)
                    sb.AppendLine($"    /// <summary>{p.Name}（无数据返回空数组）</summary>");
                sb.AppendLine($"    public {typeName} {p.Name} {{ get; set; }}");
            }

            sb.AppendLine();
            sb.AppendLine("    public void FillBy(IFundFactor[] val)");
            sb.AppendLine("    {");
            sb.AppendLine("        if (val is null) return;");
            sb.AppendLine();
            sb.AppendLine("        var g = val");
            sb.AppendLine("            .Where(x => x.FactorId is not null)");
            sb.AppendLine("            .OrderByDescending(x => x.FlowId)");
            sb.AppendLine("            .ThenBy(x => x.ShareId)");
            sb.AppendLine("            .GroupBy(x => x.FactorId)");
            sb.AppendLine("            .ToDictionary(x => x.Key, x => x.AsEnumerable());");
            sb.AppendLine();

            for (int i = 0; i < singletons.Length; i++)
            {
                var p = singletons[i];
                sb.AppendLine($"        if (g.TryGetValue(FactorFields.{p.Name}, out var _s{i}))");
                sb.AppendLine("        {");
                sb.AppendLine($"            var _f = _s{i}.OfType<FundFactor<{p.GenericArg}>>().FirstOrDefault();");
                sb.AppendLine("            if (_f != null)");
                sb.AppendLine($"                {p.Name} = _f.Data;");
                sb.AppendLine("        }");
                sb.AppendLine();
            }

            if (factorItems.Length > 0)
            {
                sb.AppendLine("        // FactorItem: 默认空数组");
                foreach (var p in factorItems)
                    sb.AppendLine($"        {p.Name} = [];");
                sb.AppendLine();
                sb.AppendLine("        if (g.TryGetValue(FactorFields.ShareClasses, out var _scData))");
                sb.AppendLine("        {");
                sb.AppendLine("            var _scArr = _scData.OfType<FundFactor<ShareClass[]>>().ToArray();");
                sb.AppendLine("            if (_scArr.Length > 0)");
                sb.AppendLine("            {");
                sb.AppendLine("                var _sci = new ShareClassFactorItem(_scArr);");
                sb.AppendLine("                var _shares = _sci.GetShares();");
                sb.AppendLine("                var _cfg = __BuildInheritedShareConfigMap(_shares);");
                sb.AppendLine();

                for (int i = 0; i < factorItems.Length; i++)
                {
                    var p = factorItems[i];
                    sb.AppendLine($"                if (g.TryGetValue(FactorFields.{p.Name}, out var _f{i}))");
                    sb.AppendLine("                {");
                    sb.AppendLine($"                    var _item = new FactorItem<{p.GenericArg}>(_f{i}.OfType<FundFactor<{p.GenericArg}>>(), _shares, _cfg);");
                    sb.AppendLine("                    if (_item.HasValue)");
                    sb.AppendLine("                    {");
                    sb.AppendLine("                        var _vals = _item.Current;");
                    sb.AppendLine("                        if (_vals.Length > 0)");
                    sb.AppendLine("                        {");
                    sb.AppendLine($"                            {p.Name} = _vals.Length == 1 || _vals.All(x => global::System.Collections.Generic.EqualityComparer<{p.GenericArg}>.Default.Equals(x, _vals[0])) ? [_vals[0]] : _vals;");
                    sb.AppendLine("                        }");
                    sb.AppendLine("                    }");
                    sb.AppendLine("                }");
                    sb.AppendLine();
                }

                sb.AppendLine("            }");
                sb.AppendLine("        }");
            }

            sb.AppendLine("    }");

            sb.AppendLine();
            sb.AppendLine("    private static ImmutableDictionary<int, InheritMap> __BuildInheritedShareConfigMap((int FlowId, ShareClass[] Shares)[] rawShares)");
            sb.AppendLine("    {");
            sb.AppendLine("        if (rawShares == null || rawShares.Length == 0) return ImmutableDictionary<int, InheritMap>.Empty;");
            sb.AppendLine();
            sb.AppendLine("        var result = new System.Collections.Generic.Dictionary<int, InheritMap>();");
            sb.AppendLine("        for (int i = 0; i < rawShares.Length; i++)");
            sb.AppendLine("        {");
            sb.AppendLine("            var item = rawShares[i];");
            sb.AppendLine("            if (item.Shares == null || item.Shares.Length == 0) continue;");
            sb.AppendLine("            foreach (var sc in item.Shares)");
            sb.AppendLine("            {");
            sb.AppendLine("                if (i > 1 && ShareClass.GetFlow(sc.Inherit) < rawShares[i - 1].FlowId && rawShares[i - 1].Shares.Length == 1)");
            sb.AppendLine("                    sc.Inherit = rawShares[i - 1].Shares[0].Id;");
            sb.AppendLine("                result[sc.Id] = new InheritMap(sc.Id, item.FlowId, sc.Inherit);");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("        return result.ToImmutableDictionary();");
            sb.AppendLine("    }");
        }

        sb.AppendLine("}");
        ctx.AddSource("ReadonlyFundInfo.Generated.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }
}
