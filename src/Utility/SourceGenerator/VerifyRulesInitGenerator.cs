using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace FMO.Generators
{
    [Generator]
    public class VerifyRulesGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // 注册语法接收器以收集需要的信息
            var provider = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (node, _) => IsVerifyRulesClass(node),
                    transform: static (ctx, _) => GetVerifyRulesInfo(ctx)
                )
                .Where(static m => m is not null);

            // 合并分析结果并生成代码
            context.RegisterSourceOutput(provider, static (spc, source) => GenerateCode(spc, source!));
        }

        // 判断是否是VerifyRules类
        private static bool IsVerifyRulesClass(SyntaxNode node)
        {
            return node is ClassDeclarationSyntax classDecl &&
                   classDecl.Identifier.Text == "VerifyRules" &&
                   classDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)) &&
                   classDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)) &&
                   classDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword));
        }

        // 提取VerifyRules类中的相关信息
        private static VerifyRulesInfo? GetVerifyRulesInfo(GeneratorSyntaxContext context)
        {
            var classDecl = (ClassDeclarationSyntax)context.Node;
            var semanticModel = context.SemanticModel;

            var properties = new List<RulePropertyInfo>();

            foreach (var member in classDecl.Members)
            {
                // 查找公共静态属性
                if (member is PropertyDeclarationSyntax propDecl &&
                    propDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)) &&
                    propDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)))
                {
                    var typeInfo = semanticModel.GetTypeInfo(propDecl.Type).Type as INamedTypeSymbol;
                    if (typeInfo == null) continue;

                    // 检查是否是VerifyRule<>或VerifyRule<,>等泛型类型
                    if (IsVerifyRuleType(typeInfo, out var typeArguments))
                    {
                        properties.Add(new RulePropertyInfo(
                            propName: propDecl.Identifier.Text,
                            typeArguments: typeArguments
                        ));
                    }
                }
            }

            return new VerifyRulesInfo(properties);
        }

        // 检查是否是VerifyRule泛型类型
        private static bool IsVerifyRuleType(INamedTypeSymbol typeSymbol, out List<ITypeSymbol> typeArguments)
        {
            typeArguments = new List<ITypeSymbol>();

            // 检查原始类型是否是VerifyRule
            if (typeSymbol.OriginalDefinition?.Name == "VerifyRule" &&
                typeSymbol.ContainingNamespace?.ToString() == "FMO.Utilities")
            {
                typeArguments.AddRange(typeSymbol.TypeArguments);
                return true;
            }

            // 检查基类型链
            if (typeSymbol.BaseType != null)
            {
                return IsVerifyRuleType(typeSymbol.BaseType, out typeArguments);
            }

            return false;
        }

        // 生成代码
        private static void GenerateCode(SourceProductionContext context, VerifyRulesInfo info)
        {
            var code = new StringBuilder();
            code.AppendLine("using System.Collections.Generic;");
            code.AppendLine("using FMO.Models;"); // 根据实际命名空间调整
            code.AppendLine("using FMO.Utilities;");
            code.AppendLine();
            code.AppendLine("namespace FMO.Utilities;");
            code.AppendLine();
            code.AppendLine("public static partial class VerifyRules");
            code.AppendLine("{");

            // 生成InitAll方法
            code.AppendLine("    public static void InitAll()");
            code.AppendLine("    {");
            foreach (var prop in info.RuleProperties)
            {
                code.AppendLine($"        VerifyRules.{prop.PropName}.Init();");
            }
            code.AppendLine("    }");
            code.AppendLine();

            // 收集所有唯一的实体类型并生成OnEntityArrival方法
            var entityTypes = new Dictionary<string, List<string>>();
            foreach (var prop in info.RuleProperties)
            {
                foreach (var typeArg in prop.TypeArguments)
                {
                    var typeName = GetFullyQualifiedTypeName(typeArg);
                    if (!entityTypes.ContainsKey(typeName))
                    {
                        entityTypes[typeName] = new List<string>();
                    }
                    entityTypes[typeName].Add(prop.PropName);
                }
            }

            // 生成每个实体类型的处理方法
            foreach (KeyValuePair<string, List<string>> typePair in entityTypes)
            {
                string typeName = typePair.Key;
                List<string> propNames = typePair.Value;

                code.AppendLine($"    public static void OnEntityArrival(IEnumerable<{typeName}> obj)");
                code.AppendLine("    {");
                foreach (var propName in propNames)
                {
                    code.AppendLine($"        {propName}.OnEntityArrival(obj);");
                }
                code.AppendLine("    }");
                code.AppendLine();
            }

            code.AppendLine("}");

            context.AddSource("VerifyRules.g.cs", SourceText.From(code.ToString(), Encoding.UTF8));
        }

        // 获取类型的完全限定名
        private static string GetFullyQualifiedTypeName(ITypeSymbol typeSymbol)
        {
            if (typeSymbol is INamedTypeSymbol namedType && namedType.IsGenericType)
            {
                var typeArgs = string.Join(", ", namedType.TypeArguments.Select(GetFullyQualifiedTypeName));
                return $"{namedType.ContainingNamespace}.{namedType.Name}<{typeArgs}>";
            }
            return $"{typeSymbol.ContainingNamespace}.{typeSymbol.Name}";
        }

        // 存储VerifyRules类信息的模型
        private class VerifyRulesInfo
        {
            public List<RulePropertyInfo> RuleProperties { get; }

            public VerifyRulesInfo(List<RulePropertyInfo> ruleProperties)
            {
                RuleProperties = ruleProperties;
            }
        }

        // 存储规则属性信息的模型
        private class RulePropertyInfo
        {
            public string PropName { get; }
            public List<ITypeSymbol> TypeArguments { get; }

            public RulePropertyInfo(string propName, List<ITypeSymbol> typeArguments)
            {
                PropName = propName;
                TypeArguments = typeArguments;
            }
        }
    }
}