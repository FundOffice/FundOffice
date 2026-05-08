using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;


namespace MissionRegisterGenerator;


[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class MissionViewModelInitAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MVM001";
    private const string Category = "Initialization";

    private static readonly LocalizableString Title = "构造函数缺少初始化标记";
    private static readonly LocalizableString MessageFormat = "'{0}' 的构造函数中必须包含 `_initialized = true;`";
    // ✅ 修复警告：以标点结尾，无首尾空格
    private static readonly LocalizableString Description = "继承自 MissionViewModel<T> 的类必须在构造函数中显式设置 _initialized = true，以确保初始化流程完整.";

    public static readonly DiagnosticDescriptor Rule = new(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description:Description, customTags: WellKnownDiagnosticTags.Telemetry);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        // ✅ 修复警告：显式配置生成代码分析策略
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // ✅ 改用 SyntaxNodeAction，避免 Compilation.GetSemanticModel() 警告
        context.RegisterSyntaxNodeAction(AnalyzeConstructor, SyntaxKind.ConstructorDeclaration);
    }

    private void AnalyzeConstructor(SyntaxNodeAnalysisContext ctx)
    {
        var ctor = (ConstructorDeclarationSyntax)ctx.Node;
        var classDecl = ctor.Parent as ClassDeclarationSyntax;
        if (classDecl?.BaseList is null) return;

        // 🔍 检查是否继承 MissionViewModel<>
        bool inheritsMissionViewModel = false;
        foreach (var baseType in classDecl.BaseList.Types)
        {
            var typeInfo = ctx.SemanticModel.GetTypeInfo(baseType.Type, ctx.CancellationToken);
            var symbol = typeInfo.Type?.OriginalDefinition;
            if (symbol?.Name == "MissionViewModel")
            {
                inheritsMissionViewModel = true;
                break;
            }
        }
        if (!inheritsMissionViewModel) return;

        // 🔍 检查构造函数体内是否有 _initialized = true;
        bool hasInit = false;
        if (ctor.Body is { Statements.Count: > 0 })
        {
            foreach (var stmt in ctor.Body.Statements)
            {
                if (stmt is ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assign } &&
                    assign.Left is IdentifierNameSyntax { Identifier.ValueText: "_initialized" } &&
                    assign.Right is LiteralExpressionSyntax { Token.ValueText: "true" })
                {
                    // ✅ 使用 ctx.SemanticModel 是官方推荐做法，不会触发警告
                    var leftSymbol = ctx.SemanticModel.GetSymbolInfo(assign.Left, ctx.CancellationToken).Symbol;
                    if (leftSymbol is IFieldSymbol)
                    {
                        hasInit = true;
                        break;
                    }
                }
            }
        }

        if (!hasInit)
        {
            ctx.ReportDiagnostic(Diagnostic.Create(
                Rule, ctor.GetLocation(), classDecl.Identifier.ValueText));
        }
    }
}



