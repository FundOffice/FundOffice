using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SG;

[Generator]
public class IViewModelIncrementalGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var classDeclarations = context.SyntaxProvider.CreateSyntaxProvider(
            predicate: (node, _) => node is ClassDeclarationSyntax,
            transform: (ctx, ct) =>
            {
                var classDecl = (ClassDeclarationSyntax)ctx.Node;
                var classSymbol = ctx.SemanticModel.GetDeclaredSymbol(classDecl, ct) as INamedTypeSymbol;
                if (classSymbol == null) return null;

                var iViewModelInterface = classSymbol.AllInterfaces.FirstOrDefault(i =>
                    i.Name == "IViewModel" && i.TypeArguments.Length == 2);

                if (iViewModelInterface == null) return null;

                var typeArg = iViewModelInterface.TypeArguments[0] as INamedTypeSymbol;
                if (typeArg == null) return null;

                return BuildGenerationModel(classSymbol, typeArg);
            })
            .Where(model => model != null);

        context.RegisterSourceOutput(classDeclarations, (spc, model) =>
        {
            if (model == null) return;
            var source = GenerateSource(model);
            spc.AddSource($"{model.ClassName}.vm.g.cs", SourceText.From(source, Encoding.UTF8));
        });
    }

    private static GenerationModel? BuildGenerationModel(INamedTypeSymbol targetClass, INamedTypeSymbol sourceType)
    {
        var logs = new List<string>();
        logs.Add($"[Start] {targetClass.Name} <- {sourceType.Name}");

        var ns = targetClass.ContainingNamespace.IsGlobalNamespace ? string.Empty : targetClass.ContainingNamespace.ToDisplayString();
        var className = targetClass.Name;
        var sourceTypeName = sourceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        var viewModelDeclaredProperties = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in targetClass.GetMembers())
            if (member is IPropertySymbol p && p.DeclaredAccessibility == Accessibility.Public)
                viewModelDeclaredProperties.Add(p.Name);

        logs.Add($"[VM Declared] {string.Join(", ", viewModelDeclaredProperties)}");

        var existingInHierarchy = new HashSet<string>(StringComparer.Ordinal);
        INamedTypeSymbol? current = targetClass.BaseType;

        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            logs.Add($"🔍 遍历VM类型：{current.Name}");
            foreach (var member in current.GetMembers())
            {
                if (member is IPropertySymbol p && p.DeclaredAccessibility == Accessibility.Public)
                {
                    existingInHierarchy.Add(p.Name);
                    logs.Add($"✅ 已包含VM属性：{p.Name}");
                }
            }

            var iViewModelInterface = current.AllInterfaces.FirstOrDefault(i => i.Name == "IViewModel" && i.TypeArguments.Length == 2);

            if (iViewModelInterface != null)
            {
                logs.Add($"ℹ️ 类型 {current.Name} 实现了 IViewModel，开始收集源模型属性");
                var srcType = iViewModelInterface.TypeArguments[0] as INamedTypeSymbol;

                if (srcType != null)
                {
                    logs.Add($"🔍 遍历源模型：{srcType.Name}");
                    foreach (var sourceMember in srcType.GetMembers())
                    {
                        if (sourceMember is IPropertySymbol sp && sp.DeclaredAccessibility == Accessibility.Public)
                        {
                            existingInHierarchy.Add(sp.Name);
                            logs.Add($"✅ 已包含源模型属性：{sp.Name}");
                        }
                    }
                }
            }

            current = current.BaseType;
        }

        logs.Add($"📊 最终已存在属性总数：{existingInHierarchy.Count}\n");

        var propertiesToGenerate = new List<PropertyInfo>();
        var propertiesToAssignOnly = new List<PropertyInfo>();

        var currentType = sourceType;
        while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
        {
            foreach (var member in currentType.GetMembers())
            {
                if (member is IPropertySymbol prop &&
                    prop.DeclaredAccessibility == Accessibility.Public &&
                    !prop.IsStatic)
                {
                    ITypeSymbol propType = prop.Type;
                    string propTypeString = propType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                    INamedTypeSymbol? propNamedType = propType switch
                    {
                        INamedTypeSymbol named => named,
                        IArrayTypeSymbol array when array.ElementType is INamedTypeSymbol elem => elem,
                        _ => null
                    };

                    if (propNamedType == null) continue;

                    if (viewModelDeclaredProperties.Contains(prop.Name))
                    {
                        logs.Add($"  ⏭️ Skip {prop.Name} (VM already declares)");
                        continue;
                    }

                    // 🔹 精确计算源属性是否可空
                    bool sourceIsNullable;
                    if (propType.IsReferenceType || propType is IArrayTypeSymbol)
                    {
                        // 引用类型：只要没有明确标记为 NotAnnotated，就认为是可空的
                        sourceIsNullable = prop.NullableAnnotation != NullableAnnotation.NotAnnotated;
                    }
                    else
                    {
                        // 值类型：只有 Nullable<T> 是可空的
                        sourceIsNullable = propType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
                    }

                    // 🔹 计算 VM 属性是否可空 (用于生成 VM 属性声明)
                    bool vmIsNullable = propType.IsReferenceType ||
                                        (propType is IArrayTypeSymbol) ||
                                        prop.NullableAnnotation == NullableAnnotation.Annotated;

                    bool isWritable = prop.SetMethod != null;
                    bool isString = propType.SpecialType == SpecialType.System_String;
                    bool isValueType = propType.IsValueType;

                    var (isNested, vmType) = propType is IArrayTypeSymbol
                        ? (false, null)
                        : CheckNestedViewModelPattern(targetClass, prop.Name, propNamedType);

                    var vmTypeName = isNested && vmType != null
                        ? vmType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                        : propTypeString;

                    var propInfo = new PropertyInfo(
                        propTypeString,
                        prop.Name,
                        sourceIsNullable,
                        vmIsNullable,
                        isWritable,
                        isNested,
                        vmTypeName,
                        isString,
                        isValueType);

                    if (existingInHierarchy.Contains(prop.Name))
                    {
                        propertiesToAssignOnly.Add(propInfo);
                        logs.Add($"     ➡️ AssignOnly");
                    }
                    else
                    {
                        propertiesToGenerate.Add(propInfo);
                        logs.Add($"     ➡️ Generate");
                    }
                }
            }
            currentType = currentType.BaseType;
        }

        logs.Add($"[End] Generate={propertiesToGenerate.Count} | AssignOnly={propertiesToAssignOnly.Count}");

        if (propertiesToGenerate.Count == 0 && propertiesToAssignOnly.Count == 0)
            return null;

        bool needsINPC = true, hasAutoBase = false;
        var baseType = targetClass.BaseType;

        while (baseType != null && baseType.SpecialType != SpecialType.System_Object)
        {
            if (baseType.AllInterfaces.Any(i => i.Name == "IViewModel" && i.TypeArguments.Length == 2))
            {
                needsINPC = false;
                hasAutoBase = true;
                break;
            }
            else if (baseType.Name.Contains("ObservableObject"))
            {
                needsINPC = false;
                hasAutoBase = false;
                break;
            }
            baseType = baseType.BaseType;
        }

        bool hasManualEquals = false;
        var checkType = targetClass;
        while (checkType != null && checkType.SpecialType != SpecialType.System_Object)
        {
            if (checkType.GetMembers("Equals").OfType<IMethodSymbol>().Any(m =>
                m.Parameters.Length == 1 &&
                SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, sourceType)))
            {
                hasManualEquals = true;
                break;
            }
            checkType = checkType.BaseType;
        }

        bool hasManualGetHashCode = false;
        var checkTypeForHash = targetClass;
        while (checkTypeForHash != null && checkTypeForHash.SpecialType != SpecialType.System_Object)
        {
            if (checkTypeForHash.GetMembers("GetHashCode").OfType<IMethodSymbol>().Any(m => m.Parameters.Length == 0))
            {
                hasManualGetHashCode = true;
                break;
            }
            checkTypeForHash = checkTypeForHash.BaseType;
        }

        bool hasManualObjectEquals = false;
        var checkTypeForObjEq = targetClass;
        while (checkTypeForObjEq != null && checkTypeForObjEq.SpecialType != SpecialType.System_Object)
        {
            if (checkTypeForObjEq.GetMembers("Equals").OfType<IMethodSymbol>().Any(m =>
                m.Parameters.Length == 1 &&
                m.Parameters[0].Type.SpecialType == SpecialType.System_Object &&
                m.IsOverride))
            {
                hasManualObjectEquals = true;
                break;
            }
            checkTypeForObjEq = checkTypeForObjEq.BaseType;
        }

        logs.Add($"最终 needsINPC = {needsINPC} | hasManualEquals = {hasManualEquals} | 类名：{targetClass.Name}");

        return new GenerationModel(className, ns, sourceTypeName,
            propertiesToGenerate, propertiesToAssignOnly, needsINPC, hasAutoBase,
            hasManualEquals, hasManualGetHashCode, hasManualObjectEquals, sourceType.IsReferenceType, logs);
    }

    private static string GenerateSource(GenerationModel model)
    {
#if DEBUG
        var debugHeader = string.Join("\n",
    new[] { "// 🔍 ===== AutoViewModel Debug Info =====" }
    .Concat(model.DebugLogs.Select(l => $"// {l}"))
    .Concat(new[] { "// =====================================\n" }));
#else
        var debugHeader = "";
#endif

        var inpcBlock = model.NeedsINPC ? """
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (!global::System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value))
        {
            field = value;
            OnPropertyChanged(propertyName);
        }
    }
""" : "";

        var fillAssignments = GenerateAssignments(model.PropertiesToGenerate, model.PropertiesToAssignOnly, "val");

        var generatedProperties = string.Join("\n\n", model.PropertiesToGenerate.Select(prop =>
        {
            var typeName = prop.GetViewModelPropertyTypeString();
            return $$"""

        public {{typeName}} {{prop.Name}} { get => field; set => SetProperty(ref field, value); }
  
""";
        }));

        var buildInitializers = string.Join("\n\t\t\t\t",
            model.PropertiesToGenerate.Concat(model.PropertiesToAssignOnly)
                .Where(p => p.IsWritable)
                .Select(p => p.GetBuildInitializer())
                .Where(s => !string.IsNullOrEmpty(s)));

        var namespaceOpen = !string.IsNullOrEmpty(model.Namespace) ? $"namespace {model.Namespace}\n{{" : "";
        var namespaceClose = !string.IsNullOrEmpty(model.Namespace) ? "}" : "";
        var inpcInheritance = model.NeedsINPC ? " : INotifyPropertyChanged" : "";
        var overnew = model.HasAutoBase ? "new" : "";

        var equalsBlock = "";
        if (!model.HasManualEquals)
        {
            var equalsLines = new List<string>();
            var paramType = model.SourceTypeIsReferenceType ? $"{model.SourceTypeName}?" : model.SourceTypeName;

            equalsLines.Add($"        public bool Equals({paramType} other)");
            equalsLines.Add("        {");
            if (model.SourceTypeIsReferenceType)
            {
                equalsLines.Add("            if (other is null) return false;");
            }

            var allProps = model.PropertiesToGenerate.Concat(model.PropertiesToAssignOnly);
            foreach (var prop in allProps)
            {
                if (prop.IsNestedViewModel)
                {
                    if (prop.VmIsNullable)
                    {
                        equalsLines.Add($"            if (!global::System.Collections.Generic.EqualityComparer<{prop.SourceTypeName}>.Default.Equals({prop.Name}?.Build(), other.{prop.Name})) return false;");
                    }
                    else
                    {
                        equalsLines.Add($"            if (!global::System.Collections.Generic.EqualityComparer<{prop.SourceTypeName}>.Default.Equals({prop.Name}!.Build(), other.{prop.Name})) return false;");
                    }
                }
                else
                {
                    equalsLines.Add($"            if (!global::System.Collections.Generic.EqualityComparer<{prop.SourceTypeName}>.Default.Equals({prop.Name}, other.{prop.Name})) return false;");
                }
            }
            equalsLines.Add("            return true;");
            equalsLines.Add("        }");
            equalsBlock = string.Join("\n", equalsLines);
        }

        var objectEqualsBlock = "";
        if (!model.HasManualEquals && !model.HasManualObjectEquals)
        {
            objectEqualsBlock = $$"""
        public override bool Equals(object? obj)
        {
            return obj is {{model.SourceTypeName}} other && Equals(other);
        }
""";
        }

        var getHashCodeBlock = "";
        if (!model.HasManualGetHashCode)
        {
            var hashCodeLines = new List<string>();
            hashCodeLines.Add("        public override int GetHashCode()");
            hashCodeLines.Add("        {");
            hashCodeLines.Add("            unchecked");
            hashCodeLines.Add("            {");
            hashCodeLines.Add("                int hash = 17;");

            var allProps = model.PropertiesToGenerate.Concat(model.PropertiesToAssignOnly);
            foreach (var prop in allProps)
            {
                if (prop.IsNestedViewModel)
                {
                    if (prop.VmIsNullable)
                        hashCodeLines.Add($"                hash = hash * 31 + global::System.Collections.Generic.EqualityComparer<{prop.SourceTypeName}>.Default.GetHashCode({prop.Name}?.Build()!);");
                    else
                        hashCodeLines.Add($"                hash = hash * 31 + global::System.Collections.Generic.EqualityComparer<{prop.SourceTypeName}>.Default.GetHashCode({prop.Name}!.Build()!);");
                }
                else
                {
                    hashCodeLines.Add($"                hash = hash * 31 + global::System.Collections.Generic.EqualityComparer<{prop.SourceTypeName}>.Default.GetHashCode({prop.Name}!);");
                }
            }

            hashCodeLines.Add("                return hash;");
            hashCodeLines.Add("            }");
            hashCodeLines.Add("        }");
            getHashCodeBlock = string.Join("\n", hashCodeLines);
        }

        var transMethods = $$"""
        public static {{model.SourceTypeName}} Trans({{model.ClassName}} vm)
        {
            if (vm is null) return default!;
            return vm.Build();
        }

        public static {{model.ClassName}} Trans({{model.SourceTypeName}}? val)
        {
            var vm = new {{model.ClassName}}();
            vm.FillBy(val);
            return vm;
        }
""";

        return $$"""
#nullable enable
{{debugHeader}}
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

{{namespaceOpen}}
    public partial class {{model.ClassName}}{{inpcInheritance}}
    {
{{inpcBlock}}
        public {{model.ClassName}}() { }

        public {{model.ClassName}}({{model.SourceTypeName}}? val)
        {
             if(val is not null)
                FillBy(val);
        }

{{transMethods}}

{{equalsBlock}}

{{objectEqualsBlock}}

{{getHashCodeBlock}}

        public {{model.ClassName}} FillBy({{model.SourceTypeName}}? obj)
        {
             if(obj is {{model.SourceTypeName}} val)
              {
{{fillAssignments}}
              }
              return this;
        }

        public {{overnew}} {{model.SourceTypeName}} Build()
        {
            var result = new {{model.SourceTypeName}}
             {
                {{buildInitializers}}
             };
            return result;
        }

{{generatedProperties}}
    }
{{namespaceClose}}
""";
    }

    private static string GenerateAssignments(
        List<PropertyInfo> toGenerate,
        List<PropertyInfo> toAssignOnly,
        string sourceVar)
    {
        var lines = new List<string>();

        foreach (var prop in toGenerate)
            lines.Add($"                {prop.GetFillAssignment(sourceVar)}");

        foreach (var prop in toAssignOnly)
            lines.Add($"                {prop.GetFillAssignment(sourceVar)}");

        return string.Join("\n", lines);
    }

    private static (bool IsMatch, INamedTypeSymbol? ViewModelType) CheckNestedViewModelPattern(
        INamedTypeSymbol viewModelClass,
        string propertyName,
        INamedTypeSymbol sourcePropertyType)
    {
        INamedTypeSymbol? current = viewModelClass;
        IPropertySymbol? vmProperty = null;

        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            vmProperty = current.GetMembers(propertyName)
                .OfType<IPropertySymbol>()
                .FirstOrDefault(p => p.DeclaredAccessibility == Accessibility.Public);

            if (vmProperty != null)
                break;

            current = current.BaseType;
        }

        if (vmProperty?.Type is not INamedTypeSymbol vmPropertyType)
            return (false, null);

        var expectedVmTypeName = sourcePropertyType.Name + "ViewModel";

        if (vmPropertyType.Name == expectedVmTypeName &&
            HasConstructorWithSourceType(vmPropertyType, sourcePropertyType))
        {
            return (true, vmPropertyType);
        }

        return (false, null);
    }

    private static bool HasConstructorWithSourceType(INamedTypeSymbol viewModelType, INamedTypeSymbol sourceType)
    {
        foreach (var ctor in viewModelType.Constructors)
        {
            if (ctor.Parameters.Length == 1 &&
                SymbolEqualityComparer.Default.Equals(ctor.Parameters[0].Type, sourceType))
            {
                return true;
            }
        }
        return false;
    }

    private class GenerationModel
    {
        public string ClassName { get; }
        public string Namespace { get; }
        public string SourceTypeName { get; }
        public List<PropertyInfo> PropertiesToGenerate { get; }
        public List<PropertyInfo> PropertiesToAssignOnly { get; }
        public bool NeedsINPC { get; }
        public bool HasAutoBase { get; }
        public bool HasManualEquals { get; }
        public bool HasManualGetHashCode { get; }
        public bool HasManualObjectEquals { get; }
        public bool SourceTypeIsReferenceType { get; }
        public List<string> DebugLogs { get; }

        public GenerationModel(
            string className,
            string @namespace,
            string sourceTypeName,
            List<PropertyInfo> propertiesToGenerate,
            List<PropertyInfo> propertiesToAssignOnly,
            bool needsINPC,
            bool hasAutoBase,
            bool hasManualEquals,
            bool hasManualGetHashCode,
            bool hasManualObjectEquals,
            bool sourceTypeIsReferenceType,
            List<string> logs)
        {
            ClassName = className;
            Namespace = @namespace;
            SourceTypeName = sourceTypeName;
            PropertiesToGenerate = propertiesToGenerate;
            PropertiesToAssignOnly = propertiesToAssignOnly;
            NeedsINPC = needsINPC;
            HasAutoBase = hasAutoBase;
            HasManualEquals = hasManualEquals;
            HasManualGetHashCode = hasManualGetHashCode;
            HasManualObjectEquals = hasManualObjectEquals;
            SourceTypeIsReferenceType = sourceTypeIsReferenceType;
            DebugLogs = logs;
        }
    }

    private class PropertyInfo
    {
        public string SourceTypeName { get; }
        public string ViewModelTypeName { get; }
        public string Name { get; }
        public bool SourceIsNullable { get; } // 源属性是否可空
        public bool VmIsNullable { get; }     // VM 属性是否可空
        public bool IsWritable { get; }
        public bool IsNestedViewModel { get; }
        public bool IsString { get; }
        public bool IsValueType { get; }

        public PropertyInfo(string sourceTypeName, string name, bool sourceIsNullable, bool vmIsNullable, bool isWritable,
            bool isNestedViewModel, string viewModelTypeName, bool isString, bool isValueType)
        {
            SourceTypeName = sourceTypeName;
            Name = name;
            SourceIsNullable = sourceIsNullable;
            VmIsNullable = vmIsNullable;
            IsWritable = isWritable;
            IsNestedViewModel = isNestedViewModel;
            ViewModelTypeName = viewModelTypeName;
            IsString = isString;
            IsValueType = isValueType;
        }

        public string GetViewModelPropertyTypeString()
        {
            var typeName = IsNestedViewModel ? ViewModelTypeName : SourceTypeName;
            return VmIsNullable ? $"{typeName}?" : typeName;
        }

        public string GetFillAssignment(string sourceVar)
        {
            if (IsNestedViewModel)
            {
                return VmIsNullable
                    ? $"{Name} = {sourceVar}.{Name} != null ? new {ViewModelTypeName}({sourceVar}.{Name}) : null;"
                    : $"{Name} = new {ViewModelTypeName}({sourceVar}.{Name});";
            }
            return $"{Name} = {sourceVar}.{Name};";
        }

        public string GetBuildInitializer()
        {
            if (IsNestedViewModel)
            {
                return VmIsNullable
                    ? $"{Name} = {Name}?.Build(),"
                    : $"{Name} = {Name}!.Build(),";
            }

            if (!IsWritable)
                return string.Empty;

            // 🔹 核心逻辑：如果源属性不可空，但 VM 属性可空，需要处理 null 值
            if (!SourceIsNullable && VmIsNullable)
            {
                if (IsString)
                {
                    return $"{Name} = {Name} ?? string.Empty,";
                }
                if (IsValueType)
                {
                    return $"{Name} = {Name} ?? default,";
                }
                // 对于非可空引用类型，直接强制赋值以消除 nullable 警告
                return $"{Name} = {Name}!,";
            }

            // 🔹 如果源属性本身可空，或者 VM 属性不可空，直接赋值
            return $"{Name} = {Name},";
        }
    }
}