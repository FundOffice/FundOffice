using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SourceGenerator;

[Generator]
public class AutoViewModelIncrementalGenerator : IIncrementalGenerator
{
    private const string AttributeMetadataName = "FMO.Models.AutoViewModelAttribute";

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

                    var attribute = ctx.Attributes.FirstOrDefault();
                    var typeArg = attribute?.ConstructorArguments.FirstOrDefault().Value as INamedTypeSymbol;
                    if (typeArg == null) return null;

                    return BuildGenerationModel(classSymbol, typeArg);
                })
            .Where(model => model != null);

        context.RegisterSourceOutput(classDeclarations, (spc, model) =>
        {
            if (model == null) return;
            var source = GenerateSource(model);
            spc.AddSource($"{model.ClassName}.AutoViewModel.g.cs", SourceText.From(source, Encoding.UTF8));
        });
    }

    private static GenerationModel? BuildGenerationModel(INamedTypeSymbol targetClass, INamedTypeSymbol sourceType)
    {
        var logs = new List<string>();
        logs.Add($"[Start] {targetClass.Name} <- {sourceType.Name}");

        var ns = targetClass.ContainingNamespace.IsGlobalNamespace ? string.Empty : targetClass.ContainingNamespace.ToDisplayString();
        var className = targetClass.Name;
        var sourceTypeName = sourceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        // 1️⃣ VM 本身已声明的属性
        var viewModelDeclaredProperties = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in targetClass.GetMembers())
            if (member is IPropertySymbol p && p.DeclaredAccessibility == Accessibility.Public)
                viewModelDeclaredProperties.Add(p.Name);

        logs.Add($"[VM Declared] {string.Join(", ", viewModelDeclaredProperties)}");

        // 2️⃣ VM 继承链属性
        var existingInHierarchy = new HashSet<string>(StringComparer.Ordinal);
        INamedTypeSymbol? current = targetClass.BaseType;

        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            // 1. 收集当前 VM 类自身的公共属性
            logs.Add($"🔍 遍历VM类型：{current.Name}");
            foreach (var member in current.GetMembers())
            {
                if (member is IPropertySymbol p && p.DeclaredAccessibility == Accessibility.Public)
                {
                    existingInHierarchy.Add(p.Name);
                    logs.Add($"✅ 已包含VM属性：{p.Name}");
                }
            }

            // 2. 查找当前类是否是 AutoVM 并收集【源模型(Source)】的属性
            var autoAttr = current.GetAttributes().FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == AttributeMetadataName);

            if (autoAttr != null)
            {
                logs.Add($"ℹ️ 类型 {current.Name} 是 AutoVM，开始收集源模型属性");
                var srcType = autoAttr.ConstructorArguments.FirstOrDefault().Value as INamedTypeSymbol;

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
                else
                {
                    logs.Add($"❌ 源模型类型为 null");
                }
            }

            // 继续往上遍历父类
            current = current.BaseType;
        }

        logs.Add($"📊 最终已存在属性总数：{existingInHierarchy.Count}\n");

        // 3️⃣ 收集源属性 + 嵌套检查
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
                    // 🔹 支持命名类型和数组类型
                    ITypeSymbol propType = prop.Type;
                    string propTypeString = propType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                    // 如果是数组，获取元素类型用于嵌套检查（可选）
                    INamedTypeSymbol? propNamedType = propType switch
                    {
                        INamedTypeSymbol named => named,
                        IArrayTypeSymbol array when array.ElementType is INamedTypeSymbol elem => elem,
                        _ => null
                    };

                    if (propNamedType == null) continue; // 跳过不支持的类型（如泛型、元组等）

                    if (viewModelDeclaredProperties.Contains(prop.Name))
                    {
                        logs.Add($"  ⏭️ Skip {prop.Name} (VM already declares)");
                        continue;
                    }

                    bool isNullable = propType.IsReferenceType ||
                                     (propType is IArrayTypeSymbol) ||
                                     prop.NullableAnnotation == NullableAnnotation.Annotated;
                    bool isWritable = prop.SetMethod != null;

                    // 🔍 嵌套检查只对非数组类型生效
                    var (isNested, vmType) = propType is IArrayTypeSymbol
                        ? (false, null)
                        : CheckNestedViewModelPattern(targetClass, prop.Name, propNamedType);

                    var vmTypeName = isNested && vmType != null
                        ? vmType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                        : propTypeString; // ✅ 使用完整类型字符串，支持数组

                    var propInfo = new PropertyInfo(
                        propTypeString,      // 源类型（含数组）
                        prop.Name,
                        isNullable,
                        isWritable,
                        isNested,
                        vmTypeName);

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

        //bool needsINPC = !targetClass.AllInterfaces.Any(i => i.Name == "INotifyPropertyChanged");
        bool needsINPC = true, hasAutoBase = false;
        var baseType = targetClass.BaseType;

        // 遍历基类链
        while (baseType != null && baseType.SpecialType != SpecialType.System_Object)
        {
            // 检查基类是否标记了 AutoViewModelAttribute
            if (baseType.GetAttributes().Any(attr =>
                attr.AttributeClass?.ToDisplayString() == AttributeMetadataName))
            {
                // 父类已经是 AutoViewModel → 不需要生成 INPC
                needsINPC = false;
                hasAutoBase = true;
                break;
            }
            else if(baseType.Name.Contains("ObservableObject"))
            {
                needsINPC = false;
                hasAutoBase = false;
                break;
            }
            baseType = baseType.BaseType;
        }

        logs.Add($"最终 needsINPC = {needsINPC} | 类名：{targetClass.Name}");


        return new GenerationModel(className, ns, sourceTypeName,
            propertiesToGenerate, propertiesToAssignOnly, needsINPC, hasAutoBase, logs);
    }

    private static string GenerateSource(GenerationModel model)
    {
        // 🔹 生成调试注释头
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

        // 🔹 构造函数中的赋值语句
        var ctorAssignments = GenerateAssignments(model.PropertiesToGenerate, model.PropertiesToAssignOnly, "val");
        var fillAssignments = GenerateAssignments(model.PropertiesToGenerate, model.PropertiesToAssignOnly, "val");


        // 🔹 生成的 Property 代码块（使用 ViewModel 中的实际类型）
        var generatedProperties = string.Join("\n\n", model.PropertiesToGenerate.Select(prop =>
        {
            var typeName = prop.GetViewModelPropertyTypeString();
            var backingField = $"_{char.ToLowerInvariant(prop.Name[0])}{prop.Name.Substring(1)}";

            return $$"""

        public {{typeName}} {{prop.Name}} { get => field; set => SetProperty(ref field, value); }
  
""";
        }));

        // 🔹 Build 方法中的初始化语句（使用新的 GetBuildInitializer）
        var buildInitializers = string.Join("\n\t\t\t\t",
            model.PropertiesToGenerate.Concat(model.PropertiesToAssignOnly)
                .Where(p => p.IsWritable)
                .Select(p => p.GetBuildInitializer())
                .Where(s => !string.IsNullOrEmpty(s)));  // 过滤空字符串

        var namespaceOpen = !string.IsNullOrEmpty(model.Namespace) ? $"namespace {model.Namespace}\n{{" : "";
        var namespaceClose = !string.IsNullOrEmpty(model.Namespace) ? "}" : "";
        var inpcInheritance = model.NeedsINPC ? " : INotifyPropertyChanged" : "";
        var overnew = model.HasAutoBase ? "new" : "";

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

    // 🔹 辅助方法：生成赋值语句块
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

    // 🔹 查找 ViewModel 中对应属性的类型，检查是否符合嵌套 ViewModel 约定
    private static (bool IsMatch, INamedTypeSymbol? ViewModelType) CheckNestedViewModelPattern(
        INamedTypeSymbol viewModelClass,
        string propertyName,
        INamedTypeSymbol sourcePropertyType)
    {
        // 🔹 遍历继承链查找属性（含基类）
        INamedTypeSymbol? current = viewModelClass;
        IPropertySymbol? vmProperty = null;

        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            vmProperty = current.GetMembers(propertyName)
                .OfType<IPropertySymbol>()
                .FirstOrDefault(p => p.DeclaredAccessibility == Accessibility.Public);

            if (vmProperty != null)
                break;  // ✅ 找到即停止

            current = current.BaseType;  // 🔹 继续查父类
        }

        // 没找到属性 → 不启用嵌套模式
        if (vmProperty?.Type is not INamedTypeSymbol vmPropertyType)
            return (false, null);

        // 🔹 检查命名约定：源类型 "SimpleFile" → VM 属性类型应为 "SimpleFileViewModel"
        var expectedVmTypeName = sourcePropertyType.Name + "ViewModel";

        if (vmPropertyType.Name == expectedVmTypeName &&
            HasConstructorWithSourceType(vmPropertyType, sourcePropertyType))
        {
            return (true, vmPropertyType);
        }

        return (false, null);
    }

    // 🔹 检查 ViewModel 是否有接受源类型作为单参数的构造函数
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


    // 🔹 GenerationModel 增加 PropertiesToAssignOnly


    private class GenerationModel
    {
        public string ClassName { get; }
        public string Namespace { get; }
        public string SourceTypeName { get; }
        public List<PropertyInfo> PropertiesToGenerate { get; }      // 需生成 property
        public List<PropertyInfo> PropertiesToAssignOnly { get; }    // 只需赋值
        public bool NeedsINPC { get; }
        public bool HasAutoBase { get; }
        public List<string> DebugLogs { get; }

        public GenerationModel(
            string className,
            string @namespace,
            string sourceTypeName,
            List<PropertyInfo> propertiesToGenerate,
            List<PropertyInfo> propertiesToAssignOnly,
            bool needsINPC,
            bool hasAutoBase,
            List<string> logs)
        {
            ClassName = className;
            Namespace = @namespace;
            SourceTypeName = sourceTypeName;
            PropertiesToGenerate = propertiesToGenerate;
            PropertiesToAssignOnly = propertiesToAssignOnly;
            NeedsINPC = needsINPC;
            HasAutoBase = hasAutoBase;
            DebugLogs = logs;
        }
    }

    private class PropertyInfo
    {
        public string SourceTypeName { get; }        // 源类属性类型，如 "global::MyApp.ClassA"
        public string ViewModelTypeName { get; }     // VM 中使用的类型，如 "global::MyApp.ClassAViewModel"
        public string Name { get; }
        public bool IsNullable { get; }
        public bool IsWritable { get; }
        public bool IsNestedViewModel { get; }       // 是否启用嵌套 ViewModel 模式

        public PropertyInfo(string sourceTypeName, string name, bool isNullable, bool isWritable,
            bool isNestedViewModel, string viewModelTypeName)
        {
            SourceTypeName = sourceTypeName;
            Name = name;
            IsNullable = isNullable;
            IsWritable = isWritable;
            IsNestedViewModel = isNestedViewModel;
            ViewModelTypeName = viewModelTypeName;
        }

        // 🔹 获取在 ViewModel 中声明属性时使用的类型（带 ? 处理）
        public string GetViewModelPropertyTypeString()
        {
            var typeName = IsNestedViewModel ? ViewModelTypeName : SourceTypeName;
            return IsNullable ? $"{typeName}?" : typeName;
        }

        // 🔹 获取 FillBy 中的赋值表达式
        public string GetFillAssignment(string sourceVar)
        {
            if (IsNestedViewModel)
            {
                return IsNullable
                    ? $"{Name} = {sourceVar}.{Name} != null ? new {ViewModelTypeName}({sourceVar}.{Name}) : null;"
                    : $"{Name} = new {ViewModelTypeName}({sourceVar}.{Name});";
            }
            // ✅ 数组/普通类型都直接赋值
            return $"{Name} = {sourceVar}.{Name};";
        }

        public string GetBuildInitializer()
        {
            if (IsNestedViewModel)
            {
                return IsNullable
                    ? $"{Name} = {Name}?.Build(),"
                    : $"{Name} = {Name}!.Build(),";
            }
            // ✅ 只处理可写属性
            if (!IsWritable)
                return string.Empty;
             
            // string
            if (SourceTypeName == "string")
                return $"{Name} = {Name} is not null ? {Name} : String.Empty,";

            // 🔹 可空类型：不为 null 时赋值，否则赋 default
            // 🔹 非可空类型：直接赋值
            return IsNullable
                ? $"{Name} = {Name} is not null ? {Name} : default,"
                : $"{Name} = {Name},";
        }
    }
}
