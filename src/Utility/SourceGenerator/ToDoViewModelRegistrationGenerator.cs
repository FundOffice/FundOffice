using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SourceGenerator;

[Generator]
public sealed class TodoViewModelRegistrationGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1. 筛选目标 ViewModel
        var viewModelTypes = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsTargetViewModel(node),
                transform: static (ctx, _) => GetViewModelSymbol(ctx))
            .Where(static m => m is not null);

        // 2. 把所有结果收集成一个集合 ✅ 关键修复
        var viewModelCollection = viewModelTypes.Collect();

        // 3. 注册输出（只执行一次）✅ 关键修复
        context.RegisterSourceOutput(viewModelCollection, (spc, vm) => Execute(spc, vm.OfType<ViewModelInfo>().ToArray()));
    }

    /// <summary>
    /// 语法筛选：匹配目标ViewModel
    /// </summary>
    private static bool IsTargetViewModel(SyntaxNode node)
    {
        return node is ClassDeclarationSyntax classDecl
               && classDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword))
               && classDecl.BaseList?.Types.Any(
                   b => b.Type.ToString().Contains("TodoViewModel")) == true;
    }

    /// <summary
    /// 获取ViewModel符号和特性信息
    /// </summary>
    private static ViewModelInfo? GetViewModelSymbol(GeneratorSyntaxContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        var symbol = context.SemanticModel.GetDeclaredSymbol(classDecl) as INamedTypeSymbol;
        if (symbol == null) return null;

        // 获取 AutoViewModel 特性
        var attr = symbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name == "AutoViewModelAttribute");
        if (attr == null || attr.ConstructorArguments.Length == 0) return null;

        // 获取特性中绑定的Model类型
        var modelType = attr.ConstructorArguments[0].Value as INamedTypeSymbol;
        if (modelType == null) return null;

        return new ViewModelInfo(
            ViewModelFullName: symbol.ToDisplayString(),
            ModelFullName: modelType.ToDisplayString()
        );
    }

    /// <summary>
    /// 生成最终注册代码 ✅ 修复：参数变成数组
    /// </summary>
    private void Execute(SourceProductionContext context, ViewModelInfo[] viewModels)
    {
        // 过滤空值
        var validViewModels = viewModels.Where(vm => vm != null).ToList()!;
        if (validViewModels.Count == 0) return;

        // 生成一次代码
        var code = GenerateRegisterCode(validViewModels);

        // 唯一文件名，只添加一次 ✅ 修复
        context.AddSource(
            "TodoViewModelFactory.AutoRegister.g.cs",
            SourceText.From(code, Encoding.UTF8)
        );
    }

    /// <summary>
    /// 生成工厂类的注册方法
    /// </summary>
    private string GenerateRegisterCode(List<ViewModelInfo> viewModels)
    {
        var registerLines = new StringBuilder();
        foreach (var vm in viewModels)
        {
            registerLines.AppendLine(
                $"            Register<{vm.ModelFullName}>(() => new {vm.ViewModelFullName}());");
            registerLines.AppendLine(
                $"            Register<{vm.ModelFullName}>((x) => new {vm.ViewModelFullName}(x as {vm.ModelFullName}));");
        }

        return $@"// 自动生成代码，请勿手动修改
namespace FMO.Todo;

public static partial class TodoViewModelFactory
{{
    /// <summary>
    /// 自动注册所有ViewModel（源代码生成器生成）
    /// </summary>
    public static void RegisterPredefined()
    {{
{registerLines.ToString().TrimEnd()}
    }}
}}";
    }

    /// <summary>
    /// ViewModel信息载体
    /// </summary>
    private class ViewModelInfo(string ViewModelFullName, string ModelFullName)
    {
        public string ViewModelFullName { get; } = ViewModelFullName;
        public string ModelFullName { get; } = ModelFullName;
    }
}