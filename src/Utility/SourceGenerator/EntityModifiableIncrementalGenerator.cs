
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SG;


[Generator]
public class EntityModifiableIncrementalGenerator : IIncrementalGenerator
{
    private const string AttributeMetadataName = "FMO.Models.EntityModifiableAttribute";
    private const string ModifiableViewModelTypeName = "FMO.Shared.ModifiableViewModel<TValue>";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var classDeclarations = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AttributeMetadataName,
                predicate: (node, _) => node is ClassDeclarationSyntax,
                transform: (ctx, ct) =>
                {
                    var classDecl = (ClassDeclarationSyntax)ctx.TargetNode;
                    var classSymbol = ctx.SemanticModel.GetDeclaredSymbol(classDecl, ct) as INamedTypeSymbol;
                    if (classSymbol == null) return null;

                    // 支持多个 [EntityModifiable] 属性，收集所有实体类型
                    var entityTypes = ctx.Attributes
                        .Select(a => a.ConstructorArguments.FirstOrDefault().Value as INamedTypeSymbol)
                        .OfType<INamedTypeSymbol>()
                        .ToList();

                    if (entityTypes.Count == 0) return null;

                    return BuildGenerationModel(classSymbol, entityTypes);
                })
            .Where(model => model != null);

        context.RegisterSourceOutput(classDeclarations, (spc, model) =>
        {
            if (model == null) return;
            var source = GenerateSource(model);
            spc.AddSource($"{model.ClassName}.EntityModifiable.g.cs", SourceText.From(source, Encoding.UTF8));
        });
    }

    private static GenerationModel? BuildGenerationModel(INamedTypeSymbol targetClass, List<INamedTypeSymbol> entityTypes)
    {
        var logs = new List<string>();
        logs.Add($"[Start] {targetClass.Name} <- [{string.Join(", ", entityTypes.Select(e => e.Name))}]");

        var ns = targetClass.ContainingNamespace.IsGlobalNamespace ? string.Empty : targetClass.ContainingNamespace.ToDisplayString();
        var className = targetClass.Name;

        // 🔹 收集 VM 中已声明的属性（跳过这些）
        var declaredProperties = new HashSet<string>(StringComparer.Ordinal);
        INamedTypeSymbol? current = targetClass;
        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            foreach (var member in current.GetMembers())
                if (member is IPropertySymbol p && p.DeclaredAccessibility == Accessibility.Public)
                    declaredProperties.Add(p.Name);
            current = current.BaseType;
        }
        logs.Add($"[Declared] {string.Join(", ", declaredProperties)}");

        // 🔹 收集所有需要生成的属性（支持多实体）
        var properties = new List<PropertyInfo>();

        foreach (var entityType in entityTypes)
        {
            var currentType = entityType;
            while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
            {
                foreach (var member in currentType.GetMembers())
                {
                    if (member is IPropertySymbol prop &&
                        prop.DeclaredAccessibility == Accessibility.Public &&
                        !prop.IsStatic && prop.SetMethod != null &&
                        !prop.SetMethod.IsInitOnly &&
                        !declaredProperties.Contains(prop.Name))
                    {
                        ITypeSymbol propType = prop.Type;
                        string propTypeString = propType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                        // 🔹 提取泛型参数：ModifiableViewModel<T> 中的 T
                        string genericArg = GetModifiableViewModelGenericArgument(propType);

                        bool isNullable = propType.IsReferenceType || prop.NullableAnnotation == NullableAnnotation.Annotated;
                        bool isWritable = prop.SetMethod != null;

                        var propInfo = new PropertyInfo(
                            sourceTypeName: propTypeString,
                            name: prop.Name,
                            genericArgument: genericArg,
                            isNullable: isNullable,
                            isWritable: isWritable,
                            entityTypeName: entityType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                            label: ToChineseLabel(prop.Name));

                        properties.Add(propInfo);
                        logs.Add($"  ➕ {prop.Name}: {genericArg} (nullable={isNullable}, writable={isWritable})");
                    }
                }
                currentType = currentType.BaseType;
            }
        }

        logs.Add($"[End] Total properties: {properties.Count}");
        if (properties.Count == 0) return null;

        return new GenerationModel(className, ns, entityTypes, properties, logs);
    }

    // 🔹 尝试提取类型作为 ModifiableViewModel<T> 的泛型参数
    // 如果是 string → string, 如果是 MyEnum → MyEnum, 如果是 List<T> → List<T> 等
    private static string GetModifiableViewModelGenericArgument(ITypeSymbol type)
    {
        // 基础类型、枚举、数组、泛型等都直接使用原类型
        return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }

    // 🔹 简单驼峰转中文标签（可自定义）
    private static string ToChineseLabel(string propertyName)
    {
        // 简单示例：ManagerName → "管理人", UserName → "用户名"
        // 实际项目中可接入资源文件或配置
        return propertyName switch
        {
            "ManagerName" => "管理人",
            "UserName" => "用户名",
            "Email" => "邮箱",
            "Phone" => "电话",
            "Name" => "名称",
            "Description" => "描述",
            "Title" => "标题",
            "Content" => "内容",
            "Status" => "状态",
            "CreateTime" => "创建时间",
            "UpdateTime" => "更新时间",
            _ => propertyName // 默认返回英文属性名
        };
    }

    private static string GenerateSource(GenerationModel model)
    {
#if DEBUG
        var debugHeader = string.Join("\n",
            new[] { "// 🔍 ===== EntityModifiable Debug Info =====" }
            .Concat(model.DebugLogs.Select(l => $"// {l}"))
            .Concat(new[] { "// =====================================\n" }));
#else
        var debugHeader = "";
#endif

        var namespaceOpen = !string.IsNullOrEmpty(model.Namespace) ? $"namespace {model.Namespace}\n{{" : "";
        var namespaceClose = !string.IsNullOrEmpty(model.Namespace) ? "}" : "";

        // 🔹 生成属性声明
        var propertyDeclarations = string.Join("\n\n", model.Properties.Select(prop =>
        {
            var nullableMark = prop.IsNullable ? "?" : "";
            var genericArg = prop.GenericArgument.Last() == '?' ? prop.GenericArgument : prop.GenericArgument + nullableMark;
            return $$"""
        public ModifiableViewModel<{{genericArg}}> {{prop.Name}} { get; private set; } = null!;
""";
        }));

        // 🔹 生成初始化赋值语句
        var initAssignments = string.Join("\n", model.Properties.Select(prop =>
        {
            var label = prop.Label;
            var entityPropAccess = $"entity.{prop.Name}";
            var nullableSuffix = prop.IsNullable ? " ?? default" : "";
            // 注意：ModifiableViewModel<T> 的 OldValue/NewValue 需要相同类型
            return $$"""
            {{prop.Name}} = new() { NewValue = CloneHelper.CloneValue({{entityPropAccess}}), OldValue = {{entityPropAccess}}{{(prop.IsNullable ? " ?? default" : "")}} };
""";
        }));

        // 🔹 生成事件订阅 + throttle 调用
        var eventSubscriptions = string.Join("\n", model.Properties.Where(p => p.IsWritable).Select(prop =>
        {
            var genericArg = prop.GenericArgument;
            var entityPropAccess = $"entity.{prop.Name}";
            var nullCoalesce = prop.IsNullable ? " ?? default" : "";
            // 对于 string 类型特殊处理空值
            var stringEmptyFix = (genericArg == "string") ? " ?? \"\"" : nullCoalesce;

            return $$"""
            {{prop.Name}}.Changed += (s, e) =>
            {
                if (e is ValueChangeEventArgs<{{genericArg}}> ee)
                    {{entityPropAccess}} = ee.NewValue{{stringEmptyFix}};
                _throttle.Execute(OnEntityChanged);
            };
""";
        }));

        // 🔹 生成 FillBy 方法参数（支持多实体，这里以第一个实体为主，或可改为字典）
        var primaryEntity = model.EntityTypes.FirstOrDefault();
        var primaryEntityTypeName = primaryEntity?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? "object";
        var primaryEntityVarName = primaryEntity?.Name.ToLowerInvariant() ?? "entity";

        return $$"""
#nullable enable
{{debugHeader}}
using System;
using System.ComponentModel;
using FMO.Models;
using FMO.Shared;

{{namespaceOpen}}
    public partial class {{model.ClassName}}
    {
        private readonly Throttle _throttle = new(TimeSpan.FromMilliseconds(200));

        // 🔹 生成的 ModifiableViewModel 属性
{{propertyDeclarations}}

        // 🔹 统一赋值入口
        public void FillBy({{primaryEntityTypeName}}? entity)
        {
            if (entity is not {{primaryEntityTypeName}} val) return;

{{initAssignments}}

            // 🔹 订阅变更事件
{{eventSubscriptions}}
        }

        // 🔹 供用户重写的实体变更回调
        public partial void OnEntityChanged();
    }
{{namespaceClose}}
""";
    }

    private class GenerationModel
    {
        public string ClassName { get; }
        public string Namespace { get; }
        public List<INamedTypeSymbol> EntityTypes { get; }
        public List<PropertyInfo> Properties { get; }
        public List<string> DebugLogs { get; }

        public GenerationModel(string className, string @namespace, List<INamedTypeSymbol> entityTypes,
            List<PropertyInfo> properties, List<string> logs)
        {
            ClassName = className;
            Namespace = @namespace;
            EntityTypes = entityTypes;
            Properties = properties;
            DebugLogs = logs;
        }
    }

    private class PropertyInfo
    {
        public string SourceTypeName { get; }
        public string Name { get; }
        public string GenericArgument { get; }  // ModifiableViewModel<T> 中的 T
        public bool IsNullable { get; }
        public bool IsWritable { get; }
        public string EntityTypeName { get; }
        public string Label { get; }

        public PropertyInfo(string sourceTypeName, string name, string genericArgument, bool isNullable,
            bool isWritable, string entityTypeName, string label)
        {
            SourceTypeName = sourceTypeName;
            Name = name;
            GenericArgument = genericArgument;
            IsNullable = isNullable;
            IsWritable = isWritable;
            EntityTypeName = entityTypeName;
            Label = label;
        }
    }
}