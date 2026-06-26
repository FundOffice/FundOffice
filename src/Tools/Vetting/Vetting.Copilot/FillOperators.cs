using System.Text.Json;

namespace Vetting.Copilot;

/// <summary>
/// 文档坐标
/// </summary>
public record DocLocation
{
    public int TableIndex { get; init; } = -1;
    public int RowIndex { get; init; }
    public int ColIndex { get; init; }
    public int ParaIndex { get; init; } = -1;

    public bool IsCell => TableIndex >= 0;
    public bool IsParagraph => ParaIndex >= 0 && TableIndex < 0;

    public static DocLocation FromJson(JsonElement el)
    {
        int Get(string key) => el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : -1;
        return new DocLocation
        {
            TableIndex = Get("table_index"),
            RowIndex = Get("row_index"),
            ColIndex = Get("col_index"),
            ParaIndex = Get("para_index"),
        };
    }
}

/// <summary>
/// 填充操作基类
/// </summary>
public abstract record FillOperator;

/// <summary>
/// Type a: 单值实体 LQRA（manager/credit/invest/risk）
/// </summary>
public record ScalarOp : FillOperator
{
    public required string Entity { get; init; }
    public required string Property { get; init; }
    public required string Question { get; init; }
    public required DocLocation Location { get; init; }
    public string? Format { get; init; }
}

/// <summary>
/// Type b: 推荐产品 LQRA
/// </summary>
public record RecommendOp : FillOperator
{
    /// <summary>AI 解析顺序排列的产品索引（0-based），与输入 recommend 数组对应</summary>
    public required int FundIndex { get; init; }
    public required string Property { get; init; }
    public required string Question { get; init; }
    public required string Table { get; init; }
    public required DocLocation Location { get; init; }
    public string? Format { get; init; }
}

/// <summary>
/// Type c: 列头列表，自动扩展行
/// </summary>
public record ListExpandOp : FillOperator
{
    public required string Entity { get; init; }
    public required Dictionary<string, string> Properties { get; init; }
    public required DocLocation Ts { get; init; }
    public required DocLocation Te { get; init; }
    public Dictionary<string, string>? Formats { get; init; }
}

/// <summary>
/// Type d: 行列头，一行一 entity（不扩展）
/// Type e: 行列头，一列一 entity（不扩展）
/// </summary>
public record GridOp : FillOperator
{
    public required string Entity { get; init; }
    public required Dictionary<string, string> Properties { get; init; }
    public required DocLocation Ts { get; init; }
    public required DocLocation Te { get; init; }
    /// <summary>true=Type d（一行一entity）, false=Type e（一列一entity）</summary>
    public required bool EntityPerRow { get; init; }
    /// <summary>按此属性匹配行头/列头（通常 "Year"）</summary>
    public string? FilterBy { get; init; }
    public Dictionary<string, string>? Formats { get; init; }
}

/// <summary>
/// Type f: 段落问题（散装或实体属性）
/// </summary>
public record ParagraphOp : FillOperator
{
    public required string Question { get; init; }
    public required DocLocation Location { get; init; }
    public string? Entity { get; init; }
    public string? Property { get; init; }
    public string? Format { get; init; }
}

public static class OperatorParser
{
    /// <summary>
    /// 从 AI 返回的 JSON operations 数组解析为 FillOperator 列表
    /// </summary>
    public static List<FillOperator> Parse(JsonElement operations)
    {
        var result = new List<FillOperator>();
        if (operations.ValueKind != JsonValueKind.Array) return result;

        foreach (var op in operations.EnumerateArray())
        {
            var type = op.GetProperty("type").GetString();
            FillOperator? parsed = type switch
            {
                "a" => ParseScalar(op),
                "b" => ParseRecommend(op),
                "c" => ParseListExpand(op),
                "d" => ParseGrid(op, entityPerRow: true),
                "e" => ParseGrid(op, entityPerRow: false),
                "f" => ParseParagraph(op),
                _ => null
            };
            if (parsed != null) result.Add(parsed);
        }
        return result;
    }

    private static ScalarOp ParseScalar(JsonElement op) => new()
    {
        Entity = op.GetProperty("entity").GetString()!,
        Property = op.GetProperty("property").GetString()!,
        Question = op.TryGetProperty("question", out var q) ? q.GetString() ?? "" : "",
        Location = DocLocation.FromJson(op.GetProperty("location")),
        Format = op.TryGetProperty("format", out var fmt) ? fmt.GetString() : null,
    };

    private static RecommendOp ParseRecommend(JsonElement op) => new()
    {
        FundIndex = op.GetProperty("fund_index").GetInt32(),
        Property = op.GetProperty("property").GetString()!,
        Question = op.TryGetProperty("question", out var q) ? q.GetString() ?? "" : "",
        Table = op.TryGetProperty("table", out var t) ? t.GetString() ?? "" : "",
        Location = DocLocation.FromJson(op.GetProperty("location")),
        Format = op.TryGetProperty("format", out var fmt) ? fmt.GetString() : null,
    };

    private static ListExpandOp ParseListExpand(JsonElement op) => new()
    {
        Entity = op.GetProperty("entity").GetString()!,
        Properties = ParseStringDict(op.GetProperty("properties")),
        Ts = DocLocation.FromJson(op.GetProperty("ts")),
        Te = DocLocation.FromJson(op.GetProperty("te")),
        Formats = op.TryGetProperty("formats", out var fmts) && fmts.ValueKind == JsonValueKind.Object
            ? ParseStringDict(fmts) : null,
    };

    private static GridOp ParseGrid(JsonElement op, bool entityPerRow) => new()
    {
        Entity = op.GetProperty("entity").GetString()!,
        Properties = ParseStringDict(op.GetProperty("properties")),
        Ts = DocLocation.FromJson(op.GetProperty("ts")),
        Te = DocLocation.FromJson(op.GetProperty("te")),
        EntityPerRow = entityPerRow,
        FilterBy = op.TryGetProperty("filter_by", out var fb) ? fb.GetString() : null,
        Formats = op.TryGetProperty("formats", out var fmts) && fmts.ValueKind == JsonValueKind.Object
            ? ParseStringDict(fmts) : null,
    };

    private static ParagraphOp ParseParagraph(JsonElement op) => new()
    {
        Question = op.TryGetProperty("question", out var q) ? q.GetString() ?? "" : "",
        Location = DocLocation.FromJson(op.GetProperty("location")),
        Entity = op.TryGetProperty("entity", out var e) ? e.GetString() : null,
        Property = op.TryGetProperty("property", out var p) ? p.GetString() : null,
        Format = op.TryGetProperty("format", out var fmt) ? fmt.GetString() : null,
    };

    private static Dictionary<string, string> ParseStringDict(JsonElement el)
    {
        var dict = new Dictionary<string, string>();
        if (el.ValueKind != JsonValueKind.Object) return dict;
        foreach (var prop in el.EnumerateObject())
            dict[prop.Name] = prop.Value.GetString() ?? "";
        return dict;
    }
}
