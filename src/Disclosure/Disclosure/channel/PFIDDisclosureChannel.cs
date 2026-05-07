using FMO.AMAC.Direct;
using FMO.Logging;
using FMO.Models;
using FMO.Utilities;
using MiniExcelLibs;
using System.Text.RegularExpressions;

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
                    break;
                case DisclosureType.SemiAnnually:
                    break;
                case DisclosureType.Annually:
                    break;
                case DisclosureType.QuarterlyUpdate:
                    break;
                case DisclosureType.Temporary:
                    break;
                case DisclosureType.TemporaryOpen:
                    break;
                case DisclosureType.HugeRedemption:
                    break;
                case DisclosureType.FundSetup:
                    break;
                case DisclosureType.FundScaleWarning:
                    break;
                case DisclosureType.OtherFundNotice:
                    break;
                case DisclosureType.ManagerLevel:
                    break;
                case DisclosureType.MangerChange:
                    break;
                case DisclosureType.OfficeAddressChange:
                    break;
                case DisclosureType.OtherManagerNotice:
                    break;
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
        var cells = ExcelReaderHelper.ReadCellsByRowOrder(fs, sheetName: "月度报告", "E4", "B12");

        if (cells[0] is not string code || code != notice.FundCode)
            return new(false, "基金与报告不匹配");

        if (cells[1] is not string date || (DateOnly.TryParse(date, out var d) && d != notice.ReportDate))
            return new(false, "报告日期不匹配");

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
            LogEx.Error(e);
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
}