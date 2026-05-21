using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace SG;

public readonly record struct PropertyMeta(
    string Name,
    string GenericArg,
    bool IsFactorItem,
    string? FactFieldName)
{
    /// <summary>
    /// FactorFields 中使用的常量名
    /// </summary>
    public string FieldKey => FactFieldName ?? Name;

    /// <summary>
    /// 初始化代码片段
    /// </summary>
    public string InitCode => IsFactorItem
        ? $"{Name} = new(Filter<{GenericArg}>(FactorFields.{FieldKey}, g), _shareConfigMap);"
        : $"{Name} = new(Filter<{GenericArg}>(FactorFields.{FieldKey}, g));";
}





[Generator]
public class FundFactorsCtorGenerator : IIncrementalGenerator
{
    private const string TargetClass = "FundFactors";
    private const string SingletonMetadataName = "FMO.Models.SingletonFactorItem`1";
    private const string FactorItemMetadataName = "FMO.Models.FactorItem`1";
    private const string FactFieldAttrName = "FactFieldAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1️⃣ 查找所有文件中声明 FundFactors 的部分类
        var partsProvider = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax c && c.Identifier.Text == TargetClass,
                transform: static (ctx, _) => ExtractPropertiesFromPart(ctx))
            .Where(static props => !props.IsEmpty);

        // 2️⃣ 收集所有分部类的属性（跨文件合并）
        var allPropsProvider = partsProvider.Collect();

        // 3️⃣ 去重并生成（整个项目只输出一次，彻底解决 hintName 冲突）
        context.RegisterSourceOutput(allPropsProvider, static (spc, allParts) =>
        {
            var mergedProps = allParts
                .SelectMany(p => p)
                .GroupBy(p => p.Name)
                .Select(g => g.First()) // 按属性名去重
                .ToImmutableArray();

            if (mergedProps.IsEmpty) return;
            GenerateCode(spc, mergedProps);
        });
    }

    /// <summary>
    /// 从单个文件的部分类中提取属性
    /// </summary>
    private static ImmutableArray<PropertyMeta> ExtractPropertiesFromPart(GeneratorSyntaxContext ctx)
    {
        if (ctx.Node is not ClassDeclarationSyntax classDecl)
            return ImmutableArray<PropertyMeta>.Empty;

        // 🔑 修复 ISymbol 报错：显式匹配为 INamedTypeSymbol
        if (ctx.SemanticModel.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol classSymbol)
            return ImmutableArray<PropertyMeta>.Empty;

        var compilation = ctx.SemanticModel.Compilation;
        var singletonDef = compilation.GetTypeByMetadataName(SingletonMetadataName);
        var factorItemDef = compilation.GetTypeByMetadataName(FactorItemMetadataName);

        var builder = ImmutableArray.CreateBuilder<PropertyMeta>();
        foreach (var member in classSymbol.GetMembers().OfType<IPropertySymbol>())
        {
            if (member.IsStatic || member.IsIndexer || member.Type.TypeKind == TypeKind.Error) continue;

            if (TryExtractProperty(member, singletonDef, factorItemDef, out var meta))
                builder.Add(meta);
        }

        return builder.ToImmutable();
    }

    private static bool TryExtractProperty(
        IPropertySymbol prop,
        INamedTypeSymbol? singletonDef,
        INamedTypeSymbol? factorItemDef,
        out PropertyMeta meta)
    {
        meta = default;
        if (prop.Type is not INamedTypeSymbol namedType || namedType.TypeArguments.Length != 1)
            return false;

        bool isFactorItem = false;
        bool isMatch = false;

        if (singletonDef != null && namedType.OriginalDefinition.Equals(singletonDef, SymbolEqualityComparer.Default))
        {
            isMatch = true; isFactorItem = false;
        }
        else if (factorItemDef != null)
        {
            var current = namedType;
            while (current != null)
            {
                if (current.OriginalDefinition.Equals(factorItemDef, SymbolEqualityComparer.Default))
                { isMatch = true; isFactorItem = true; break; }
                current = current.BaseType;
            }
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

        meta = new PropertyMeta(prop.Name, genericArg, isFactorItem, factFieldName);
        return true;
    }

    private static void GenerateCode(SourceProductionContext ctx, ImmutableArray<PropertyMeta> props)
    {
        var shareTypes = props.FirstOrDefault(p => p.Name == "ShareTypes");
        var others = props.Where(p => p.Name != "ShareTypes").ToImmutableArray();

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("namespace FMO.Models;");
        sb.AppendLine();

        sb.Append($$"""
            /// <summary>
            /// 自动生成的 FactorId 常量映射，键为属性名，值为实际使用的 FactorId
            /// </summary>
            public static class FactorFields
            {
                public const string ShareClasses = "ShareClasses";
            {{GenerateFactorFields(props)}}
            }


            """);

        sb.AppendLine($"public partial class {TargetClass}");
        sb.AppendLine("{");

 
        sb.AppendLine();

        sb.AppendLine("    [global::System.Diagnostics.CodeAnalysis.SuppressMessage(\"Style\", \"IDE0055\", Justification = \"Generated\")]");
        sb.AppendLine("    private void __AutoInitializeCtor(global::System.Collections.Generic.IEnumerable<IFundFactor> facts)");
        sb.AppendLine("    {");
        sb.AppendLine("        var g = facts");
        sb.AppendLine("            .Where(x => x.FactorId is not null)");
        sb.AppendLine("            .OrderByDescending(x => x.FlowId)");
        sb.AppendLine("            .ThenBy(x => x.ShareId)");
        sb.AppendLine("            .GroupBy(x => x.FactorId)");
        sb.AppendLine("            .ToDictionary(x => x.Key, x => x.AsEnumerable());");
        sb.AppendLine();
        sb.AppendLine("        ShareClasses = new(Filter<ShareClass[]>(FactorFields.ShareClasses, g));");
        sb.AppendLine("        var _shareConfigMap = BuildInheritedShareConfigMap(ShareClasses.GetShares());\n");

        //if (!shareTypes.Equals(default(PropertyMeta)))
        //{
        //    sb.AppendLine($"        ShareTypes = new(Filter<{shareTypes.GenericArg}>(FactorFields.{shareTypes.FieldKey}, g));");
        //    sb.AppendLine("        _shareConfigMap = BuildInheritedShareConfigMap(ShareTypes.GetShares());");
        //    sb.AppendLine();
        //}

        foreach (var p in others)
        {
            sb.AppendLine($"        {p.InitCode}");
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        // ✅ 固定 hintName，因 .Collect() 保证全局只执行一次，不会冲突
        ctx.AddSource("FundFactors.AutoInit.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }


    /// <summary>
    /// 生成 FactorFields 常量类内容
    /// </summary>
    private static string GenerateFactorFields(ImmutableArray<PropertyMeta> props) =>
        string.Join("\n\n", props.OfType<PropertyMeta>().Select(p =>
            $"        /// <summary>FactorId for property '{p.Name}'</summary>\n        public const string {p.Name} = \"{p.FieldKey}\";"));
}