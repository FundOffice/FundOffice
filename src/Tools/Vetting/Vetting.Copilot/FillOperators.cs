using System.Text.Json;

namespace Vetting.Copilot;

/// <summary>
/// 单个位置（表格单元格或段落）
/// </summary>
public record Location
{
    // Table cell
    public int? Table { get; init; }   // table_index, null for paragraph
    public int? Row { get; init; }     // row_index, null for paragraph
    public int? Col { get; init; }     // col_index, null for paragraph

    // Paragraph
    public int? Para { get; init; }    // para_index, null for table cell

    public bool IsCell => Table.HasValue && Row.HasValue && Col.HasValue;
    public bool IsParagraph => Para.HasValue && !Table.HasValue;

    public static Location FromJson(JsonElement el)
    {
        return new Location
        {
            Table = el.TryGetProperty("table", out var t) && t.ValueKind == JsonValueKind.Number ? t.GetInt32() : null,
            Row = el.TryGetProperty("row", out var r) && r.ValueKind == JsonValueKind.Number ? r.GetInt32() : null,
            Col = el.TryGetProperty("col", out var c) && c.ValueKind == JsonValueKind.Number ? c.GetInt32() : null,
            Para = el.TryGetProperty("para", out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt32() : null,
        };
    }
}

/// <summary>
/// 表格范围（含 table_index + start/end）
/// </summary>
public record Range
{
    public required int Table { get; init; }
    public required Location Start { get; init; }
    public required Location End { get; init; }

    public static Range FromJson(JsonElement el)
    {
        return new Range
        {
            Table = el.TryGetProperty("table", out var t) && t.ValueKind == JsonValueKind.Number ? t.GetInt32() : 0,
            Start = el.TryGetProperty("start", out var s) ? Location.FromJson(s) : new Location(),
            End = el.TryGetProperty("end", out var e) ? Location.FromJson(e) : new Location(),
        };
    }

    /// <summary>生成绑定键: {Table}_{StartRow}_{StartCol}_{EndRow}_{EndCol}</summary>
    public string ToKey() => $"{Table}_{Start.Row ?? 0}_{Start.Col ?? 0}_{End.Row ?? 0}_{End.Col ?? 0}";
}

/// <summary>
/// 属性映射项：prop 为 null 表示该列未映射（占位）
/// </summary>
public record PropItem(string? Prop, string Header);

/// <summary>
/// 填充操作基类
/// </summary>
public abstract record FillOperator;

/// <summary>
/// Type a: 单值实体 LQRA（manager/credit/invest/risk）
/// </summary>
public record ScalarOp : FillOperator
{
    public required Location Location { get; init; }
    public required string Entity { get; init; }
    public required string Property { get; init; }
    public required string Question { get; init; }
    public string? Format { get; init; }
}

/// <summary>
/// Type b: 推荐产品表格（合并多个属性）
/// </summary>
public record RecommendOp : FillOperator
{
    public required Range Range { get; init; }
    public required int FundIndex { get; init; }     // 推荐产品索引（从 0 开始）
    public required string Table { get; init; }      // 表格描述
    public required List<RecommendPropItem> Props { get; init; }
}

/// <summary>
/// Type b 的属性项（绝对 row/col）
/// </summary>
public record RecommendPropItem
{
    public required int Row { get; init; }
    public required int Col { get; init; }
    public string? Prop { get; init; }
    public required string Header { get; init; }
}

/// <summary>
/// Type c: 列头列表，自动扩展行
/// </summary>
public record ListExpandOp : FillOperator
{
    public required Range Range { get; init; }
    public required string Entity { get; init; }
    public required List<PropItem> Properties { get; init; }
    public Dictionary<string, string>? Formats { get; init; }
}

/// <summary>
/// Type d: 行列头，一行一 entity（不扩展）
/// Type e: 行列头，一列一 entity（不扩展）
/// </summary>
public record GridOp : FillOperator
{
    public required Range Range { get; init; }
    public required string Entity { get; init; }
    public required List<PropItem> Properties { get; init; }
    /// <summary>true=Type d（一行一entity）, false=Type e（一列一entity）</summary>
    public required bool EntityPerRow { get; init; }
    /// <summary>按此属性匹配行头/列头（通常 "Year"）</summary>
    public string? FilterBy { get; init; }
    public Dictionary<string, string>? Formats { get; init; }
}

/// <summary>
/// Type z: 段落问题（散装或实体属性）
/// </summary>
public record ParagraphOp : FillOperator
{
    public required Location Location { get; init; }
    public required string Question { get; init; }
    public string? Entity { get; init; }
    public string? Property { get; init; }
    public string? Format { get; init; }
}

/// <summary>
/// Type g: 未知实体表格 — 无法映射到已知 entity 的表格，记录结构供调试和后续追加 entity
/// </summary>
public record UnknownTableOp : FillOperator
{
    /// <summary>表格用途描述（如"离职人员信息"、"实缴交易规模构成"），方便人工识别</summary>
    public required string Description { get; init; }
    public required Range Range { get; init; }
    public required List<PropItem> Properties { get; init; }
}

/// <summary>
/// 尽调所需附件文件（与 operations 平级的顶层 files 数组项）
/// </summary>
public record RequiredFile
{
    /// <summary>尽调文件中的序号（如资料清单行号、附件1/附件2 的数字），1-based</summary>
    public int Index { get; init; }
    /// <summary>原始文件要求（AI 从文档中提取的原文，如"营业执照正副本（盖公章）"）</summary>
    public required string Raw { get; init; }
    /// <summary>映射到的已有文件名（来自 user prompt 注入的 pred 文件名列表）；不匹配为 null</summary>
    public string? Map { get; init; }
    /// <summary>是否需要盖公章</summary>
    public required bool Stamped { get; init; }
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
                    "z" => TryParseParagraph(op, warnings, idx),
                    "g" => TryParseUnknown(op, warnings, idx),
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

    /// <summary>
    /// 从 AI 返回的 JSON files 数组解析为 RequiredFile 列表。
    /// 单项解析失败时跳过并记录警告。map 字段会与注入的已有文件名列表比对，不在列表内的降级为 null 并告警。
    /// </summary>
    /// <param name="files">AI 返回的 files 数组</param>
    /// <param name="availableNames">注入 user prompt 的已有文件名列表（来自 pred 目录）；为 null 时不校验</param>
    public static (List<RequiredFile> Files, List<string> Warnings) ParseFiles(JsonElement files, IReadOnlySet<string>? availableNames = null)
    {
        var result = new List<RequiredFile>();
        var warnings = new List<string>();
        if (files.ValueKind != JsonValueKind.Array) return (result, warnings);

        int idx = 0;
        foreach (var f in files.EnumerateArray())
        {
            try
            {
                var raw = GetStringOrEmpty(f, "raw");
                if (string.IsNullOrWhiteSpace(raw))
                {
                    warnings.Add($"files[{idx}]: raw 为空，跳过");
                    idx++;
                    continue;
                }

                string? map = null;
                if (f.TryGetProperty("map", out var mapEl))
                {
                    map = mapEl.ValueKind == JsonValueKind.String ? mapEl.GetString() : null;
                }
                // 校验 map 是否在已有文件名列表中
                if (!string.IsNullOrEmpty(map) && availableNames != null && !availableNames.Contains(map))
                {
                    warnings.Add($"files[{idx}]: map='{map}' 不在已有文件列表中，降级为 null");
                    map = null;
                }

                var index = f.TryGetProperty("index", out var idxEl) && idxEl.ValueKind == JsonValueKind.Number
                    ? idxEl.GetInt32() : idx + 1;

                var stamped = f.TryGetProperty("stamped", out var s) && s.ValueKind == JsonValueKind.True;
                result.Add(new RequiredFile { Index = index, Raw = raw, Map = map, Stamped = stamped });
            }
            catch (Exception ex)
            {
                warnings.Add($"files[{idx}]: 解析异常 {ex.Message}，跳过");
            }
            idx++;
        }
        return (result, warnings);
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

        var location = op.TryGetProperty("location", out var loc) ? Location.FromJson(loc) : new Location();
        if (!location.IsCell && !location.IsParagraph)
        {
            warnings.Add($"操作 #{idx} (type=a): location 无效，跳过");
            return null;
        }

        return new ScalarOp
        {
            Location = location,
            Entity = entity,
            Property = property,
            Question = GetStringOrEmpty(op, "question"),
            Format = GetOptionalString(op, "format"),
        };
    }

    // ── Type b ──────────────────────────────────────────

    private static RecommendOp? TryParseRecommend(JsonElement op, List<string> warnings, int idx)
    {
        if (!op.TryGetProperty("range", out var rangeEl))
        {
            warnings.Add($"操作 #{idx} (type=b): 缺少 range，跳过");
            return null;
        }

        var range = Range.FromJson(rangeEl);
        var table = GetStringOrEmpty(op, "table");
        var fundIndex = TryGetInt(op, "fund_index", out var fi) ? fi : 0;

        if (!op.TryGetProperty("props", out var propsEl) || propsEl.ValueKind != JsonValueKind.Array)
        {
            warnings.Add($"操作 #{idx} (type=b): 缺少 props 数组，跳过");
            return null;
        }

        var props = new List<RecommendPropItem>();
        foreach (var p in propsEl.EnumerateArray())
        {
            if (!TryGetInt(p, "row", out var row) || !TryGetInt(p, "col", out var col))
            {
                warnings.Add($"操作 #{idx} (type=b): props 项缺少 row 或 col，跳过该项");
                continue;
            }
            props.Add(new RecommendPropItem
            {
                Row = row,
                Col = col,
                Prop = GetOptionalString(p, "prop"),
                Header = GetStringOrEmpty(p, "header"),
            });
        }

        if (props.Count == 0)
        {
            warnings.Add($"操作 #{idx} (type=b): props 为空，跳过");
            return null;
        }

        return new RecommendOp
        {
            Range = range,
            FundIndex = fundIndex,
            Table = table,
            Props = props,
        };
    }

    // ── Type c ──────────────────────────────────────────

    private static ListExpandOp? TryParseListExpand(JsonElement op, List<string> warnings, int idx)
    {
        if (!TryGetString(op, "entity", out var entity))
        {
            warnings.Add($"操作 #{idx} (type=c): 缺少 entity，跳过");
            return null;
        }

        if (!op.TryGetProperty("range", out var rangeEl))
        {
            warnings.Add($"操作 #{idx} (type=c): 缺少 range，跳过");
            return null;
        }

        var range = Range.FromJson(rangeEl);

        if (!op.TryGetProperty("properties", out var propsEl))
        {
            warnings.Add($"操作 #{idx} (type=c): 缺少 properties，跳过");
            return null;
        }

        var properties = ParsePropItems(propsEl);
        if (properties.Count == 0)
        {
            warnings.Add($"操作 #{idx} (type=c): properties 为空，跳过");
            return null;
        }

        return new ListExpandOp
        {
            Range = range,
            Entity = entity,
            Properties = properties,
            Formats = op.TryGetProperty("formats", out var fmts) && fmts.ValueKind == JsonValueKind.Object
                ? ParseStringDict(fmts) : null,
        };
    }

    // ── Type d/e ────────────────────────────────────────

    private static GridOp? TryParseGrid(JsonElement op, bool entityPerRow, List<string> warnings, int idx)
    {
        if (!TryGetString(op, "entity", out var entity))
        {
            warnings.Add($"操作 #{idx} (type={(entityPerRow ? "d" : "e")}): 缺少 entity，跳过");
            return null;
        }

        if (!op.TryGetProperty("range", out var rangeEl))
        {
            warnings.Add($"操作 #{idx} (type={(entityPerRow ? "d" : "e")}): 缺少 range，跳过");
            return null;
        }

        var range = Range.FromJson(rangeEl);

        if (!op.TryGetProperty("properties", out var propsEl))
        {
            warnings.Add($"操作 #{idx} (type={(entityPerRow ? "d" : "e")}): 缺少 properties，跳过");
            return null;
        }

        var properties = ParsePropItems(propsEl);
        if (properties.Count == 0)
        {
            warnings.Add($"操作 #{idx} (type={(entityPerRow ? "d" : "e")}): properties 为空，跳过");
            return null;
        }

        return new GridOp
        {
            Range = range,
            Entity = entity,
            Properties = properties,
            EntityPerRow = entityPerRow,
            FilterBy = GetOptionalString(op, "filter_by"),
            Formats = op.TryGetProperty("formats", out var fmts) && fmts.ValueKind == JsonValueKind.Object
                ? ParseStringDict(fmts) : null,
        };
    }

    // ── Type z ──────────────────────────────────────────

    private static ParagraphOp? TryParseParagraph(JsonElement op, List<string> warnings, int idx)
    {
        var location = op.TryGetProperty("location", out var loc) ? Location.FromJson(loc) : new Location();
        if (!location.IsParagraph)
        {
            // Type z 也可能在表格中（entity+property），检查 cell
            if (!location.IsCell)
            {
                warnings.Add($"操作 #{idx} (type=z): location 无效（无 para），跳过");
                return null;
            }
        }

        return new ParagraphOp
        {
            Location = location,
            Question = GetStringOrEmpty(op, "question"),
            Entity = GetOptionalString(op, "entity"),
            Property = GetOptionalString(op, "property"),
            Format = GetOptionalString(op, "format"),
        };
    }

    // ── Type g ──────────────────────────────────────────

    private static UnknownTableOp? TryParseUnknown(JsonElement op, List<string> warnings, int idx)
    {
        if (!op.TryGetProperty("range", out var rangeEl))
        {
            warnings.Add($"操作 #{idx} (type=g): 缺少 range，跳过");
            return null;
        }

        var range = Range.FromJson(rangeEl);
        var properties = op.TryGetProperty("properties", out var propsEl)
            ? ParsePropItems(propsEl) : [];

        return new UnknownTableOp
        {
            Description = GetStringOrEmpty(op, "description"),
            Range = range,
            Properties = properties,
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

    public static Dictionary<string, string> ParseStringDict(JsonElement el)
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

    /// <summary>
    /// 解析 properties 字段，支持两种格式：
    /// 新格式（数组）：[{"prop": "Name", "header": "股东名称"}, {"prop": null, "header": "出资方式"}]
    /// 旧格式（字典）：{"Name": "股东名称", "Ratio": "持股比例"} — 自动转换，所有项都有 prop
    /// </summary>
    public static List<PropItem> ParsePropItems(JsonElement el)
    {
        var list = new List<PropItem>();
        if (el.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in el.EnumerateArray())
            {
                var header = GetStringOrEmpty(item, "header");
                if (string.IsNullOrEmpty(header)) continue;
                var prop = GetOptionalString(item, "prop");
                list.Add(new PropItem(prop, header));
            }
        }
        else if (el.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in el.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.String)
                    list.Add(new PropItem(prop.Name, prop.Value.GetString() ?? ""));
            }
        }
        return list;
    }
}

/// <summary>
/// 已有附件文件目录（files/vetting/pred/）读写助手。
/// 文件名会注入 AI user prompt，供 AI 在 files[].map 中引用。
/// </summary>
public static class PredFiles
{
    public static string Dir => Path.Combine("files", "vetting", "pred");

    /// <summary>读取 pred 目录下所有文件名（仅文件名，不含路径）</summary>
    public static string[] ListNames()
    {
        return Directory.Exists(Dir)
            ? new DirectoryInfo(Dir).GetFiles().Select(f => f.Name).OrderBy(n => n).ToArray()
            : Array.Empty<string>();
    }

    /// <summary>构造注入 user prompt 的文本块。无文件时明示为空，提示 map 全填 null。</summary>
    public static string BuildPromptSection()
    {
        var names = ListNames();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine();
        sb.AppendLine("\n## 已有附件文件列表（供 files[].map 引用）");
        if (names.Length == 0)
        {
            sb.AppendLine("（暂无已有附件文件。files 中每个 map 字段填 null，但仍须列出文档要求的全部附件 raw。）");
        }
        else
        {
            sb.AppendLine("对每个 files 项，若能与下列某个文件对应，则在 map 中填该文件名（必须逐字一致）；否则 map 填 null。");
            sb.AppendLine("当 stamped=true 时，应优先映射用印版本（如存在）；stamped=false 时映射普通版本。");
            foreach (var n in names) sb.AppendLine($"- {n}");
        }
        return sb.ToString();
    }

    /// <summary>复制文件到 pred 目录（覆盖），返回目标路径</summary>
    public static string CopyIn(string sourcePath)
    {
        Directory.CreateDirectory(Dir);
        var dest = Path.Combine(Dir, Path.GetFileName(sourcePath));
        File.Copy(sourcePath, dest, overwrite: true);
        return dest;
    }

    /// <summary>
    /// 为每个常用文件名创建空占位文件（扫描件 + 用印件），已存在的跳过。
    /// 便于测试 map 引用与 fill 复制流程。
    /// </summary>
    public static void CreatePlaceholders(IEnumerable<string> names, string ext = ".pdf")
    {
        Directory.CreateDirectory(Dir);
        foreach (var n in names)
        {
            foreach (var fn in new[] { n + ext, n + "_用印" + ext })
            {
                var p = Path.Combine(Dir, fn);
                if (!File.Exists(p)) File.Create(p).Dispose();
            }
        }
    }

    /// <summary>
    /// 按 {Index}.{Map} 把 pred 中已映射的附件复制到 final/附件 子目录。
    /// indexToMap: 附件序号 → pred 文件名。源文件缺失则记录告警跳过。
    /// </summary>
    public static void CopyMappedFiles(string finalDir, IEnumerable<KeyValuePair<int, string>> indexToMap, Action<string>? onLog = null)
    {
        var attachDir = Path.Combine(finalDir, "附件");
        Directory.CreateDirectory(attachDir);
        foreach (var kv in indexToMap)
        {
            var src = Path.Combine(Dir, kv.Value);
            if (!File.Exists(src)) { onLog?.Invoke($"附件 {kv.Key} 缺源文件: {kv.Value}"); continue; }
            var dest = Path.Combine(attachDir, $"{kv.Key}.{kv.Value}");
            try { File.Copy(src, dest, overwrite: true); onLog?.Invoke($"附件已复制: {dest}"); }
            catch (Exception ex) { onLog?.Invoke($"附件 {kv.Key} 复制失败: {ex.Message}"); }
        }
    }
}
