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

                        if (isUserDeclared && declaredProp != null)
                        {
                            var declaredType = declaredProp.Type as INamedTypeSymbol;
                            if (declaredType != null && IsModifiableViewModel(declaredType) && declaredType.TypeArguments.Length > 0)
                            {
                                var vmInnerType = declaredType.TypeArguments[0];
                                var entityPropType = prop.Type;

                                // 🔹 匹配策略 1：基础/可空类型直接匹配 (DateOnly? ↔ DateOnly)
                                var vmUnderlying = GetUnderlyingType(vmInnerType);
                                var entityUnderlying = GetUnderlyingType(entityPropType);
                                bool isDirectMatch = SymbolEqualityComparer.Default.Equals(vmUnderlying, entityUnderlying) ||
                                                     vmUnderlying.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == entityUnderlying.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                                // 🔹 匹配策略 2：复杂 ViewModel 接口匹配 (DateEfficientViewModel ↔ IViewModel<DateEfficient>)
                                bool isComplexVm = false;
                                if (!isDirectMatch && vmInnerType is INamedTypeSymbol vmNamed)
                                {
                                    isComplexVm = IsIViewModelOf(vmNamed, entityPropType);
                                }

                                if (isDirectMatch || isComplexVm)
                                {
                                    string genericArg = vmInnerType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                                    bool isNullable = vmInnerType.IsReferenceType || vmInnerType.NullableAnnotation == NullableAnnotation.Annotated ||
                                                      (vmInnerType is INamedTypeSymbol n && n.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T);

                                    properties.Add(new PropertyInfo(
                                        prop.Name, genericArg, isNullable, isWritable: true,
                                        entityType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                                        isUserDeclared: true, isComplexViewModel: isComplexVm));

                                    logs.Add($"  ➕ {prop.Name}: {genericArg} (UserDeclared, {(isComplexVm ? "ComplexVM" : "SimpleNullable")})");
                                    continue;
                                }
                            }
                            logs.Add($"  ⏭️ {prop.Name}: Skipped (UserDeclared, not VM-backed or type mismatch)");
                            continue;
                        }

                        // 🔹 自动生成属性
                        {
                            ITypeSymbol propType = prop.Type;
                            string genericArg = propType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                            bool isNullable = propType.IsReferenceType || propType.NullableAnnotation == NullableAnnotation.Annotated;

                            properties.Add(new PropertyInfo(
                                prop.Name, genericArg, isNullable, isWritable: true,
                                entityType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                                isUserDeclared: false, isComplexViewModel: false));
                            logs.Add($"  ➕ {prop.Name}: {genericArg} (AutoGenerated)");
                        }
                    }
                }
                currentType = currentType.BaseType;
            }
        }

        logs.Add($"[End] Total properties: {properties.Count}");
        if (properties.Count == 0) return null;

        return new GenerationModel(className, ns, entityTypes, properties, logs);
    }

    private static ITypeSymbol GetUnderlyingType(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol named && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            return named.TypeArguments[0];
        return type.WithNullableAnnotation(NullableAnnotation.NotAnnotated);
    }

    private static bool IsModifiableViewModel(INamedTypeSymbol type) =>
        type.OriginalDefinition.Name == "ModifiableViewModel" &&
        type.ContainingNamespace?.ToDisplayString() == "FMO.Shared";

    private static bool IsIViewModelOf(INamedTypeSymbol vmType, ITypeSymbol entityType)
    {
        var targetEntity = entityType.WithNullableAnnotation(NullableAnnotation.NotAnnotated);
        var targetEntityFqn = targetEntity.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        foreach (var iface in vmType.AllInterfaces)
        {
            if (iface.OriginalDefinition.Name == "IViewModel" && iface.TypeArguments.Length == 1)
            {
                var arg = iface.TypeArguments[0].WithNullableAnnotation(NullableAnnotation.NotAnnotated);
                if (SymbolEqualityComparer.Default.Equals(arg, targetEntity)) return true;
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

            if (prop.IsComplexViewModel)
            {
                // 🔹 复杂 VM：调用构造函数 new VM(entity)
                return $$"""            {{prop.Name}} = new() { NewValue = new {{prop.GenericArgument}}({{entityPropAccess}}), OldValue = new {{prop.GenericArgument}}({{entityPropAccess}}) };""";
            }
            else if (prop.IsUserDeclared)
            {
                // 🔹 简单/可空类型：直接赋值，依赖 C# 隐式转换 T -> T?
                return $$"""            {{prop.Name}} = new() { NewValue = {{entityPropAccess}}, OldValue = {{entityPropAccess}} };""";
            }
            else
            {
                // 🔹 自动生成：使用 CloneHelper
                var nullCoalesce = prop.IsNullable ? " ?? default" : "";
                return $$"""            {{prop.Name}} = new() { NewValue = CloneHelper.CloneValue({{entityPropAccess}}), OldValue = {{entityPropAccess}}{{nullCoalesce}} };""";
            }
        }));

        var eventSubscriptions = string.Join("\n", model.Properties.Where(p => p.IsWritable).Select(prop =>
        {
            var genericArg = prop.GenericArgument;
            var entityPropAccess = $"entity.{prop.Name}";

            if (prop.IsComplexViewModel)
            {
                // 🔹 复杂 VM：调用 .Build() 还原实体
                return $$"""
            {{prop.Name}}.Changed += (s, e) =>
            {
                if (e is ValueChangeEventArgs<{{genericArg}}> ee)
                    {{entityPropAccess}} = ee.NewValue.Build();
                _throttle.Execute(OnEntityChanged);
            };
""";
            }
            else if (prop.IsUserDeclared)
            {
                // 🔹 简单/可空类型：统一使用 ?? default 安全回写
                return $$"""
            {{prop.Name}}.Changed += (s, e) =>
            {
                if (e is ValueChangeEventArgs<{{genericArg}}> ee)
                    {{entityPropAccess}} = ee.NewValue ?? default;
                _throttle.Execute(OnEntityChanged);
            };
""";
            }
            else
            {
                // 🔹 自动生成
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
            }
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
        public bool IsComplexViewModel { get; } // 🔹 新增：区分复杂VM与简单可空类型

        public PropertyInfo(string name, string genericArgument, bool isNullable, bool isWritable,
            string entityTypeName, bool isUserDeclared, bool isComplexViewModel)
        {
            Name = name;
            GenericArgument = genericArgument;
            IsNullable = isNullable;
            IsWritable = isWritable;
            EntityTypeName = entityTypeName;
            IsUserDeclared = isUserDeclared;
            IsComplexViewModel = isComplexViewModel;
        }
    }
}