using CommunityToolkit.Mvvm.ComponentModel;
using FMO.Trustee;
using LiteDB;
using MoT;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using Utilities;

namespace FMO;

/// <summary>
/// TrusteeLogViewerWindow.xaml 的交互逻辑
/// </summary>
public partial class TrusteeLogViewerWindow : Window
{
    public TrusteeLogViewerWindow()
    {
        InitializeComponent();
    }
}


public partial class TrusteeLogViewerWindowViewModel : ObservableObject
{
    private ILiteDatabase _db { get; } = new LiteDatabase(@$"FileName=data\platformlog.db;Connection=Shared");

    public TrusteeLogViewerWindowViewModel()
    {
        Trustees = TrusteeGallay.Trustees.Select(x => new TrusteeInfo(x.Title, x.Identifier)).ToArray();

        Functions =  Logg.Read<TrusteeCallHistory>().Select(x => x.Method).Distinct().ToArray();
        //Functions = _db.GetCollection<TrusteeCallHistory>().Query().Select(x => x.Method).ToList().Distinct().ToArray();
    }

    [ObservableProperty]
    public partial TrusteeInfo[] Trustees { get; set; } = [];


    [ObservableProperty]
    public partial string[] Functions { get; set; } = [];


    [ObservableProperty]
    public partial TrusteeCallHistory[] Logs { get; set; } = [];


    [ObservableProperty]
    public partial IEnumerable<TrusteeCallHistory>? LogsByDate { get; set; } = null;



    [ObservableProperty]
    public partial TrusteeInfo? SelectedTrustee { get; set; }

    [ObservableProperty]
    public partial string? SelectedFunction { get; set; }

    [ObservableProperty]
    public partial DateTime? SelectedDate { get; set; }

    [ObservableProperty]
    public partial TrusteeCallHistory? SelectedLog { get; set; }


    [ObservableProperty]
    public partial List<JsonTreeNode>? TreeNodes { get; set; }


    [ObservableProperty]
    public partial DateTime[] Dates { get; set; } = [];

    [ObservableProperty]
    public partial TimeSpan[] Times { get; set; } = [];

    partial void OnSelectedFunctionChanged(string? value)
    {
        UpdateLogs();
    }

    partial void OnSelectedTrusteeChanged(TrusteeInfo? value)
    {
        UpdateLogs();
    }

    private void UpdateLogs()
    {
        if (SelectedTrustee is null || string.IsNullOrWhiteSpace(SelectedFunction))
            Logs = [];
        else
            Logs = Logg.Read<TrusteeCallHistory>().Where(x => x.Identifier == SelectedTrustee.Idenntifier && x.Method == SelectedFunction).OrderByDescending(x => x.Time).Take(400).ToArray();

        Dates = Logs.Select(x => x.Time.Date).Distinct().ToArray();

        OnSelectedDateChanged(Dates.FirstOrDefault());
        SelectedLog = null;
        //Times = Logs.Select(x => x.Time.TimeOfDay).Distinct().ToArray();
    }

    partial void OnSelectedDateChanged(DateTime? value)
    {
        if (value is null) LogsByDate = null;
        else LogsByDate = Logs.Where(x => x.Time.Date == value);
    }

    partial void OnSelectedLogChanged(TrusteeCallHistory? value)
    {
        try
        {
            TreeNodes = JsonTreeParser.Parse(value?.Json ?? string.Empty, 2);
        }
        catch (JsonException ex)
        {
            Toast.Warning($"JSON 格式错误: {ex.Message} 解析失败");
        }
    }

    public record TrusteeInfo(string Name, string Idenntifier);
}

public class JsonTreeNode
{
    public string? Name { get; set; }
    public string? Value { get; set; }
    public List<JsonTreeNode> Children { get; set; } = new();

    // 是否为叶子节点（用于控制 UI 显示）
    public bool IsLeaf => Children.Count == 0;

    public bool IsExpanded { get; set; }
}


public static class JsonTreeParser
{
    public static List<JsonTreeNode> Parse(string json, int expandDepth = 1)
    {
        using var doc = JsonDocument.Parse(json);
        return new List<JsonTreeNode> { ParseElement(doc.RootElement, "Root", 0, expandDepth) };
    }

    private static JsonTreeNode ParseElement(JsonElement element, string name, int depth, int expandDepth)
    {
        var node = new JsonTreeNode
        {
            Name = name,
            // 当前层级 < 目标展开层级时，设为展开
            IsExpanded = depth < expandDepth
        };

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                    node.Children.Add(ParseElement(prop.Value, prop.Name, depth + 1, expandDepth));
                break;

            case JsonValueKind.Array:
                int index = 0;
                foreach (var item in element.EnumerateArray())
                    node.Children.Add(ParseElement(item, $"[{index++}]", depth + 1, expandDepth));
                break;

            case JsonValueKind.String:
                var strVal = element.GetString();
                if (strVal is not null && TryParseStringAsJson(strVal, out JsonElement innerElement))
                {
                    var innerNode = ParseElement(innerElement, name, depth, expandDepth);
                    node.Children = innerNode.Children;
                    node.IsExpanded = innerNode.IsExpanded; // 继承内部节点的展开状态
                }
                else
                {
                    node.Value = strVal;
                }
                break;

            default:
                node.Value = element.GetRawText();
                break;
        }

        return node;
    }


    /// <summary>
    /// 安全探测字符串是否为 JSON 对象/数组
    /// </summary>
    private static bool TryParseStringAsJson(string str, out JsonElement parsed)
    {
        parsed = default;
        if (string.IsNullOrWhiteSpace(str)) return false;

        string trimmed = str.Trim();
        if (trimmed.Length < 2) return false;

        // ⚡ 快速预判：避免对海量普通字符串调用 JsonDocument.Parse
        if (trimmed[0] != '{' && trimmed[0] != '[') return false;

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            // 仅当内部确实是 Object 或 Array 时才视为嵌套结构
            if (doc.RootElement.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
            {
                // 🔒 必须 Clone！否则 doc 被 using 释放后，JsonElement 会抛出 ObjectDisposedException
                parsed = doc.RootElement.Clone();
                return true;
            }
        }
        catch (JsonException)
        {
            // 格式非法或包含不可见字符，按普通字符串处理
        }

        return false;
    }
}