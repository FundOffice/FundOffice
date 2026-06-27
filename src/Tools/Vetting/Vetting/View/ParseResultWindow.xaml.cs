using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Vetting.Copilot;
using Vetting.Copilot.Models;

namespace Vetting.View;

public partial class ParseResultWindow : Window
{
    public string FileName { get; }
    public string ProviderName { get; }
    public ObservableCollection<OperationGroup> OperationGroups { get; } = [];
    public ObservableCollection<RequiredFileVM> RequiredFiles { get; } = [];

    private OperationDetailVM? _selectedOperation;
    public OperationDetailVM? SelectedOperation => _selectedOperation;

    public ParseResultWindow(string jsonPath, string fileName, string providerName)
    {
        FileName = fileName;
        ProviderName = providerName;
        DataContext = this;

        InitializeComponent();

        // 读取并解析 JSON
        var json = File.ReadAllText(jsonPath);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // 解析 operations
        var ops = root.TryGetProperty("operations", out var opsEl)
            ? OperatorParser.ParseWithWarnings(opsEl).Item1
            : [];

        // 按 entity 分组（z/g 单独分组）
        var groups = ops
            .GroupBy(op => GetGroupName(op))
            .OrderBy(g => g.Key)
            .Select(g => new OperationGroup(g.Key, g.Select(GetDisplayText).ToList()));

        foreach (var g in groups)
            OperationGroups.Add(g);

        // 解析 files
        if (root.TryGetProperty("files", out var filesEl))
        {
            var files = OperatorParser.ParseFiles(filesEl, null).Item1;
            foreach (var f in files)
                RequiredFiles.Add(new RequiredFileVM(f));
        }

        // 默认选中第一个操作
        if (ops.Count > 0)
        {
            _selectedOperation = new OperationDetailVM(ops[0]);
            OnPropertyChanged(new System.Windows.DependencyPropertyChangedEventArgs());
        }

        // TreeView 选择事件
        var tree = (TreeView)FindName("OperationGroups");
        tree.SelectedItemChanged += (s, e) =>
        {
            if (e.NewValue is string displayText)
            {
                var op = ops.FirstOrDefault(o => GetDisplayText(o) == displayText);
                if (op != null)
                {
                    _selectedOperation = new OperationDetailVM(op);
                    OnPropertyChanged(new System.Windows.DependencyPropertyChangedEventArgs());
                }
            }
        };
    }

    private static string GetGroupName(FillOperator op) => op switch
    {
        ScalarOp a => a.Entity ?? "未知实体",
        ListExpandOp c => c.Entity ?? "列表",
        GridOp d => d.Entity ?? "网格",
        ParagraphOp z => "段落问题",
        RecommendOp b => "推荐产品",
        UnknownTableOp g => "未知表格",
        _ => "其他"
    };

    private static string GetDisplayText(FillOperator op) => op switch
    {
        ScalarOp a => $"{a.Question ?? a.Property}",
        ListExpandOp c => $"列表展开: {c.Entity}",
        GridOp d => $"网格: {d.Entity}",
        ParagraphOp z => z.Question ?? "段落问题",
        RecommendOp b => $"推荐产品 #{b.FundIndex}: {b.Property}",
        UnknownTableOp g => g.Description ?? "未知表格",
        _ => "未知操作"
    };
}

public record OperationGroup(string Header, List<string> Items);

public partial class OperationDetailVM : ObservableObject
{
    public string TypeLabel { get; }
    public string? Entity { get; }
    public string? Property { get; }
    public string? Question { get; }
    public string? Description { get; }
    public string? LocationText { get; }
    public string? PropertiesText { get; }

    public OperationDetailVM(FillOperator op)
    {
        TypeLabel = op switch
        {
            ScalarOp => "标量属性 (type a)",
            ListExpandOp => "列表展开 (type c)",
            GridOp d => d.EntityPerRow ? "网格 - 每行一个实体 (type d)" : "网格 - 每列一个实体 (type e)",
            ParagraphOp => "段落问题 (type z)",
            RecommendOp => "推荐产品属性 (type b)",
            UnknownTableOp => "未知表格 (type g)",
            _ => "未知操作"
        };

        Entity = op switch
        {
            ScalarOp a => a.Entity,
            ListExpandOp c => c.Entity,
            GridOp d => d.Entity,
            _ => null
        };

        Property = op switch
        {
            ScalarOp a => a.Property,
            RecommendOp b => b.Property,
            _ => null
        };

        Question = op switch
        {
            ScalarOp a => a.Question,
            ParagraphOp z => z.Question,
            RecommendOp b => b.Question,
            _ => null
        };

        Description = op switch
        {
            UnknownTableOp g => g.Description,
            _ => null
        };

        LocationText = op switch
        {
            ScalarOp a => FormatLocation(a.Location),
            ParagraphOp z => FormatLocation(z.Location),
            RecommendOp b => FormatLocation(b.Location),
            ListExpandOp c => $"数据区域: T{c.Ts.TableIndex}[{c.Ts.RowIndex},{c.Ts.ColIndex}] → [{c.Te.RowIndex},{c.Te.ColIndex}]",
            GridOp d => $"数据区域: T{d.Ts.TableIndex}[{d.Ts.RowIndex},{d.Ts.ColIndex}] → [{d.Te.RowIndex},{d.Te.ColIndex}]",
            UnknownTableOp g => $"数据区域: T{g.Ts.TableIndex}[{g.Ts.RowIndex},{g.Ts.ColIndex}] → [{g.Te.RowIndex},{g.Te.ColIndex}]",
            _ => null
        };

        PropertiesText = op switch
        {
            ListExpandOp c => string.Join("\n", c.Properties.Select(kv => $"  {kv.Value} → {kv.Key}")),
            GridOp d => string.Join("\n", d.Properties.Select(kv => $"  {kv.Key} → {kv.Value}")),
            UnknownTableOp g => string.Join("\n", g.Properties.Select(kv => $"  {kv.Key} → {kv.Value}")),
            _ => null
        };
    }

    private static string FormatLocation(DocLocation loc) => loc.IsCell
        ? $"T{loc.TableIndex}[{loc.RowIndex},{loc.ColIndex}]"
        : loc.IsParagraph
            ? $"段落 #{loc.ParaIndex}"
            : "未知位置";
}

public record RequiredFileVM(RequiredFile File)
{
    public string Raw => File.Raw ?? "";
    public string MapText => File.Map ?? "—";
    public string StampedText => File.Stamped ? "需要" : "不需要";
}