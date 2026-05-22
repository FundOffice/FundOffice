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

        // ==========================================
        // 🔹 核心逻辑 1：精确计算 C# 无约束泛型 T? 的实例化类型
        // ==========================================
        string nullableParamTypeString; // 对应接口中的 TValue?
        string nonNullableTypeString;   // 用于 is 模式匹配
        string comparerTypeString;      // 用于 EqualityComparer<T> 的泛型参数

        if (sourceType.IsReferenceType)
        {
            // 引用类型：T? 变成 可空引用类型 (如 FundFeeInfo?)
            nonNullableTypeString = sourceTypeName.EndsWith("?") ? sourceTypeName.Substring(0, sourceTypeName.Length - 1) : sourceTypeName;
            nullableParamTypeString = nonNullableTypeString + "?";
            comparerTypeString = nonNullableTypeString; // EqualityComparer 不接受引用类型的 ?
        }
        else
        {
            // 值类型 (包含 int, Enum, DateTime, 以及 Nullable<int>)：T? 就是 T 本身！
            nullableParamTypeString = sourceTypeName;
            comparerTypeString = sourceTypeName;      // EqualityComparer 接受 int, int?, FundMode

            if (sourceType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            {
                var underlyingType = ((INamedTypeSymbol)sourceType).TypeArguments[0];
                nonNullableTypeString = underlyingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            }
            else
            {
                nonNullableTypeString = sourceTypeName;
            }
        }

        bool canBeNull = sourceType.IsReferenceType || sourceType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;

        var viewModelDeclaredProperties = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in targetClass.GetMembers())
            if (member is IPropertySymbol p && p.DeclaredAccessibility == Accessibility.Public)
                viewModelDeclaredProperties.Add(p.Name);

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

        var propertiesToGenerate = new List<PropertyInfo>();
        var propertiesToAssignOnly = new List<PropertyInfo>();

        bool isWrapperType = sourceType.SpecialType switch
        {
            SpecialType.System_Boolean or SpecialType.System_Char or SpecialType.System_SByte or
            SpecialType.System_Byte or SpecialType.System_Int16 or SpecialType.System_UInt16 or
            SpecialType.System_Int32 or SpecialType.System_UInt32 or SpecialType.System_Int64 or
            SpecialType.System_UInt64 or SpecialType.System_Decimal or SpecialType.System_Single or
            SpecialType.System_Double or SpecialType.System_String or SpecialType.System_DateTime => true,
            _ => sourceType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T || sourceType.TypeKind == TypeKind.Enum
        };

        if (isWrapperType)
        {
            logs.Add("⚠️ 启用 Wrapper 模式 (基础类型/Enum/Nullable)");
            bool sourceIsNullable = sourceType.IsReferenceType || sourceType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
            var propInfo = new PropertyInfo(sourceTypeName, "Value", sourceIsNullable, sourceIsNullable, true, false, sourceTypeName, sourceType.SpecialType == SpecialType.System_String, sourceType.IsValueType);
            propertiesToGenerate.Add(propInfo);
        }
        else
        {
            var currentType = sourceType;
            while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
            {
                foreach (var member in currentType.GetMembers())
                {
                    if (member is IPropertySymbol prop && prop.DeclaredAccessibility == Accessibility.Public && !prop.IsStatic)
                    {
                        ITypeSymbol propType = prop.Type;
                        string propTypeString = propType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                        INamedTypeSymbol? propNamedType = propType switch
                        {
                            INamedTypeSymbol namedType => namedType,
                            IArrayTypeSymbol array when array.ElementType is INamedTypeSymbol elem => elem,
                            _ => null
                        };

                        if (propNamedType == null || viewModelDeclaredProperties.Contains(prop.Name)) continue;

                        bool sourceIsNullable = propType.IsReferenceType || propType is IArrayTypeSymbol
                            ? prop.NullableAnnotation != NullableAnnotation.NotAnnotated
                            : propType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;

                        bool vmIsNullable = propType.IsReferenceType || (propType is IArrayTypeSymbol) || prop.NullableAnnotation == NullableAnnotation.Annotated;
                        bool isWritable = prop.SetMethod != null;

                        var (isNested, vmType) = propType is IArrayTypeSymbol
                            ? (false, null)
                            : CheckNestedViewModelPattern(targetClass, prop.Name, propNamedType);

                        var vmTypeName = isNested && vmType != null
                            ? vmType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                            : propTypeString;

                        var propInfo = new PropertyInfo(propTypeString, prop.Name, sourceIsNullable, vmIsNullable, isWritable, isNested, vmTypeName, propType.SpecialType == SpecialType.System_String, propType.IsValueType);

                        if (existingInHierarchy.Contains(prop.Name)) propertiesToAssignOnly.Add(propInfo);
                        else propertiesToGenerate.Add(propInfo);
                    }
                }
                currentType = currentType.BaseType;
            }
        }

        if (propertiesToGenerate.Count == 0 && propertiesToAssignOnly.Count == 0) return null;

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

        return new GenerationModel(className, ns, sourceTypeName, nullableParamTypeString, nonNullableTypeString, comparerTypeString,
            propertiesToGenerate, propertiesToAssignOnly, needsINPC, hasAutoBase,
            hasManualEquals, hasManualGetHashCode, hasManualObjectEquals, isWrapperType, canBeNull, logs);
    }

    private static string GenerateSource(GenerationModel model)
    {
#if DEBUG
        var debugHeader = string.Join("\n", new[] { "// 🔍 ===== AutoViewModel Debug Info =====" }
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

        var assignments = new List<string>();
        if (model.IsWrapperType) assignments.Add("Value = v;");
        else foreach (var prop in model.PropertiesToGenerate.Concat(model.PropertiesToAssignOnly)) assignments.Add(prop.GetFillAssignment("v"));
        string assignmentsBlock = string.Join("\n            ", assignments);

        var generatedProperties = string.Join("\n\n", model.PropertiesToGenerate.Select(prop =>
        {
            var typeName = prop.GetViewModelPropertyTypeString();
            return $"\n    public {typeName} {prop.Name} {{ get => field; set => SetProperty(ref field, value); }}";
        }));

        var buildInitializers = string.Join("\n                ",
            model.PropertiesToGenerate.Concat(model.PropertiesToAssignOnly)
                .Where(p => p.IsWritable).Select(p => p.GetBuildInitializer()).Where(s => !string.IsNullOrEmpty(s)));

        var namespaceOpen = !string.IsNullOrEmpty(model.Namespace) ? $"namespace {model.Namespace}\n{{" : "";
        var namespaceClose = !string.IsNullOrEmpty(model.Namespace) ? "}" : "";
        var inpcInheritance = model.NeedsINPC ? " : INotifyPropertyChanged" : "";
        var overnew = model.HasAutoBase ? "new " : "";

        // 🔹 优化：根据 CanBeNull 决定是否需要 if 判断，消除非空值类型的 "always true" 警告
        string ctorBody;
        if (model.CanBeNull)
        {
            ctorBody = $$"""
                if (val is {{model.NonNullableTypeString}} v)
                {
                    {{assignmentsBlock}}
                }
            """;
        }
        else
        {
            ctorBody = assignmentsBlock.Replace("v", "val");
        }

        string fillByBody;
        if (model.CanBeNull)
        {
            fillByBody = $$"""
                if (obj is {{model.NonNullableTypeString}} val)
                {
                    {{assignmentsBlock.Replace("v", "val")}}
                }
                return this;
            """;
        }
        else
        {
            fillByBody = $$"""
                {{assignmentsBlock.Replace("v", "obj")}}
                return this;
            """;
        }

        var equalsBlock = "";
        if (!model.HasManualEquals)
        {
            var equalsLines = new List<string>();
            equalsLines.Add($"        public bool Equals({model.NullableParamTypeString} other)");
            equalsLines.Add("        {");
            if (model.CanBeNull) equalsLines.Add("            if (other is null) return false;");

            if (model.IsWrapperType)
            {
                equalsLines.Add($"            if (!global::System.Collections.Generic.EqualityComparer<{model.ComparerTypeString}>.Default.Equals(Value, other)) return false;");
            }
            else
            {
                foreach (var prop in model.PropertiesToGenerate.Concat(model.PropertiesToAssignOnly))
                {
                    if (prop.IsNestedViewModel)
                    {
                        var nullOp = prop.VmIsNullable ? "?" : "!";
                        equalsLines.Add($"            if (!global::System.Collections.Generic.EqualityComparer<{prop.SourceTypeName}>.Default.Equals({prop.Name}{nullOp}.Build(), other.{prop.Name})) return false;");
                    }
                    else
                    {
                        equalsLines.Add($"            if (!global::System.Collections.Generic.EqualityComparer<{prop.SourceTypeName}>.Default.Equals({prop.Name}, other.{prop.Name})) return false;");
                    }
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
                return obj is {{model.NonNullableTypeString}} other && Equals(other);
            }
            """;
        }

        var getHashCodeBlock = "";
        if (!model.HasManualGetHashCode)
        {
            var allProps = model.PropertiesToGenerate.Concat(model.PropertiesToAssignOnly).ToList();
            if (allProps.Count == 1 && model.IsWrapperType)
            {
                var nullForgiving = model.CanBeNull ? "!" : "";
                getHashCodeBlock = $$"""
                public override int GetHashCode()
                {
                    return global::System.Collections.Generic.EqualityComparer<{{model.ComparerTypeString}}>.Default.GetHashCode(Value{{nullForgiving}});
                }
                """;
            }
            else
            {
                var hashCodeLines = new List<string> { "public override int GetHashCode()", "{", "    unchecked", "    {", "        int hash = 17;" };
                foreach (var prop in allProps)
                {
                    var nullForgiving = (prop.SourceIsNullable || prop.VmIsNullable || !prop.IsValueType) ? "!" : "";
                    if (prop.IsNestedViewModel)
                        hashCodeLines.Add($"        hash = hash * 31 + global::System.Collections.Generic.EqualityComparer<{prop.SourceTypeName}>.Default.GetHashCode({prop.Name}{(prop.VmIsNullable ? "?" : "!")}.Build()!);");
                    else
                        hashCodeLines.Add($"        hash = hash * 31 + global::System.Collections.Generic.EqualityComparer<{prop.SourceTypeName}>.Default.GetHashCode({prop.Name}{nullForgiving});");
                }
                hashCodeLines.AddRange(new[] { "        return hash;", "    }", "}" });
                getHashCodeBlock = string.Join("\n", hashCodeLines);
            }
        }

        var transMethods = $$"""
            public static {{model.SourceTypeName}} Trans({{model.ClassName}} vm)
            {
                if (vm is null) return default!;
                return vm.Build();
            }

            public static {{model.ClassName}} Trans({{model.NullableParamTypeString}} val)
            {
                return new {{model.ClassName}}(val);
            }
        """;

        string buildBlock = model.IsWrapperType
            ? $"public {overnew}{model.SourceTypeName} Build()\n{{\n    return Value;\n}}"
            : $"public {overnew}{model.SourceTypeName} Build()\n{{\n    var result = new {model.SourceTypeName}\n    {{\n        {buildInitializers}\n    }};\n    return result;\n}}";

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
            
            public {{model.ClassName}}({{model.NullableParamTypeString}} val)
            {
                {{ctorBody}}
            }
            
        {{transMethods}}
        {{equalsBlock}}
        {{objectEqualsBlock}}
        {{getHashCodeBlock}}
            
            public {{model.ClassName}} FillBy({{model.NullableParamTypeString}} obj)
            {
                {{fillByBody}}
            }
            
        {{buildBlock}}
        {{generatedProperties}}
        }
        {{namespaceClose}}
        """;
    }

    private static (bool IsMatch, INamedTypeSymbol? ViewModelType) CheckNestedViewModelPattern(INamedTypeSymbol viewModelClass, string propertyName, INamedTypeSymbol sourcePropertyType)
    {
        INamedTypeSymbol? current = viewModelClass;
        IPropertySymbol? vmProperty = null;
        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            vmProperty = current.GetMembers(propertyName).OfType<IPropertySymbol>().FirstOrDefault(p => p.DeclaredAccessibility == Accessibility.Public);
            if (vmProperty != null) break;
            current = current.BaseType;
        }
        if (vmProperty?.Type is not INamedTypeSymbol vmPropertyType) return (false, null);
        var expectedVmTypeName = sourcePropertyType.Name + "ViewModel";
        if (vmPropertyType.Name == expectedVmTypeName && HasConstructorWithSourceType(vmPropertyType, sourcePropertyType))
            return (true, vmPropertyType);
        return (false, null);
    }

    private static bool HasConstructorWithSourceType(INamedTypeSymbol viewModelType, INamedTypeSymbol sourceType)
    {
        return viewModelType.Constructors.Any(ctor => ctor.Parameters.Length == 1 && SymbolEqualityComparer.Default.Equals(ctor.Parameters[0].Type, sourceType));
    }

    private class GenerationModel
    {
        public string ClassName { get; }
        public string Namespace { get; }
        public string SourceTypeName { get; }
        public string NullableParamTypeString { get; }
        public string NonNullableTypeString { get; }
        public string ComparerTypeString { get; }
        public List<PropertyInfo> PropertiesToGenerate { get; }
        public List<PropertyInfo> PropertiesToAssignOnly { get; }
        public bool NeedsINPC { get; }
        public bool HasAutoBase { get; }
        public bool HasManualEquals { get; }
        public bool HasManualGetHashCode { get; }
        public bool HasManualObjectEquals { get; }
        public bool IsWrapperType { get; }
        public bool CanBeNull { get; }
        public List<string> DebugLogs { get; }

        public GenerationModel(string className, string @namespace, string sourceTypeName, string nullableParamTypeString, string nonNullableTypeString, string comparerTypeString,
            List<PropertyInfo> propertiesToGenerate, List<PropertyInfo> propertiesToAssignOnly, bool needsINPC, bool hasAutoBase,
            bool hasManualEquals, bool hasManualGetHashCode, bool hasManualObjectEquals, bool isWrapperType, bool canBeNull, List<string> logs)
        {
            ClassName = className; Namespace = @namespace; SourceTypeName = sourceTypeName;
            NullableParamTypeString = nullableParamTypeString; NonNullableTypeString = nonNullableTypeString; ComparerTypeString = comparerTypeString;
            PropertiesToGenerate = propertiesToGenerate; PropertiesToAssignOnly = propertiesToAssignOnly; NeedsINPC = needsINPC;
            HasAutoBase = hasAutoBase; HasManualEquals = hasManualEquals; HasManualGetHashCode = hasManualGetHashCode;
            HasManualObjectEquals = hasManualObjectEquals; IsWrapperType = isWrapperType; CanBeNull = canBeNull; DebugLogs = logs;
        }
    }

    private class PropertyInfo
    {
        public string SourceTypeName { get; }
        public string ViewModelTypeName { get; }
        public string Name { get; }
        public bool SourceIsNullable { get; }
        public bool VmIsNullable { get; }
        public bool IsWritable { get; }
        public bool IsNestedViewModel { get; }
        public bool IsString { get; }
        public bool IsValueType { get; }

        public PropertyInfo(string sourceTypeName, string name, bool sourceIsNullable, bool vmIsNullable, bool isWritable, bool isNestedViewModel, string viewModelTypeName, bool isString, bool isValueType)
        {
            SourceTypeName = sourceTypeName; Name = name; SourceIsNullable = sourceIsNullable; VmIsNullable = vmIsNullable;
            IsWritable = isWritable; IsNestedViewModel = isNestedViewModel; ViewModelTypeName = viewModelTypeName; IsString = isString; IsValueType = isValueType;
        }

        public string GetViewModelPropertyTypeString()
        {
            var typeName = IsNestedViewModel ? ViewModelTypeName : SourceTypeName;
            if (typeName.EndsWith("?") || typeName.Contains("Nullable<")) return typeName;
            return VmIsNullable ? $"{typeName}?" : typeName;
        }

        public string GetFillAssignment(string sourceVar)
        {
            if (IsNestedViewModel)
                return VmIsNullable ? $"{Name} = {sourceVar}.{Name} != null ? new {ViewModelTypeName}({sourceVar}.{Name}) : null;" : $"{Name} = new {ViewModelTypeName}({sourceVar}.{Name});";
            return $"{Name} = {sourceVar}.{Name};";
        }

        public string GetBuildInitializer()
        {
            if (IsNestedViewModel) return VmIsNullable ? $"{Name} = {Name}?.Build()," : $"{Name} = {Name}!.Build(),";
            if (!IsWritable) return string.Empty;
            if (!SourceIsNullable && VmIsNullable)
            {
                if (IsString) return $"{Name} = {Name} ?? string.Empty,";
                if (IsValueType) return $"{Name} = {Name} ?? default,";
                return $"{Name} = {Name}!,";
            }
            return $"{Name} = {Name},";
        }
    }
}