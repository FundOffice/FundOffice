using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FMO.SourceGenerator;

//[Generator]
//public class PartialUpdateGenerator : IIncrementalGenerator
//{
//    private const string AttributeTypeName = "UpdateBy";
//    private const string AttributeNamespace = "FMO.Models";
//    private const string FullAttributeName = $"{AttributeNamespace}.{AttributeTypeName}";

//    public void Initialize(IncrementalGeneratorInitializationContext context)
//    {

//        // 查找所有带有 UpdateByAttribute 特性的类
//        var classDeclarations = context.SyntaxProvider
//            .CreateSyntaxProvider(
//                predicate: static (s, _) => IsSyntaxTargetForGeneration(s),
//                transform: static (ctx, _) => GetSemanticTargetForGeneration(ctx)
//            )
//            .Where(static m => m is not null);

//        // 合并为一个单一的生成器
//        var compilationAndClasses = context.CompilationProvider.Combine(classDeclarations.Collect());

//        // 注册生成器
//        context.RegisterSourceOutput(compilationAndClasses,
//            static (spc, source) => Execute(source.Left, source.Right, spc));
//    }

//    // 检查语法节点是否是我们的目标（带有UpdateByAttribute特性的类）
//    private static bool IsSyntaxTargetForGeneration(SyntaxNode node)
//    {
//        if (node is not ClassDeclarationSyntax classDeclaration)
//            return false;

//        if (classDeclaration.AttributeLists.Count == 0)
//            return false;

//        // 检查特性名称（支持简写，如[UpdateBy]或[UpdateByAttribute]）
//        foreach (var attributeList in classDeclaration.AttributeLists)
//        {
//            foreach (var attribute in attributeList.Attributes)
//            {
//                var attributeName = attribute.Name.ToString();
//                // 允许全名、简写、带命名空间的写法
//                if (attributeName.Equals(AttributeTypeName, StringComparison.OrdinalIgnoreCase) ||
//                    attributeName.Equals(FullAttributeName, StringComparison.OrdinalIgnoreCase) ||
//                    attributeName.EndsWith($".{AttributeTypeName}", StringComparison.OrdinalIgnoreCase))
//                {
//                    return true;
//                }
//            }
//        }

//        return false;
//    }

//    // 获取语义目标（带有UpdateByAttribute特性的类及相关信息）
//    private static (ClassDeclarationSyntax Class, INamedTypeSymbol SourceType, string DebugInfo)? GetSemanticTargetForGeneration(GeneratorSyntaxContext context)
//    {
//        var debugInfo = new StringBuilder();
//        var classDeclaration = (ClassDeclarationSyntax)context.Node;
//        debugInfo.AppendLine($"检查类: {classDeclaration.Identifier.Text}");
//        debugInfo.AppendLine($"类位置: {classDeclaration.GetLocation().GetLineSpan()}");

//        // 获取类的符号信息
//        var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration);
//        if (classSymbol == null)
//        {
//            debugInfo.AppendLine("无法获取类的符号信息");
//            return (classDeclaration, null, debugInfo.ToString());
//        }

//        debugInfo.AppendLine($"找到类符号: {classSymbol.ToDisplayString()}");
//        debugInfo.AppendLine($"类命名空间: {classSymbol.ContainingNamespace.ToDisplayString()}");

//        // 检查类是否有任何特性
//        var attributes = classSymbol.GetAttributes().ToList();
//        debugInfo.AppendLine($"类上的特性数量: {attributes.Count}");

//        foreach (var attr in attributes)
//        {
//            debugInfo.AppendLine($"检查特性: {attr.AttributeClass?.ToDisplayString()}");
//        }

//        // 检查类是否有 UpdateByAttribute 特性（语义层面，精确检查）
//        foreach (var attributeData in attributes)
//        {
//            // 检查特性类型是否匹配
//            var attrTypeName = attributeData.AttributeClass?.ToDisplayString();
//            debugInfo.AppendLine($"检查特性类型: {attrTypeName} (目标: {FullAttributeName})");

//            if (attrTypeName == FullAttributeName || attrTypeName == FullAttributeName + "Attribute")
//            {
//                debugInfo.AppendLine("找到匹配的UpdateByAttribute特性");

//                // 检查特性是否有构造函数参数
//                if (attributeData.ConstructorArguments.Length > 0)
//                {
//                    debugInfo.AppendLine($"特性构造函数参数数量: {attributeData.ConstructorArguments.Length}");

//                    // 获取源类型参数
//                    var sourceTypeArg = attributeData.ConstructorArguments[0];
//                    debugInfo.AppendLine($"源类型参数类型: {sourceTypeArg.Type?.ToDisplayString()}");
//                    debugInfo.AppendLine($"源类型参数值类型: {sourceTypeArg.Value?.GetType().Name}");

//                    // 确保参数是类型
//                    if (sourceTypeArg.Value is INamedTypeSymbol sourceTypeSymbol)
//                    {
//                        debugInfo.AppendLine($"成功解析源类型: {sourceTypeSymbol.ToDisplayString()}");
//                        return (classDeclaration, sourceTypeSymbol, debugInfo.ToString());
//                    }
//                    else
//                    {
//                        debugInfo.AppendLine("源类型参数不是INamedTypeSymbol");
//                    }
//                }
//                else
//                {
//                    debugInfo.AppendLine("UpdateByAttribute特性没有构造函数参数");
//                }
//            }
//            else debugInfo.AppendLine($"{attrTypeName} 不匹配 {FullAttributeName}");
//        }

//        debugInfo.AppendLine("未找到匹配的UpdateByAttribute特性");
//        return (classDeclaration, null, debugInfo.ToString());
//    }

//    // 执行代码生成
//    private static void Execute(Compilation compilation, IEnumerable<(ClassDeclarationSyntax Class, INamedTypeSymbol SourceType, string DebugInfo)?> classes, SourceProductionContext context)
//    {
//        var debugLog = new StringBuilder();
//        // 将调试信息包装在有效的C#注释和命名空间中
//        debugLog.AppendLine("// 自动生成的调试日志 - 不要手动修改");
//        debugLog.AppendLine("namespace FMO.SourceGenerator.DebugLogs;");
//        debugLog.AppendLine("public static class PartialUpdateGeneratorDebugLog");
//        debugLog.AppendLine("{");
//        debugLog.AppendLine("    // === PartialUpdateGenerator 调试日志 ===");
//        debugLog.AppendLine($"    // 生成时间: {DateTime.Now:o}");
//        debugLog.AppendLine($"    // 找到的候选类数量: {classes.Count()}");

//        // 记录所有类的调试信息
//        foreach (var item in classes)
//        {
//            if (item.HasValue)
//            {
//                debugLog.AppendLine("    // ");
//                debugLog.AppendLine("    // --- 类调试信息 ---");
//                foreach (var line in item.Value.DebugInfo.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
//                {
//                    debugLog.AppendLine($"    // {line.Replace("//", "##")}");
//                }
//            }
//            else
//            {
//                debugLog.AppendLine("    // ");
//                debugLog.AppendLine("    // --- 空的候选类 ---");
//            }
//        }

//        // 筛选出有效项
//        var validItems = classes.Where(c => c.HasValue && c.Value.SourceType != null).ToList();
//        debugLog.AppendLine($"    // ");
//        debugLog.AppendLine($"    // 有效类数量: {validItems.Count}");

//        if (!validItems.Any())
//        {
//            context.ReportDiagnostic(Diagnostic.Create(
//                new DiagnosticDescriptor(
//                    id: "UPDGEN001",
//                    title: "No classes found",
//                    messageFormat: "No classes with valid UpdateByAttribute found",
//                    category: "UpdateGenerator",
//                    DiagnosticSeverity.Info,
//                    isEnabledByDefault: true
//                ),
//                Location.None
//            ));
//        }
//        else
//        {
//            // 处理每个符合条件的类
//            foreach (var item in validItems)
//            {
//                var (classDeclaration, sourceType, _) = item.Value;

//                // 获取类的语义模型
//                var semanticModel = compilation.GetSemanticModel(classDeclaration.SyntaxTree);
//                if (semanticModel.GetDeclaredSymbol(classDeclaration) is not ITypeSymbol targetType)
//                {
//                    debugLog.AppendLine($"    // 无法获取目标类型符号: {classDeclaration.Identifier.Text}");
//                    continue;
//                }

//                debugLog.AppendLine($"    // ");
//                debugLog.AppendLine($"    // 处理类: {targetType.Name}, 源类型: {sourceType.Name}");

//                // 添加诊断信息，确认找到目标类
//                context.ReportDiagnostic(Diagnostic.Create(
//                    new DiagnosticDescriptor(
//                        id: "UPDGEN002",
//                        title: "Class found",
//                        messageFormat: "Generating update method for {0} from {1}",
//                        category: "UpdateGenerator",
//                        DiagnosticSeverity.Info,
//                        isEnabledByDefault: true
//                    ),
//                    classDeclaration.Identifier.GetLocation(),
//                    targetType.Name,
//                    sourceType.Name
//                ));

//                // 生成扩展方法代码
//                var sourceCode = GenerateExtensionClass(targetType, sourceType, debugLog);
//                if (string.IsNullOrEmpty(sourceCode))
//                {
//                    debugLog.AppendLine("    // 未生成代码：没有匹配的属性");
//                    continue;
//                }

//                // 添加生成的代码到上下文中
//                var targetClassName = targetType.Name;
//                var sourceClassName = sourceType.Name;
//                var namespaceName = targetType.ContainingNamespace.IsGlobalNamespace
//                    ? ""
//                    : targetType.ContainingNamespace.ToDisplayString();

//                // 生成安全的文件名
//                var safeNamespace = namespaceName.Replace(".", "_");
//                var fileName = $"UpdateByExtensions_{safeNamespace}_{targetClassName}_From_{sourceClassName}.g.cs";
//                context.AddSource(fileName, SourceText.From(sourceCode, Encoding.UTF8));
//                debugLog.AppendLine($"    // 生成代码文件: {fileName}");
//            }
//        }

//        debugLog.AppendLine("}");

//        // 生成调试文件
//        context.AddSource("PartialUpdateGenerator.debug.g.cs",
//            SourceText.From(debugLog.ToString(), Encoding.UTF8));
//    }

//    // 生成扩展方法类
//    private static string GenerateExtensionClass(ITypeSymbol targetType, INamedTypeSymbol sourceType, StringBuilder debugLog)
//    {
//        var targetClassName = targetType.Name;
//        var sourceClassName = sourceType.Name;
//        var namespaceName = targetType.ContainingNamespace.IsGlobalNamespace
//            ? ""
//            : targetType.ContainingNamespace.ToDisplayString();

//        debugLog.AppendLine($"    // 目标类: {targetClassName}, 命名空间: {namespaceName}");
//        debugLog.AppendLine($"    // 源类: {sourceClassName}");

//        // 获取目标类和源类型中同名同类型的公共可写属性
//        var targetProperties = targetType.GetMembers()
//            .OfType<IPropertySymbol>()
//            .Where(p => p.SetMethod != null && p.SetMethod.DeclaredAccessibility == Accessibility.Public)
//            .ToList();

//        debugLog.AppendLine($"    // 目标类公共可写属性数量: {targetProperties.Count}");
//        foreach (var prop in targetProperties)
//        {
//            debugLog.AppendLine($"    // 目标属性: {prop.Name}, 类型: {prop.Type.ToDisplayString()}");
//        }

//        var sourceProperties = sourceType.GetMembers()
//            .OfType<IPropertySymbol>()
//            .Where(p => p.GetMethod != null && p.GetMethod.DeclaredAccessibility == Accessibility.Public)
//            .ToDictionary(p => p.Name);

//        debugLog.AppendLine($"    // 源类公共可读属性数量: {sourceProperties.Count}");
//        foreach (var prop in sourceProperties)
//        {
//            debugLog.AppendLine($"    // 源属性: {prop.Key}, 类型: {prop.Value.Type.ToDisplayString()}");
//        }

//        // 找到同名同类型的属性
//        var matchingProperties = targetProperties
//            .Where(tp => sourceProperties.TryGetValue(tp.Name, out var sp) &&
//                       SymbolEqualityComparer.Default.Equals(tp.Type, sp.Type))
//            .ToList();

//        debugLog.AppendLine($"    // 匹配的属性数量: {matchingProperties.Count}");
//        foreach (var prop in matchingProperties)
//        {
//            debugLog.AppendLine($"    // 匹配属性: {prop.Name}, 类型: {prop.Type.ToDisplayString()}");
//        }

//        if (!matchingProperties.Any())
//            return string.Empty; // 没有匹配的属性，无需生成代码

//        // 生成属性更新逻辑代码块
//        var propertyUpdates = new StringBuilder();
//        foreach (var property in matchingProperties)
//        {
//            var propertyName = property.Name;
//            var propertyType = property.Type.ToDisplayString();

//            // 改进的默认值检查逻辑
//            propertyUpdates.AppendLine($@"
//        // 更新 {propertyName} 属性
//        if (EqualityComparer<{propertyType}>.Default.Equals(target.{propertyName}, default({propertyType})))
//        {{
//            target.{propertyName} = source.{propertyName};
//        }}");
//        }

//        // 构建代码
//        var code = new StringBuilder();

//        if (!string.IsNullOrEmpty(namespaceName))
//        {
//            code.AppendLine($"namespace {namespaceName};");
//            code.AppendLine();
//        }

//        code.AppendLine($@"/// <summary>
///// 为 {targetClassName} 提供从 {sourceClassName} 更新功能的扩展方法
///// </summary>
//public static class {targetClassName}UpdateBy{sourceClassName}Extensions
//{{
//    /// <summary>
//    /// 更新 {targetClassName} 对象的属性，仅覆盖目标对象中值为默认值的属性
//    /// 只更新与 {sourceClassName} 中同名同类型的属性
//    /// </summary>
//    /// <param name=""target"">要更新的目标对象</param>
//    /// <param name=""source"">提供更新值的源对象</param>
//    /// <returns>更新后的目标对象</returns>
//    public static {targetClassName} KeepNotDefault(this {targetClassName} target, {sourceClassName} source)
//    {{
//        if (target == null) throw new ArgumentNullException(nameof(target));
//        if (source == null) throw new ArgumentNullException(nameof(source));
//{propertyUpdates}
//        return target;
//    }}
//}}");

//        return code.ToString();
//    }
//}
