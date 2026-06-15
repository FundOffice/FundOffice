using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SG;


/// <summary>
/// class a;
/// class b : a;
/// class avm:IViewModel<a, avm>
/// class bvm : avm, IViewModel<b, bvm>
/// 
/// </summary>
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

                // 🔽 修复 1：优先获取当前类【直接实现】的 IViewModel 接口
                var iViewModelInterface = classSymbol.Interfaces.FirstOrDefault(i =>
                    i.Name == "IViewModel" && i.TypeArguments.Length == 2);

                if (iViewModelInterface == null)
                {
                    iViewModelInterface = classSymbol.AllInterfaces.FirstOrDefault(i =>
                        i.Name == "IViewModel" && i.TypeArguments.Length == 2 &&
                        SymbolEqualityComparer.Default.Equals(i.TypeArguments[1], classSymbol));
                }

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

        string comparerTypeString;
        if (sourceType.IsValueType)
        {
            if (sourceType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
                comparerTypeString = ((INamedTypeSymbol)sourceType).TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + "?";
            else
                comparerTypeString = sourceTypeName;
        }
        else
        {
            comparerTypeString = sourceTypeName.EndsWith("?") ? sourceTypeName.Substring(0, sourceTypeName.Length - 1) : sourceTypeName;
        }

        bool canBeNull = sourceType.IsReferenceType || sourceType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;

        var forceNullProps = new HashSet<string>(StringComparer.Ordinal);
        foreach (var attr in targetClass.GetAttributes())
        {
            if (attr.AttributeClass?.Name is "ForceNullAttribute" or "ForceNull")
            {
                if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is string propName)
                    forceNullProps.Add(propName);
            }
        }

        // 🔽 关键：收集基类 ViewModel 已经“占有”的属性（包括手写的和自动生成的）
        var existingInHierarchy = new HashSet<string>(StringComparer.Ordinal);
        INamedTypeSymbol? current = targetClass.BaseType;
        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            // 1. 收集基类 ViewModel 中【手动编写】的属性
            foreach (var member in current.GetMembers())
                if (member is IPropertySymbol p && p.DeclaredAccessibility == Accessibility.Public)
                    existingInHierarchy.Add(p.Name);

            // 2. 查找基类 ViewModel 对应的 IViewModel 接口，获取其 Source Model
            var baseIViewModel = current.Interfaces.FirstOrDefault(i => i.Name == "IViewModel" && i.TypeArguments.Length == 2);
            if (baseIViewModel == null)
            {
                baseIViewModel = current.AllInterfaces.FirstOrDefault(i =>
                    i.Name == "IViewModel" && i.TypeArguments.Length == 2 &&
                    SymbolEqualityComparer.Default.Equals(i.TypeArguments[1], current));
            }

            if (baseIViewModel != null)
            {
                var srcType = baseIViewModel.TypeArguments[0] as INamedTypeSymbol;
                if (srcType != null)
                {
                    // 3. 遍历基类 ViewModel 对应的 Source Model 及其所有父类
                    // 这些属性会被基类 ViewModel 的生成器自动生成，所以也必须加入黑名单
                    var currentSrc = srcType;
                    while (currentSrc != null && currentSrc.SpecialType != SpecialType.System_Object)
                    {
                        foreach (var sourceMember in currentSrc.GetMembers())
                            if (sourceMember is IPropertySymbol sp && sp.DeclaredAccessibility == Accessibility.Public)
                                existingInHierarchy.Add(sp.Name);
                        currentSrc = currentSrc.BaseType;
                    }
                }
            }
            current = current.BaseType;
        }

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
                            vmMembers[propName] = new VmMemberInfo(field.Type, true, true);
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
                name: "Value", sourceTypeString: sourceTypeName, vmTypeString: sourceTypeName,
                comparerTypeString: comparerTypeString, isNested: false, sourceIsNullable: canBeNull,
                vmIsNullable: canBeNull, sourceIsWritable: true, vmIsWritable: isWritable,
                isString: sourceType.SpecialType == SpecialType.System_String, isValueType: sourceType.IsValueType,
                isWrapperValue: true, isGenerated: !hasVmValue, isCollection: false,
                sourceCollectionElementTypeString: null, vmCollectionElementTypeString: null, isNestedCollection: false
            ));
        }
        else
        {
            // 🔽 传入 existingInHierarchy
            properties = AnalyzeProperties(sourceType, compilation, vmMembers, existingInHierarchy, forceNullProps, logs, targetClass);
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

        bool hasManualDefaultCtor = false, hasManualParamCtor = false, hasManualFillBy = false;
        bool hasManualBuild = false, hasManualTrans = false, hasManualEquals = false;
        bool hasManualGetHashCode = false, hasManualObjectEquals = false;

        var currentCheck = targetClass;
        while (currentCheck != null && currentCheck.SpecialType != SpecialType.System_Object)
        {
            if (!hasManualDefaultCtor && currentCheck.InstanceConstructors.Any(c => c.Parameters.Length == 0 && c.DeclaredAccessibility == Accessibility.Public && !c.IsImplicitlyDeclared))
                hasManualDefaultCtor = true;
            if (!hasManualParamCtor && currentCheck.InstanceConstructors.Any(c => c.Parameters.Length == 1 && c.DeclaredAccessibility == Accessibility.Public && !c.IsImplicitlyDeclared && IsTypeMatch(c.Parameters[0].Type, sourceType)))
                hasManualParamCtor = true;
            if (!hasManualFillBy && currentCheck.GetMembers("FillBy").OfType<IMethodSymbol>().Any(m => m.Parameters.Length == 1 && m.DeclaredAccessibility == Accessibility.Public && IsTypeMatch(m.Parameters[0].Type, sourceType)))
                hasManualFillBy = true;
            if (!hasManualBuild && currentCheck.GetMembers("Build").OfType<IMethodSymbol>().Any(m => m.Parameters.Length == 0 && m.DeclaredAccessibility == Accessibility.Public))
                hasManualBuild = true;
            if (!hasManualTrans && currentCheck.GetMembers("Trans").OfType<IMethodSymbol>().Any(m => m.Parameters.Length == 1 && m.IsStatic && m.DeclaredAccessibility == Accessibility.Public && (IsTypeMatch(m.Parameters[0].Type, sourceType) || SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, targetClass))))
                hasManualTrans = true;
            if (!hasManualEquals && currentCheck.GetMembers("Equals").OfType<IMethodSymbol>().Any(m => m.Parameters.Length == 1 && IsTypeMatch(m.Parameters[0].Type, sourceType)))
                hasManualEquals = true;
            if (!hasManualGetHashCode && currentCheck.GetMembers("GetHashCode").OfType<IMethodSymbol>().Any(m => m.Parameters.Length == 0 && !m.IsImplicitlyDeclared))
                hasManualGetHashCode = true;
            if (!hasManualObjectEquals && currentCheck.GetMembers("Equals").OfType<IMethodSymbol>().Any(m => m.Parameters.Length == 1 && m.Parameters[0].Type.SpecialType == SpecialType.System_Object && m.IsOverride))
                hasManualObjectEquals = true;

            currentCheck = currentCheck.BaseType;
        }

        return new GenerationModel(
            className, ns, sourceTypeName, nullableParamTypeString, nonNullableTypeString, comparerTypeString,
            properties, needsINPC, hasAutoBase, hasManualEquals, hasManualGetHashCode, hasManualObjectEquals,
            isWrapperType, canBeNull, logs,
            hasManualDefaultCtor, hasManualParamCtor, hasManualFillBy, hasManualBuild, hasManualTrans
        );
    }

    private static List<PropertyMapping> AnalyzeProperties(
           INamedTypeSymbol sourceType,
           Compilation compilation,
           Dictionary<string, VmMemberInfo> vmMembers,
           HashSet<string> existingInHierarchy,
           HashSet<string> forceNullProps,
           List<string> logs,
           INamedTypeSymbol? targetClass = null)
    {
        var properties = new List<PropertyMapping>();
        var currentType = sourceType;
        var processedProps = new HashSet<string>(StringComparer.Ordinal);

        while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
        {
            foreach (var member in currentType.GetMembers())
            {
                if (member is IPropertySymbol prop && prop.DeclaredAccessibility == Accessibility.Public && !prop.IsStatic)
                {
                    if (!processedProps.Add(prop.Name)) continue;

                    ITypeSymbol propType = prop.Type;
                    string propTypeString = propType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                    bool sourceIsNullable = propType.IsReferenceType || propType is IArrayTypeSymbol
                        ? prop.NullableAnnotation != NullableAnnotation.NotAnnotated
                        : propType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;

                    bool hasVmMember = vmMembers.TryGetValue(prop.Name, out var vmMember);
                    bool isInheritedFromBaseVm = existingInHierarchy.Contains(prop.Name); // 🔽 关键：检查是否被基类VM占有
                    bool forceNull = forceNullProps.Contains(prop.Name);

                    ITypeSymbol targetType = propType;
                    bool targetIsNullable = sourceIsNullable;
                    bool vmIsWritable = true;

                    // 🔽 核心逻辑：如果是当前类手写的，或者是基类VM已经处理过的，都不生成属性定义
                    // 但不跳过它，让它继续加入 properties 列表参与 Constructor/Build/Equals
                    bool isGenerated = !hasVmMember && !isInheritedFromBaseVm;

                    if (hasVmMember)
                    {
                        targetType = vmMember!.Type;
                        targetIsNullable = targetType.IsReferenceType || targetType is IArrayTypeSymbol
                            ? targetType.NullableAnnotation != NullableAnnotation.NotAnnotated
                            : targetType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
                        vmIsWritable = vmMember.IsWritable;
                    }

                    if (forceNull) targetIsNullable = true;

                    bool isNested = false;
                    bool isCollection = IsCollectionType(propType);
                    bool isNestedCollection = false;
                    string? sourceCollectionElementTypeString = null;
                    string? vmCollectionElementTypeString = null;

                    if (isCollection)
                    {
                        var sourceElementType = GetCollectionElementType(propType);
                        if (sourceElementType != null) sourceCollectionElementTypeString = sourceElementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                        if (hasVmMember && vmMember != null)
                        {
                            var vmElementType = GetCollectionElementType(vmMember.Type);
                            if (vmElementType != null)
                            {
                                vmCollectionElementTypeString = vmElementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                                if (vmElementType is not IArrayTypeSymbol && vmElementType.TypeKind != TypeKind.TypeParameter)
                                {
                                    var nestedVmType = sourceElementType != null ? FindNestedVmType(sourceElementType, compilation, targetClass) : null;
                                    if (nestedVmType != null) { isNestedCollection = true; vmCollectionElementTypeString = nestedVmType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat); }
                                }
                            }
                        }
                        else
                        {
                            if (sourceElementType != null && sourceElementType is not IArrayTypeSymbol && sourceElementType.TypeKind != TypeKind.TypeParameter)
                            {
                                var nestedVmType = FindNestedVmType(sourceElementType, compilation, targetClass);
                                if (nestedVmType != null) { isNestedCollection = true; vmCollectionElementTypeString = nestedVmType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat); }
                            }
                        }
                    }
                    else if (propType is not IArrayTypeSymbol && propType.TypeKind != TypeKind.TypeParameter)
                    {
                        var nestedVmType = FindNestedVmType(propType, compilation, targetClass);
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
                    if (targetIsNullable && !vmTypeString.EndsWith("?")) vmTypeString += "?";

                    string comparerTypeString;
                    if (propType.IsValueType)
                    {
                        if (sourceIsNullable || targetIsNullable)
                        {
                            ITypeSymbol underlying = propType;
                            if (propType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T) underlying = ((INamedTypeSymbol)propType).TypeArguments[0];
                            comparerTypeString = underlying.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + "?";
                        }
                        else comparerTypeString = propType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    }
                    else
                    {
                        string baseTypeStr = propType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                        comparerTypeString = baseTypeStr.EndsWith("?") ? baseTypeStr.Substring(0, baseTypeStr.Length - 1) : baseTypeStr;
                    }

                    properties.Add(new PropertyMapping(
                        name: prop.Name, sourceTypeString: propTypeString, vmTypeString: vmTypeString,
                        comparerTypeString: comparerTypeString, isNested: isNested, sourceIsNullable: sourceIsNullable,
                        vmIsNullable: targetIsNullable, sourceIsWritable: prop.SetMethod != null, vmIsWritable: vmIsWritable,
                        isString: propType.SpecialType == SpecialType.System_String, isValueType: propType.IsValueType,
                        isWrapperValue: false, isGenerated: isGenerated, isCollection: isCollection,
                        sourceCollectionElementTypeString: sourceCollectionElementTypeString,
                        vmCollectionElementTypeString: vmCollectionElementTypeString, isNestedCollection: isNestedCollection
                    ));
                }
            }
            currentType = currentType.BaseType;
        }
        return properties;
    }

    private static bool IsTypeMatch(ITypeSymbol paramType, ITypeSymbol targetType)
    {
        if (SymbolEqualityComparer.Default.Equals(paramType, targetType)) return true;
        if (targetType.IsValueType && targetType.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T && paramType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            if (SymbolEqualityComparer.Default.Equals(((INamedTypeSymbol)paramType).TypeArguments[0], targetType)) return true;
        if (targetType.IsReferenceType)
            if (SymbolEqualityComparer.Default.Equals(paramType.WithNullableAnnotation(NullableAnnotation.NotAnnotated), targetType.WithNullableAnnotation(NullableAnnotation.NotAnnotated))) return true;
        return false;
    }
    private static bool IsCollectionType(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol) return true;
        if (type.SpecialType == SpecialType.System_String) return false;
        if (type is INamedTypeSymbol namedType)
            foreach (var iface in namedType.AllInterfaces)
                if (iface.OriginalDefinition?.ToDisplayString() == "System.Collections.Generic.IEnumerable<T>") return true;
        return false;
    }
    private static ITypeSymbol? GetCollectionElementType(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol arrayType) return arrayType.ElementType;
        if (type is INamedTypeSymbol namedType)
            foreach (var iface in namedType.AllInterfaces)
                if (iface.OriginalDefinition?.ToDisplayString() == "System.Collections.Generic.IEnumerable<T>" && iface is INamedTypeSymbol enumerableNamed)
                    return enumerableNamed.TypeArguments[0];
        return null;
    }
    private static INamedTypeSymbol? FindNestedVmType(ITypeSymbol sourcePropType, Compilation compilation, INamedTypeSymbol? targetClass = null)
    {
        if (sourcePropType.Name == null) return null;
        var vmTypeName = sourcePropType.Name + "ViewModel";
        var ns = sourcePropType.ContainingNamespace;
        INamedTypeSymbol? nestedVmType = null;
        if (!ns.IsGlobalNamespace) nestedVmType = compilation.GetTypeByMetadataName($"{ns.ToDisplayString()}.{vmTypeName}");
        if (nestedVmType == null) nestedVmType = compilation.GetTypeByMetadataName(vmTypeName);
        if (nestedVmType == null && targetClass != null)
        {
            var targetNs = targetClass.ContainingNamespace;
            while (targetNs != null && !targetNs.IsGlobalNamespace)
            {
                nestedVmType = compilation.GetTypeByMetadataName($"{targetNs.ToDisplayString()}.{vmTypeName}");
                if (nestedVmType != null) break;
                targetNs = targetNs.ContainingNamespace;
            }
        }
        if (nestedVmType != null)
        {
            var iface = nestedVmType.AllInterfaces.FirstOrDefault(i => i.Name == "IViewModel" && i.TypeArguments.Length == 2);
            if (iface != null && SymbolEqualityComparer.Default.Equals(iface.TypeArguments[0], sourcePropType)) return nestedVmType;
        }
        return null;
    }
    private static string GetNullableTypeString(ITypeSymbol type)
    {
        var str = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (type.IsValueType) return type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T ? str : str + "?";
        else return str.EndsWith("?") ? str : str + "?";
    }

}

internal class VmMemberInfo
{
    public ITypeSymbol Type { get; }
    public bool IsWritable { get; }
    public bool IsMvvmGenerated { get; }
    public VmMemberInfo(ITypeSymbol type, bool isWritable, bool isMvvmGenerated) { Type = type; IsWritable = isWritable; IsMvvmGenerated = isMvvmGenerated; }
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
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Collections.ObjectModel;");
        sb.AppendLine();

        // 🔽 使用 C# 10 文件范围命名空间，去掉大括号，解决缩进对齐问题
        if (!string.IsNullOrEmpty(model.Namespace))
        {
            sb.AppendLine($"namespace {model.Namespace};");
            sb.AppendLine();
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
        // 🔽 移除了原有的命名空间闭合大括号 }
        return sb.ToString();
    }

    private static string GenerateInpcBlock() => """
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

    private static string GenerateConstructors(GenerationModel model)
    {
        var sb = new StringBuilder();
        if (!model.HasManualDefaultCtor) { sb.AppendLine($"public {model.ClassName}() {{ }}"); sb.AppendLine(); }
        if (!model.HasManualParamCtor)
        {
            sb.AppendLine($"public {model.ClassName}({model.NullableParamTypeString} val)");
            sb.AppendLine("{");
            if (model.CanBeNull)
            {
                sb.AppendLine("    if (val is null) {");
                foreach (var prop in model.Properties) if (prop.VmIsWritable) sb.AppendLine($"        {prop.GetResetExpr()}");
                sb.AppendLine("    } else {");
                foreach (var prop in model.Properties) if (prop.VmIsWritable) sb.AppendLine($"        {prop.GetAssignFromSourceExpr("val")}");
                sb.AppendLine("    }");
            }
            else
            {
                foreach (var prop in model.Properties) if (prop.VmIsWritable) sb.AppendLine($"    {prop.GetAssignFromSourceExpr("val")}");
            }
            sb.AppendLine("}");
        }
        return sb.ToString();
    }
    private static string GenerateTransMethods(GenerationModel model)
    {
        if (model.HasManualTrans) return string.Empty;
        var sb = new StringBuilder();
        sb.AppendLine($@"public static {model.NullableParamTypeString} Trans({model.ClassName} vm) {{ if (vm is null) return default!; return vm.Build(); }}");
        sb.AppendLine();
        sb.AppendLine($@"public static {model.ClassName} Trans({model.NullableParamTypeString} vm) {{ return new {model.ClassName}(vm); }}");
        return sb.ToString();
    }
    private static string GenerateFillMethod(GenerationModel model)
    {
        if (model.HasManualFillBy) return string.Empty;
        var sb = new StringBuilder();
        sb.AppendLine($"public {model.ClassName} FillBy({model.NullableParamTypeString} obj)");
        sb.AppendLine("{");
        if (model.CanBeNull)
        {
            sb.AppendLine("    if (obj is null) {");
            foreach (var prop in model.Properties) if (prop.VmIsWritable) sb.AppendLine($"        {prop.GetResetExpr()}");
            sb.AppendLine("        return this; }");
        }
        foreach (var prop in model.Properties) if (prop.VmIsWritable) sb.AppendLine($"    {prop.GetAssignFromSourceExpr("obj")}");
        sb.AppendLine("    return this; }");
        return sb.ToString();
    }
    private static string GenerateBuildMethod(GenerationModel model)
    {
        if (model.HasManualBuild) return string.Empty;
        var sb = new StringBuilder();
        string overnew = model.HasAutoBase ? "new " : "";
        if (model.IsWrapperType) { sb.AppendLine($"public {overnew}{model.SourceTypeName} Build() {{ return Value; }}"); }
        else
        {
            sb.AppendLine($"public {overnew}{model.SourceTypeName} Build()");
            sb.AppendLine("{ var result = new " + model.SourceTypeName + " {");
            foreach (var prop in model.Properties)
            {
                if (prop.SourceIsWritable)
                {
                    var expr = prop.GetBuildExpr();
                    if (!string.IsNullOrWhiteSpace(expr)) sb.AppendLine($"        {expr}");
                }
            }
            sb.AppendLine("    }; return result; }");
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
            sb.AppendLine("    if (ReferenceEquals(this, other)) return true;");
            if (model.CanBeNull)
            {
                if (model.IsWrapperType) sb.AppendLine($"    if (other is null) return global::System.Collections.Generic.EqualityComparer<{model.ComparerTypeString}>.Default.Equals(Value, default);");
                else
                {
                    sb.AppendLine("    if (other is null) {");
                    if (model.Properties.Count == 0) sb.AppendLine("        return true;");
                    else { var checks = model.Properties.Select(p => p.GetNullCheckExpr()); sb.AppendLine($"        return {string.Join(" &&\n               ", checks)};"); }
                    sb.AppendLine("    }");
                }
            }
            if (model.IsWrapperType) sb.AppendLine($"    if (!global::System.Collections.Generic.EqualityComparer<{model.ComparerTypeString}>.Default.Equals(Value, other)) return false;");
            else foreach (var prop in model.Properties) sb.AppendLine($"    {prop.GetEqualsExpr("other")}");
            sb.AppendLine("    return true; }"); sb.AppendLine();
        }
        if (!model.HasManualEquals && !model.HasManualObjectEquals)
        {
            sb.AppendLine($@"public override bool Equals(object? obj) {{ return obj is {model.NonNullableTypeString} other && Equals(other); }}");
            sb.AppendLine();
        }
        if (!model.HasManualGetHashCode)
        {
            if (model.Properties.Count == 1 && model.IsWrapperType) sb.AppendLine($@"public override int GetHashCode() {{ return global::System.Collections.Generic.EqualityComparer<{model.ComparerTypeString}>.Default.GetHashCode(Value!); }}");
            else
            {
                sb.AppendLine("public override int GetHashCode() { unchecked { int hash = 17;");
                foreach (var prop in model.Properties) sb.AppendLine($"        {prop.GetHashCodeExpr()}");
                sb.AppendLine("        return hash; } }");
            }
        }
        return sb.ToString();
    }

    // 🔽 核心：只在生成属性代码时，过滤掉 IsGenerated == false 的属性
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
    public bool HasManualDefaultCtor { get; }
    public bool HasManualParamCtor { get; }
    public bool HasManualFillBy { get; }
    public bool HasManualBuild { get; }
    public bool HasManualTrans { get; }

    public GenerationModel(string className, string @namespace, string sourceTypeName, string nullableParamTypeString, string nonNullableTypeString, string comparerTypeString,
        List<PropertyMapping> properties, bool needsINPC, bool hasAutoBase, bool hasManualEquals, bool hasManualGetHashCode, bool hasManualObjectEquals, bool isWrapperType, bool canBeNull, List<string> logs,
        bool hasManualDefaultCtor, bool hasManualParamCtor, bool hasManualFillBy, bool hasManualBuild, bool hasManualTrans)
    {
        ClassName = className; Namespace = @namespace; SourceTypeName = sourceTypeName;
        NullableParamTypeString = nullableParamTypeString; NonNullableTypeString = nonNullableTypeString; ComparerTypeString = comparerTypeString;
        Properties = properties; NeedsINPC = needsINPC;
        HasAutoBase = hasAutoBase; HasManualEquals = hasManualEquals; HasManualGetHashCode = hasManualGetHashCode;
        HasManualObjectEquals = hasManualObjectEquals; IsWrapperType = isWrapperType; CanBeNull = canBeNull; DebugLogs = logs;
        HasManualDefaultCtor = hasManualDefaultCtor; HasManualParamCtor = hasManualParamCtor; HasManualFillBy = hasManualFillBy;
        HasManualBuild = hasManualBuild; HasManualTrans = hasManualTrans;
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

    // 🔽 修复：分别存储源端和VM端的集合元素类型
    public bool IsCollection { get; }
    public string? SourceCollectionElementTypeString { get; }  // 源类型集合的元素类型，如: global::FMO.Models.PartRedemptionFee
    public string? VmCollectionElementTypeString { get; }      // VM类型集合的元素类型，如: global::FMO.ViewModels.PartFeeViewModel
    public bool IsNestedCollection { get; }

    public PropertyMapping(string name, string sourceTypeString, string vmTypeString, string comparerTypeString,
        bool isNested, bool sourceIsNullable, bool vmIsNullable, bool sourceIsWritable, bool vmIsWritable,
        bool isString, bool isValueType, bool isWrapperValue, bool isGenerated,
        bool isCollection = false, string? sourceCollectionElementTypeString = null, string? vmCollectionElementTypeString = null, bool isNestedCollection = false)
    {
        Name = name; SourceTypeString = sourceTypeString; VmTypeString = vmTypeString; ComparerTypeString = comparerTypeString;
        IsNested = isNested; SourceIsNullable = sourceIsNullable; VmIsNullable = vmIsNullable;
        SourceIsWritable = sourceIsWritable; VmIsWritable = vmIsWritable;
        IsString = isString; IsValueType = isValueType; IsWrapperValue = isWrapperValue;
        IsGenerated = isGenerated;

        IsCollection = isCollection;
        SourceCollectionElementTypeString = sourceCollectionElementTypeString;
        VmCollectionElementTypeString = vmCollectionElementTypeString;
        IsNestedCollection = isNestedCollection;
    }

    public string GetAssignFromSourceExpr(string sourceObjVar)
    {
        string srcAccess = IsWrapperValue ? sourceObjVar : $"{sourceObjVar}.{Name}";

        // 🔽 修复：集合类型处理 - 使用 VM 端元素类型
        if (IsCollection)
        {
            string vmElementType = VmCollectionElementTypeString ?? SourceCollectionElementTypeString ?? "object";

            if (IsNestedCollection)
            {
                // 嵌套 ViewModel 集合：List<Source> -> ObservableCollection<VM>
                if (SourceIsNullable)
                {
                    string fallback = VmIsNullable ? "null" : $"new global::System.Collections.ObjectModel.ObservableCollection<{vmElementType}>()";
                    return $"{Name} = {srcAccess} != null ? new global::System.Collections.ObjectModel.ObservableCollection<{vmElementType}>({srcAccess}.Select(x => new {vmElementType}(x))) : {fallback};";
                }
                else
                {
                    return $"{Name} = new global::System.Collections.ObjectModel.ObservableCollection<{vmElementType}>({srcAccess}.Select(x => new {vmElementType}(x)));";
                }
            }
            else
            {
                // 普通集合：使用 VM 端元素类型创建新集合
                if (SourceIsNullable)
                {
                    string fallback = VmIsNullable ? "null" : $"new global::System.Collections.ObjectModel.ObservableCollection<{vmElementType}>()";
                    return $"{Name} = {srcAccess} != null ? new global::System.Collections.ObjectModel.ObservableCollection<{vmElementType}>({srcAccess}) : {fallback};";
                }
                else
                {
                    return $"{Name} = new global::System.Collections.ObjectModel.ObservableCollection<{vmElementType}>({srcAccess});";
                }
            }
        }

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
        if (IsCollection)
        {
            string vmElementType = VmCollectionElementTypeString ?? SourceCollectionElementTypeString ?? "object";
            if (VmIsNullable) return $"{Name} = default;";
            return $"{Name} = new global::System.Collections.ObjectModel.ObservableCollection<{vmElementType}>();";
        }
        if (VmIsNullable) return $"{Name} = default;";
        if (IsNested) return $"{Name} = default!;";
        if (IsValueType) return $"{Name} = default;";
        return $"{Name} = default!;";
    }

    public string GetBuildExpr()
    {
        if (!SourceIsWritable) return "";

        // 🔽 修复：集合类型 Build - 使用源端元素类型
        if (IsCollection)
        {
            string srcElementType = SourceCollectionElementTypeString ?? VmCollectionElementTypeString ?? "object";

            if (IsNestedCollection)
            {
                // ObservableCollection<VM> -> List<Source>
                if (VmIsNullable)
                {
                    if (!SourceIsNullable) return $"{Name} = {Name}?.Select(x => x.Build()).ToList() ?? default!,";
                    return $"{Name} = {Name}?.Select(x => x.Build()).ToList(),";
                }
                else
                {
                    return $"{Name} = {Name}.Select(x => x.Build()).ToList(),";
                }
            }
            else
            {
                // ObservableCollection<T> -> List<T> (类型相同或兼容)
                if (VmIsNullable)
                {
                    if (!SourceIsNullable) return $"{Name} = {Name}?.ToList() ?? default!,";
                    return $"{Name} = {Name}?.ToList(),";
                }
                else
                {
                    return $"{Name} = {Name}.ToList(),";
                }
            }
        }

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

        // 🔽 修复：集合类型使用显式泛型的 SequenceEqual
        if (IsCollection)
        {
            if (IsNestedCollection)
            {
                // 嵌套 ViewModel 集合比较 - 使用 VM 端元素类型
                string elemType = VmCollectionElementTypeString ?? SourceCollectionElementTypeString ?? "object";

                if (VmIsNullable || SourceIsNullable)
                {
                    return $"if (!({Name} is null ? {otherAccess} is null : {otherAccess} is null ? false : global::System.Linq.Enumerable.SequenceEqual<{elemType}>({Name}.Select(x => x.Build()), {otherAccess}.Select(x => x)))) return false;";
                }
                else
                {
                    return $"if (!global::System.Linq.Enumerable.SequenceEqual<{elemType}>({Name}.Select(x => x.Build()), {otherAccess}.Select(x => x))) return false;";
                }
            }
            else
            {
                // 普通集合比较 - 使用 VM 端元素类型
                string elemType = VmCollectionElementTypeString ?? SourceCollectionElementTypeString ?? "object";

                if (VmIsNullable || SourceIsNullable)
                {
                    return $"if (!({Name} is null ? {otherAccess} is null : {otherAccess} is null ? false : global::System.Linq.Enumerable.SequenceEqual<{elemType}>({Name}, {otherAccess}))) return false;";
                }
                else
                {
                    return $"if (!global::System.Linq.Enumerable.SequenceEqual<{elemType}>({Name}, {otherAccess})) return false;";
                }
            }
        }

        if (IsNested)
        {
            string thisBuild = VmIsNullable ? $"{Name}?.Build()" : $"{Name}.Build()";
            string otherBuild = SourceIsNullable ? $"{otherAccess}?.Build()" : $"{otherAccess}.Build()";
            return $"if (!global::System.Collections.Generic.EqualityComparer<{ComparerTypeString}>.Default.Equals({thisBuild}, {otherBuild})) return false;";
        }
        else
        {
            return $"if (!global::System.Collections.Generic.EqualityComparer<{ComparerTypeString}>.Default.Equals({Name}, {otherAccess})) return false;";
        }
    }

    public string GetNullCheckExpr()
    {
        if (IsCollection)
        {
            return $"({Name} is null || !{Name}.Any())";
        }
        return $"global::System.Collections.Generic.EqualityComparer<{ComparerTypeString}>.Default.Equals({Name}, default)";
    }

    public string GetHashCodeExpr()
    {
        // 🔽 修复：集合类型 GetHashCode - 使用 VM 端元素类型
        if (IsCollection)
        {
            string elemType = IsNestedCollection ? (SourceCollectionElementTypeString ?? VmCollectionElementTypeString ?? "object") : (VmCollectionElementTypeString ?? SourceCollectionElementTypeString ?? "object");

            if (IsNestedCollection)
            {
                string seqExpr = VmIsNullable ? $"{Name}?.Select(x => x.Build())" : $"{Name}.Select(x => x.Build())";
                return $"if ({Name} != null) {{ foreach (var item in {seqExpr}!) {{ hash = hash * 31 + global::System.Collections.Generic.EqualityComparer<{elemType}>.Default.GetHashCode(item!); }} }}";
            }
            else
            {
                return $"if ({Name} != null) {{ foreach (var item in {Name}!) {{ hash = hash * 31 + global::System.Collections.Generic.EqualityComparer<{elemType}>.Default.GetHashCode(item!); }} }}";
            }
        }

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
