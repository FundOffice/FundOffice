using FMO.AMAC.Direct;

using FMO.Models;
using FMO.Utilities;
using MiniExcelLibs;
using MoT;

namespace FMO.Disclosure;

public class PFIDDisclosureChannel : IDisclosureChannel
{
    public string Code => DisclosureChannelCode.Pfid;


    public string Name => "信批备份系统";

    public string Description => "在中基协信批系统发布信批公告";

    public IWorkConfig? DefaultWorkConfig(DisclosureType type) => null;

    public async Task<ErrorReturn> Disclosure(IDisclosureNotice Notice, IWorkConfig? config)
    {
        using var db = DbHelper.Base();
        var cc = db.GetCollection<DisclosureChannelConfig>().FindById(Code) as PfidChannelConfig;
        if (cc is null) return new(false, "配置不正确");


        switch (Notice)
        {
            case PeriodicalDisclosureNotice n:
                return await AmacDirectReporter.DislosurePeriodical(n, new AmacDirectAccount(cc.UserName, cc.Password, cc.Secret));
            default:
                return new(false, $"不支持的公告类型{Notice.Type}");
        }
    }

    public bool IsSupported(DisclosureType type)
    {
        return type switch
        {
            DisclosureType.Monthly => true,
            DisclosureType.Quarterly => true,
            DisclosureType.SemiAnnually => true,
            DisclosureType.Annually => true,
            _ => false
        };
    }

    public bool IsWorkflowSealed(DisclosureType type) => true;

    public DisclosureWorkflow? BuildWorkflow(DisclosureType type) => IsSupported(type) ? new DisclosureWorkflow { Channel = Code, Type = type, IsEnabled = true, ForAllFunds = true } : null;

    public bool RequireConfigWork(DisclosureType type) => false;

    public ErrorReturn VerifyNotice(IDisclosureNotice Notice)
    {
        if (Notice is PeriodicalDisclosureNotice notice)
        {
            // 检验文件
            if (notice.Excel?.Exists != true)
                return new(false, "文件不存在");

            switch (notice.Type)
            {
                case DisclosureType.Monthly:
                    return CheckMonthly(notice);
                case DisclosureType.Quarterly:
                    return CheckQuarterly(notice);
                case DisclosureType.SemiAnnually:
                    return CheckQuarterly(notice);
                case DisclosureType.Annually:
                    return CheckAnnually(notice);

                default:
                    break;
            }
        }
        return new(false, "不支持的报告");
    }


    private static ErrorReturn CheckMonthly(PeriodicalDisclosureNotice notice)
    {
        if (notice.Excel?.Exists != true)
            return new(false, "文件不存在");

        FileStream fs = notice.Excel!.File!.OpenRead()!;
        var cells = ExcelReaderHelper.ReadCellsByFieldValue(fs, sheetName: "月度报告", "基金编码:R", "估值日期:B");
        //var cells = ExcelReaderHelper.ReadCellsByRowOrder(fs, sheetName: "月度报告", "E4", "B12");

        if (cells[0] is not string code || code != notice.FundCode)
            return new(false, "基金与报告不匹配");

        if (cells[1] is not string date || (DateOnly.TryParse(date, out var d) && d != notice.ReportDate))
            return new(false, "报告日期不匹配");

        return new(true);
    }


    private static ErrorReturn CheckQuarterly(PeriodicalDisclosureNotice notice)
    {
        if (notice.Excel?.Exists != true)
            return new(false, "文件不存在");

        FileStream fs = notice.Excel!.File!.OpenRead()!;
        var cells = ExcelReaderHelper.ReadCellsByFieldValue(fs, sheetName: "1 基金基本情况", "基金编码:R");
        //var cells = ExcelReaderHelper.ReadCellsByRowOrder(fs, sheetName: "月度报告", "E4", "B12");

        if (cells[0] is not string code || code != notice.FundCode)
            return new(false, "基金与报告不匹配");

        //cells = ExcelReaderHelper.ReadCellsByFieldValueWithOffset(fs, sheetName: "3 主要财务指标", "项目:R3");

        //if (cells[0] is not string date || (DateOnly.TryParse(date, out var d) && d != notice.ReportDate))
        //    return new(false, "报告日期不匹配");

        return new(true);
    }

    private static ErrorReturn CheckAnnually(PeriodicalDisclosureNotice notice)
    {
        if (notice.Excel?.Exists != true)
            return new(false, "文件不存在");

        FileStream fs = notice.Excel!.File!.OpenRead()!;
        var cells = ExcelReaderHelper.ReadCellsByFieldValue(fs, sheetName: "1.1 基金基本情况", "基金编码:R");
        //var cells = ExcelReaderHelper.ReadCellsByRowOrder(fs, sheetName: "月度报告", "E4", "B12");

        if (cells[0] is not string code || code != notice.FundCode)
            return new(false, "基金与报告不匹配");

        //cells = ExcelReaderHelper.ReadCellsByFieldValueWithOffset(fs, sheetName: "3 主要财务指标", "项目:R3");

        //if (cells[0] is not string date || (DateOnly.TryParse(date, out var d) && d != notice.ReportDate))
        //    return new(false, "报告日期不匹配");

        return new(true);
    }

    private static bool HasText(FileMeta fileMeta, params string[] text)
    {
        var remain = text.ToList();

        try
        {
            using var reader = ExcelDataReader.ExcelReaderFactory.CreateReader(fileMeta.OpenRead());
            while (reader.NextResult())
            {
                for (int i = 0; i < reader.ResultsCount; i++)
                {
                    if (reader.GetValue(i) is string s)
                    {
                        foreach (var pa in remain.ToArray())
                        {
                            if (s.Contains(pa))
                                remain.Remove(pa);
                        }

                        if (remain.Count == 0) return true;
                    }
                }
            }
        }
        catch (Exception e)
        {
            Logg.Error(e);
        }

        return false;
    }



}


file static class ExcelReaderHelper
{
    /// <summary>
    /// 批量读取指定单元格（按行排序优化 + 结果保持输入顺序）
    /// </summary>
    /// <param name="stream">Excel 文件流</param>
    /// <param name="sheetName">工作表名</param>
    /// <param name="cellRefs">单元格集合，如 A3,B5,C3</param>
    /// <returns>按输入顺序返回的单元格值数组 object?[]</returns>
    public static object?[] ReadCellsByRowOrder(Stream stream, string sheetName, params string[] cellRefs)
    {
        // 空输入直接返回空数组
        if (cellRefs == null || cellRefs.Length == 0)
            return Array.Empty<object?>();

        // 1. 解析并按【行号升序排序】，同时保留原始索引
        var cellInfos = cellRefs
            .Select((refStr, index) => new
            {
                CellRef = refStr,
                Index = index, // 原始输入顺序索引（关键）
                Info = ParseCellRef(refStr)
            })
            .OrderBy(x => x.Info.Row)
            .ToList();

        // 2. 读取Excel行
        var rowList = MiniExcel.Query(stream, sheetName: sheetName, useHeaderRow: false)
                               .Cast<IDictionary<string, object?>>();

        // 3. 结果数组（长度 = 输入个数，顺序 = 输入顺序）
        var resultArray = new object?[cellRefs.Length];

        // 4. 逐行匹配
        using var enumerator = rowList.GetEnumerator();
        int currentExcelRow = 0;
        int foundCount = 0;

        while (enumerator.MoveNext())
        {
            currentExcelRow++;
            var currentRowData = enumerator.Current;

            // 匹配当前行的所有目标单元格
            var matchCells = cellInfos.Where(x => x.Info.Row == currentExcelRow).ToList();

            foreach (var cell in matchCells)
            {
                // 按列名取值
                var value = currentRowData.TryGetValue(cell.Info.Column, out var val) ? val : null;
                resultArray[cell.Index] = value; // 回填到原始顺序位置
                foundCount++;
            }

            // 全部找到，提前退出
            if (foundCount == cellInfos.Count)
                break;
        }

        return resultArray;
    }

    // 解析 A3 → 行3，列A
    private static (string CellRef, int Row, string Column) ParseCellRef(string cellRef)
    {
        var rowStr = new string(cellRef.SkipWhile(char.IsLetter).ToArray());
        var colStr = new string(cellRef.TakeWhile(char.IsLetter).ToArray());
        return (cellRef, int.Parse(rowStr), colStr);
    }

    /// <summary>
    /// 批量读取指定字段相邻单元格的值（支持方向+偏移量 + 结果保持输入顺序）
    /// </summary>
    /// <param name="stream">Excel 文件流</param>
    /// <param name="sheetName">工作表名</param>
    /// <param name="fields">
    /// 字段配置集合，格式："字段值:方向[偏移量]"
    /// - 方向: L(左), R(右), T(上), B(下)
    /// - 偏移量: 正整数，表示跳过几个单元格，默认为1（可省略）
    /// 示例：
    ///   "姓名:R"    → 匹配"姓名"，返回右侧第1个单元格（等价于 "姓名:R1"）
    ///   "年龄:R3"   → 匹配"年龄"，返回右侧第3个单元格（跳过2个）
    ///   "电话:L2"   → 匹配"电话"，返回左侧第2个单元格
    ///   "地址:B"    → 匹配"地址"，返回下方第1个单元格
    /// </param>
    /// <returns>按输入顺序返回的相邻单元格值数组 object?[]，未找到匹配或越界时对应位置为 null</returns>
    /// <example>
    /// Excel 内容：
    /// | A列   | B列  | C列  | D列  | E列  |
    /// |-------|------|------|------|------|
    /// | 姓名  | 标签 | 张三 | 男   | 北京 |  ← Row1
    /// | 电话  | 标签 | 138* | ***  | ***  |  ← Row2
    /// 
    /// ReadCellsByFieldValueWithOffset(stream, "Sheet1", 
    ///     "姓名:R2",    // → "张三" (姓名→跳过"标签"→取"张三")
    ///     "姓名:R",     // → "标签" (默认偏移1)
    ///     "电话:R3",    // → "138*" (电话→跳过"标签","138*"前一位? 不，是电话在A2，R3=A2→B2→C2→D2="138*")
    ///     "张三:T"      // → null (张三在C1，上方越界)
    /// )
    /// 返回：["张三", "标签", "138*", null]
    /// </example>
    public static object?[] ReadCellsByFieldValue(Stream stream, string sheetName, params string[] fields)
    {
        if (fields == null || fields.Length == 0)
            return Array.Empty<object?>();

        // 1. 解析输入参数：字段值、方向、偏移量、原始索引
        var fieldConfigs = new List<(string FieldValue, char Direction, int Offset, int OriginalIndex)>();

        for (int i = 0; i < fields.Length; i++)
        {
            var config = ParseFieldConfig(fields[i], i);
            fieldConfigs.Add(config);
        }

        // 2. 初始化结果数组
        var resultArray = new object?[fields.Length];
        var foundFlags = new bool[fields.Length];
        int foundCount = 0;

        // 3. 读取所有行到内存（支持上下方向查找）
        var allRows = MiniExcel.Query(stream, sheetName: sheetName, useHeaderRow: false)
                               .Cast<IDictionary<string, object?>>()
                               .ToList();

        // 4. 构建行号(1基)到行数据的映射
        var rowMap = new Dictionary<int, IDictionary<string, object?>>();
        for (int i = 0; i < allRows.Count; i++)
        {
            rowMap[i + 1] = allRows[i];
        }

        // 5. 逐行遍历匹配字段值
        foreach (var (rowNum, rowData) in rowMap)
        {
            foreach (var kvp in rowData)
            {
                var cellValue = kvp.Value?.ToString();
                if (string.IsNullOrEmpty(cellValue))
                    continue;

                foreach (var config in fieldConfigs)
                {
                    if (foundFlags[config.OriginalIndex])
                        continue;

                    // 精确匹配字段值（区分大小写）
                    if (string.Equals(cellValue, config.FieldValue, StringComparison.Ordinal))
                    {
                        var targetValue = GetAdjacentCellValueWithOffset(rowMap, kvp.Key, rowNum, config.Direction, config.Offset);

                        resultArray[config.OriginalIndex] = targetValue;
                        foundFlags[config.OriginalIndex] = true;
                        foundCount++;

                        if (foundCount == fieldConfigs.Count)
                            return resultArray;
                    }
                }
            }
        }

        return resultArray;
    }

    /// <summary>
    /// 解析字段配置字符串 "字段值:方向[偏移量]"
    /// </summary>
    private static (string FieldValue, char Direction, int Offset, int OriginalIndex) ParseFieldConfig(string field, int originalIndex)
    {
        var parts = field.Split(':', 2);
        if (parts.Length != 2)
            throw new ArgumentException($"Invalid field format: '{field}'. Expected: 'FieldValue:Direction[Offset]' (e.g., '姓名:R3')");

        var fieldValue = parts[0];
        var dirPart = parts[1].Trim().ToUpperInvariant();

        if (string.IsNullOrEmpty(dirPart) || "LRTB".IndexOf(dirPart[0]) == -1)
            throw new ArgumentException($"Invalid direction: '{dirPart}'. Must start with L(Left), R(Right), T(Top), B(Bottom)");

        char direction = dirPart[0];
        int offset = 1; // 默认偏移量为1

        // 解析偏移量数字部分（如 "R12" → direction='R', offset=12）
        if (dirPart.Length > 1)
        {
            var numStr = dirPart.Substring(1);
            if (!string.IsNullOrEmpty(numStr) && !int.TryParse(numStr, out offset) || offset <= 0)
            {
                throw new ArgumentException($"Invalid offset in: '{field}'. Offset must be a positive integer (e.g., 'R3', 'L12')");
            }
        }

        return (fieldValue, direction, offset, originalIndex);
    }

    /// <summary>
    /// 获取带偏移量的相邻单元格值，包含边界检查
    /// </summary>
    private static object? GetAdjacentCellValueWithOffset(
        Dictionary<int, IDictionary<string, object?>> rowMap,
        string currentCol,
        int currentRow,
        char direction,
        int offset)
    {
        string? targetCol = null;
        int targetRow = currentRow;

        switch (direction)
        {
            case 'L': // Left: 列号 - offset
                var targetColNum = ColumnToNumber(currentCol) - offset;
                if (targetColNum < 1) return null; // 越界
                targetCol = NumberToColumn(targetColNum);
                break;

            case 'R': // Right: 列号 + offset
                var nextColNum = ColumnToNumber(currentCol) + offset;
                // Excel最大列 XFD = 16384，可根据需要添加上限检查
                targetCol = NumberToColumn(nextColNum);
                break;

            case 'T': // Top: 行号 - offset
                targetRow = currentRow - offset;
                if (targetRow < 1) return null; // 越界
                targetCol = currentCol;
                break;

            case 'B': // Bottom: 行号 + offset
                targetRow = currentRow + offset;
                targetCol = currentCol;
                break;
        }

        // 检查目标行是否存在
        if (!rowMap.TryGetValue(targetRow, out var targetRowData))
            return null;

        // 获取目标单元格值（不存在则返回 null）
        return targetRowData.TryGetValue(targetCol!, out var val) ? val : null;
    }

    /// <summary>
    /// Excel列名转数字（A=1, B=2, ..., Z=26, AA=27, AB=28, ..., XFD=16384）
    /// </summary>
    private static int ColumnToNumber(string column)
    {
        if (string.IsNullOrEmpty(column)) return 0;

        int num = 0;
        foreach (char c in column.ToUpperInvariant())
        {
            if (c < 'A' || c > 'Z') return 0; // 非法字符
            num = num * 26 + (c - 'A' + 1);
        }
        return num;
    }

    /// <summary>
    /// 数字转Excel列名（1→A, 26→Z, 27→AA, 28→AB, ..., 16384→XFD）
    /// </summary>
    private static string NumberToColumn(int num)
    {
        if (num <= 0) return string.Empty;

        var result = new System.Text.StringBuilder();
        while (num > 0)
        {
            num--; // 调整为0基索引 (0→A, 25→Z)
            result.Insert(0, (char)('A' + (num % 26)));
            num /= 26;
        }
        return result.ToString();
    }
}