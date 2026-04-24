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
        var ns = targetClass.ContainingNamespace.IsGlobalNamespace ? string.Empty : targetClass.ContainingNamespace.ToDisplayString();
        var className = targetClass.Name;
        var sourceTypeName = sourceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        // 🔹 1. ViewModel 本身（不包括父类）已声明的属性 → 构造函数/Build 时跳过
        var viewModelDeclaredProperties = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in targetClass.GetMembers())
        {
            if (member is IPropertySymbol prop && prop.DeclaredAccessibility == Accessibility.Public)
                viewModelDeclaredProperties.Add(prop.Name);
        }

        // 🔹 2. ViewModel 继承链（含自身）的所有公开属性 → 判断是否需要生成新 property
        var existingInHierarchy = new HashSet<string>(StringComparer.Ordinal);
        var currentType = targetClass;
        while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
        {
            foreach (var member in currentType.GetMembers())
            {
                if (member is IPropertySymbol prop && prop.DeclaredAccessibility == Accessibility.Public)
                    existingInHierarchy.Add(prop.Name);
            }
            currentType = currentType.BaseType;
        }

        // 🔹 3. 收集源类型属性，分类处理
        var propertiesToGenerate = new List<PropertyInfo>();      // 需生成 property + 赋值
        var propertiesToAssignOnly = new List<PropertyInfo>();    // 只需赋值（父类已有 property）

        currentType = sourceType;
        while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
        {
            foreach (var member in currentType.GetMembers())
            {
                if (member is IPropertySymbol prop &&
                    prop.DeclaredAccessibility == Accessibility.Public &&
                    !prop.IsStatic)
                {
                    // ViewModel 本身已声明 → 完全跳过
                    if (viewModelDeclaredProperties.Contains(prop.Name))
                        continue;

                    bool isNullable = prop.Type.TypeKind == TypeKind.Class ||
                                      prop.Type.SpecialType == SpecialType.System_String ||
                                      prop.Type.IsReferenceType;

                    var propInfo = new PropertyInfo(
                        prop.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        prop.Name,
                        isNullable);

                    // ViewModel 继承链已有 → 只赋值；否则 → 生成 property + 赋值
                    if (existingInHierarchy.Contains(prop.Name))
                        propertiesToAssignOnly.Add(propInfo);
                    else
                        propertiesToGenerate.Add(propInfo);
                }
            }
            currentType = currentType.BaseType;
        }

        // 如果没有任何需要处理的属性，跳过生成
        if (propertiesToGenerate.Count == 0 && propertiesToAssignOnly.Count == 0)
            return null;

        bool needsINPC = !targetClass.AllInterfaces.Any(i => i.Name == "INotifyPropertyChanged");

        return new GenerationModel(
            className,
            ns,
            sourceTypeName,
            propertiesToGenerate,
            propertiesToAssignOnly,
            needsINPC);
    }

    private static string GenerateSource(GenerationModel model)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.ComponentModel;");
        sb.AppendLine("using System.Runtime.CompilerServices;");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(model.Namespace))
        {
            sb.AppendLine($"namespace {model.Namespace}");
            sb.AppendLine("{");
        }

        sb.AppendLine($"    public partial class {model.ClassName}" + (model.NeedsINPC ? " : INotifyPropertyChanged" : ""));
        sb.AppendLine("    {");

        if (model.NeedsINPC)
        {
            sb.AppendLine("        public event PropertyChangedEventHandler? PropertyChanged;");
            sb.AppendLine("        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>");
            sb.AppendLine("            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));");
            sb.AppendLine();
        }

        // 🔹 无参构造函数
        sb.AppendLine($"        public {model.ClassName}() {{ }}");
        sb.AppendLine();

        // 🔹 带源类型参数的构造函数
        sb.AppendLine($"        public {model.ClassName}({model.SourceTypeName} val)");
        sb.AppendLine("        {");
        // 先处理需生成 property 的
        foreach (var prop in model.PropertiesToGenerate)
        {
            sb.AppendLine($"            {prop.Name} = val.{prop.Name};");
        }
        // 再处理只需赋值的（父类已有 property）
        foreach (var prop in model.PropertiesToAssignOnly)
        {
            sb.AppendLine($"            {prop.Name} = val.{prop.Name};");
        }
        sb.AppendLine("        }");
        sb.AppendLine();

        // 🔹 Build() 方法
        sb.AppendLine($"        public {model.SourceTypeName} Build()");
        sb.AppendLine("        {");
        sb.AppendLine($"            var result = new {model.SourceTypeName}");
        sb.AppendLine("             {");
        foreach (var prop in model.PropertiesToGenerate)
        {
            sb.AppendLine($"                {prop.Name} = {prop.Name}!,");
        }
        foreach (var prop in model.PropertiesToAssignOnly)
        {
            sb.AppendLine($"                {prop.Name} = {prop.Name}!,");
        }
        sb.AppendLine("             };");
        sb.AppendLine("            return result;");
        sb.AppendLine("        }");
        sb.AppendLine();

        // 🔹 生成新的 property（仅针对继承链中不存在的）
        foreach (var prop in model.PropertiesToGenerate)
        {
            var typeName = prop.IsNullable ? $"{prop.TypeName}?" : prop.TypeName;
            var backingField = $"_{char.ToLowerInvariant(prop.Name[0])}{prop.Name.Substring(1)}";

            sb.AppendLine($"        private {typeName} {backingField};");
            sb.AppendLine($"        public {typeName} {prop.Name}");
            sb.AppendLine("        {");
            sb.AppendLine($"            get => {backingField};");
            sb.AppendLine("            set");
            sb.AppendLine("            {");
            sb.AppendLine($"                if (!global::System.Collections.Generic.EqualityComparer<{typeName}>.Default.Equals({backingField}, value))");
            sb.AppendLine("                {");
            sb.AppendLine($"                    {backingField} = value;");
            sb.AppendLine("                    OnPropertyChanged();");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        sb.AppendLine("    }");
        if (!string.IsNullOrEmpty(model.Namespace)) sb.AppendLine("}");

        return sb.ToString();
    }

    private class PropertyInfo
    {
        public string TypeName { get; }
        public string Name { get; }
        public bool IsNullable { get; }

        public PropertyInfo(string typeName, string name, bool isNullable)
        {
            TypeName = typeName;
            Name = name;
            IsNullable = isNullable;
        }
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

        public GenerationModel(
            string className,
            string @namespace,
            string sourceTypeName,
            List<PropertyInfo> propertiesToGenerate,
            List<PropertyInfo> propertiesToAssignOnly,
            bool needsINPC)
        {
            ClassName = className;
            Namespace = @namespace;
            SourceTypeName = sourceTypeName;
            PropertiesToGenerate = propertiesToGenerate;
            PropertiesToAssignOnly = propertiesToAssignOnly;
            NeedsINPC = needsINPC;
        }
    }
}