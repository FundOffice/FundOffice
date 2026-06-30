using System.Text.Json;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using Vetting.Copilot.Data;
using Vetting.Copilot.Models.Info;

namespace Vetting.Copilot;

/// <summary>
/// docx 文档操作工具 — 给 AI agent 调用
/// </summary>
public static class DocOps
{
    /// <summary>
    /// 以只读方式打开 Word 文档。若文件被占用（如 Word 正在编辑），自动复制到临时文件后打开。
    /// 返回 (doc, tempPath)：tempPath 非 null 时需在 doc Dispose 后删除。
    /// </summary>
    private static (WordprocessingDocument doc, string? tempPath) OpenReadOnly(string filePath)
    {
        try
        {
            var doc = WordprocessingDocument.Open(filePath, false, new OpenSettings { AutoSave = false });
            return (doc, null);
        }
        catch (IOException)
        {
            // 文件被占用，复制到临时文件后打开
            var tempPath = Path.Combine(Path.GetTempPath(), $"vetting_{Guid.NewGuid():N}{Path.GetExtension(filePath)}");
            File.Copy(filePath, tempPath, overwrite: true);
            var doc = WordprocessingDocument.Open(tempPath, false, new OpenSettings { AutoSave = false });
            return (doc, tempPath);
        }
    }

    /// <summary>释放只读文档，并删除临时文件（若有）</summary>
    private static void DisposeReadOnly(WordprocessingDocument doc, string? tempPath)
    {
        doc.Dispose();
        if (tempPath != null)
        {
            try { File.Delete(tempPath); } catch { }
        }
    }

    /// <summary>分析文档结构：章节标题列表、表格类型检测</summary>
    public static string AnalyzeStructure(string filePath)
    {
        var (doc, tempPath) = OpenReadOnly(filePath);
        try
        {
        var body = doc.MainDocumentPart!.Document.Body!;
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("=== HEADINGS ===");
        int pi = 0;
        foreach (var p in body.Elements<Paragraph>())
        {
            var style = GetParagraphStyle(p);
            if (style.Length > 0)
                sb.AppendLine($"  P[{pi}] [{style}] {p.InnerText}");
            pi++;
        }

        sb.AppendLine("\n=== TABLES ===");
        int ti = 0;
        foreach (var table in body.Elements<Table>())
        {
            var rows = table.Elements<TableRow>().ToList();
            var cols = rows.FirstOrDefault()?.Elements<TableCell>().Count() ?? 0;
            var firstRowText = rows.FirstOrDefault()?.InnerText?.Trim() ?? "";
            if (firstRowText.Length > 60) firstRowText = firstRowText[..60] + "...";
            var hasHeader = DetectHeaderRow(rows.FirstOrDefault());
            sb.AppendLine($"  T[{ti}] {rows.Count}x{cols}{(hasHeader ? " [has header]" : "")} first_row=\"{firstRowText}\"");
            ti++;
        }

        sb.AppendLine("\n=== PARAGRAPH STYLES ===");
        var styleCounts = new Dictionary<string, int>();
        foreach (var p in body.Elements<Paragraph>())
        {
            var style = GetParagraphStyle(p);
            var key = style.Length > 0 ? style : "(body)";
            styleCounts[key] = styleCounts.GetValueOrDefault(key) + 1;
        }
        foreach (var kv in styleCounts.OrderByDescending(x => x.Value))
            sb.AppendLine($"  {kv.Key}: {kv.Value}");

        return sb.ToString();
        }
        finally { DisposeReadOnly(doc, tempPath); }
    }

    /// <summary>解析完整文档内容（段落和表格按文档顺序，含索引和内容）</summary>
    public static string ParseDocument(string filePath)
    {
        var (doc, tempPath) = OpenReadOnly(filePath);
        try
        {
        var body = doc.MainDocumentPart!.Document.Body!;
        var sb = new System.Text.StringBuilder();

        int pi = 0, ti = 0;
        foreach (var element in body.ChildElements)
        {
            switch (element)
            {
                case Paragraph p:
                    var text = p.InnerText;
                    var style = GetParagraphStyle(p);
                    var prefix = style.Length > 0 ? $"[{style}] " : "";
                    sb.AppendLine($"P[{pi}] {prefix}{(string.IsNullOrWhiteSpace(text) ? "(EMPTY)" : text)}");
                    pi++;
                    break;
                case Table table:
                    var rows = table.Elements<TableRow>().ToList();
                    bool hasHeader = DetectHeaderRow(rows.FirstOrDefault());
                    sb.AppendLine($"T[{ti}] ({rows.Count} rows){(hasHeader ? " [has header]" : "")}");
                    for (int ri = 0; ri < rows.Count; ri++)
                    {
                        var row = rows[ri];
                        int ci = 0;
                        foreach (var cell in row.Elements<TableCell>())
                        {
                            var cellText = cell.InnerText.Replace("\n", "\\n");
                            var (rs, cs) = GetMergeInfo(cell);
                            var merge = cs > 1 ? $"(span={cs})" : rs == 0 ? "(vcont)" : "";
                            sb.AppendLine($"  [{ri},{ci}]{merge} {(string.IsNullOrWhiteSpace(cellText) ? "(EMPTY)" : cellText)}");
                            ci++;
                        }
                    }
                    ti++;
                    break;
            }
        }
        return sb.ToString();
        }
        finally { DisposeReadOnly(doc, tempPath); }
    }

    /// <summary>读取文档段落（含索引、样式标记、空段落标记）。可选 start/end 分段读取</summary>
    public static string ReadParagraphs(string filePath, int? start = null, int? end = null)
    {
        var (doc, tempPath) = OpenReadOnly(filePath);
        try
        {
        var body = doc.MainDocumentPart!.Document.Body!;
        var paragraphs = body.Elements<Paragraph>().ToList();

        int from = start ?? 0;
        int to = Math.Min(end ?? paragraphs.Count, paragraphs.Count);
        if (from < 0) from = 0;
        if (to > paragraphs.Count) to = paragraphs.Count;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Total: {paragraphs.Count} paragraphs (showing {from}..{to - 1})");
        for (int i = from; i < to; i++)
        {
            var p = paragraphs[i];
            var text = p.InnerText;
            var style = GetParagraphStyle(p);
            var prefix = style.Length > 0 ? $"[{style}] " : "";
            sb.AppendLine(i <= 999
                ? $"P[{i,3}] {prefix}{(string.IsNullOrWhiteSpace(text) ? "(EMPTY)" : text)}"
                : $"P[{i}] {prefix}{(string.IsNullOrWhiteSpace(text) ? "(EMPTY)" : text)}");
        }
        return sb.ToString();
        }
        finally { DisposeReadOnly(doc, tempPath); }
    }

    /// <summary>读取指定表格的结构和内容（含表头检测）。可选 startRow/endRow 分行读取</summary>
    public static string ReadTable(string filePath, int tableIndex, int? startRow = null, int? endRow = null)
    {
        var (doc, tempPath) = OpenReadOnly(filePath);
        try
        {
        var body = doc.MainDocumentPart!.Document.Body!;
        var tables = body.Elements<Table>().ToList();
        if (tableIndex < 0 || tableIndex >= tables.Count)
            return $"Error: table index {tableIndex} out of range (0-{tables.Count - 1})";

        var table = tables[tableIndex];
        var rows = table.Elements<TableRow>().ToList();
        int totalRows = rows.Count;
        bool hasHeader = DetectHeaderRow(rows.FirstOrDefault());

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Table {tableIndex} ({totalRows} rows){(hasHeader ? " [has header]" : "")}");

        int from = startRow ?? 0;
        int to = Math.Min(endRow ?? totalRows, totalRows);
        if (from < 0) from = 0;
        if (to > totalRows) to = totalRows;

        for (int ri = from; ri < to; ri++)
        {
            var row = rows[ri];
            var rowLabel = $"{ri,2}";
            int ci = 0;
            foreach (var cell in row.Elements<TableCell>())
            {
                var text = cell.InnerText.Replace("\n", "\\n");
                var (rs, cs) = GetMergeInfo(cell);
                var merge = cs > 1 ? $"(span={cs})" : rs == 0 ? "(vcont)" : "";
                sb.AppendLine($"  [{rowLabel},{ci,2}]{merge} {(string.IsNullOrWhiteSpace(text) ? "(EMPTY)" : text)}");
                ci++;
            }
        }
        return sb.ToString();
        }
        finally { DisposeReadOnly(doc, tempPath); }
    }

    /// <summary>读取已打开的表格元素的结构和内容（含表头检测）</summary>
    public static string ReadTableFromElement(Table table, int tableIndex)
    {
        var rows = table.Elements<TableRow>().ToList();
        int totalRows = rows.Count;
        bool hasHeader = DetectHeaderRow(rows.FirstOrDefault());

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Table {tableIndex} ({totalRows} rows){(hasHeader ? " [has header]" : "")}");

        for (int ri = 0; ri < totalRows; ri++)
        {
            var row = rows[ri];
            var rowLabel = $"{ri,2}";
            int ci = 0;
            foreach (var cell in row.Elements<TableCell>())
            {
                var text = cell.InnerText.Replace("\n", "\\n");
                var (rs, cs) = GetMergeInfo(cell);
                var merge = cs > 1 ? $"(span={cs})" : rs == 0 ? "(vcont)" : "";
                sb.AppendLine($"  [{rowLabel},{ci,2}]{merge} {(string.IsNullOrWhiteSpace(text) ? "(EMPTY)" : text)}");
                ci++;
            }
        }
        return sb.ToString();
    }

    /// <summary>读取已打开的段落元素列表</summary>
    public static string ReadParagraphsFromElements(IReadOnlyList<Paragraph> paragraphs)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Total: {paragraphs.Count} paragraphs");
        for (int i = 0; i < paragraphs.Count; i++)
        {
            var p = paragraphs[i];
            var text = p.InnerText;
            var style = GetParagraphStyle(p);
            var prefix = style.Length > 0 ? $"[{style}] " : "";
            sb.AppendLine(i <= 999
                ? $"P[{i,3}] {prefix}{(string.IsNullOrWhiteSpace(text) ? "(EMPTY)" : text)}"
                : $"P[{i}] {prefix}{(string.IsNullOrWhiteSpace(text) ? "(EMPTY)" : text)}");
        }
        return sb.ToString();
    }

    /// <summary>获取文档表格数量</summary>
    public static int GetTableCount(string filePath)
    {
        var (doc, tempPath) = OpenReadOnly(filePath);
        try { return doc.MainDocumentPart!.Document.Body!.Elements<Table>().Count(); }
        finally { DisposeReadOnly(doc, tempPath); }
    }

    /// <summary>获取文档段落数量</summary>
    public static int GetParagraphCount(string filePath)
    {
        var (doc, tempPath) = OpenReadOnly(filePath);
        try { return doc.MainDocumentPart!.Document.Body!.Elements<Paragraph>().Count(); }
        finally { DisposeReadOnly(doc, tempPath); }
    }

    /// <summary>设置表格单元格文本</summary>
    public static void SetCellText(string filePath, int tableIndex, int rowIndex, int colIndex, string text)
    {
        using var doc = WordprocessingDocument.Open(filePath, true);
        var body = doc.MainDocumentPart!.Document.Body!;
        var table = body.Elements<Table>().ElementAt(tableIndex);
        var row = table.Elements<TableRow>().ElementAt(rowIndex);
        var cell = row.Elements<TableCell>().ElementAt(colIndex);
        SetCellContent(cell, text);
        doc.MainDocumentPart.Document.Save();
    }

    /// <summary>设置段落文本</summary>
    public static void SetParagraphText(string filePath, int paraIndex, string text)
    {
        using var doc = WordprocessingDocument.Open(filePath, true);
        var body = doc.MainDocumentPart!.Document.Body!;
        var para = body.Elements<Paragraph>().ElementAt(paraIndex);
        SetParaContent(para, text);
        doc.MainDocumentPart.Document.Save();
    }

    /// <summary>批量写入：单次打开文档，应用所有 set_cell/set_paragraph 操作，一次保存</summary>
    public static void BatchWrite(string filePath, IEnumerable<(string tool, Dictionary<string, JsonElement> input)> operations)
    {
        using var doc = WordprocessingDocument.Open(filePath, true);
        var body = doc.MainDocumentPart!.Document.Body!;

        foreach (var (tool, input) in operations)
        {
            try
            {
                switch (tool)
                {
                    case "set_cell":
                        var table = body.Elements<Table>().ElementAt(GetInt(input, "table_index"));
                        var row = table.Elements<TableRow>().ElementAt(GetInt(input, "row_index"));
                        var cell = row.Elements<TableCell>().ElementAt(GetInt(input, "col_index"));
                        SetCellContent(cell, GetString(input, "text"));
                        break;
                    case "set_paragraph":
                        var para = body.Elements<Paragraph>().ElementAt(GetInt(input, "para_index"));
                        SetParaContent(para, GetString(input, "text"));
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DocOps.BatchWrite error: {ex}");
            }
        }
        doc.MainDocumentPart.Document.Save();
    }

    internal static int GetInt(Dictionary<string, JsonElement> input, string snakeKey)
    {
        if (input.TryGetValue(snakeKey, out var v)) return v.GetInt32();
        var camel = string.Concat(snakeKey.Split('_').Select((s, i) => i == 0 ? s : char.ToUpper(s[0]) + s[1..]));
        if (input.TryGetValue(camel, out v)) return v.GetInt32();
        throw new KeyNotFoundException($"key '{snakeKey}' not found in: {string.Join(", ", input.Keys)}");
    }

    internal static string GetString(Dictionary<string, JsonElement> input, string snakeKey)
    {
        if (input.TryGetValue(snakeKey, out var v)) return v.GetString() ?? "";
        var camel = string.Concat(snakeKey.Split('_').Select((s, i) => i == 0 ? s : char.ToUpper(s[0]) + s[1..]));
        if (input.TryGetValue(camel, out v)) return v.GetString() ?? "";
        throw new KeyNotFoundException($"key '{snakeKey}' not found in: {string.Join(", ", input.Keys)}");
    }

    /// <summary>获取合并信息 (rowSpan, colSpan)</summary>
    public static (int rowSpan, int colSpan) GetMergeInfo(TableCell cell)
    {
        var tcPr = cell.TableCellProperties;
        var gridSpan = tcPr?.GridSpan?.Val?.Value;
        int colSpan = gridSpan ?? 1;

        var vMerge = tcPr?.VerticalMerge;
        if (vMerge?.Val?.Value == MergedCellValues.Continue)
            return (0, colSpan);
        return (1, colSpan);
    }

    /// <summary>
    /// 按 operator 列表直接填充文档（无占位符，直接写值）
    /// </summary>
    public static void Fill(string templatePath, string outputPath, IReadOnlyList<FillOperator> operators, DataResolver resolver)
    {
        var outDir = Path.GetDirectoryName(outputPath)!;
        Directory.CreateDirectory(outDir);
        File.Copy(templatePath, outputPath, true);

        using var doc = WordprocessingDocument.Open(outputPath, true);
        var body = doc.MainDocumentPart!.Document.Body!;
        var tables = body.Elements<Table>().ToList();
        var paragraphs = body.Elements<Paragraph>().ToList();

        // 累计行偏移：同一表格内 Type c 扩展行后，后续操作的 row 需要加上偏移
        var tableOffsets = new Dictionary<int, int>();

        foreach (var op in operators)
        {
            try
            {
                switch (op)
                {
                    case ScalarOp scalar:
                        FillScalar(tables, paragraphs, scalar, resolver, tableOffsets);
                        break;
                    case RecommendOp rec:
                        FillRecommend(tables, rec, resolver, tableOffsets);
                        break;
                    case ListExpandOp list:
                        FillListExpand(tables, list, resolver, tableOffsets);
                        break;
                    case GridOp grid:
                        FillGrid(tables, grid, resolver, tableOffsets);
                        break;
                    case ParagraphOp para:
                        FillParagraph(paragraphs, para, resolver);
                        break;
                    case UnknownTableOp unknown:
                        // Type g: 未知实体表格，无数据可填，记录日志供调试
                        System.Diagnostics.Debug.WriteLine($"[Type g] 跳过未知表格 T[{unknown.Range.Table}]: {unknown.Description}");
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DocOps.Fill error ({op}): {ex}");
            }
        }

        // 处理图片占位符
        var imgCount = ImagePlaceholderRegex.Matches(body.InnerText).Count;
        if (imgCount > 0)
            System.Diagnostics.Debug.WriteLine($"[图片处理] 发现 {imgCount} 个图片占位符");
        ProcessImagePlaceholders(doc.MainDocumentPart, body);

        doc.MainDocumentPart.Document.Save();
    }

    #region Fill Implementations

    private static void FillScalar(List<Table> tables, List<Paragraph> paragraphs, ScalarOp op, DataResolver resolver, Dictionary<int, int> offsets)
    {
        var value = resolver.Resolve(op.Entity, op.Property, op.Format);
        if (op.Location.IsCell && op.Location.Table.HasValue && op.Location.Row.HasValue && op.Location.Col.HasValue)
        {
            var tableIdx = op.Location.Table.Value;
            var rowIdx = op.Location.Row.Value + offsets.GetValueOrDefault(tableIdx);
            var colIdx = op.Location.Col.Value;
            var cell = GetCell(tables, tableIdx, rowIdx, colIdx);
            if (cell != null) SetCellContent(cell, value);
        }
        else if (op.Location.IsParagraph && op.Location.Para.HasValue)
        {
            var para = paragraphs.ElementAtOrDefault(op.Location.Para.Value);
            if (para != null)
            {
                // 目标段落已有内容（如问题文本），在其后插入新段落写入答案，避免覆盖
                // 但如果段落少于6个字，视为空段落，直接覆盖
                if (!string.IsNullOrWhiteSpace(para.InnerText) && para.InnerText.Length >= 6)
                    para = InsertParagraphAfter(para);
                SetParaContent(para, value);
            }
        }
    }

    private static void FillRecommend(List<Table> tables, RecommendOp op, DataResolver resolver, Dictionary<int, int> offsets)
    {
        var tableIdx = op.Range.Table;
        var offset = offsets.GetValueOrDefault(tableIdx);

        foreach (var prop in op.Props)
        {
            if (prop.Prop == null) continue;

            var rowIdx = prop.Row + offset;
            var colIdx = prop.Col;
            var cell = GetCell(tables, tableIdx, rowIdx, colIdx);
            if (cell == null) continue;

            var value = resolver.ResolveRecommendForFund(op.FundIndex, op.Range, prop.Prop, prop.Header);
            if (!string.IsNullOrEmpty(value))
                SetCellContent(cell, value);
        }
    }

    private static void FillListExpand(List<Table> tables, ListExpandOp op, DataResolver resolver, Dictionary<int, int> offsets)
    {
        var tableIdx = op.Range.Table;
        var table = tables.ElementAtOrDefault(tableIdx);
        if (table == null) return;

        var items = resolver.GetList(op.Entity);
        if (items.Length == 0) return;

        var rows = table.Elements<TableRow>().ToList();
        int offset = offsets.GetValueOrDefault(tableIdx);
        int startRow = op.Range.Start.Row ?? 0;
        int endRow = op.Range.End.Row ?? 0;
        int availableRows = endRow - startRow + 1;
        int preExpandOffset = offset;  // offset before this op's expansion

        // 需要扩展行
        if (items.Length > availableRows)
        {
            int extraCount = items.Length - availableRows;
            var templateRow = rows[endRow + offset];
            var insertAfter = rows[endRow + offset];

            for (int i = 0; i < extraCount; i++)
            {
                var newRow = (TableRow)templateRow.CloneNode(true);
                // 清除克隆行中合并单元格的 vMerge 标记（首行应设为 Restart）
                foreach (var cell in newRow.Elements<TableCell>())
                {
                    var vMerge = cell.TableCellProperties?.VerticalMerge;
                    if (vMerge != null)
                    {
                        // 克隆行不继承跨行合并
                        vMerge.Val = null; // Restart
                    }
                }
                insertAfter = (TableRow)insertAfter.InsertAfterSelf(newRow);
            }

            offsets[tableIdx] = offset + extraCount;
        }

        // 填充数据
        // 重新获取行列表（可能已插入新行）
        rows = table.Elements<TableRow>().ToList();

        for (int i = 0; i < items.Length; i++)
        {
            int rowIdx = startRow + i + preExpandOffset;
            var row = rows.ElementAtOrDefault(rowIdx);
            if (row == null) continue;

            var cells = row.Elements<TableCell>().ToList();
            for (int j = 0; j < op.Properties.Count; j++)
            {
                var propName = op.Properties[j].Prop;
                if (propName == null) continue; // 跳过未映射列

                // 使用绝对列号（从 properties 中的 col 字段）
                int colIdx = op.Properties[j].Col ?? (op.Range.Start.Col ?? 0) + j;
                var cell = cells.ElementAtOrDefault(colIdx);
                if (cell == null) continue;

                var value = items[i].TryGetValue(propName, out var v) ? v : "";
                if (!string.IsNullOrEmpty(value) && op.Formats != null && op.Formats.TryGetValue(propName, out var fmt))
                    value = ResolveHelper.ToString(value, fmt);
                SetCellContent(cell, value);
            }
        }
    }

    private static void FillGrid(List<Table> tables, GridOp op, DataResolver resolver, Dictionary<int, int> offsets)
    {
        var tableIdx = op.Range.Table;
        var table = tables.ElementAtOrDefault(tableIdx);
        if (table == null) return;

        var items = resolver.GetList(op.Entity);
        if (items.Length == 0) return;

        int offset = offsets.GetValueOrDefault(tableIdx);
        var rows = table.Elements<TableRow>().ToList();

        int startRow = op.Properties.FirstOrDefault()?.Row ?? op.Range.Start.Row ?? 0;
        int endRow = op.Properties.LastOrDefault()?.Row ?? op.Range.End.Row ?? 0;
        int startCol = op.Properties.FirstOrDefault()?.Col ?? op.Range.Start.Col ?? 0;
        int endCol = op.Properties.LastOrDefault()?.Col ?? op.Range.End.Col ?? 0;

        // 检测 Type d 坐标错误：所有 properties 的 col 都为 0（行头列）
        // 这是 AI 解析错误，跳过该操作避免覆盖行头
        // 注意：Type e 的 properties col=0 是正常的（指向行头列用于定位属性），不影响填充
        if (op.EntityPerRow)
        {
            var allColZero = op.Properties.All(p => p.Col == 0);
            if (allColZero && op.Properties.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[跳过] Type d T[{tableIdx}] 所有 properties 的 col=0（行头列），请检查 AI 解析结果");
                return;
            }
        }

        if (op.EntityPerRow)
        {
            // Type d: 一行一 entity，列头是属性
            for (int ri = startRow; ri <= endRow; ri++)
            {
                var row = rows.ElementAtOrDefault(ri + offset);
                if (row == null) continue;

                var cells = row.Elements<TableCell>().ToList();
                // 行头列：第一个属性列的左边一列
                int firstPropCol = op.Properties.FirstOrDefault()?.Col ?? startCol;
                var rowHeader = cells.ElementAtOrDefault(firstPropCol - 1)?.InnerText?.Trim() ?? "";

                Dictionary<string, string>? matched = null;
                if (!string.IsNullOrEmpty(op.FilterBy))
                {
                    matched = items.FirstOrDefault(item =>
                        item.TryGetValue(op.FilterBy, out var val) && val?.Trim() == rowHeader);
                }
                matched ??= items.ElementAtOrDefault(ri - startRow);

                if (matched == null) continue;

                for (int j = 0; j < op.Properties.Count; j++)
                {
                    var propName = op.Properties[j].Prop;
                    if (propName == null) continue; // 跳过未映射列

                    // 跳过用于 FilterBy 的属性列（行头列，只用于匹配行，不填充）
                    if (!string.IsNullOrEmpty(op.FilterBy) && propName == op.FilterBy)
                        continue;

                    // 使用绝对列号（从 properties 中的 col 字段）
                    int colIdx = op.Properties[j].Col ?? startCol + j;
                    var cell = cells.ElementAtOrDefault(colIdx);
                    if (cell == null) continue;

                    var value = matched.TryGetValue(propName, out var v) ? v : "";
                    if (!string.IsNullOrEmpty(value) && op.Formats != null && op.Formats.TryGetValue(propName, out var fmt))
                        value = ResolveHelper.ToString(value, fmt);
                    SetCellContent(cell, value);
                }
            }
        }
        else
        {
            // Type e: 一列一 entity，行头是属性
            // 列头行：第一个属性行的上面一行
            int firstPropRow = op.Properties.FirstOrDefault()?.Row ?? startRow;
            var headerRow = rows.ElementAtOrDefault(firstPropRow - 1 + offset);

            // 从 properties 中获取所有需要填充的列号
            var distinctCols = op.Properties
                .Where(p => p.Col.HasValue && p.Col > 0)  // 排除 col=0（行头列）
                .Select(p => p.Col!.Value)
                .Distinct()
                .OrderBy(c => c)
                .ToList();
            // 如果 properties 没有提供有效的数据列，回退到 range 的列范围
            if (distinctCols.Count == 0)
            {
                int rangeStartCol = op.Range.Start.Col ?? 0;
                int rangeEndCol = op.Range.End.Col ?? 0;
                for (int c = rangeStartCol; c <= rangeEndCol; c++) distinctCols.Add(c);
            }

            foreach (var ci in distinctCols)
            {
                var headerCells = headerRow?.Elements<TableCell>().ToList();
                var colHeader = headerCells?.ElementAtOrDefault(ci)?.InnerText?.Trim() ?? "";

                Dictionary<string, string>? matched = null;
                if (!string.IsNullOrEmpty(op.FilterBy))
                {
                    matched = items.FirstOrDefault(item =>
                        item.TryGetValue(op.FilterBy, out var val) && val?.Trim() == colHeader);
                }
                matched ??= items.ElementAtOrDefault(ci - startCol);

                if (matched == null) continue;

                for (int i = 0; i < op.Properties.Count; i++)
                {
                    var propName = op.Properties[i].Prop;
                    if (propName == null) continue; // 跳过未映射行

                    // 跳过用于 FilterBy 的属性（列头对应的属性，只用于匹配列，不填充）
                    if (!string.IsNullOrEmpty(op.FilterBy) && propName == op.FilterBy)
                        continue;

                    // 使用绝对行号（从 properties 中的 row 字段）+ 偏移
                    int rowIdx = (op.Properties[i].Row ?? startRow + i) + offset;
                    var row = rows.ElementAtOrDefault(rowIdx);
                    if (row == null) continue;

                    var cell = row.Elements<TableCell>().ElementAtOrDefault(ci);
                    if (cell == null) continue;

                    var value = matched.TryGetValue(propName, out var v) ? v : "";
                    if (!string.IsNullOrEmpty(value) && op.Formats != null && op.Formats.TryGetValue(propName, out var fmt))
                        value = ResolveHelper.ToString(value, fmt);
                    SetCellContent(cell, value);
                }
            }
        }
    }

    private static void FillParagraph(List<Paragraph> paragraphs, ParagraphOp op, DataResolver resolver)
    {
        if (!op.Location.IsParagraph) return;
        var para = op.Location.Para.HasValue ? paragraphs.ElementAtOrDefault(op.Location.Para.Value) : null;
        if (para == null) return;

        string value;
        if (op.Entity != null && op.Property != null)
            value = resolver.Resolve(op.Entity, op.Property, op.Format);
        else
            value = resolver.ResolvePlaceholders(resolver.GetAnswerByQuestion(op.Question));

        if (!string.IsNullOrEmpty(value))
        {
            // 目标段落已有内容（如问题文本），在其后插入新段落写入答案，避免覆盖
            // 但如果段落少于6个字，视为空段落，直接覆盖
            if (!string.IsNullOrWhiteSpace(para.InnerText) && para.InnerText.Length >= 6)
                para = InsertParagraphAfter(para);
            SetParaContent(para, value);
        }
    }

    private static TableCell? GetCell(List<Table> tables, int tableIndex, int rowIndex, int colIndex)
    {
        var table = tables.ElementAtOrDefault(tableIndex);
        if (table == null) return null;
        var row = table.Elements<TableRow>().ElementAtOrDefault(rowIndex);
        if (row == null) return null;
        return row.Elements<TableCell>().ElementAtOrDefault(colIndex);
    }

    #endregion

    #region Private Helpers

    private static string GetParagraphStyle(Paragraph p)
    {
        var pPr = p.ParagraphProperties;
        var styleId = pPr?.ParagraphStyleId?.Val?.Value;
        if (styleId != null)
        {
            if (styleId.StartsWith("Heading", StringComparison.OrdinalIgnoreCase))
                return styleId;
            if (styleId is "Title" or "Subtitle")
                return styleId;
        }
        var outlineLvl = pPr?.OutlineLevel?.Val?.Value;
        if (outlineLvl.HasValue && outlineLvl.Value < 9)
            return $"H{outlineLvl.Value + 1}";
        return "";
    }

    private static bool DetectHeaderRow(TableRow? row)
    {
        if (row == null) return false;
        return row.Elements<TableCell>().Any(c =>
            c.Descendants<Run>().Any(r => r.RunProperties?.Bold != null) ||
            c.TableCellProperties?.Shading?.Fill?.Value != null);
    }

    private static void SetCellContent(TableCell cell, string text)
    {
        var para = cell.Elements<Paragraph>().FirstOrDefault();
        if (para == null) return;

        var runs = para.Elements<Run>().ToList();
        if (runs.Count > 0)
        {
            var rPr = runs[0].RunProperties?.CloneNode(true) as RunProperties;
            foreach (var r in runs) r.Remove();
            foreach (var fc in para.Elements<FieldChar>().ToList()) fc.Remove();
            foreach (var fr in para.Elements<FieldCode>().ToList()) fr.Remove();
            var newRun = new Run();
            if (rPr != null) newRun.RunProperties = rPr;
            newRun.AppendChild(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
            para.AppendChild(newRun);
        }
        else
        {
            para.AppendChild(new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
        }
    }

    private static Paragraph InsertParagraphAfter(Paragraph target)
    {
        // 不继承样式：避免继承编号/缩进导致答案段落格式异常
        var newPara = new Paragraph();
        target.InsertAfterSelf(newPara);
        return newPara;
    }

    private static void SetParaContent(Paragraph para, string text)
    {
        var runs = para.Elements<Run>().ToList();
        if (runs.Count > 0)
        {
            var rPr = runs[0].RunProperties?.CloneNode(true) as RunProperties;
            foreach (var r in runs) r.Remove();
            foreach (var fc in para.Elements<FieldChar>().ToList()) fc.Remove();
            foreach (var fr in para.Elements<FieldCode>().ToList()) fr.Remove();
            var newRun = new Run();
            if (rPr != null) newRun.RunProperties = rPr;
            newRun.AppendChild(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
            para.AppendChild(newRun);
        }
        else
        {
            para.AppendChild(new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
        }
    }

    #endregion

    #region Image Processing

    /// <summary>匹配 [img#id] 格式的正则表达式</summary>
    private static readonly Regex ImagePlaceholderRegex = new(@"\[img#(\d+)\]", RegexOptions.Compiled);

    /// <summary>
    /// 处理文档中的图片占位符 [img#id]，在原位置替换为实际图片
    /// </summary>
    private static void ProcessImagePlaceholders(MainDocumentPart mainPart, Body body)
    {
        // 预加载所有图片元数据
        var photoCache = new Dictionary<int, PhotoMap>();
        using (var db = new VettingDbContext())
        {
            foreach (var photo in db.PhotoMaps.FindAll())
                photoCache[photo.Id] = photo;
        }

        // 遍历所有段落
        foreach (var para in body.Elements<Paragraph>())
        {
            ProcessParagraphImages(mainPart, para, photoCache);
        }

        // 处理表格单元格中的段落
        foreach (var table in body.Elements<Table>())
        {
            foreach (var row in table.Elements<TableRow>())
            {
                foreach (var cell in row.Elements<TableCell>())
                {
                    foreach (var para in cell.Elements<Paragraph>())
                    {
                        ProcessParagraphImages(mainPart, para, photoCache);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 处理单个段落中的图片占位符
    /// </summary>
    private static void ProcessParagraphImages(MainDocumentPart mainPart, Paragraph para, Dictionary<int, PhotoMap> photoCache)
    {
        // 收集需要处理的 Run（从后往前处理，避免索引问题）
        var runs = para.Elements<Run>().ToList();
        if (runs.Count == 0) return;

        // 从后往前处理，这样插入不会影响前面的索引
        for (int i = runs.Count - 1; i >= 0; i--)
        {
            var run = runs[i];
            var text = run.InnerText;
            if (!DataResolver.HasImagePlaceholders(text)) continue;

            ProcessRunImages(mainPart, para, run, photoCache);
        }
    }

    /// <summary>
    /// 处理单个 Run 中的图片占位符，将占位符替换为图片
    /// </summary>
    private static void ProcessRunImages(MainDocumentPart mainPart, Paragraph para, Run run, Dictionary<int, PhotoMap> photoCache)
    {
        var text = run.InnerText;
        if (string.IsNullOrEmpty(text)) return;

        // 保留原 Run 的格式
        var runProps = run.RunProperties?.CloneNode(true) as RunProperties;

        // 查找所有图片占位符
        var matches = ImagePlaceholderRegex.Matches(text);
        if (matches.Count == 0) return;

        // 从后往前替换，这样位置不会变
        var elementsToInsert = new List<(int index, int length, OpenXmlElement element)>();

        for (int m = matches.Count - 1; m >= 0; m--)
        {
            var match = matches[m];
            if (!int.TryParse(match.Groups[1].Value, out var imgId)) continue;
            if (!photoCache.TryGetValue(imgId, out var photo))
            {
                System.Diagnostics.Debug.WriteLine($"[img#{imgId}] PhotoMap 不存在，跳过");
                continue;
            }

            var drawing = CreateImageDrawing(mainPart, photo);
            if (drawing == null)
            {
                System.Diagnostics.Debug.WriteLine($"[img#{imgId}] CreateImageDrawing 返回 null，跳过");
                continue;
            }

            // 创建图片 Run
            var imgRun = new Run();
            imgRun.AppendChild(drawing);
            elementsToInsert.Add((match.Index, match.Length, imgRun));
        }

        if (elementsToInsert.Count == 0) return;

        // 清空原 Run 内容
        run.RemoveAllChildren<Text>();

        // 按原位置重新构建内容
        int lastEnd = 0;
        var sortedElements = elementsToInsert.OrderBy(e => e.index).ToList();

        foreach (var (index, length, element) in sortedElements)
        {
            // 添加占位符前的文本
            if (index > lastEnd)
            {
                var beforeText = text[lastEnd..index];
                if (!string.IsNullOrEmpty(beforeText))
                {
                    var textRun = new Run();
                    if (runProps != null) textRun.RunProperties = runProps.CloneNode(true) as RunProperties;
                    textRun.AppendChild(new Text(beforeText) { Space = SpaceProcessingModeValues.Preserve });
                    run.InsertBeforeSelf(textRun);
                }
            }

            // 插入图片（在原 Run 之前）
            run.InsertBeforeSelf(element);

            lastEnd = index + length;
        }

        // 添加最后剩余的文本
        if (lastEnd < text.Length)
        {
            var afterText = text[lastEnd..];
            if (!string.IsNullOrEmpty(afterText))
            {
                var textRun = new Run();
                if (runProps != null) textRun.RunProperties = runProps.CloneNode(true) as RunProperties;
                textRun.AppendChild(new Text(afterText) { Space = SpaceProcessingModeValues.Preserve });
                run.InsertBeforeSelf(textRun);
            }
        }

        // 移除原 Run（已被替换）
        run.Remove();
    }

    /// <summary>
    /// 创建 OpenXML Drawing 元素，插入图片到文档
    /// </summary>
    private static Drawing? CreateImageDrawing(MainDocumentPart mainPart, PhotoMap photo)
    {
        if (photo.FileId == null) { System.Diagnostics.Debug.WriteLine($"[img#{photo.Id}] FileId 为 null"); return null; }

        // 从 FileStorage 读取图片
        using var db = new VettingDbContext();
        using var stream = db.GetPhotoStream(photo.FileId);
        if (stream == null) { System.Diagnostics.Debug.WriteLine($"[img#{photo.Id}] GetPhotoStream 返回 null (FileId={photo.FileId})"); return null; }

        // 确定 ImagePartType
        var imagePartType = photo.ContentType switch
        {
            "image/png" => ImagePartType.Png,
            "image/jpeg" => ImagePartType.Jpeg,
            "image/gif" => ImagePartType.Gif,
            "image/bmp" => ImagePartType.Bmp,
            "image/webp" => ImagePartType.Png, // WebP 作为 PNG 存储
            _ => ImagePartType.Png
        };

        // 添加图片部件
        var imagePart = mainPart.AddImagePart(imagePartType);
        stream.Position = 0;
        imagePart.FeedData(stream);

        // 获取关系 ID
        var relId = mainPart.GetIdOfPart(imagePart);

        // 计算尺寸：最大宽度 500pt，按比例计算高度
        // EMU (English Metric Unit): 1 inch = 914400 EMU, 1 point = 914400/72 = 12700 EMU
        const long maxWidthPt = 500L;
        const long emuPerPt = 12700L;
        long maxWidthEmu = maxWidthPt * emuPerPt;

        long widthEmu, heightEmu;
        if (photo.Width > 0 && photo.Height > 0)
        {
            // 按比例计算
            if (photo.Width > maxWidthPt)
            {
                var scale = (double)maxWidthPt / photo.Width;
                widthEmu = maxWidthEmu;
                heightEmu = (long)(photo.Height * scale * emuPerPt);
            }
            else
            {
                widthEmu = photo.Width * emuPerPt;
                heightEmu = photo.Height * emuPerPt;
            }
        }
        else
        {
            // 默认尺寸
            widthEmu = maxWidthEmu;
            heightEmu = 300L * emuPerPt; // 300pt 高度
        }

        // 构建 Drawing 元素
        var drawing = new Drawing(
            new DW.Inline(
                new DW.Extent { Cx = widthEmu, Cy = heightEmu },
                new DW.DocProperties { Id = (UInt32Value)1U, Name = $"Photo_{photo.Id}" },
                new DW.NonVisualGraphicFrameDrawingProperties(
                    new A.GraphicFrameLocks { NoChangeAspect = true }
                ),
                new A.Graphic(
                    new A.GraphicData(
                        new PIC.Picture(
                            new PIC.NonVisualPictureProperties(
                                new PIC.NonVisualDrawingProperties { Id = (UInt32Value)0U, Name = photo.FileName ?? "image" },
                                new PIC.NonVisualPictureDrawingProperties()
                            ),
                            new PIC.BlipFill(
                                new A.Blip { Embed = relId },
                                new A.Stretch(new A.FillRectangle())
                            ),
                            new PIC.ShapeProperties(
                                new A.Transform2D(
                                    new A.Offset { X = 0L, Y = 0L },
                                    new A.Extents { Cx = widthEmu, Cy = heightEmu }
                                ),
                                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }
                            )
                        )
                    ) { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }
                )
            )
        );

        return drawing;
    }

    #endregion
}

