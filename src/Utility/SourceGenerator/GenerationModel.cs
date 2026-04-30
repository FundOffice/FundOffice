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

                    bool isWritable = prop.SetMethod != null && !prop.SetMethod.IsInitOnly;

                    var propInfo = new PropertyInfo(
                        prop.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        prop.Name,
                        isNullable,
                        isWritable);

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
        var inpcBlock = model.NeedsINPC ? """
                public event PropertyChangedEventHandler? PropertyChanged;
                protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        """ : "";

        // 🔹 构造函数中的赋值语句
        var ctorAssignments = GenerateAssignments(model.PropertiesToGenerate, model.PropertiesToAssignOnly, "val");
        var fillAssignments = GenerateAssignments(model.PropertiesToGenerate, model.PropertiesToAssignOnly, "val");

        // 🔹 Build 方法中的初始化语句（仅可写属性）
        var buildInitializers = string.Join("\n",
            model.PropertiesToGenerate.Concat(model.PropertiesToAssignOnly)
                .Where(p => p.IsWritable)
                .Select(p => $"                {p.Name} = {p.Name}!,"));

        // 🔹 生成的 Property 代码块
        var generatedProperties = string.Join("\n", model.PropertiesToGenerate.Select(prop =>
        {
            var typeName = prop.IsNullable ? $"{prop.TypeName}?" : prop.TypeName;
            var backingField = $"_{char.ToLowerInvariant(prop.Name[0])}{prop.Name.Substring(1)}";
            return $$"""
                private {{typeName}} {{backingField}};
                public {{typeName}} {{prop.Name}}
                {
                    get => {{backingField}};
                    set
                    {
                        if (!global::System.Collections.Generic.EqualityComparer<{{typeName}}>.Default.Equals({{backingField}}, value))
                        {
                            {{backingField}} = value;
                            OnPropertyChanged();
                        }
                    }
                }

        """;
        }));

        var namespaceOpen = !string.IsNullOrEmpty(model.Namespace) ? $"namespace {model.Namespace}\n{{" : "";
        var namespaceClose = !string.IsNullOrEmpty(model.Namespace) ? "}" : "";
        var inpcInheritance = model.NeedsINPC ? " : INotifyPropertyChanged" : "";

        return $$"""
#nullable enable
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

        public void FillBy({{model.SourceTypeName}}? obj)
        {
             if(obj is {{model.SourceTypeName}} val)
              {
{{fillAssignments}}
              }
        }

        public {{model.SourceTypeName}} Build()
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
            lines.Add($"                {prop.Name} = {sourceVar}.{prop.Name};");
        foreach (var prop in toAssignOnly)
            lines.Add($"                {prop.Name} = {sourceVar}.{prop.Name};");
        return string.Join("\n", lines);
    }

    private class PropertyInfo
    {
        public string TypeName { get; }
        public string Name { get; }
        public bool IsNullable { get; }
        public bool IsWritable { get; }

        public PropertyInfo(string typeName, string name, bool isNullable, bool isWritable)
        {
            TypeName = typeName;
            Name = name;
            IsNullable = isNullable;
            IsWritable = isWritable;
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