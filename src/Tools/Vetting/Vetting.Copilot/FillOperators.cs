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
    /// 从 AI 返回的 JSON operations 数组解析为 FillOperator 列表。
    /// 单个操作解析失败时跳过并记录警告，不影响其他操作。
    /// </summary>
    public static (List<FillOperator> Operators, List<string> Warnings) ParseWithWarnings(JsonElement operations)
    {
        var result = new List<FillOperator>();
        var warnings = new List<string>();
        if (operations.ValueKind != JsonValueKind.Array) return (result, warnings);

        int idx = 0;
        foreach (var op in operations.EnumerateArray())
        {
            try
            {
                if (!TryGetString(op, "type", out var type))
                {
                    warnings.Add($"操作 #{idx}: 缺少 type 字段，跳过");
                    idx++;
                    continue;
                }

                FillOperator? parsed = type switch
                {
                    "a" => TryParseScalar(op, warnings, idx),
                    "b" => TryParseRecommend(op, warnings, idx),
                    "c" => TryParseListExpand(op, warnings, idx),
                    "d" => TryParseGrid(op, true, warnings, idx),
                    "e" => TryParseGrid(op, false, warnings, idx),
                    "f" => TryParseParagraph(op, warnings, idx),
                    _ => null
                };

                if (parsed != null) result.Add(parsed);
                else warnings.Add($"操作 #{idx} (type={type}): 解析失败，跳过");
            }
            catch (Exception ex)
            {
                warnings.Add($"操作 #{idx}: 解析异常 {ex.Message}，跳过");
            }
            idx++;
        }
        return (result, warnings);
    }

    /// <summary>
    /// 兼容旧调用（无警告）
    /// </summary>
    public static List<FillOperator> Parse(JsonElement operations)
    {
        var (ops, _) = ParseWithWarnings(operations);
        return ops;
    }

    // ── Type a ──────────────────────────────────────────

    private static ScalarOp? TryParseScalar(JsonElement op, List<string> warnings, int idx)
    {
        if (!TryGetString(op, "entity", out var entity) ||
            !TryGetString(op, "property", out var property))
        {
            warnings.Add($"操作 #{idx} (type=a): 缺少 entity 或 property，跳过");
            return null;
        }
        var location = op.TryGetProperty("location", out var loc) ? DocLocation.FromJson(loc) : new DocLocation();
        if (!location.IsCell && !location.IsParagraph)
        {
            warnings.Add($"操作 #{idx} (type=a): location 无效，跳过");
            return null;
        }
        return new ScalarOp
        {
            Entity = entity,
            Property = property,
            Question = GetStringOrEmpty(op, "question"),
            Location = location,
            Format = GetOptionalString(op, "format"),
        };
    }

    // ── Type b ──────────────────────────────────────────

    private static RecommendOp? TryParseRecommend(JsonElement op, List<string> warnings, int idx)
    {
        if (!TryGetInt(op, "fund_index", out var fundIndex) ||
            !TryGetString(op, "property", out var property))
        {
            warnings.Add($"操作 #{idx} (type=b): 缺少 fund_index 或 property，跳过");
            return null;
        }
        var location = op.TryGetProperty("location", out var loc) ? DocLocation.FromJson(loc) : new DocLocation();
        if (!location.IsCell && !location.IsParagraph)
        {
            warnings.Add($"操作 #{idx} (type=b): location 无效，跳过");
            return null;
        }
        return new RecommendOp
        {
            FundIndex = fundIndex,
            Property = property,
            Question = GetStringOrEmpty(op, "question"),
            Table = GetStringOrEmpty(op, "table"),
            Location = location,
            Format = GetOptionalString(op, "format"),
        };
    }

    // ── Type c ──────────────────────────────────────────

    private static ListExpandOp? TryParseListExpand(JsonElement op, List<string> warnings, int idx)
    {
        if (!TryGetString(op, "entity", out var entity) ||
            !op.TryGetProperty("properties", out var propsEl) ||
            !op.TryGetProperty("ts", out var tsEl) ||
            !op.TryGetProperty("te", out var teEl))
        {
            warnings.Add($"操作 #{idx} (type=c): 缺少必要字段 (entity/properties/ts/te)，跳过");
            return null;
        }
        var properties = ParseStringDict(propsEl);
        if (properties.Count == 0)
        {
            warnings.Add($"操作 #{idx} (type=c): properties 为空，跳过");
            return null;
        }
        return new ListExpandOp
        {
            Entity = entity,
            Properties = properties,
            Ts = DocLocation.FromJson(tsEl),
            Te = DocLocation.FromJson(teEl),
            Formats = op.TryGetProperty("formats", out var fmts) && fmts.ValueKind == JsonValueKind.Object
                ? ParseStringDict(fmts) : null,
        };
    }

    // ── Type d/e ────────────────────────────────────────

    private static GridOp? TryParseGrid(JsonElement op, bool entityPerRow, List<string> warnings, int idx)
    {
        if (!TryGetString(op, "entity", out var entity) ||
            !op.TryGetProperty("properties", out var propsEl) ||
            !op.TryGetProperty("ts", out var tsEl) ||
            !op.TryGetProperty("te", out var teEl))
        {
            warnings.Add($"操作 #{idx} (type={(entityPerRow ? "d" : "e")}): 缺少必要字段，跳过");
            return null;
        }
        var properties = ParseStringDict(propsEl);
        if (properties.Count == 0)
        {
            warnings.Add($"操作 #{idx} (type={(entityPerRow ? "d" : "e")}): properties 为空，跳过");
            return null;
        }
        return new GridOp
        {
            Entity = entity,
            Properties = properties,
            Ts = DocLocation.FromJson(tsEl),
            Te = DocLocation.FromJson(teEl),
            EntityPerRow = entityPerRow,
            FilterBy = GetOptionalString(op, "filter_by"),
            Formats = op.TryGetProperty("formats", out var fmts) && fmts.ValueKind == JsonValueKind.Object
                ? ParseStringDict(fmts) : null,
        };
    }

    // ── Type f ──────────────────────────────────────────

    private static ParagraphOp? TryParseParagraph(JsonElement op, List<string> warnings, int idx)
    {
        var location = op.TryGetProperty("location", out var loc) ? DocLocation.FromJson(loc) : new DocLocation();
        if (!location.IsParagraph)
        {
            // Type f 也可能在表格中（entity+property），检查 cell
            if (!location.IsCell)
            {
                warnings.Add($"操作 #{idx} (type=f): location 无效（无 para_index），跳过");
                return null;
            }
        }
        return new ParagraphOp
        {
            Question = GetStringOrEmpty(op, "question"),
            Location = location,
            Entity = GetOptionalString(op, "entity"),
            Property = GetOptionalString(op, "property"),
            Format = GetOptionalString(op, "format"),
        };
    }

    // ── 工具方法 ────────────────────────────────────────

    private static bool TryGetString(JsonElement el, string key, out string value)
    {
        if (el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String)
        {
            value = v.GetString() ?? "";
            return true;
        }
        value = "";
        return false;
    }

    private static bool TryGetInt(JsonElement el, string key, out int value)
    {
        if (el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number)
        {
            value = v.GetInt32();
            return true;
        }
        value = 0;
        return false;
    }

    private static string GetStringOrEmpty(JsonElement el, string key) =>
        el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static string? GetOptionalString(JsonElement el, string key) =>
        el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static Dictionary<string, string> ParseStringDict(JsonElement el)
    {
        var dict = new Dictionary<string, string>();
        if (el.ValueKind != JsonValueKind.Object) return dict;
        foreach (var prop in el.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.String)
                dict[prop.Name] = prop.Value.GetString() ?? "";
        }
        return dict;
    }
}
