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

                                // 🔍 诊断打印：输出两侧完整类型元数据
                                logs.Add($"  🔍 [DEBUG] 检查属性: {prop.Name}");
                                logs.Add(DumpTypeDebugInfo(vmInnerType, "VM Inner Type"));
                                logs.Add(DumpTypeDebugInfo(entityPropType, "Entity Prop Type"));

                                string vmClean = GetCleanTypeName(vmInnerType);
                                string entityClean = GetCleanTypeName(entityPropType);
                                logs.Add($"  📏 比对字符串: VM='{vmClean}' | Entity='{entityClean}'");

                                bool isSimpleMatch = vmClean == entityClean;
                                bool isComplexVm = false;

                                if (!isSimpleMatch && vmInnerType is INamedTypeSymbol vmNamed)
                                {
                                    isComplexVm = CheckIViewModelMatch(vmNamed, entityClean);
                                    logs.Add($"  🔗 IViewModel<T> 接口匹配结果: {isComplexVm}");
                                }

                                logs.Add($"  ✅ 最终匹配结果: Simple={isSimpleMatch} | Complex={isComplexVm}");

                                if (isSimpleMatch || isComplexVm)
                                {
                                    string genericArg2 = vmInnerType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                                    bool isNullable2 = vmInnerType.IsReferenceType ||
                                                      vmInnerType.NullableAnnotation == NullableAnnotation.Annotated ||
                                                      (vmInnerType is INamedTypeSymbol n && n.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T);

                                    properties.Add(new PropertyInfo(
                                        prop.Name, genericArg2, isNullable2, isWritable: true,
                                        entityType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                                        isUserDeclared: true, isComplexViewModel: isComplexVm));

                                    logs.Add($"  ➕ {prop.Name}: {genericArg2} (UserDeclared, {(isComplexVm ? "ComplexVM" : "SimpleNullable")})");
                                    continue;
                                }
                            }
                            logs.Add($"  ⏭️ {prop.Name}: Skipped (UserDeclared, type mismatch)");
                            continue;
                        }

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
                currentType = currentType.BaseType;
            }
        }

        logs.Add($"[End] Total properties: {properties.Count}");
        if (properties.Count == 0) return null;

        return new GenerationModel(className, ns, entityTypes, properties, logs);
    }

    // 🔍 诊断辅助：格式化类型元数据
    private static string DumpTypeDebugInfo(ITypeSymbol? type, string label)
    {
        if (type == null) return $"{label}: null";
        var named = type as INamedTypeSymbol;
        var typeArgInfo = named != null && named.IsGenericType && named.TypeArguments.Length > 0
            ? $"TypeArg[0]: {named.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}"
            : "TypeArgs: N/A";

        return $$"""
{{label}}:
  DisplayString: {{type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}}
  OriginalDef:   {{type.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}}
  SpecialType:   {{type.SpecialType}}
  IsValueType:   {{type.IsValueType}}
  NullableAnn:   {{type.NullableAnnotation}}
  Namespace:     {{type.ContainingNamespace?.ToDisplayString()}}
  {{typeArgInfo}}
""";
    }

    private static string GetCleanTypeName(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol named && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            type = named.TypeArguments[0];

        type = type.WithNullableAnnotation(NullableAnnotation.NotAnnotated);
        return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }

    private static bool CheckIViewModelMatch(INamedTypeSymbol vmType, string entityCleanName)
    {
        foreach (var iface in vmType.AllInterfaces)
        {
            if (iface.OriginalDefinition.Name == "IViewModel" && iface.TypeArguments.Length == 1)
            {
                if (GetCleanTypeName(iface.TypeArguments[0]) == entityCleanName)
                    return true;
            }
        }
        return false;
    }

    private static bool IsModifiableViewModel(INamedTypeSymbol type) =>
        type.OriginalDefinition.Name == "ModifiableViewModel" &&
        type.ContainingNamespace?.ToDisplayString() == "FMO.Shared";

    private static string GenerateSource(GenerationModel model)
    {
        // 🔍 强制开启诊断日志输出（换行自动转注释格式）
        var debugHeader = string.Join("\n",
            new[] { "// 🔍 ===== EntityModifiable DEBUG DUMP =====" }
            .Concat(model.DebugLogs.Select(l => $"// {l.Replace("\n", "\n// ")}"))
            .Concat(new[] { "// =========================================\n" }));

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
            var entityAccess = $"entity.{prop.Name}";
            if (prop.IsComplexViewModel)
                return $$"""            {{prop.Name}} = new() { NewValue = new {{prop.GenericArgument}}({{entityAccess}}), OldValue = new {{prop.GenericArgument}}({{entityAccess}}) };""";
            else if (prop.IsUserDeclared)
                return $$"""            {{prop.Name}} = new() { NewValue = {{entityAccess}}, OldValue = {{entityAccess}} };""";
            else
            {
                var nullCoalesce = prop.IsNullable ? " ?? default" : "";
                return $$"""            {{prop.Name}} = new() { NewValue = CloneHelper.CloneValue({{entityAccess}}), OldValue = {{entityAccess}}{{nullCoalesce}} };""";
            }
        }));

        var eventSubscriptions = string.Join("\n", model.Properties.Where(p => p.IsWritable).Select(prop =>
        {
            var genericArg = prop.GenericArgument;
            var entityAccess = $"entity.{prop.Name}";

            if (prop.IsComplexViewModel)
                return $$"""
            {{prop.Name}}.Changed += (s, e) =>
            {
                if (e is ValueChangeEventArgs<{{genericArg}}> ee)
                    {{entityAccess}} = ee.NewValue.Build();
                _throttle.Execute(OnEntityChanged);
            };
""";
            else if (prop.IsUserDeclared)
                return $$"""
            {{prop.Name}}.Changed += (s, e) =>
            {
                if (e is ValueChangeEventArgs<{{genericArg}}> ee)
                    {{entityAccess}} = ee.NewValue ?? default;
                _throttle.Execute(OnEntityChanged);
            };
""";
            else
            {
                var nullCoalesce = prop.IsNullable ? " ?? default" : "";
                var stringFix = (genericArg == "string") ? " ?? \"\"" : nullCoalesce;
                return $$"""
            {{prop.Name}}.Changed += (s, e) =>
            {
                if (e is ValueChangeEventArgs<{{genericArg}}> ee)
                    {{entityAccess}} = ee.NewValue{{stringFix}};
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

{{propertyDeclarations}}

        public void FillBy({{primaryEntityTypeName}}? entity)
        {
            if (entity is null) throw new InvalidDataException("entity 不能为null");

{{initAssignments}}

{{eventSubscriptions}}
        }

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
        public bool IsComplexViewModel { get; }

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