using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ViewModelAnalyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class IViewModelConstructorAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "VM001";
        private static readonly LocalizableString Title = "缺少 IViewModel<T> 必需的构造函数";
        private static readonly LocalizableString MessageFormat = "实现 IViewModel<T> 的类 '{0}' 必须包含一个参数类型为 T 的构造函数";
        private static readonly LocalizableString Description = "实现 IViewModel<T> 接口的类必须提供参数类型为 T 的构造函数，否则将导致编译错误。";
        private const string Category = "Usage";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId, Title, MessageFormat, Category,
            DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
        }

        private void AnalyzeNamedType(SymbolAnalysisContext context)
        {
            var namedType = (INamedTypeSymbol)context.Symbol;
            if (namedType.TypeKind != TypeKind.Class) return;

            // 🔍 获取 IViewModel<T> 接口符号（请替换为你的实际命名空间）
            // 注意：泛型接口的 MetadataName 需要带 `1 后缀
            var iViewModelInterface = context.Compilation.GetTypeByMetadataName("FMO.Models.IViewModel`1");
            if (iViewModelInterface == null) return;

            // 检查当前类是否实现了该接口
            var implementedInterface = namedType.AllInterfaces
                .FirstOrDefault(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, iViewModelInterface));

            if (implementedInterface == null) return;

            // 获取泛型参数 T 的实际类型
            var typeArgumentT = implementedInterface.TypeArguments[0];

            // 🔍 检查是否存在匹配的构造函数
            bool hasRequiredCtor = false;
            foreach (var ctor in namedType.Constructors)
            {
                if (ctor.IsStatic) continue;
                if (ctor.Parameters.Length == 1 &&
                    SymbolEqualityComparer.Default.Equals(ctor.Parameters[0].Type, typeArgumentT))
                {
                    hasRequiredCtor = true;
                    break;
                }
            }

            if (!hasRequiredCtor)
            {
                var syntaxRef = namedType.DeclaringSyntaxReferences.FirstOrDefault();
                if (syntaxRef == null) return;

                var classDecl = syntaxRef.GetSyntax(context.CancellationToken) as ClassDeclarationSyntax;
                if (classDecl == null) return;

                // 在类名处报错
                var diagnostic = Diagnostic.Create(Rule, classDecl.Identifier.GetLocation(), namedType.Name);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}