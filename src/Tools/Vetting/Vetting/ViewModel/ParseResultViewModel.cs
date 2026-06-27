using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Text.Json;
using Vetting.Copilot;
using Vetting.Copilot.Models;
using Vetting.Data;
using Vetting.Entity;

namespace Vetting.ViewModel;

public partial class ParseResultViewModel : ObservableObject
{ 
    /// <summary>历史记录列表</summary>
   [ObservableProperty]  
    public partial ObservableCollection<ParsedJson> HistoryItems { get; set; } = [];

    /// <summary>当前展示的 operations</summary>
    public ObservableCollection<OperationItemViewModel> Operations { get; } = [];

    private ParsedJson? _selectedItem;

    public ParseResultViewModel(string fileHash, string providerId)
    {

        using var db = new VettingAppDbContext();
        var all = db.ParsedJsons.Query().Where(x => x.FileHash == fileHash && x.Provider == providerId)
            .OrderByDescending(j => j.Time)
            .ToList();

        HistoryItems = [.. all];

        if (HistoryItems.Count > 0)
            SelectedItem = HistoryItems[0];
    }

    public ParsedJson? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (!SetProperty(ref _selectedItem, value)) return;
            if (value != null)
                LoadOperations(value);
        }
    }
     

    private void LoadOperations(ParsedJson item)
    {
        Operations.Clear();

        if (string.IsNullOrEmpty(item.Json)) return;

        using var doc = JsonDocument.Parse(item.Json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("operations", out var opsEl)) return;

        var operators = OperatorParser.ParseWithWarnings(opsEl).Item1;
        foreach (var op in operators)
            Operations.Add(new OperationItemViewModel(op));
    }
}


/// <summary>
/// 单个操作的展示 ViewModel
/// </summary>
public partial class OperationItemViewModel : ObservableObject
{
    public string OpType { get; }
    public string TypeLabel { get; }
    public string? Description { get; }
    public string? Entity { get; }
    public string? Property { get; }
    public string? Question { get; }
    public string? LocationText { get; }

    public IList<PropItem> PropertyMaps { get; set; } = [];

    public bool HasEntity => !string.IsNullOrEmpty(Entity);
    public bool HasProperty => !string.IsNullOrEmpty(Property);
    public bool HasQuestion => !string.IsNullOrEmpty(Question);
    public bool HasLocation => !string.IsNullOrEmpty(LocationText);
    public bool HasPropertiesMap => PropertyMaps?.Count > 0;
    public bool HasDescription => !string.IsNullOrEmpty(Description);

    public OperationItemViewModel(FillOperator op)
    {
        OpType = GetOpType(op);
        TypeLabel = GetTypeLabel(op);

        switch (op)
        {
            case ScalarOp a:
                Entity = a.Entity;
                Property = a.Property;
                Question = a.Question;
                LocationText = FormatLocation(a.Location);
                break;

            case ListExpandOp c:
                Entity = c.Entity;
                LocationText = FormatRange(c.Ts, c.Te);
                PropertyMaps = c.Properties;
                break;

            case GridOp d:
                Entity = d.Entity;
                LocationText = FormatRange(d.Ts, d.Te);
                PropertyMaps = d.Properties;
                break;

            case ParagraphOp z:
                Question = z.Question;
                Entity = z.Entity;
                Property = z.Property;
                LocationText = FormatLocation(z.Location);
                break;

            case RecommendOp b:
                Entity = $"推荐产品 #{b.FundIndex}";
                Property = b.Property;
                Question = b.Question;
                LocationText = FormatLocation(b.Location);
                break;

            case UnknownTableOp g:
                Description = g.Description;
                LocationText = FormatRange(g.Ts, g.Te);
                PropertyMaps = g.Properties;
                break;
        }
    }

    private static string GetOpType(FillOperator op) => op switch
    {
        ScalarOp => "Scalar",
        ListExpandOp => "ListExpand",
        GridOp => "Grid",
        ParagraphOp => "Paragraph",
        RecommendOp => "Recommend",
        UnknownTableOp => "UnknownTable",
        _ => "Unknown"
    };

    private static string GetTypeLabel(FillOperator op) => op switch
    {
        ScalarOp => "type a",
        ListExpandOp => "type c",
        GridOp d => d.EntityPerRow ? "type d" : "type e",
        ParagraphOp => "type z",
        RecommendOp => "type b",
        UnknownTableOp => "type g",
        _ => "未知"
    };

    private static string FormatLocation(DocLocation loc) => loc.IsCell
        ? $"T{loc.TableIndex}[{loc.RowIndex},{loc.ColIndex}]"
        : loc.IsParagraph
            ? $"段落 #{loc.ParaIndex}"
            : "";

    private static string FormatRange(DocLocation ts, DocLocation te)
        => $"T{ts.TableIndex}[{ts.RowIndex},{ts.ColIndex}] → [{te.RowIndex},{te.ColIndex}]";

   



}
