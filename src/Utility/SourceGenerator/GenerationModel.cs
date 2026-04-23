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
    // 注意：若特性定义在其他命名空间，请修改为完整限定名，如 "YourNs.AutoViewModelAttribute"
    private const string AttributeMetadataName = "FMO.Models.AutoViewModelAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1. 语法提供器：筛选带有目标特性的 ClassDeclarationSyntax
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

        // 2. 注册源码输出
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

        // 🟢 收集目标类及其所有父类中已存在的公开属性名（用于排除）
        var existingPropertyNames = new HashSet<string>(StringComparer.Ordinal);
        var currentType = targetClass;
        while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
        {
            foreach (var member in currentType.GetMembers())
            {
                if (member is IPropertySymbol prop && prop.DeclaredAccessibility == Accessibility.Public)
                    existingPropertyNames.Add(prop.Name);
            }
            currentType = currentType.BaseType;
        }

        // 🔵 收集 T 及其基类中的公开实例属性
        var propertiesToGenerate = new List<PropertyInfo>();
        currentType = sourceType;
        while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
        {
            foreach (var member in currentType.GetMembers())
            {
                if (member is IPropertySymbol prop && prop.DeclaredAccessibility == Accessibility.Public && !prop.IsStatic)
                {
                    if (!existingPropertyNames.Contains(prop.Name))
                    {
                        // 规则：如果是类(class)或 string，则标记为 nullable
                        bool isNullable = prop.Type.TypeKind == TypeKind.Class ||
                                          prop.Type.SpecialType == SpecialType.System_String ||
                                          prop.Type.IsReferenceType;

                        propertiesToGenerate.Add(new PropertyInfo(
                            prop.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                            prop.Name,
                            isNullable));
                    }
                }
            }
            currentType = currentType.BaseType;
        }

        if (propertiesToGenerate.Count == 0) return null;

        // 🔴 检查类型体系是否已实现 INotifyPropertyChanged
        bool needsINPC = !targetClass.AllInterfaces.Any(i => i.Name == "INotifyPropertyChanged");

        return new GenerationModel(className, ns, propertiesToGenerate, needsINPC);
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

        // 若未实现 INPC，则自动添加接口声明
        sb.AppendLine($"    public partial class {model.ClassName}" + (model.NeedsINPC ? " : INotifyPropertyChanged" : ""));
        sb.AppendLine("    {");

        if (model.NeedsINPC)
        {
            sb.AppendLine("        public event PropertyChangedEventHandler? PropertyChanged;");
            sb.AppendLine("        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>");
            sb.AppendLine("            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));");
            sb.AppendLine();
        }

        foreach (var prop in model.Properties)
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

    // 普通类，替代 record PropertyInfo
    private class PropertyInfo
    {
        // 只读自动属性（无字段）
        public string TypeName { get; }
        public string Name { get; }
        public bool IsNullable { get; }

        // 构造函数
        public PropertyInfo(string typeName, string name, bool isNullable)
        {
            TypeName = typeName;
            Name = name;
            IsNullable = isNullable;
        }
    }

    // 普通类，替代主构造函数 GenerationModel
    private class GenerationModel
    {
        // 只读自动属性（无字段）
        public string ClassName { get; }
        public string Namespace { get; }
        public List<PropertyInfo> Properties { get; }
        public bool NeedsINPC { get; }

        // 构造函数
        public GenerationModel(string className, string @namespace, List<PropertyInfo> properties, bool needsINPC)
        {
            ClassName = className;
            Namespace = @namespace;
            Properties = properties;
            NeedsINPC = needsINPC;
        }
    }
}