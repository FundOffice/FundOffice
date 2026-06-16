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

    private static readonly SymbolDisplayFormat FullyQualifiedWithNullableFormat = SymbolDisplayFormat.FullyQualifiedFormat
        .AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

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
        logs.Add($"[Start] {targetClass.Name}");

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

        bool hasIsReadOnly = declaredPropertiesMap.ContainsKey("IsReadOnly");
        bool supportsInpc = targetClass.AllInterfaces.Any(i =>
            i.Name == "INotifyPropertyChanged" &&
            i.ContainingNamespace?.ToDisplayString() == "System.ComponentModel");
        bool derivesObservableObject = DerivesFromObservableObject(targetClass);
        logs.Add($"  ℹ️ IsReadOnly: has={hasIsReadOnly}, generate={!hasIsReadOnly}, inpc={supportsInpc}, observableObject={derivesObservableObject}");

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
                        string entityPropTypeStr = prop.Type.ToDisplayString(FullyQualifiedWithNullableFormat);
                        bool isEntityValOrStr = prop.Type.IsValueType || prop.Type.SpecialType == SpecialType.System_String;
                        bool isEntityString = GetCleanTypeName(prop.Type) == "string";

                        if (isUserDeclared && declaredProp != null)
                        {
                            var declaredType = declaredProp.Type as INamedTypeSymbol;
                            if (declaredType != null && IsModifiableViewModel(declaredType))
                            {
                                var typeArgs = declaredType.TypeArguments;
                                if (typeArgs.Length > 0)
                                {
                                    int argCount = typeArgs.Length;
                                    var typeArg1 = typeArgs[0];
                                    var typeArg2 = argCount > 1 ? typeArgs[1] : typeArgs[0];

                                    string t1Str = typeArg1.ToDisplayString(FullyQualifiedWithNullableFormat);
                                    string t2Str = typeArg2.ToDisplayString(FullyQualifiedWithNullableFormat);

                                    string t1Clean = GetCleanTypeName(typeArg1);
                                    string entityClean = GetCleanTypeName(prop.Type);
                                    bool isT1Entity = t1Clean == entityClean;

                                    bool isNullable = IsNullableType(typeArg1);

                                    properties.Add(new PropertyInfo(
                                        prop.Name, t1Str, t2Str, argCount, isT1Entity,
                                        isNullable, true, entityPropTypeStr, true, isEntityValOrStr, isEntityString));

                                    logs.Add($"  ➕ {prop.Name}: UserDeclared (Args:{argCount}, T1IsEntity:{isT1Entity})");
                                    continue;
                                }
                            }
                            logs.Add($"  ⏭️ {prop.Name}: Skipped (UserDeclared, not ModifiableViewModel)");
                            continue;
                        }

                        // 自动生成：默认只有 1 个参数，且就是 Entity 类型 (情况1)
                        properties.Add(new PropertyInfo(
                            prop.Name, entityPropTypeStr, entityPropTypeStr, 1, true,
                            IsNullableType(prop.Type), true, entityPropTypeStr, false, isEntityValOrStr, isEntityString));

                        logs.Add($"  ➕ {prop.Name}: AutoGenerated");
                    }
                }
                currentType = currentType.BaseType;
            }
        }

        logs.Add($"[End] Total properties: {properties.Count}");
        if (properties.Count == 0) return null;

        return new GenerationModel(className, ns, entityTypes, properties, logs, !hasIsReadOnly, supportsInpc, derivesObservableObject);
    }

    private static bool IsNullableType(ITypeSymbol type)
    {
        if (type.IsReferenceType) return type.NullableAnnotation != NullableAnnotation.NotAnnotated;
        return type is INamedTypeSymbol n && n.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
    }

    private static string GetCleanTypeName(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol named && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            type = named.TypeArguments[0];
        return type.WithNullableAnnotation(NullableAnnotation.NotAnnotated).ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }

    private static bool IsModifiableViewModel(INamedTypeSymbol type) =>
        type.OriginalDefinition.Name == "ModifiableViewModel" &&
        type.ContainingNamespace?.ToDisplayString() == "FMO.Shared";

    private static bool DerivesFromObservableObject(INamedTypeSymbol type)
    {
        INamedTypeSymbol? current = type.BaseType;
        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            if (current.Name == "ObservableObject" &&
                current.ContainingNamespace?.ToDisplayString() == "CommunityToolkit.Mvvm.ComponentModel")
                return true;
            current = current.BaseType;
        }
        return false;
    }

    private static string GenerateSource(GenerationModel model)
    {
        var debugHeader = string.Join("\n",
            new[] { "// 🔍 ===== EntityModifiable DEBUG DUMP =====" }
            .Concat(model.DebugLogs.Select(l => $"// {l.Replace("\n", "\n// ")}"))
            .Concat(new[] { "// =========================================\n" }));

        var namespaceOpen = !string.IsNullOrEmpty(model.Namespace) ? $"namespace {model.Namespace}\n{{" : "";
        var namespaceClose = !string.IsNullOrEmpty(model.Namespace) ? "}" : "";

        var isReadOnlyDeclaration = model.GenerateIsReadOnly
            ? (model.DerivesFromObservableObject
                ? """

        public bool IsReadOnly { get => field; set => SetProperty(ref field, value); } = true;
"""
                : model.SupportsINotifyPropertyChanged
                    ? """

        private bool _isReadOnly = true;

        public bool IsReadOnly
        {
            get => _isReadOnly;
            set
            {
                if (_isReadOnly != value)
                {
                    _isReadOnly = value;
                    PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsReadOnly)));
                }
            }
        }
"""
                    : """

        public bool IsReadOnly { get; set; } = true;
""")
            : string.Empty;

        var propertyDeclarations = string.Join("\n\n", model.Properties.Where(p => !p.IsUserDeclared).Select(prop =>
        {
            var genericArgClean = prop.TypeArg1.TrimEnd('?');
            var nullableMark = prop.IsNullable ? "?" : "";
            return $$"""
        public ModifiableViewModel<{{genericArgClean}}{{nullableMark}}> {{prop.Name}} { get; private set; } = null!;
""";
        }));

        var initAssignments = string.Join("\n", model.Properties.Select(prop =>
        {
            var entityAccess = $"entity.{prop.Name}";
            bool applyDefaultToNull = prop.IsEntityValueTypeOrString && prop.IsNullable;

            string newValueExpr;
            string oldValueExpr;

            if (prop.ArgCount == 2)
            {
                // 🎯 情况三：ModifiableViewModel<TEntity, TVM>
                // NewValue = new TVM(entity), OldValue = entity
                newValueExpr = $"new {prop.TypeArg2}({entityAccess})";
                oldValueExpr = applyDefaultToNull
                    ? $"{entityAccess} == default ? null : {entityAccess}"
                    : (prop.IsNullable ? $"{entityAccess} ?? default" : entityAccess);
            }
            else if (prop.ArgCount == 1)
            {
                if (prop.IsT1Entity)
                {
                    // 🎯 情况一：ModifiableViewModel<TEntity>
                    // NewValue = clone, OldValue = entity
                    string cloneExpr = $"CloneHelper.CloneValue({entityAccess})";
                    newValueExpr = applyDefaultToNull
                        ? $"{entityAccess} == default ? null : {cloneExpr}"
                        : cloneExpr;

                    oldValueExpr = applyDefaultToNull
                        ? $"{entityAccess} == default ? null : {entityAccess}"
                        : (prop.IsNullable ? $"{entityAccess} ?? default" : entityAccess);
                }
                else
                {
                    // 🎯 情况二：ModifiableViewModel<TVM>
                    // NewValue = new TVM(entity), OldValue = new TVM(entity)
                    newValueExpr = $"new {prop.TypeArg1}({entityAccess})";
                    oldValueExpr = $"new {prop.TypeArg1}({entityAccess})";
                }
            }
            else
            {
                newValueExpr = entityAccess;
                oldValueExpr = entityAccess;
            }

            return $$"""            {{prop.Name}} = new() { NewValue = {{newValueExpr}}, OldValue = {{oldValueExpr}} };""";
        }));

        var eventSubscriptions = string.Join("\n", model.Properties.Where(p => p.IsWritable).Select(prop =>
        {
            var entityAccess = $"entity.{prop.Name}";

            // 🎯 铁律：Changed 事件中的泛型参数永远是第一个模板参数
            string eventGenericType = prop.TypeArg1;

            if (prop.ArgCount == 1 && !prop.IsT1Entity)
            {
                // 情况二：单泛型且是 TVM，ee.NewValue 是 TVM，需要 Build() 转回 Entity
                return $$"""
            {{prop.Name}}.Changed += e =>
            {
                if (e is ValueChangeEventArgs<{{eventGenericType}}> ee)
                    {{entityAccess}} = ee.NewValue?.Build() ?? default;
                _throttle.Execute(OnEntityChanged);
            };
""";
            }
            else
            {
                // 情况一 (TEntity) 和 情况三 (TEntity, TVM)
                // 第一个参数都是 TEntity，ee.NewValue 直接就是 Entity，不需要 Build()！
                var nullCoalesce = prop.IsNullable ? " ?? default" : "";
                var stringFix = prop.IsEntityString ? " ?? \"\"" : nullCoalesce;

                return $$"""
            {{prop.Name}}.Changed += e =>
            {
                if (e is ValueChangeEventArgs<{{eventGenericType}}> ee)
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
{{isReadOnlyDeclaration}}

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
        public bool GenerateIsReadOnly { get; }
        public bool SupportsINotifyPropertyChanged { get; }
        public bool DerivesFromObservableObject { get; }

        public GenerationModel(string className, string @namespace, List<INamedTypeSymbol> entityTypes,
            List<PropertyInfo> properties, List<string> logs, bool generateIsReadOnly, bool supportsINotifyPropertyChanged, bool derivesFromObservableObject)
        {
            ClassName = className;
            Namespace = @namespace;
            EntityTypes = entityTypes;
            Properties = properties;
            DebugLogs = logs;
            GenerateIsReadOnly = generateIsReadOnly;
            SupportsINotifyPropertyChanged = supportsINotifyPropertyChanged;
            DerivesFromObservableObject = derivesFromObservableObject;
        }
    }

    private class PropertyInfo
    {
        public string Name { get; }
        public string TypeArg1 { get; }
        public string TypeArg2 { get; }
        public int ArgCount { get; }
        public bool IsT1Entity { get; }

        public bool IsNullable { get; }
        public bool IsWritable { get; }
        public string EntityTypeName { get; }
        public bool IsUserDeclared { get; }
        public bool IsEntityValueTypeOrString { get; }
        public bool IsEntityString { get; }

        public PropertyInfo(string name, string typeArg1, string typeArg2, int argCount, bool isT1Entity,
            bool isNullable, bool isWritable, string entityTypeName, bool isUserDeclared,
            bool isEntityValueTypeOrString, bool isEntityString)
        {
            Name = name;
            TypeArg1 = typeArg1;
            TypeArg2 = typeArg2;
            ArgCount = argCount;
            IsT1Entity = isT1Entity;
            IsNullable = isNullable;
            IsWritable = isWritable;
            EntityTypeName = entityTypeName;
            IsUserDeclared = isUserDeclared;
            IsEntityValueTypeOrString = isEntityValueTypeOrString;
            IsEntityString = isEntityString;
        }
    }
}