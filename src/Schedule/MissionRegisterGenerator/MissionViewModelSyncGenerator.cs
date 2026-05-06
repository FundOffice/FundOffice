using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;

namespace MissionViewModelSyncGenerator;

[Generator]
public class MissionViewModelSyncGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidateClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax { BaseList: not null },
                transform: static (ctx, _) => (ClassDeclarationSyntax)ctx.Node)
            .Combine(context.CompilationProvider)
            .Select(ResolveClassData)
            .Where(static data => data.Symbol is not null && data.MatchedProperties.Length > 0);

        context.RegisterSourceOutput(candidateClasses, EmitSource);
    }

    private static (INamedTypeSymbol? Symbol, ImmutableArray<(IPropertySymbol Vm, IPropertySymbol Mission)> MatchedProperties)
        ResolveClassData((ClassDeclarationSyntax ClassSyntax, Compilation Compilation) pair, CancellationToken ct)
    {
        var (classSyntax, compilation) = pair;
        var model = compilation.GetSemanticModel(classSyntax.SyntaxTree);
        var symbol = model.GetDeclaredSymbol(classSyntax) as INamedTypeSymbol;

        if (symbol is null ||
            symbol.DeclaredAccessibility != Accessibility.Public ||
            !classSyntax.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
        {
            return (null, ImmutableArray<(IPropertySymbol, IPropertySymbol)>.Empty);
        }

        var baseType = symbol.BaseType;
        if (baseType is null || baseType.Name != "MissionViewModel" || !baseType.IsGenericType)
        {
            return (null, ImmutableArray<(IPropertySymbol, IPropertySymbol)>.Empty);
        }

        var missionType = baseType.TypeArguments[0];
        if (missionType is null)
            return (null, ImmutableArray<(IPropertySymbol, IPropertySymbol)>.Empty);

        var matches = symbol.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(p => p.DeclaredAccessibility == Accessibility.Public &&
                        SymbolEqualityComparer.Default.Equals(p.ContainingType, symbol))
            .Select(vmProp =>
            {
                var missionProp = GetBaseTypesAndThis(missionType)
                     .SelectMany(t => t.GetMembers(vmProp.Name))
                    .OfType<IPropertySymbol>()
                    .FirstOrDefault(p => p.Name == vmProp.Name && !p.IsReadOnly);
                return (vmProp, missionProp);
            })
            .Where(t => t.missionProp is not null)
            .ToImmutableArray();

        return (symbol, matches);
    }

    private static IEnumerable<ITypeSymbol> GetBaseTypesAndThis(ITypeSymbol? type)
    {
        var current = type;
        while (current is not null && current.SpecialType != SpecialType.System_Object)
        {
            yield return current;
            current = current.BaseType;
        }
    }

    private static void EmitSource(SourceProductionContext spc,
        (INamedTypeSymbol? Symbol, ImmutableArray<(IPropertySymbol Vm, IPropertySymbol Mission)> MatchedProperties) data)
    {
        var (symbol, props) = data;
        if (symbol is null) return;

        var ns = symbol.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : $"namespace {symbol.ContainingNamespace.ToDisplayString()}\n{{";
        var closeNs = symbol.ContainingNamespace.IsGlobalNamespace ? string.Empty : "\n}";
        var className = symbol.Name;

        var switchBuilder = new StringBuilder();
        foreach (var (vmProp, missionProp) in props)
        {
            var vmType = vmProp.Type;
            var missionType = missionProp.Type;
            var propName = vmProp.Name;
            var missionPropName = missionProp.Name;

            // 🔍 判断是否需要空值保护：VM 可空 → Mission 非可空
            var isVmNullableRef = vmType.NullableAnnotation == NullableAnnotation.Annotated && !vmType.IsValueType;
            var isVmNullableVal = IsNullableValueType(vmType);
            var isMissionNonNullableRef = missionType.NullableAnnotation == NullableAnnotation.NotAnnotated && !missionType.IsValueType;
            var isMissionNonNullableVal = IsNonNullableValueType(missionType);

            var needsNullGuard = (isVmNullableRef && isMissionNonNullableRef) ||
                                 (isVmNullableVal && isMissionNonNullableVal);

            if (needsNullGuard)
            {
                // 🟡 VM 可空，Mission 非可空：null 时直接返回，不赋值
                var condition = isVmNullableVal ? $"{propName}.HasValue" : $"{propName} is not null";
                var value = isVmNullableVal ? $"{propName}.Value" : propName;

                switchBuilder.AppendLine($"            case nameof({propName}):");
                switchBuilder.AppendLine($"                if ({condition})");
                switchBuilder.AppendLine($"                {{");
                switchBuilder.AppendLine($"                    Mission.{missionPropName} = {value};");
                switchBuilder.AppendLine($"                    MissionSchedule.SaveChanges(Mission);");
                switchBuilder.AppendLine($"                }}");
                switchBuilder.AppendLine($"                break;");
            }
            else
            {
                // 🟢 类型兼容或同为可空：直接赋值
                switchBuilder.AppendLine($"            case nameof({propName}):");
                switchBuilder.AppendLine($"                Mission.{missionPropName} = {propName};");
                switchBuilder.AppendLine($"                MissionSchedule.SaveChanges(Mission);");
                switchBuilder.AppendLine($"                break;");
            }
        }

        var source = $$"""
            // <auto-generated/>
            #nullable enable
            using System;
            using System.ComponentModel;
            using FMO.Schedule;

            {{ns}}
                public partial class {{className}}
                {
                    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
                    {
                        base.OnPropertyChanged(e);

                        if (e.PropertyName is null) return;

                        switch (e.PropertyName)
                        {
            {{switchBuilder}}
                        }
                    }
                }
            {{closeNs}}
            """;

        spc.AddSource($"{symbol.ContainingNamespace.Name}_{className}_MissionSync.g.cs", SourceText.From(source, Encoding.UTF8));
    }

    private static bool IsNullableValueType(ITypeSymbol type)
    {
        return type is INamedTypeSymbol namedType &&
               namedType.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T;
    }

    private static bool IsNonNullableValueType(ITypeSymbol type)
    {
        return type.IsValueType && !IsNullableValueType(type);
    }
}