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
    public required List<PropItem> Properties { get; init; }
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
    public required List<PropItem> Properties { get; init; }
    public required DocLocation Ts { get; init; }
    public required DocLocation Te { get; init; }
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
    public required string Question { get; init; }
    public required DocLocation Location { get; init; }
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
    /// <summary>列头文本 → 属性名占位（属性名用列头原文，后续追加 entity 时再映射）</summary>
    public required List<PropItem> Properties { get; init; }
    public required DocLocation Ts { get; init; }
    public required DocLocation Te { get; init; }
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
        var properties = ParsePropItems(propsEl);
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
        var properties = ParsePropItems(propsEl);
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

    // ── Type z ──────────────────────────────────────────

    private static ParagraphOp? TryParseParagraph(JsonElement op, List<string> warnings, int idx)
    {
        var location = op.TryGetProperty("location", out var loc) ? DocLocation.FromJson(loc) : new DocLocation();
        if (!location.IsParagraph)
        {
            // Type z 也可能在表格中（entity+property），检查 cell
            if (!location.IsCell)
            {
                warnings.Add($"操作 #{idx} (type=z): location 无效（无 para_index），跳过");
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

    // ── Type g ──────────────────────────────────────────

    private static UnknownTableOp? TryParseUnknown(JsonElement op, List<string> warnings, int idx)
    {
        if (!op.TryGetProperty("ts", out var tsEl) ||
            !op.TryGetProperty("te", out var teEl))
        {
            warnings.Add($"操作 #{idx} (type=g): 缺少 ts 或 te，跳过");
            return null;
        }
        var properties = op.TryGetProperty("properties", out var propsEl)
            ? ParsePropItems(propsEl) : [];
        return new UnknownTableOp
        {
            Description = GetStringOrEmpty(op, "description"),
            Properties = properties,
            Ts = DocLocation.FromJson(tsEl),
            Te = DocLocation.FromJson(teEl),
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
