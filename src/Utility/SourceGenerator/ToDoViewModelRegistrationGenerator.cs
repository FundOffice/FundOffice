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
        // 筛选：类 + 分部类 + 有AutoViewModel特性 + 继承TodoViewModel
        var viewModelTypes = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsTargetViewModel(node),
                transform: static (ctx, _) => GetViewModelSymbol(ctx))
            .Where(static m => m is not null);

        // 生成代码
        context.RegisterSourceOutput(viewModelTypes, Execute);
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

    /// <summary>
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
    /// 生成最终注册代码
    /// </summary>
    private void Execute(SourceProductionContext context, ViewModelInfo? viewModel)
    {
        if (viewModel == null) return;

        // 收集所有符合条件的ViewModel
        var viewModels = new List<ViewModelInfo>();
        if (viewModel != null) viewModels.Add(viewModel);

        // 构建代码
        var code = GenerateRegisterCode(viewModels);

        // 添加到编译中
        context.AddSource("TodoViewModelFactory.AutoRegister.g.cs",
            SourceText.From(code, Encoding.UTF8));
    }

    /// <summary>
    /// 生成工厂类的注册方法
    /// </summary>
    private string GenerateRegisterCode(List<ViewModelInfo> viewModels)
    {
        var registerLines = new StringBuilder();
        foreach (var vm in viewModels)
        {
            // 生成：Register<HugeRedemptionTodo>(() => new HugeRedemptionTodoViewModel());
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