using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace ViewModelAnalyzer
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(IViewModelConstructorCodeFixProvider)), Shared]
    public class IViewModelConstructorCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds =>
            ImmutableArray.Create(IViewModelConstructorAnalyzer.DiagnosticId);

        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            var classDecl = root.FindToken(diagnosticSpan.Start)
                .Parent.AncestorsAndSelf().OfType<ClassDeclarationSyntax>().First();

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "添加 IViewModel<T> 构造函数",
                    createChangedDocument: c => AddConstructorAsync(context.Document, classDecl, c),
                    equivalenceKey: nameof(AddConstructorAsync)),
                diagnostic);
        }

        private async Task<Document> AddConstructorAsync(Document document, ClassDeclarationSyntax classDecl, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var classSymbol = semanticModel.GetDeclaredSymbol(classDecl, cancellationToken);
            if (classSymbol == null) return document;

            // 再次查找 IViewModel<T> 获取 T 的类型
            var iViewModelInterface = semanticModel.Compilation.GetTypeByMetadataName("FMO.Models.IViewModel`1");
            if (iViewModelInterface == null) return document;

            var implementedInterface = classSymbol.AllInterfaces
                .FirstOrDefault(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, iViewModelInterface));

            if (implementedInterface == null) return document;

            var typeT = implementedInterface.TypeArguments[0];
            var typeSyntax = SyntaxFactory.ParseTypeName(typeT.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));

            // 生成参数: T v
            var parameter = SyntaxFactory.Parameter(SyntaxFactory.Identifier("v")).WithType(typeSyntax);

            // 生成构造函数: public ClassName(T v) { }
            var constructor = SyntaxFactory.ConstructorDeclaration(classDecl.Identifier)
                .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                .WithParameterList(SyntaxFactory.ParameterList(SyntaxFactory.SingletonSeparatedList(parameter)))
                .WithBody(SyntaxFactory.Block())
                .WithAdditionalAnnotations(Formatter.Annotation); // 交由 IDE 自动格式化

            // 替换语法树
            var newClassDecl = classDecl.AddMembers(constructor);
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var newRoot = root.ReplaceNode(classDecl, newClassDecl);

            return document.WithSyntaxRoot(newRoot);
        }
    }
}