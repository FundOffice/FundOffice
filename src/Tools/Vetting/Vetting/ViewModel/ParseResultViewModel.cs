using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Text.Json;
using Vetting.Copilot;
using Vetting.Copilot.Data;
using Vetting.Copilot.Models.Entities;
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

    /// <summary>所有可选基金</summary>
    public ObservableCollection<FundInfoVM> AvailableFunds { get; } = [];

    /// <summary>当前文件名（用于保存绑定）</summary>
    public string FileName { get; }
    public string VettingId { get; }

    private ParsedJson? _selectedItem;

    public ParseResultViewModel(string fileName, string providerId, string vettingId = "")
    {
        FileName = fileName;
        VettingId = vettingId;

        using var db = new VettingAppDbContext();
        var all = db.ParsedJsons.Query().Where(x => x.FileName == fileName && x.Provider == providerId)
            .OrderByDescending(j => j.Time)
            .ToList();

        HistoryItems = [.. all];

        // 加载所有基金
        using var db2 = new VettingDbContext();
        foreach (var f in db2.FundInfos.FindAll())
            AvailableFunds.Add(new FundInfoVM(f));



        if (HistoryItems.Count > 0)
            SelectedItem = HistoryItems[0];
    }



    public ParsedJson? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (!SetProperty(ref _selectedItem, value)) return;
            DeleteSelectedCommand.NotifyCanExecuteChanged();
            if (value != null)
                LoadOperations(value);
            else
                Operations.Clear();
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
            Operations.Add(new OperationItemViewModel(op, this));
    }

    /// <summary>获取指定 RangeKey 的已绑定基金名称</summary>
    public string? GetBoundFundName(string rangeKey)
    {
        var fundId = GetBoundFundId(rangeKey);
        if (fundId == null) return null;
        return AvailableFunds.FirstOrDefault(f => f.Entity.Id == fundId.Value)?.Name;
    }

    /// <summary>获取指定 RangeKey 的已绑定基金 VM</summary>
    public FundInfoVM? GetBoundFund(string rangeKey)
    {
        var fundId = GetBoundFundId(rangeKey);
        if (fundId == null) return null;
        return AvailableFunds.FirstOrDefault(f => f.Entity.Id == fundId.Value);
    }

    /// <summary>从数据库查询指定 RangeKey 的绑定 FundId</summary>
    private int? GetBoundFundId(string rangeKey)
    {
        using var db = new VettingDbContext();
        var binding = db.FundBindings.FindOne(b => b.FileName == FileName && b.Range == rangeKey);
        return binding?.FundId;
    }

    /// <summary>删除选中的历史解析结果</summary>
    [RelayCommand(CanExecute = nameof(HasSelectedItem))]
    public void DeleteSelected()
    {
        if (_selectedItem == null) return;

        if (HandyControl.Controls.MessageBox.Show(
            $"确认删除 {_selectedItem.Time:MM-dd HH:mm:ss} 的解析结果？",
            "确认删除",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question) != System.Windows.MessageBoxResult.Yes) return;

        using var db = new VettingAppDbContext();
        db.ParsedJsons.Delete(_selectedItem.Id);

        var idx = HistoryItems.IndexOf(_selectedItem);
        HistoryItems.Remove(_selectedItem);

        // 自动选中相邻项
        if (HistoryItems.Count > 0)
            SelectedItem = HistoryItems[Math.Min(idx, HistoryItems.Count - 1)];
        else
            SelectedItem = null;
    }

    private bool HasSelectedItem => _selectedItem != null;

    /// <summary>绑定基金到指定 RangeKey，保存到数据库</summary>
    [RelayCommand]
    public void BindFund(string rangeKeyAndFundId)
    {
        // 格式: "rangeKey|fundId"
        var parts = rangeKeyAndFundId.Split('|');
        if (parts.Length != 2) return;
        var rangeKey = parts[0];
        if (!int.TryParse(parts[1], out var fundId)) return;

        using var db = new VettingDbContext();
        var existing = db.FundBindings.FindOne(b => b.FileName == FileName && b.Range == rangeKey);
        if (existing != null)
        {
            existing.FundId = fundId;
            db.FundBindings.Update(existing);
        }
        else
        {
            db.FundBindings.Insert(new FundBinding
            {
                FileName = FileName,
                Range = rangeKey,
                FundId = fundId,
            });
        }

        // 刷新 UI：通知 OperationItemViewModel 更新绑定显示
        foreach (var op in Operations)
            op.RefreshBinding();
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
    public string? RangeText { get; }
    public string? Table { get; }

    public IList<PropItem> PropertyMaps { get; set; } = [];
    public IList<RecommendPropItem> RecommendProps { get; set; } = [];

    public bool HasEntity => !string.IsNullOrEmpty(Entity);
    public bool HasProperty => !string.IsNullOrEmpty(Property);
    public bool HasQuestion => !string.IsNullOrEmpty(Question);
    public bool HasLocation => !string.IsNullOrEmpty(LocationText);
    public bool HasRange => !string.IsNullOrEmpty(RangeText);
    public bool HasPropertiesMap => PropertyMaps?.Count > 0;
    public bool HasRecommendProps => RecommendProps?.Count > 0;
    public bool HasDescription => !string.IsNullOrEmpty(Description);
    public bool HasTable => !string.IsNullOrEmpty(Table);

    // ── Type b 绑定相关 ──
    public int FundIndex { get; }
    private readonly ParseResultViewModel? _parent;
    public string? RangeKey { get; }

    [ObservableProperty]
    public partial string? BoundFundName { get; set; }

    [ObservableProperty]
    public partial FundInfoVM? SelectedFund { get; set; }

    public bool HasBinding => OpType == "Recommend";

    [ObservableProperty]
    public partial bool IsPopupOpen { get; set; }

    [ObservableProperty]
    public partial string? SearchText { get; set; }

    public ObservableCollection<FundInfoVM> FilteredFunds { get; } = [];


    partial void OnSearchTextChanged(string? value) => FilterFunds();

    private void FilterFunds()
    {
        FilteredFunds.Clear();
        var source = _parent?.AvailableFunds ?? [];
        foreach (var f in source)
        {
            if (string.IsNullOrWhiteSpace(SearchText)
                || (f.Name?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) == true)
                || (f.Code?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) == true))
                FilteredFunds.Add(f);
        }
    }

    public OperationItemViewModel(FillOperator op, ParseResultViewModel? parent = null)
    {
        _parent = parent;

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
                RangeText = FormatRange(c.Range);
                PropertyMaps = c.Properties;
                break;

            case GridOp d:
                Entity = d.Entity;
                RangeText = FormatRange(d.Range);
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
                FundIndex = b.FundIndex;
                Table = b.Table;
                RangeText = FormatRange(b.Range);
                RecommendProps = b.Props;
                RangeKey = b.Range.ToKey();
                // 加载已绑定基金
                BoundFundName = parent?.GetBoundFundName(RangeKey);
                SelectedFund = parent?.GetBoundFund(RangeKey);
                FilterFunds();
                break;

            case UnknownTableOp g:
                Description = g.Description;
                RangeText = FormatRange(g.Range);
                PropertyMaps = g.Properties;
                break;
        }
    }

    partial void OnSelectedFundChanged(FundInfoVM? value)
    {
        if (_parent == null || RangeKey == null) return;
        var fundId = value?.Entity.Id.ToString() ?? "0";
        _parent.BindFundCommand.Execute($"{RangeKey}|{fundId}");
        BoundFundName = value?.Name;
        IsPopupOpen = false;
    }

    /// <summary>刷新绑定显示（绑定保存后调用）</summary>
    public void RefreshBinding()
    {
        if (_parent == null || RangeKey == null) return;
        BoundFundName = _parent.GetBoundFundName(RangeKey);
        SelectedFund = _parent.GetBoundFund(RangeKey);
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

    private static string FormatLocation(Location loc)
    {
        if (loc.IsCell)
            return $"T{loc.Table}[{loc.Row},{loc.Col}]";
        if (loc.IsParagraph)
            return $"段落 #{loc.Para}";
        return "";
    }

    private static string FormatRange(Vetting.Copilot.Range range)
        => $"T{range.Table}[{range.Start.Row},{range.Start.Col}] → [{range.End.Row},{range.End.Col}]";
}
