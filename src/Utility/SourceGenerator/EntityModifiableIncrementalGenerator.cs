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

        var declaredPropertiesMap = new Dictionary<string, IPropertySymbol>(StringComparer.Ordinal);
        INamedTypeSymbol? current = targetClass;
        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            foreach (var member in current.GetMembers())
                if (member is IPropertySymbol p && p.DeclaredAccessibility == Accessibility.Public)
                    declaredPropertiesMap[p.Name] = p;
            current = current.BaseType;
        }

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
                        !prop.IsStatic && prop.SetMethod != null && !prop.SetMethod.IsInitOnly)
                    {
                        bool isUserDeclared = declaredPropertiesMap.TryGetValue(prop.Name, out var declaredProp);
                        string genericArg;
                        bool isNullable;

                        if (isUserDeclared && declaredProp != null)
                        {
                            var declaredType = declaredProp.Type as INamedTypeSymbol;
                            if (declaredType != null && IsModifiableViewModel(declaredType) && declaredType.TypeArguments.Length > 0)
                            {
                                var vmType = declaredType.TypeArguments[0];
                                var vmTypeClean = (vmType as INamedTypeSymbol)?.WithNullableAnnotation(NullableAnnotation.NotAnnotated) ?? vmType;

                                // 🔹 修复：增强接口匹配逻辑，解决跨程序集/可空注解导致的误判跳过
                                if (vmTypeClean is INamedTypeSymbol vmNamed && IsIViewModelOf(vmNamed, prop.Type))
                                {
                                    genericArg = vmType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                                    isNullable = vmType.IsReferenceType || vmType.NullableAnnotation == NullableAnnotation.Annotated;

                                    properties.Add(new PropertyInfo(
                                        prop.Name, genericArg, isNullable, isWritable: true,
                                        entityType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                                        isUserDeclared: true, isViewModelBacked: true));
                                    logs.Add($"  ➕ {prop.Name}: {genericArg} (UserDeclared, ViewModelBacked)");
                                    continue;
                                }
                            }
                            logs.Add($"  ⏭️ {prop.Name}: Skipped (UserDeclared, not VM-backed or type mismatch)");
                            continue;
                        }

                        ITypeSymbol propType = prop.Type;
                        genericArg = propType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                        isNullable = propType.IsReferenceType || propType.NullableAnnotation == NullableAnnotation.Annotated;

                        properties.Add(new PropertyInfo(
                            prop.Name, genericArg, isNullable, isWritable: true,
                            entityType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                            isUserDeclared: false, isViewModelBacked: false));
                        logs.Add($"  ➕ {prop.Name}: {genericArg} (AutoGenerated)");
                    }
                }
                currentType = currentType.BaseType;
            }
        }

        logs.Add($"[End] Total properties: {properties.Count}");
        if (properties.Count == 0) return null;

        return new GenerationModel(className, ns, entityTypes, properties, logs);
    }

    private static bool IsModifiableViewModel(INamedTypeSymbol type) =>
        type.OriginalDefinition.Name == "ModifiableViewModel" &&
        type.ContainingNamespace?.ToDisplayString() == "FMO.Shared";

    // 🔹 核心修复：双轨匹配（符号相等 + 全限定名字符串降级），移除命名空间硬编码限制
    private static bool IsIViewModelOf(INamedTypeSymbol vmType, ITypeSymbol entityType)
    {
        var targetEntity = entityType.WithNullableAnnotation(NullableAnnotation.NotAnnotated);
        var targetEntityFqn = targetEntity.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        foreach (var iface in vmType.AllInterfaces)
        {
            if (iface.OriginalDefinition.Name == "IViewModel" && iface.TypeArguments.Length == 1)
            {
                var arg = iface.TypeArguments[0].WithNullableAnnotation(NullableAnnotation.NotAnnotated);

                // 1. 优先使用 Roslyn 符号比对
                if (SymbolEqualityComparer.Default.Equals(arg, targetEntity)) return true;
                // 2. 降级使用全限定名字符串比对（解决跨程序集引用符号不一致问题）
                if (arg.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == targetEntityFqn) return true;
            }
        }
        return false;
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

        var propertyDeclarations = string.Join("\n\n", model.Properties.Where(p => !p.IsUserDeclared).Select(prop =>
        {
            var genericArgClean = prop.GenericArgument.TrimEnd('?');
            var nullableMark = prop.IsNullable ? "?" : "";
            return $$"""
        public ModifiableViewModel<{{genericArgClean}}{{nullableMark}}> {{prop.Name}} { get; private set; } = null!;
""";
        }));

        var initAssignments = string.Join("\n", model.Properties.Select(prop =>
        {
            var entityPropAccess = $"entity.{prop.Name}";
            if (prop.IsViewModelBacked)
            {
                return $$"""            {{prop.Name}} = new() { NewValue = new {{prop.GenericArgument}}({{entityPropAccess}}), OldValue =  new {{prop.GenericArgument}}({{entityPropAccess}}) };""";
            }

            var nullCoalesce = prop.IsNullable ? " ?? default" : "";
            return $$"""            {{prop.Name}} = new() { NewValue = CloneHelper.CloneValue({{entityPropAccess}}), OldValue = {{entityPropAccess}}{{nullCoalesce}} };""";
        }));

        var eventSubscriptions = string.Join("\n", model.Properties.Where(p => p.IsWritable).Select(prop =>
        {
            var genericArg = prop.GenericArgument;
            var entityPropAccess = $"entity.{prop.Name}";

            if (prop.IsViewModelBacked)
            {
                return $$"""
            {{prop.Name}}.Changed += (s, e) =>
            {
                if (e is ValueChangeEventArgs<{{genericArg}}> ee)
                    {{entityPropAccess}} = ee.NewValue.Build();
                _throttle.Execute(OnEntityChanged);
            };
""";
            }

            var nullCoalesce = prop.IsNullable ? " ?? default" : "";
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

        var primaryEntity = model.EntityTypes.FirstOrDefault();
        var primaryEntityTypeName = primaryEntity?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? "object";

        return $$"""
#nullable enable
{{debugHeader}}
using System;
using System.IO;
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
            if (entity is null) throw new InvalidDataException("entity 不能为null");

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
        public string Name { get; }
        public string GenericArgument { get; }
        public bool IsNullable { get; }
        public bool IsWritable { get; }
        public string EntityTypeName { get; }
        public bool IsUserDeclared { get; }
        public bool IsViewModelBacked { get; }

        public PropertyInfo(string name, string genericArgument, bool isNullable, bool isWritable,
            string entityTypeName, bool isUserDeclared, bool isViewModelBacked)
        {
            Name = name;
            GenericArgument = genericArgument;
            IsNullable = isNullable;
            IsWritable = isWritable;
            EntityTypeName = entityTypeName;
            IsUserDeclared = isUserDeclared;
            IsViewModelBacked = isViewModelBacked;
        }
    }
}