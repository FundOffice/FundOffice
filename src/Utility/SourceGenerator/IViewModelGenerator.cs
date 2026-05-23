using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SG;





/*

public partial class IntViewModel : IViewModel<int, IntViewModel>
public partial class IntViewModel : IViewModel<int?, IntViewModel>
public partial class IntViewModel : IViewModel<string, IntViewModel>
public partial class IntViewModel : IViewModel<string?, IntViewModel>


public class FundModeInfo
{
    public FundMode Mode { get; set; }

    public string? Other { get; set; }
}

[ForceNull("Mode")] // 强制 FundMode? Mode
public partial class FundModeViewModel
{
    
}

public partial class FundModeViewModel
{
    支持手写// ..  FundMode Mode..
    支持手写// ..  FundMode? Mode..

}

*/
 

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

                return ViewModelAnalyzer.Analyze(classSymbol, typeArg, ctx.SemanticModel.Compilation);
            })
            .Where(model => model != null);

        context.RegisterSourceOutput(classDeclarations, (spc, model) =>
        {
            if (model == null) return;
            var source = SourceCodeBuilder.Generate(model);
            spc.AddSource($"{model.ClassName}.vm.g.cs", SourceText.From(source, Encoding.UTF8));
        });
    }
}

#region Semantic Analysis (语义分析)

internal static class ViewModelAnalyzer
{
    public static GenerationModel? Analyze(INamedTypeSymbol targetClass, INamedTypeSymbol sourceType, Compilation compilation)
    {
        var logs = new List<string> { $"[Start] {targetClass.Name} <- {sourceType.Name}" };

        var ns = targetClass.ContainingNamespace.IsGlobalNamespace ? string.Empty : targetClass.ContainingNamespace.ToDisplayString();
        var className = targetClass.Name;
        var sourceTypeName = sourceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        string nullableParamTypeString = GetNullableTypeString(sourceType);
        string nonNullableTypeString = sourceTypeName.EndsWith("?") ? sourceTypeName.Substring(0, sourceTypeName.Length - 1) : sourceTypeName;

        // 👇 修复：Wrapper 模式下的 ComparerTypeString 计算
        string comparerTypeString;
        if (sourceType.IsValueType)
        {
            if (sourceType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            {
                comparerTypeString = ((INamedTypeSymbol)sourceType).TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + "?";
            }
            else
            {
                comparerTypeString = sourceTypeName;
            }
        }
        else
        {
            comparerTypeString = sourceTypeName.EndsWith("?") ? sourceTypeName.Substring(0, sourceTypeName.Length - 1) : sourceTypeName;
        }

        bool canBeNull = sourceType.IsReferenceType || sourceType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;

        // 1. 收集 [ForceNull] 特性
        var forceNullProps = new HashSet<string>(StringComparer.Ordinal);
        foreach (var attr in targetClass.GetAttributes())
        {
            if (attr.AttributeClass?.Name is "ForceNullAttribute" or "ForceNull")
            {
                if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is string propName)
                {
                    forceNullProps.Add(propName);
                }
            }
        }

        // 2. 收集基类和接口中已存在的属性，避免重复生成
        var existingInHierarchy = new HashSet<string>(StringComparer.Ordinal);
        INamedTypeSymbol? current = targetClass.BaseType;
        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            foreach (var member in current.GetMembers())
                if (member is IPropertySymbol p && p.DeclaredAccessibility == Accessibility.Public)
                    existingInHierarchy.Add(p.Name);

            var baseIViewModel = current.AllInterfaces.FirstOrDefault(i => i.Name == "IViewModel" && i.TypeArguments.Length == 2);
            if (baseIViewModel != null)
            {
                var srcType = baseIViewModel.TypeArguments[0] as INamedTypeSymbol;
                if (srcType != null)
                    foreach (var sourceMember in srcType.GetMembers())
                        if (sourceMember is IPropertySymbol sp && sp.DeclaredAccessibility == Accessibility.Public)
                            existingInHierarchy.Add(sp.Name);
            }
            current = current.BaseType;
        }

        // 3. 收集当前 ViewModel 中已手写的属性及 MVVM 生成的属性
        var vmMembers = new Dictionary<string, VmMemberInfo>(StringComparer.Ordinal);
        INamedTypeSymbol? currentTypeForVmProps = targetClass;
        while (currentTypeForVmProps != null && currentTypeForVmProps.SpecialType != SpecialType.System_Object)
        {
            foreach (var member in currentTypeForVmProps.GetMembers())
            {
                if (member is IPropertySymbol p && p.DeclaredAccessibility == Accessibility.Public && !vmMembers.ContainsKey(p.Name))
                {
                    vmMembers[p.Name] = new VmMemberInfo(p.Type, p.SetMethod != null, false);
                }
                else if (member is IFieldSymbol field && field.DeclaredAccessibility == Accessibility.Private)
                {
                    bool isObsProp = field.GetAttributes().Any(a => a.AttributeClass?.Name is "ObservablePropertyAttribute" or "ObservableProperty");
                    if (isObsProp)
                    {
                        string propName = field.Name.StartsWith("_")
                            ? char.ToUpperInvariant(field.Name[1]) + field.Name.Substring(2)
                            : char.ToUpperInvariant(field.Name[0]) + field.Name.Substring(1);

                        if (!vmMembers.ContainsKey(propName))
                        {
                            vmMembers[propName] = new VmMemberInfo(field.Type, true, true);
                        }
                    }
                }
            }
            currentTypeForVmProps = currentTypeForVmProps.BaseType;
        }

        bool isWrapperType = sourceType.SpecialType switch
        {
            SpecialType.System_Boolean or SpecialType.System_Char or SpecialType.System_SByte or
            SpecialType.System_Byte or SpecialType.System_Int16 or SpecialType.System_UInt16 or
            SpecialType.System_Int32 or SpecialType.System_UInt32 or SpecialType.System_Int64 or
            SpecialType.System_UInt64 or SpecialType.System_Decimal or SpecialType.System_Single or
            SpecialType.System_Double or SpecialType.System_String or SpecialType.System_DateTime => true,
            _ => sourceType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T || sourceType.TypeKind == TypeKind.Enum
        };

        var properties = new List<PropertyMapping>();

        if (isWrapperType)
        {
            logs.Add("⚠️ 启用 Wrapper 模式 (基础类型/Enum/Nullable)");

            bool hasVmValue = vmMembers.TryGetValue("Value", out var vmValueInfo);
            bool isWritable = hasVmValue ? vmValueInfo.IsWritable : true;

            properties.Add(new PropertyMapping(
                name: "Value",
                sourceTypeString: sourceTypeName,
                vmTypeString: sourceTypeName,
                comparerTypeString: comparerTypeString,
                isNested: false,
                sourceIsNullable: canBeNull,
                vmIsNullable: canBeNull,
                sourceIsWritable: true,
                vmIsWritable: isWritable,
                isString: sourceType.SpecialType == SpecialType.System_String,
                isValueType: sourceType.IsValueType,
                isWrapperValue: true,
                isGenerated: !hasVmValue
            ));
        }
        else
        {
            properties = AnalyzeProperties(targetClass, sourceType, compilation, vmMembers, existingInHierarchy, forceNullProps, logs);
        }

        if (properties.Count == 0) return null;

        bool needsINPC = true, hasAutoBase = false;
        var baseType = targetClass.BaseType;
        while (baseType != null && baseType.SpecialType != SpecialType.System_Object)
        {
            if (baseType.AllInterfaces.Any(i => i.Name == "IViewModel" && i.TypeArguments.Length == 2)) { needsINPC = false; hasAutoBase = true; break; }
            else if (baseType.Name.Contains("ObservableObject")) { needsINPC = false; hasAutoBase = false; break; }
            baseType = baseType.BaseType;
        }

        bool hasManualEquals = targetClass.GetMembers("Equals").OfType<IMethodSymbol>().Any(m => m.Parameters.Length == 1 && SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, sourceType));
        bool hasManualGetHashCode = targetClass.GetMembers("GetHashCode").OfType<IMethodSymbol>().Any(m => m.Parameters.Length == 0);
        bool hasManualObjectEquals = targetClass.GetMembers("Equals").OfType<IMethodSymbol>().Any(m => m.Parameters.Length == 1 && m.Parameters[0].Type.SpecialType == SpecialType.System_Object && m.IsOverride);

        return new GenerationModel(
            className, ns, sourceTypeName, nullableParamTypeString, nonNullableTypeString, comparerTypeString,
            properties, needsINPC, hasAutoBase, hasManualEquals, hasManualGetHashCode, hasManualObjectEquals,
            isWrapperType, canBeNull, logs
        );
    }

    private static List<PropertyMapping> AnalyzeProperties(
        INamedTypeSymbol targetClass,
        INamedTypeSymbol sourceType,
        Compilation compilation,
        Dictionary<string, VmMemberInfo> vmMembers,
        HashSet<string> existingInHierarchy,
        HashSet<string> forceNullProps,
        List<string> logs)
    {
        var properties = new List<PropertyMapping>();
        var currentType = sourceType;

        while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
        {
            foreach (var member in currentType.GetMembers())
            {
                if (member is IPropertySymbol prop && prop.DeclaredAccessibility == Accessibility.Public && !prop.IsStatic)
                {
                    if (existingInHierarchy.Contains(prop.Name)) continue;

                    ITypeSymbol propType = prop.Type;
                    string propTypeString = propType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                    bool sourceIsNullable = propType.IsReferenceType || propType is IArrayTypeSymbol
                        ? prop.NullableAnnotation != NullableAnnotation.NotAnnotated
                        : propType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;

                    bool hasVmMember = vmMembers.TryGetValue(prop.Name, out var vmMember);
                    bool forceNull = forceNullProps.Contains(prop.Name);

                    ITypeSymbol targetType = propType;
                    bool targetIsNullable = sourceIsNullable;
                    bool isGenerated = true;
                    bool vmIsWritable = true;

                    if (hasVmMember)
                    {
                        targetType = vmMember.Type;
                        targetIsNullable = targetType.IsReferenceType || targetType is IArrayTypeSymbol
                            ? targetType.NullableAnnotation != NullableAnnotation.NotAnnotated
                            : targetType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
                        isGenerated = false;
                        vmIsWritable = vmMember.IsWritable;
                    }
                    else if (forceNull)
                    {
                        targetIsNullable = true;
                    }

                    bool isNested = false;
                    if (propType is not IArrayTypeSymbol && propType.TypeKind != TypeKind.TypeParameter)
                    {
                        var nestedVmType = FindNestedVmType(propType, compilation);
                        if (nestedVmType != null)
                        {
                            isNested = true;
                            if (!hasVmMember)
                            {
                                targetType = nestedVmType;
                                targetIsNullable = sourceIsNullable || forceNull;
                            }
                        }
                    }

                    string vmTypeString = targetType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    if (targetIsNullable && !vmTypeString.EndsWith("?"))
                    {
                        vmTypeString += "?";
                    }

                    // 👇 修复：精确计算 ComparerTypeString，支持 Nullable<T> 与 T 的安全比较
                    string comparerTypeString;
                    if (propType.IsValueType)
                    {
                        if (sourceIsNullable || targetIsNullable)
                        {
                            ITypeSymbol underlying = propType;
                            if (propType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
                                underlying = ((INamedTypeSymbol)propType).TypeArguments[0];
                            comparerTypeString = underlying.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + "?";
                        }
                        else
                        {
                            comparerTypeString = propType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                        }
                    }
                    else
                    {
                        string baseTypeStr = propType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                        comparerTypeString = baseTypeStr.EndsWith("?") ? baseTypeStr.Substring(0, baseTypeStr.Length - 1) : baseTypeStr;
                    }

                    properties.Add(new PropertyMapping(
                        name: prop.Name,
                        sourceTypeString: propTypeString,
                        vmTypeString: vmTypeString,
                        comparerTypeString: comparerTypeString,
                        isNested: isNested,
                        sourceIsNullable: sourceIsNullable,
                        vmIsNullable: targetIsNullable,
                        sourceIsWritable: prop.SetMethod != null,
                        vmIsWritable: vmIsWritable,
                        isString: propType.SpecialType == SpecialType.System_String,
                        isValueType: propType.IsValueType,
                        isWrapperValue: false,
                        isGenerated: isGenerated
                    ));
                }
            }
            currentType = currentType.BaseType;
        }
        return properties;
    }

    private static INamedTypeSymbol? FindNestedVmType(ITypeSymbol sourcePropType, Compilation compilation)
    {
        if (sourcePropType.Name == null) return null;
        var vmTypeName = sourcePropType.Name + "ViewModel";

        var ns = sourcePropType.ContainingNamespace;
        INamedTypeSymbol? nestedVmType = null;

        if (!ns.IsGlobalNamespace)
        {
            nestedVmType = compilation.GetTypeByMetadataName($"{ns.ToDisplayString()}.{vmTypeName}");
        }

        if (nestedVmType == null)
        {
            nestedVmType = compilation.GetTypeByMetadataName(vmTypeName);
        }

        if (nestedVmType != null)
        {
            var iface = nestedVmType.AllInterfaces.FirstOrDefault(i => i.Name == "IViewModel" && i.TypeArguments.Length == 2);
            if (iface != null && SymbolEqualityComparer.Default.Equals(iface.TypeArguments[0], sourcePropType))
            {
                return nestedVmType;
            }
        }
        return null;
    }

    private static string GetNullableTypeString(ITypeSymbol type)
    {
        var str = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (type.IsValueType)
        {
            if (type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T) return str;
            return str + "?";
        }
        else
        {
            return str.EndsWith("?") ? str : str + "?";
        }
    }
}

internal class VmMemberInfo
{
    public ITypeSymbol Type { get; }
    public bool IsWritable { get; }
    public bool IsMvvmGenerated { get; }
    public VmMemberInfo(ITypeSymbol type, bool isWritable, bool isMvvmGenerated)
    {
        Type = type; IsWritable = isWritable; IsMvvmGenerated = isMvvmGenerated;
    }
}

#endregion

#region Code Generation (代码生成)

internal static class SourceCodeBuilder
{
    public static string Generate(GenerationModel model)
    {
#if DEBUG
        var debugHeader = string.Join("\n", new[] { "// 🔍 ===== AutoViewModel Debug Info =====" }
            .Concat(model.DebugLogs.Select(l => $"// {l}"))
            .Concat(new[] { "// =====================================\n" }));
#else
        var debugHeader = "";
#endif
        var sb = new StringBuilder();
        sb.AppendLine("#nullable enable");
        sb.AppendLine(debugHeader);
        sb.AppendLine("using System;");
        sb.AppendLine("using System.ComponentModel;");
        sb.AppendLine("using System.Runtime.CompilerServices;");

        if (!string.IsNullOrEmpty(model.Namespace))
        {
            sb.AppendLine($"namespace {model.Namespace}");
            sb.AppendLine("{");
        }

        string inpcInheritance = model.NeedsINPC ? " : INotifyPropertyChanged" : "";
        sb.AppendLine($"public partial class {model.ClassName}{inpcInheritance}");
        sb.AppendLine("{");

        if (model.NeedsINPC) sb.AppendLine(GenerateInpcBlock());

        sb.AppendLine(GenerateProperties(model));
        sb.AppendLine();
        sb.AppendLine(GenerateConstructors(model));
        sb.AppendLine();
        sb.AppendLine(GenerateTransMethods(model));
        sb.AppendLine();
        sb.AppendLine(GenerateEqualityMethods(model));
        sb.AppendLine();
        sb.AppendLine(GenerateFillMethod(model));
        sb.AppendLine();
        sb.AppendLine(GenerateBuildMethod(model));

        sb.AppendLine("}");
        if (!string.IsNullOrEmpty(model.Namespace)) sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GenerateInpcBlock()
    {
        return """
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
        """;
    }

    private static string GenerateConstructors(GenerationModel model)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"public {model.ClassName}() {{ }}");
        sb.AppendLine();
        sb.AppendLine($"public {model.ClassName}({model.NullableParamTypeString} val)");
        sb.AppendLine("{");

        if (model.CanBeNull)
        {
            sb.AppendLine("    if (val is null)");
            sb.AppendLine("    {");
            foreach (var prop in model.Properties)
            {
                if (prop.VmIsWritable)
                    sb.AppendLine($"        {prop.GetResetExpr()}");
            }
            sb.AppendLine("    }");
            sb.AppendLine("    else");
            sb.AppendLine("    {");
            foreach (var prop in model.Properties)
            {
                if (prop.VmIsWritable)
                    sb.AppendLine($"        {prop.GetAssignFromSourceExpr("val")}");
            }
            sb.AppendLine("    }");
        }
        else
        {
            foreach (var prop in model.Properties)
            {
                if (prop.VmIsWritable)
                    sb.AppendLine($"    {prop.GetAssignFromSourceExpr("val")}");
            }
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GenerateTransMethods(GenerationModel model)
    {
        var sb = new StringBuilder();
        sb.AppendLine($@"public static {model.SourceTypeName} Trans({model.ClassName} vm)
{{
    if (vm is null) return default!;
    return vm.Build();
}}");
        sb.AppendLine();

        sb.AppendLine($@"public static {model.ClassName} Trans({model.NullableParamTypeString} vm)
{{
    return new {model.ClassName}(vm);
}}");
        return sb.ToString();
    }

    private static string GenerateFillMethod(GenerationModel model)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"public {model.ClassName} FillBy({model.NullableParamTypeString} obj)");
        sb.AppendLine("{");

        if (model.CanBeNull)
        {
            sb.AppendLine("    if (obj is null)");
            sb.AppendLine("    {");
            foreach (var prop in model.Properties)
            {
                if (prop.VmIsWritable)
                    sb.AppendLine($"        {prop.GetResetExpr()}");
            }
            sb.AppendLine("        return this;");
            sb.AppendLine("    }");
        }

        foreach (var prop in model.Properties)
        {
            if (prop.VmIsWritable)
                sb.AppendLine($"    {prop.GetAssignFromSourceExpr("obj")}");
        }

        sb.AppendLine("    return this;");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GenerateBuildMethod(GenerationModel model)
    {
        var sb = new StringBuilder();
        string overnew = model.HasAutoBase ? "new " : "";

        if (model.IsWrapperType)
        {
            sb.AppendLine($"public {overnew}{model.SourceTypeName} Build()");
            sb.AppendLine("{");
            sb.AppendLine("    return Value;");
            sb.AppendLine("}");
        }
        else
        {
            sb.AppendLine($"public {overnew}{model.SourceTypeName} Build()");
            sb.AppendLine("{");
            sb.AppendLine($"    var result = new {model.SourceTypeName}");
            sb.AppendLine("    {");
            foreach (var prop in model.Properties)
            {
                if (prop.SourceIsWritable)
                {
                    var expr = prop.GetBuildExpr();
                    if (!string.IsNullOrWhiteSpace(expr))
                        sb.AppendLine($"        {expr}");
                }
            }
            sb.AppendLine("    };");
            sb.AppendLine("    return result;");
            sb.AppendLine("}");
        }
        return sb.ToString();
    }

    private static string GenerateEqualityMethods(GenerationModel model)
    {
        var sb = new StringBuilder();

        if (!model.HasManualEquals)
        {
            sb.AppendLine($"public bool Equals({model.NullableParamTypeString} other)");
            sb.AppendLine("{");
            if (model.CanBeNull) sb.AppendLine("    if (other is null) return false;");

            if (model.IsWrapperType)
            {
                sb.AppendLine($"    if (!global::System.Collections.Generic.EqualityComparer<{model.ComparerTypeString}>.Default.Equals(Value, other)) return false;");
            }
            else
            {
                foreach (var prop in model.Properties)
                {
                    sb.AppendLine($"    {prop.GetEqualsExpr("other")}");
                }
            }
            sb.AppendLine("    return true;");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        if (!model.HasManualEquals && !model.HasManualObjectEquals)
        {
            sb.AppendLine($@"public override bool Equals(object? obj)
{{
    return obj is {model.NonNullableTypeString} other && Equals(other);
}}");
            sb.AppendLine();
        }

        if (!model.HasManualGetHashCode)
        {
            if (model.Properties.Count == 1 && model.IsWrapperType)
            {
                sb.AppendLine($@"public override int GetHashCode()
{{
    return global::System.Collections.Generic.EqualityComparer<{model.ComparerTypeString}>.Default.GetHashCode(Value!);
}}");
            }
            else
            {
                sb.AppendLine("public override int GetHashCode()");
                sb.AppendLine("{");
                sb.AppendLine("    unchecked");
                sb.AppendLine("    {");
                sb.AppendLine("        int hash = 17;");
                foreach (var prop in model.Properties)
                {
                    sb.AppendLine($"        {prop.GetHashCodeExpr()}");
                }
                sb.AppendLine("        return hash;");
                sb.AppendLine("    }");
                sb.AppendLine("}");
            }
        }

        return sb.ToString();
    }

    private static string GenerateProperties(GenerationModel model)
    {
        var sb = new StringBuilder();
        foreach (var prop in model.Properties.Where(p => p.IsGenerated))
        {
            sb.AppendLine($@"public {prop.VmTypeString} {prop.Name} 
{{ 
    get => field; 
    set => SetProperty(ref field, value); 
}}");
            sb.AppendLine();
        }
        return sb.ToString();
    }
}

#endregion

#region Models (数据模型)

internal class GenerationModel
{
    public string ClassName { get; }
    public string Namespace { get; }
    public string SourceTypeName { get; }
    public string NullableParamTypeString { get; }
    public string NonNullableTypeString { get; }
    public string ComparerTypeString { get; }
    public List<PropertyMapping> Properties { get; }
    public bool NeedsINPC { get; }
    public bool HasAutoBase { get; }
    public bool HasManualEquals { get; }
    public bool HasManualGetHashCode { get; }
    public bool HasManualObjectEquals { get; }
    public bool IsWrapperType { get; }
    public bool CanBeNull { get; }
    public List<string> DebugLogs { get; }

    public GenerationModel(string className, string @namespace, string sourceTypeName, string nullableParamTypeString, string nonNullableTypeString, string comparerTypeString,
        List<PropertyMapping> properties, bool needsINPC, bool hasAutoBase,
        bool hasManualEquals, bool hasManualGetHashCode, bool hasManualObjectEquals, bool isWrapperType, bool canBeNull, List<string> logs)
    {
        ClassName = className; Namespace = @namespace; SourceTypeName = sourceTypeName;
        NullableParamTypeString = nullableParamTypeString; NonNullableTypeString = nonNullableTypeString; ComparerTypeString = comparerTypeString;
        Properties = properties; NeedsINPC = needsINPC;
        HasAutoBase = hasAutoBase; HasManualEquals = hasManualEquals; HasManualGetHashCode = hasManualGetHashCode;
        HasManualObjectEquals = hasManualObjectEquals; IsWrapperType = isWrapperType; CanBeNull = canBeNull; DebugLogs = logs;
    }
}

internal class PropertyMapping
{
    public string Name { get; }
    public string SourceTypeString { get; }
    public string VmTypeString { get; }
    public string ComparerTypeString { get; }
    public bool IsNested { get; }
    public bool SourceIsNullable { get; }
    public bool VmIsNullable { get; }
    public bool SourceIsWritable { get; }
    public bool VmIsWritable { get; }
    public bool IsString { get; }
    public bool IsValueType { get; }
    public bool IsWrapperValue { get; }
    public bool IsGenerated { get; }

    public PropertyMapping(string name, string sourceTypeString, string vmTypeString, string comparerTypeString,
        bool isNested, bool sourceIsNullable, bool vmIsNullable, bool sourceIsWritable, bool vmIsWritable,
        bool isString, bool isValueType, bool isWrapperValue, bool isGenerated)
    {
        Name = name; SourceTypeString = sourceTypeString; VmTypeString = vmTypeString; ComparerTypeString = comparerTypeString;
        IsNested = isNested; SourceIsNullable = sourceIsNullable; VmIsNullable = vmIsNullable;
        SourceIsWritable = sourceIsWritable; VmIsWritable = vmIsWritable;
        IsString = isString; IsValueType = isValueType; IsWrapperValue = isWrapperValue;
        IsGenerated = isGenerated;
    }

    public string GetAssignFromSourceExpr(string sourceObjVar)
    {
        string srcAccess = IsWrapperValue ? sourceObjVar : $"{sourceObjVar}.{Name}";

        if (IsNested)
        {
            if (SourceIsNullable)
            {
                string fallback = VmIsNullable ? "null" : $"default({VmTypeString})!";
                return $"{Name} = {srcAccess} != null ? new {VmTypeString}({srcAccess}) : {fallback};";
            }
            else
            {
                return $"{Name} = new {VmTypeString}({srcAccess});";
            }
        }
        else
        {
            if (SourceIsNullable && !VmIsNullable)
            {
                if (IsValueType) return $"{Name} = {srcAccess} ?? default;";
                else return $"{Name} = {srcAccess} ?? default!;";
            }
            return $"{Name} = {srcAccess};";
        }
    }

    public string GetResetExpr()
    {
        if (VmIsNullable) return $"{Name} = default;";
        if (IsNested) return $"{Name} = default!;";
        if (IsValueType) return $"{Name} = default;";
        return $"{Name} = default!;";
    }

    public string GetBuildExpr()
    {
        if (!SourceIsWritable) return "";

        if (IsNested)
        {
            if (VmIsNullable)
            {
                if (!SourceIsNullable) return $"{Name} = {Name}?.Build() ?? default!,";
                return $"{Name} = {Name}?.Build(),";
            }
            else
            {
                return $"{Name} = {Name}.Build(),";
            }
        }
        else
        {
            if (VmIsNullable && !SourceIsNullable)
            {
                if (IsValueType) return $"{Name} = {Name} ?? default,";
                else return $"{Name} = {Name} ?? default!,";
            }
            return $"{Name} = {Name},";
        }
    }

    public string GetEqualsExpr(string otherVar)
    {
        string otherAccess = IsWrapperValue ? otherVar : $"{otherVar}.{Name}";

        if (IsNested)
        {
            string thisBuild = VmIsNullable ? $"{Name}?.Build()" : $"{Name}.Build()";
            // 👇 修复：otherAccess 是 Source 端的属性，其是否可空由 SourceIsNullable 决定
            string otherBuild = SourceIsNullable ? $"{otherAccess}?.Build()" : $"{otherAccess}.Build()";
            return $"if (!global::System.Collections.Generic.EqualityComparer<{ComparerTypeString}>.Default.Equals({thisBuild}, {otherBuild})) return false;";
        }
        else
        {
            return $"if (!global::System.Collections.Generic.EqualityComparer<{ComparerTypeString}>.Default.Equals({Name}, {otherAccess})) return false;";
        }
    }

    public string GetHashCodeExpr()
    {
        if (IsNested)
        {
            string buildExpr = VmIsNullable ? $"{Name}?.Build()" : $"{Name}.Build()";
            return $"hash = hash * 31 + global::System.Collections.Generic.EqualityComparer<{ComparerTypeString}>.Default.GetHashCode({buildExpr}!);";
        }
        else
        {
            return $"hash = hash * 31 + global::System.Collections.Generic.EqualityComparer<{ComparerTypeString}>.Default.GetHashCode({Name}!);";
        }
    }
}

#endregion