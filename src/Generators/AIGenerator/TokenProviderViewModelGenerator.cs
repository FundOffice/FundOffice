using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace SG;


[Generator]
public class TokenProviderViewModelGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var viewModelInfos = context.SyntaxProvider.CreateSyntaxProvider(
            predicate: static (node, _) => node is ClassDeclarationSyntax { Identifier.ValueText: { } name } && name.EndsWith("ViewModel"),
            transform: static (ctx, _) => GetViewModelInfo(ctx))
            .Where(static info => info is not null)
            .Collect();

        var targetNamespace = context.SyntaxProvider.CreateSyntaxProvider(
            predicate: static (node, _) => node is ClassDeclarationSyntax { Identifier.ValueText: "TokenProviderViewModel" },
            transform: static (ctx, _) =>
            {
                var symbol = ctx.SemanticModel.GetDeclaredSymbol((ClassDeclarationSyntax)ctx.Node) as INamedTypeSymbol;
                return symbol?.ContainingNamespace.IsGlobalNamespace == false
                    ? symbol.ContainingNamespace.ToDisplayString()
                    : null;
            })
            .Where(static ns => ns is not null)
            .Collect();

        var combined = viewModelInfos.Combine(targetNamespace);
        context.RegisterSourceOutput(combined, static (spc, source) => Execute(source.Left, source.Right, spc));
    }

    private static ViewModelInfo? GetViewModelInfo(GeneratorSyntaxContext context)
    {
        try
        {
            var classDeclaration = (ClassDeclarationSyntax)context.Node;
            if (context.SemanticModel.GetDeclaredSymbol(classDeclaration) is not INamedTypeSymbol classSymbol)
                return null;

            foreach (var iface in classSymbol.AllInterfaces)
            {
                if (iface.Name is "IViewModel" && iface.TypeArguments.Length == 2)
                {
                    var modelType = iface.TypeArguments[0];
                    var vmType = iface.TypeArguments[1];

                    if (TryGetBaseType(modelType, "TokenProvider", out var tokenProviderSymbol))
                    {
                        var fqnFormat = SymbolDisplayFormat.FullyQualifiedFormat;
                        string companyName = ExtractCompanyName(modelType);

                        return new ViewModelInfo(
                            ModelType: modelType.ToDisplayString(fqnFormat),
                            ViewModelType: vmType.ToDisplayString(fqnFormat),
                            TokenProviderType: tokenProviderSymbol.ToDisplayString(fqnFormat),
                            CompanyName: companyName);
                    }
                }
            }
        }
        catch { /* 忽略单个类的解析错误，不影响其他类 */ }

        return null;
    }

    private static string ExtractCompanyName(ITypeSymbol modelType)
    {
        try
        {
            var currentType = modelType as INamedTypeSymbol;

            // 遍历继承链，寻找 Company 属性
            while (currentType != null)
            {
                var companyProperty = currentType.GetMembers("Company").OfType<IPropertySymbol>().FirstOrDefault();
                if (companyProperty != null)
                {
                    // 如果属性来自外部 DLL，DeclaringSyntaxReferences 为空，不会进入此循环，安全跳过
                    foreach (var syntaxRef in companyProperty.DeclaringSyntaxReferences)
                    {
                        if (syntaxRef.GetSyntax() is PropertyDeclarationSyntax propertySyntax)
                        {
                            // 1. 匹配表达式体: public override string Company => "XiaoMi";
                            if (propertySyntax.ExpressionBody?.Expression is LiteralExpressionSyntax exprLiteral &&
                                exprLiteral.Token.Value is string exprVal)
                            {
                                return exprVal;
                            }

                            // 2. 匹配属性初始化器: public string Company { get; } = "XiaoMi";
                            if (propertySyntax.Initializer?.Value is LiteralExpressionSyntax initLiteral &&
                                initLiteral.Token.Value is string initVal)
                            {
                                return initVal;
                            }

                            // 3. 匹配 Getter 方法体: get { return "XiaoMi"; }
                            var accessorList = propertySyntax.AccessorList;
                            if (accessorList != null)
                            {
                                foreach (var accessor in accessorList.Accessors)
                                {
                                    // 使用原生的 Kind() 避免 IsKind 扩展方法缺失导致的崩溃
                                    if (accessor.Kind() == SyntaxKind.GetAccessorDeclaration && accessor.Body != null)
                                    {
                                        foreach (var statement in accessor.Body.Statements)
                                        {
                                            if (statement is ReturnStatementSyntax returnStmt &&
                                                returnStmt.Expression is LiteralExpressionSyntax returnLiteral &&
                                                returnLiteral.Token.Value is string returnVal)
                                            {
                                                return returnVal;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                currentType = currentType.BaseType;
            }
        }
        catch
        {
            // 发生任何未知异常，直接吞掉，走下方的降级逻辑
        }

        // Fallback (降级策略): 截取类名
        string fallbackName = modelType.Name;
        if (fallbackName.EndsWith("TokenProvider"))
            fallbackName = fallbackName.Substring(0, fallbackName.Length - "TokenProvider".Length);

        return fallbackName;
    }

    private static bool TryGetBaseType(ITypeSymbol? type, string baseTypeName, out INamedTypeSymbol baseTypeSymbol)
    {
        var current = type?.BaseType;
        while (current is not null)
        {
            if (current.Name == baseTypeName)
            {
                baseTypeSymbol = current;
                return true;
            }
            current = current.BaseType;
        }
        baseTypeSymbol = null!;
        return false;
    }

    private static void Execute(ImmutableArray<ViewModelInfo?> viewModels, ImmutableArray<string?> namespaces, SourceProductionContext context)
    {
        try
        {
            if (viewModels.IsDefaultOrEmpty) return;

            var validViewModels = viewModels.OfType<ViewModelInfo>().ToArray();
            if (validViewModels.Length == 0) return;

            var tokenProviderFqn = validViewModels[0].TokenProviderType;
            var ns = namespaces.FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));
            var hasNamespace = !string.IsNullOrWhiteSpace(ns);

            var sb = new StringBuilder();

            sb.AppendLine("""
                #nullable enable
                using System;
                """);

            if (hasNamespace)
            {
                sb.AppendLine($$"""
                    
                    namespace {{ns}}
                    {
                """);
            }

            sb.AppendLine($$"""
                    public partial class TokenProviderViewModel
                    {
                        public static string[] Providers { get; } = [
                """);

            foreach (var vm in validViewModels)
            {
                sb.AppendLine($"            \"{vm.CompanyName}\",");
            }

            sb.AppendLine($$"""
                        ];

                        public static TokenProviderViewModel Create({{tokenProviderFqn}} model)
                        {
                            return model switch
                            {
                """);

            foreach (var vm in validViewModels)
            {
                sb.AppendLine($"            {vm.ModelType} m => new {vm.ViewModelType}(m),");
            }

            sb.AppendLine($$"""
                            _ => throw new ArgumentException("Unsupported TokenProvider type.", nameof(model))
                        };
                    }

                        public static TokenProviderViewModel Create(string company)
                        {
                            return company switch
                            {
                """);

            foreach (var vm in validViewModels)
            {
                sb.AppendLine($"            \"{vm.CompanyName}\" => new {vm.ViewModelType}(),");
            }

            sb.AppendLine($$"""
                            _ => throw new ArgumentException($"Unknown provider company: {company}", nameof(company))
                        };
                    }
                }
                """);

            if (hasNamespace)
            {
                sb.AppendLine("}");
            }

            context.AddSource("TokenProviderViewModel.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        }
        catch (Exception ex)
        {
            // 💡 崩溃报告器：如果生成器出错，生成一个包含错误信息的文件，方便你在 IDE 中查看
            var errorSb = new StringBuilder();
            errorSb.AppendLine("/* ==========================================");
            errorSb.AppendLine("   SOURCE GENERATOR CRASHED!");
            errorSb.AppendLine($"   Message: {ex.Message}");
            errorSb.AppendLine($"   StackTrace: ");
            errorSb.AppendLine(ex.StackTrace);
            errorSb.AppendLine("   ========================================== */");
            context.AddSource("GeneratorError_Debug.g.cs", SourceText.From(errorSb.ToString(), Encoding.UTF8));
        }
    }
}

internal sealed record ViewModelInfo(string ModelType, string ViewModelType, string TokenProviderType, string CompanyName);