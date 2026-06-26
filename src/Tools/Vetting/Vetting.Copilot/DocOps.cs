using System.Text.Json;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Vetting.Copilot.Models.Info;

namespace Vetting.Copilot;

/// <summary>
/// docx 文档操作工具 — 给 AI agent 调用
/// </summary>
public static class DocOps
{
    /// <summary>分析文档结构：章节标题列表、表格类型检测</summary>
    public static string AnalyzeStructure(string filePath)
    {
        using var doc = WordprocessingDocument.Open(filePath, false);
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

    /// <summary>解析完整文档内容（段落和表格按文档顺序，含索引和内容）</summary>
    public static string ParseDocument(string filePath)
    {
        using var doc = WordprocessingDocument.Open(filePath, false);
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

    /// <summary>读取文档段落（含索引、样式标记、空段落标记）。可选 start/end 分段读取</summary>
    public static string ReadParagraphs(string filePath, int? start = null, int? end = null)
    {
        using var doc = WordprocessingDocument.Open(filePath, false);
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

    /// <summary>读取指定表格的结构和内容（含表头检测）。可选 startRow/endRow 分行读取</summary>
    public static string ReadTable(string filePath, int tableIndex, int? startRow = null, int? endRow = null)
    {
        using var doc = WordprocessingDocument.Open(filePath, false);
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
        using var doc = WordprocessingDocument.Open(filePath, false);
        return doc.MainDocumentPart!.Document.Body!.Elements<Table>().Count();
    }

    /// <summary>获取文档段落数量</summary>
    public static int GetParagraphCount(string filePath)
    {
        using var doc = WordprocessingDocument.Open(filePath, false);
        return doc.MainDocumentPart!.Document.Body!.Elements<Paragraph>().Count();
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
                        FillRecommend(tables, paragraphs, rec, resolver, tableOffsets);
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
                        System.Diagnostics.Debug.WriteLine($"[Type g] 跳过未知表格 T[{unknown.Ts.TableIndex}]: {unknown.Description}");
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DocOps.Fill error ({op}): {ex}");
            }
        }
        doc.MainDocumentPart.Document.Save();
    }

    #region Fill Implementations

    private static void FillScalar(List<Table> tables, List<Paragraph> paragraphs, ScalarOp op, DataResolver resolver, Dictionary<int, int> offsets)
    {
        var value = resolver.Resolve(op.Entity, op.Property, op.Format);
        if (op.Location.IsCell)
        {
            var cell = GetCell(tables, op.Location.TableIndex, op.Location.RowIndex + offsets.GetValueOrDefault(op.Location.TableIndex), op.Location.ColIndex);
            if (cell != null) SetCellContent(cell, value);
        }
        else if (op.Location.IsParagraph)
        {
            var para = paragraphs.ElementAtOrDefault(op.Location.ParaIndex);
            if (para != null) SetParaContent(para, value);
        }
    }

    private static void FillRecommend(List<Table> tables, List<Paragraph> paragraphs, RecommendOp op, DataResolver resolver, Dictionary<int, int> offsets)
    {
        var value = resolver.ResolveRecommend(op.FundIndex, op.Property, op.Format);
        if (string.IsNullOrEmpty(value)) return; // recommend 越界或属性为空，不填充

        if (op.Location.IsCell)
        {
            var cell = GetCell(tables, op.Location.TableIndex, op.Location.RowIndex + offsets.GetValueOrDefault(op.Location.TableIndex), op.Location.ColIndex);
            if (cell != null) SetCellContent(cell, value);
        }
        else if (op.Location.IsParagraph)
        {
            var para = paragraphs.ElementAtOrDefault(op.Location.ParaIndex);
            if (para != null) SetParaContent(para, value);
        }
    }

    private static void FillListExpand(List<Table> tables, ListExpandOp op, DataResolver resolver, Dictionary<int, int> offsets)
    {
        var table = tables.ElementAtOrDefault(op.Ts.TableIndex);
        if (table == null) return;

        var items = resolver.GetList(op.Entity);
        if (items.Length == 0) return;

        var rows = table.Elements<TableRow>().ToList();
        int offset = offsets.GetValueOrDefault(op.Ts.TableIndex);
        int availableRows = op.Te.RowIndex - op.Ts.RowIndex + 1;

        // 需要扩展行
        if (items.Length > availableRows)
        {
            int extraCount = items.Length - availableRows;
            var templateRow = rows[op.Te.RowIndex];
            var insertAfter = rows[op.Te.RowIndex];

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

            offsets[op.Ts.TableIndex] = offset + extraCount;
        }

        // 填充数据
        // 重新获取行列表（可能已插入新行）
        rows = table.Elements<TableRow>().ToList();
        var propKeys = op.Properties.Keys.ToArray();

        for (int i = 0; i < items.Length; i++)
        {
            int rowIdx = op.Ts.RowIndex + i + offset;
            var row = rows.ElementAtOrDefault(rowIdx);
            if (row == null) continue;

            var cells = row.Elements<TableCell>().ToList();
            for (int j = 0; j < propKeys.Length; j++)
            {
                int colIdx = op.Ts.ColIndex + j;
                var cell = cells.ElementAtOrDefault(colIdx);
                if (cell == null) continue;

                var propName = propKeys[j];
                var value = items[i].TryGetValue(propName, out var v) ? v : "";
                if (!string.IsNullOrEmpty(value) && op.Formats != null && op.Formats.TryGetValue(propName, out var fmt))
                    value = ResolveHelper.ToString(value, fmt);
                SetCellContent(cell, value);
            }
        }
    }

    private static void FillGrid(List<Table> tables, GridOp op, DataResolver resolver, Dictionary<int, int> offsets)
    {
        var table = tables.ElementAtOrDefault(op.Ts.TableIndex);
        if (table == null) return;

        var items = resolver.GetList(op.Entity);
        if (items.Length == 0) return;

        int offset = offsets.GetValueOrDefault(op.Ts.TableIndex);
        var rows = table.Elements<TableRow>().ToList();
        var propKeys = op.Properties.Keys.ToArray();

        if (op.EntityPerRow)
        {
            // Type d: 一行一 entity，列头是属性
            for (int ri = op.Ts.RowIndex; ri <= op.Te.RowIndex; ri++)
            {
                var row = rows.ElementAtOrDefault(ri + offset);
                if (row == null) continue;

                var cells = row.Elements<TableCell>().ToList();
                var rowHeader = cells.ElementAtOrDefault(op.Ts.ColIndex - 1)?.InnerText?.Trim() ?? "";

                Dictionary<string, string>? matched = null;
                if (!string.IsNullOrEmpty(op.FilterBy))
                {
                    matched = items.FirstOrDefault(item =>
                        item.TryGetValue(op.FilterBy, out var val) && val?.Trim() == rowHeader);
                }
                matched ??= items.ElementAtOrDefault(ri - op.Ts.RowIndex);

                if (matched == null) continue;

                for (int j = 0; j < propKeys.Length; j++)
                {
                    int colIdx = op.Ts.ColIndex + j;
                    var cell = cells.ElementAtOrDefault(colIdx);
                    if (cell == null) continue;

                    var propName = propKeys[j];
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
            var headerRow = rows.ElementAtOrDefault(op.Ts.RowIndex - 1 + offset);

            for (int ci = op.Ts.ColIndex; ci <= op.Te.ColIndex; ci++)
            {
                var headerCells = headerRow?.Elements<TableCell>().ToList();
                var colHeader = headerCells?.ElementAtOrDefault(ci)?.InnerText?.Trim() ?? "";

                Dictionary<string, string>? matched = null;
                if (!string.IsNullOrEmpty(op.FilterBy))
                {
                    matched = items.FirstOrDefault(item =>
                        item.TryGetValue(op.FilterBy, out var val) && val?.Trim() == colHeader);
                }
                matched ??= items.ElementAtOrDefault(ci - op.Ts.ColIndex);

                if (matched == null) continue;

                for (int i = 0; i < propKeys.Length; i++)
                {
                    int rowIdx = op.Ts.RowIndex + i + offset;
                    var row = rows.ElementAtOrDefault(rowIdx);
                    if (row == null) continue;

                    var cell = row.Elements<TableCell>().ElementAtOrDefault(ci);
                    if (cell == null) continue;

                    var propName = propKeys[i];
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
        var para = paragraphs.ElementAtOrDefault(op.Location.ParaIndex);
        if (para == null) return;

        string value;
        if (op.Entity != null && op.Property != null)
            value = resolver.Resolve(op.Entity, op.Property, op.Format);
        else
            value = resolver.GetAnswerByQuestion(op.Question);

        if (!string.IsNullOrEmpty(value))
            SetParaContent(para, value);
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
}
