using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace SG;

[Generator]
public class ElementsViewModelGenerator : IIncrementalGenerator
{
    // [核心修复] 自定义格式：获取全限定名但不包含泛型参数（如 <T>），防止拼接时出现 <T><string> 的错误
    private static readonly SymbolDisplayFormat FullyQualifiedWithoutGenericsFormat = new SymbolDisplayFormat(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.None,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers | SymbolDisplayMiscellaneousOptions.UseErrorTypeSymbolName);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1. 获取 ElementsViewModel 的命名空间、手写属性 及 INPC 状态
        var vmInfoProvider = context.SyntaxProvider.CreateSyntaxProvider(
            predicate: static (node, _) => node is ClassDeclarationSyntax { Identifier.Text: "ElementsViewModel" },
            transform: static (ctx, _) =>
            {
                var cls = (ClassDeclarationSyntax)ctx.Node;
                var ns = cls.Parent switch
                {
                    NamespaceDeclarationSyntax nsDecl => nsDecl.Name.ToString(),
                    FileScopedNamespaceDeclarationSyntax fnsDecl => fnsDecl.Name.ToString(),
                    _ => string.Empty
                };

                var manualProps = ImmutableHashSet<string>.Empty;
                bool needsINPC = true;

                var symbol = ctx.SemanticModel.GetDeclaredSymbol(cls) as INamedTypeSymbol;
                if (symbol is not null)
                {
                    manualProps = symbol.GetMembers()
                        .OfType<IPropertySymbol>()
                        .Select(p => p.Name)
                        .ToImmutableHashSet();

                    if (symbol.GetMembers("PropertyChanged").Any(m => m is IEventSymbol) ||
                        symbol.GetMembers("SetProperty").Any(m => m is IMethodSymbol))
                    {
                        needsINPC = false;
                    }
                    else if (symbol.AllInterfaces.Any(i => i.Name == "INotifyPropertyChanged"))
                    {
                        needsINPC = false;
                    }
                    else
                    {
                        var baseType = symbol.BaseType;
                        while (baseType != null && baseType.SpecialType != SpecialType.System_Object)
                        {
                            if (baseType.AllInterfaces.Any(i => i.Name == "INotifyPropertyChanged") ||
                                baseType.Name.Contains("ObservableObject"))
                            {
                                needsINPC = false;
                                break;
                            }
                            baseType = baseType.BaseType;
                        }
                    }
                }

                return (Namespace: ns, ManualProps: manualProps, NeedsINPC: needsINPC);
            }
        ).Collect();

        // 2. 收集具体的 ViewModel 映射
        var viewModelMappings = context.SyntaxProvider.CreateSyntaxProvider(
            predicate: static (node, _) => node is ClassDeclarationSyntax,
            transform: static (ctx, _) =>
            {
                var classSymbol = ctx.SemanticModel.GetDeclaredSymbol(ctx.Node) as INamedTypeSymbol;
                if (classSymbol == null) return null;

                string? modelName = null;
                string? vmName = null;

                foreach (var iface in classSymbol.AllInterfaces)
                {
                    if (iface.Name is "IViewModel" or "IViewModle")
                    {
                        if (iface.IsGenericType && iface.TypeArguments.Length == 2)
                        {
                            modelName = iface.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                            vmName = iface.TypeArguments[1].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                            break;
                        }
                    }
                }

                if (modelName != null && vmName != null)
                {
                    return new VMMapping(modelName, vmName);
                }
                return null;
            }
        )
        .Where(static m => m is not null)
        .Collect();

        // 3. 组合编译上下文、ViewModel信息和映射关系
        var compilationAndFactorsAndMappings = context.CompilationProvider
            .Combine(vmInfoProvider)
            .Combine(viewModelMappings);

        // 4. 语义层：提取符合条件的属性元数据
        var propertiesToGenerate = compilationAndFactorsAndMappings.Select((pair, ct) =>
        {
            var ((compilation, vmInfos), mappings) = pair;
            var hasViewModel = !vmInfos.IsEmpty;

            var targetNamespace = vmInfos.FirstOrDefault().Namespace ?? string.Empty;
            var manualProps = vmInfos.SelectMany(v => v.ManualProps).ToImmutableHashSet();
            var needsINPC = vmInfos.FirstOrDefault().NeedsINPC;

            var debug = new DebugPayload(targetNamespace, "null", "null", "null", 0, 0, false, 0, manualProps.Count, "", needsINPC);

            var fundFactorsSymbol = FindTypeGlobally(compilation, "FundFactors");
            if (fundFactorsSymbol is null)
            {
                debug = debug with { HasFundFactors = false, FundFactors = "Not Found" };
                return (targetNamespace, hasViewModel, ImmutableArray<PropertyMeta>.Empty, ImmutableArray<PropertyMeta>.Empty, ImmutableArray<PropertyMeta>.Empty, debug, mappings, "", "", "", "", "", "");
            }

            debug = debug with { HasFundFactors = true, FundFactors = fundFactorsSymbol.ToDisplayString() };

            var singletonBase = FindTypeGlobally(compilation, "SingletonFactorItem");
            var factorBase = FindTypeGlobally(compilation, "FactorItem");

            debug = debug with
            {
                Singleton = singletonBase?.ToDisplayString() ?? "Not Found",
                Factor = factorBase?.ToDisplayString() ?? "Not Found",
                MappingsCount = mappings.Length
            };

            var fmvSymbol = FindTypeGlobally(compilation, "FactorModifiableViewModel");
            var sfvSymbol = FindTypeGlobally(compilation, "ShareFactorViewModel");
            var cloneHelperSymbol = FindTypeGlobally(compilation, "CloneHelper");
            var shareClassSymbol = FindTypeGlobally(compilation, "ShareClass");
            var factorFieldsSymbol = FindTypeGlobally(compilation, "FactorFields");
            var shareClassVmSymbol = FindTypeGlobally(compilation, "ShareClassViewModel");

            // [核心修复] 基类使用不带泛型参数的格式，防止拼接时出现 <T><string>
            string fmvFullName = fmvSymbol?.ToDisplayString(FullyQualifiedWithoutGenericsFormat) ?? "global::FactorModifiableViewModel";
            string sfvFullName = sfvSymbol?.ToDisplayString(FullyQualifiedWithoutGenericsFormat) ?? "global::ShareFactorViewModel";

            // 其他非泛型基类/辅助类使用标准格式即可
            string cloneHelperFullName = cloneHelperSymbol?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? "global::CloneHelper";
            string shareClassFullName = shareClassSymbol?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? "global::ShareClass";
            string factorFieldsFullName = factorFieldsSymbol?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? "global::FactorFields";
            string shareClassVmFullName = shareClassVmSymbol?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? "global::ShareClassViewModel";

            var vmDict = new Dictionary<string, string>();
            foreach (var m in mappings)
            {
                if (!vmDict.ContainsKey(m.ModelType)) vmDict[m.ModelType] = m.ViewModelType;
            }

            INamedTypeSymbol? vmSymbol = null;
            if (!string.IsNullOrEmpty(targetNamespace))
                vmSymbol = compilation.GetTypeByMetadataName($"{targetNamespace}.ElementsViewModel");
            else
                vmSymbol = compilation.GetTypeByMetadataName("ElementsViewModel");

            var allModifiersBuilder = ImmutableArray.CreateBuilder<PropertyMeta>();
            var propsToDeclareBuilder = ImmutableArray.CreateBuilder<PropertyMeta>();
            var propsForFillByBuilder = ImmutableArray.CreateBuilder<PropertyMeta>();
            var excludedProps = new List<string>();

            foreach (var prop in fundFactorsSymbol.GetMembers().OfType<IPropertySymbol>())
            {
                if (prop.Name == "ShareClasses" || prop.Type.Name == "ShareClasses" || prop.Type.Name == "ShareClass")
                {
                    excludedProps.Add($"{prop.Name} (ShareClasses)");
                    continue;
                }

                var matchedBase = FindMatchingGenericBase(prop.Type, singletonBase, factorBase);
                if (matchedBase is null)
                {
                    excludedProps.Add($"{prop.Name} (Not a supported Factor type)");
                    continue;
                }

                bool isSingleton = SymbolEqualityComparer.Default.Equals(matchedBase.OriginalDefinition, singletonBase);
                var tSymbol = matchedBase.TypeArguments[0];
                string tName = tSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                bool isDoubleTemplate = false;
                string vmName = tName;

                if (vmDict.TryGetValue(tName, out var specificVm))
                {
                    isDoubleTemplate = true;
                    vmName = specificVm;
                }

                string? ctorParams = null;
                string? ctorBaseArgs = null;
                if (!isSingleton && sfvSymbol != null)
                {
                    var ctor = sfvSymbol.Constructors.FirstOrDefault(c => !c.IsStatic && c.Parameters.Length == 5);
                    if (ctor != null)
                    {
                        ctorParams = string.Join(", ", ctor.Parameters.Select(par => $"{par.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} {par.Name}"));
                        ctorBaseArgs = string.Join(", ", ctor.Parameters.Select(par => par.Name));
                    }
                }

                var meta = new PropertyMeta(prop.Name, isSingleton, tName, vmName, isDoubleTemplate, ctorParams, ctorBaseArgs);
                allModifiersBuilder.Add(meta);

                bool isHandwritten = manualProps.Contains(prop.Name);
                bool includeInFillBy = false;

                if (isHandwritten)
                {
                    var vmProp = vmSymbol?.GetMembers(prop.Name).OfType<IPropertySymbol>().FirstOrDefault();
                    if (vmProp != null && vmProp.Type is INamedTypeSymbol vmPropType && vmPropType.IsGenericType)
                    {
                        if (vmPropType.OriginalDefinition.Name == "FactorModifiableViewModel" || vmPropType.OriginalDefinition.Name == "ShareFactorViewModel")
                        {
                            includeInFillBy = true;
                        }
                    }
                    if (!includeInFillBy) excludedProps.Add($"{prop.Name} (Manual - Not a supported VM type)");
                }
                else
                {
                    includeInFillBy = true;
                }

                if (includeInFillBy) propsForFillByBuilder.Add(meta);
                if (!isHandwritten) propsToDeclareBuilder.Add(meta);
            }

            debug = debug with
            {
                RefCount = propsToDeclareBuilder.Count,
                FillByCount = propsForFillByBuilder.Count,
                ExcludedProps = string.Join(", ", excludedProps)
            };

            return (targetNamespace, hasViewModel, propsToDeclareBuilder.ToImmutable(), propsForFillByBuilder.ToImmutable(), allModifiersBuilder.ToImmutable(), debug, mappings, fmvFullName, sfvFullName, cloneHelperFullName, shareClassFullName, factorFieldsFullName, shareClassVmFullName);
        });

        // 5. 代码生成
        context.RegisterSourceOutput(propertiesToGenerate, (spc, data) =>
        {
            var (ns, hasViewModel, propsToDeclare, propsForFillBy, allModifiers, debug, mappings, fmvFullName, sfvFullName, cloneHelperFullName, shareClassFullName, factorFieldsFullName, shareClassVmFullName) = data;

            // ==========================================
            // A. 生成全局 Modifier 类 (独立文件)
            // ==========================================
            if (allModifiers.Length > 0)
            {
                var modifierSb = new StringBuilder();
                modifierSb.AppendLine("// <auto-generated/>");
                modifierSb.AppendLine("#nullable enable");
                modifierSb.AppendLine();
                modifierSb.AppendLine("namespace FMO.Factor.Modifier;");
                modifierSb.AppendLine();

                foreach (var p in allModifiers)
                {
                    string baseTypeStr;
                    if (p.IsSingleton)
                    {
                        baseTypeStr = p.IsDoubleTemplate
                            ? $"{fmvFullName}<{p.TName}, {p.VMName}>"
                            : $"{fmvFullName}<{p.TName}>";
                    }
                    else
                    {
                        baseTypeStr = p.IsDoubleTemplate
                            ? $"{sfvFullName}<{p.TName}, {p.VMName}>"
                            : $"{sfvFullName}<{p.TName}>";
                    }

                    string modifierClassName = $"{p.Name}Modifier";
                    string ctorBlock = "";

                    if (!p.IsSingleton && !string.IsNullOrEmpty(p.CtorParams))
                    {
                        ctorBlock = $$"""
        public {{modifierClassName}}({{p.CtorParams}}) : base({{p.CtorBaseArgs}}) { }
""";
                    }

                    modifierSb.Append($$"""
public partial class {{modifierClassName}} : {{baseTypeStr}} 
{
{{ctorBlock}}
}

""");
                }
                spc.AddSource("FactorModifiers.g.cs", modifierSb.ToString());
            }

            // ==========================================
            // B. 生成 ElementsViewModel (如果存在)
            // ==========================================
            if (!hasViewModel) return;

            var sb = new StringBuilder();
            foreach (var p in propsToDeclare)
            {
                string typeStr = $"global::FMO.Factor.Modifier.{p.Name}Modifier";
                sb.Append($$"""
    public {{typeStr}} {{p.Name}} { get => field; set => SetProperty(ref field, value); }

""");
            }

            var fillBySb = new StringBuilder();
            fillBySb.Append($$"""

    public void FillBy({{debug.FundFactors}} factors, int flowId)
    {
        var sc = factors.ShareClasses[flowId];
        var classIds = sc.Select(x => x.Id).ToArray();
        Shares = new(sc.Select(x => new {{shareClassVmFullName}}(x)));

""");

            foreach (var p in propsForFillBy)
            {
                string modifierFullName = $"global::FMO.Factor.Modifier.{p.Name}Modifier";

                if (p.IsSingleton)
                {
                    if (p.IsDoubleTemplate)
                    {
                        fillBySb.Append($$"""
        {{p.Name}} = new {{modifierFullName}}()
        {
            FlowId = flowId,
            ShareId = {{shareClassFullName}}.Singleton,
            FactorId = {{factorFieldsFullName}}.{{p.Name}},
            FundId = this.FundId,
            NewValue = {{p.VMName}}.Trans(factors.{{p.Name}}[flowId]),
            OldValue = factors.{{p.Name}}[flowId],
            FallbackValue = factors.{{p.Name}}[flowId - 1]
        };

""");
                    }
                    else
                    {
                        fillBySb.Append($$"""
        {{p.Name}} = new {{modifierFullName}}()
        {
            FlowId = flowId,
            ShareId = {{shareClassFullName}}.Singleton,
            FactorId = {{factorFieldsFullName}}.{{p.Name}},
            FundId = this.FundId,
            NewValue = {{cloneHelperFullName}}.CloneValue(factors.{{p.Name}}[flowId]),
            OldValue = factors.{{p.Name}}[flowId],
            FallbackValue = factors.{{p.Name}}[flowId - 1]
        };

""");
                    }
                }
                else
                {
                    fillBySb.Append($$"""
        var mfi_{{p.Name}} = factors.{{p.Name}}.GetInheritValues(flowId, classIds);
        {{p.Name}} = new {{modifierFullName}}(
            this.FundId,
            flowId,
            {{factorFieldsFullName}}.{{p.Name}},
            sc,
            mfi_{{p.Name}}
        );

""");
                }
            }
            fillBySb.AppendLine("   }");

            var inpcInheritance = debug.NeedsINPC ? " : global::System.ComponentModel.INotifyPropertyChanged" : "";
            var inpcBlock = debug.NeedsINPC ? """

    public event global::System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([global::System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new global::System.ComponentModel.PropertyChangedEventArgs(propertyName));

    protected void SetProperty<T>(ref T storage, T value, [global::System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (!global::System.Collections.Generic.EqualityComparer<T>.Default.Equals(storage, value))
        {
            storage = value;
            OnPropertyChanged(propertyName);
        }
    }
""" : "";

            var debugComments = new StringBuilder();
            debugComments.AppendLine("// <auto-generated/>");
            debugComments.AppendLine("// --- DEBUG INFO ---");
            debugComments.AppendLine($"// Namespace: {debug.Namespace}");
            debugComments.AppendLine($"// HasFundFactors: {debug.HasFundFactors}");
            debugComments.AppendLine($"// FundFactors Symbol: {debug.FundFactors}");
            debugComments.AppendLine($"// Singleton Base: {debug.Singleton}");
            debugComments.AppendLine($"// Factor Base: {debug.Factor}");
            debugComments.AppendLine($"// Detected VM Mappings: {debug.MappingsCount}");
            debugComments.AppendLine($"// Manual Props Detected: {debug.ManualPropsCount}");
            debugComments.AppendLine($"// Needs INPC: {debug.NeedsINPC}");
            debugComments.AppendLine($"// Excluded Props: {(string.IsNullOrEmpty(debug.ExcludedProps) ? "None" : debug.ExcludedProps)}");

            foreach (var m in mappings.Take(5))
            {
                debugComments.AppendLine($"//   -> {m.ModelType} : {m.ViewModelType}");
            }
            if (mappings.Length > 5) debugComments.AppendLine($"//   ... and {mappings.Length - 5} more");

            debugComments.AppendLine($"// Auto-Generated Props (RefCount): {debug.RefCount}");
            debugComments.AppendLine($"// FillBy Props Count: {debug.FillByCount}");
            debugComments.AppendLine($"// Total Modifiers Generated: {allModifiers.Length}");
            debugComments.AppendLine("// ------------------");

            var source = string.IsNullOrEmpty(ns) ? $$"""
{{debugComments.ToString()}}
#nullable enable

public partial class ElementsViewModel{{inpcInheritance}}
{
{{inpcBlock}}
{{sb.ToString()}}
{{fillBySb.ToString()}}
}
""" : $$"""
{{debugComments.ToString()}}
#nullable enable

namespace {{ns}};

public partial class ElementsViewModel{{inpcInheritance}}
{
{{inpcBlock}}
{{sb.ToString()}}
{{fillBySb.ToString()}}
}
""";

            spc.AddSource("ElementsViewModel.g.cs", source);
        });
    }

    private static INamedTypeSymbol? FindTypeGlobally(Compilation compilation, string typeName)
    {
        var symbols = compilation.GetSymbolsWithName(typeName, SymbolFilter.Type).OfType<INamedTypeSymbol>().ToList();
        if (symbols.Count > 0) return symbols[0];

        foreach (var reference in compilation.References)
        {
            if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assemblySymbol)
            {
                var found = FindTypeInNamespace(assemblySymbol.GlobalNamespace, typeName);
                if (found != null) return found;
            }
        }
        return null;
    }

    private static INamedTypeSymbol? FindTypeInNamespace(INamespaceSymbol ns, string typeName)
    {
        foreach (var member in ns.GetMembers())
        {
            if (member is INamedTypeSymbol namedType && namedType.Name == typeName)
                return namedType;
            if (member is INamespaceSymbol childNs)
            {
                var found = FindTypeInNamespace(childNs, typeName);
                if (found != null) return found;
            }
        }
        return null;
    }

    private static INamedTypeSymbol? FindMatchingGenericBase(ITypeSymbol type, INamedTypeSymbol? singletonBase, INamedTypeSymbol? factorBase)
    {
        var current = type as INamedTypeSymbol;
        while (current is not null)
        {
            if (current.IsGenericType)
            {
                var def = current.OriginalDefinition;
                if (singletonBase is not null && SymbolEqualityComparer.Default.Equals(def, singletonBase))
                    return current;
                if (factorBase is not null && SymbolEqualityComparer.Default.Equals(def, factorBase))
                    return current;
            }
            current = current.BaseType;
        }
        return null;
    }

    private record DebugPayload(
      string Namespace,
      string FundFactors,
      string Singleton,
      string Factor,
      int RefCount,
      int FillByCount,
      bool HasFundFactors,
      int MappingsCount,
      int ManualPropsCount,
      string ExcludedProps,
      bool NeedsINPC);

    private record PropertyMeta(
        string Name,
        bool IsSingleton,
        string TName,
        string VMName,
        bool IsDoubleTemplate,
        string? CtorParams = null,
        string? CtorBaseArgs = null);

    private record VMMapping(string ModelType, string ViewModelType);
}