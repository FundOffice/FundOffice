using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Vetting.Copilot.Models.Info;
using Xunit;

namespace Vetting.Copilot.Tests;

/// <summary>
/// 测试用 IResolve 实现，从字典取值
/// </summary>
public class DictEntity : IResolve
{
    private readonly Dictionary<string, string> _props;
    public DictEntity(Dictionary<string, string> props) => _props = props;
    public object? Resolve(string propertyName) =>
        _props.TryGetValue(propertyName, out var v) ? v : null;
}

/// <summary>
/// 构造测试用 DataResolver 的辅助方法
/// </summary>
public static class TestData
{
    public static DataResolver Resolver(
        Dictionary<string, string>? scalars = null,
        Dictionary<string, Dictionary<string, string>[]>? lists = null,
        Dictionary<int, Dictionary<string, string>>? recommends = null,
        Dictionary<string, string>? answers = null)
    {
        // 将扁平 scalars dict 转为 IResolve 字典：entity → DictEntity
        var scalarObjs = new Dictionary<string, IResolve>();
        if (scalars != null)
        {
            var grouped = new Dictionary<string, Dictionary<string, string>>();
            foreach (var kv in scalars)
            {
                var dot = kv.Key.IndexOf('.');
                if (dot > 0)
                {
                    var entity = kv.Key[..dot];
                    if (!grouped.TryGetValue(entity, out var dict))
                    {
                        dict = new Dictionary<string, string>();
                        grouped[entity] = dict;
                    }
                    dict[kv.Key[(dot + 1)..]] = kv.Value;
                }
            }
            foreach (var kv in grouped)
                scalarObjs[kv.Key] = new DictEntity(kv.Value);
        }

        var recommendFunds = new Dictionary<int, FundInfo>();
        if (recommends != null)
        {
            foreach (var kv in recommends)
            {
                var fund = new FundInfo();
                foreach (var pkv in kv.Value)
                {
                    var prop = typeof(FundInfo).GetProperty(pkv.Key);
                    prop?.SetValue(fund, pkv.Value);
                }
                recommendFunds[kv.Key] = fund;
            }
        }

        return new DataResolver(
            scalarObjs,
            lists ?? new(),
            recommendFunds,
            answers ?? new());
    }
}

/// <summary>
/// 用 OpenXML 程序化创建测试文档的工具类
/// </summary>
public static class TestDocBuilder
{
    /// <summary>
    /// 创建包含指定表格和段落的 docx 文件
    /// </summary>
    public static string Create(string path, List<Table>? tables = null, List<string>? paragraphs = null)
    {
        using var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        var mainPart = doc.AddMainDocumentPart();
        mainPart.Document = new Document();
        var body = new Body();
        mainPart.Document.Body = body;

        int pi = 0, ti = 0;
        // 交替插入段落和表格（按顺序）
        var paraQueue = new Queue<string>(paragraphs ?? []);
        var tableQueue = new Queue<Table>(tables ?? []);

        // 简单策略：先所有段落，再所有表格，或按调用者指定
        // 这里按调用顺序：paragraphs 先，tables 后
        while (paraQueue.Count > 0 || tableQueue.Count > 0)
        {
            if (paraQueue.Count > 0)
                body.AppendChild(new Paragraph(new Run(new Text(paraQueue.Dequeue()))));
            if (tableQueue.Count > 0)
                body.AppendChild(tableQueue.Dequeue());
        }

        mainPart.Document.Save();
        return path;
    }

    /// <summary>创建简单的 LQRA 表格（左问右答）</summary>
    public static Table LQRATable(params (string question, string answer)[] rows)
    {
        var table = new Table();
        var tblPr = new TableProperties(
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 4 },
                new BottomBorder { Val = BorderValues.Single, Size = 4 },
                new LeftBorder { Val = BorderValues.Single, Size = 4 },
                new RightBorder { Val = BorderValues.Single, Size = 4 }
            ));
        table.AppendChild(tblPr);

        foreach (var (q, a) in rows)
        {
            var row = new TableRow();
            row.AppendChild(MakeCell(q));
            row.AppendChild(MakeCell(a));
            table.AppendChild(row);
        }
        return table;
    }

    /// <summary>
    /// 创建列表表格（列头 + 数据行）。headerRow=true 时第一行加粗。
    /// </summary>
    public static Table ListTable(string[] headers, string?[][] dataRows, bool headerRow = true)
    {
        var table = new Table();

        // 表头行
        if (headerRow)
        {
            var hRow = new TableRow();
            foreach (var h in headers)
                hRow.AppendChild(MakeCell(h, bold: true));
            table.AppendChild(hRow);
        }

        // 数据行
        foreach (var rowData in dataRows)
        {
            var row = new TableRow();
            for (int i = 0; i < headers.Length; i++)
            {
                var text = (i < rowData.Length ? rowData[i] : null) ?? "";
                row.AppendChild(MakeCell(text));
            }
            table.AppendChild(row);
        }
        return table;
    }

    /// <summary>
    /// 创建行列头表格（行头 + 列头 + 数据区域）
    /// </summary>
    public static Table GridTable(
        string[] colHeaders,
        string[] rowHeaders,
        string?[,] data)
    {
        var table = new Table();

        // 列头行（第一列为空，对应行头列）
        var hRow = new TableRow();
        hRow.AppendChild(MakeCell("", bold: true)); // 左上角空
        foreach (var ch in colHeaders)
            hRow.AppendChild(MakeCell(ch, bold: true));
        table.AppendChild(hRow);

        // 数据行（带行头）
        for (int ri = 0; ri < rowHeaders.Length; ri++)
        {
            var row = new TableRow();
            row.AppendChild(MakeCell(rowHeaders[ri])); // 行头
            for (int ci = 0; ci < colHeaders.Length; ci++)
            {
                var text = data[ri, ci] ?? "";
                row.AppendChild(MakeCell(text));
            }
            table.AppendChild(row);
        }
        return table;
    }

    public static TableCell MakeCell(string text, bool bold = false)
    {
        var cell = new TableCell();
        var para = new Paragraph();
        var run = new Run();
        if (bold) run.AppendChild(new RunProperties(new Bold()));
        run.AppendChild(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        para.AppendChild(run);
        cell.AppendChild(para);
        return cell;
    }
}

// ───────────────────── 测试类 ─────────────────────

public class DocOpsFillTests : IDisposable
{
    private readonly string _tempDir;

    public DocOpsFillTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"vetting_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private string TempFile(string name) => Path.Combine(_tempDir, name);

    // ── Type a: ScalarOp ──────────────────────────────

    [Fact]
    public void TypeA_ScalarOp_FillsCorrectCell()
    {
        var src = TempFile("src.docx");
        TestDocBuilder.Create(src,
            tables: [TestDocBuilder.LQRATable(("公司名称", ""), ("注册资本", ""))],
            paragraphs: []);

        var resolver = TestData.Resolver(scalars: new()
        {
            ["manager.Name"] = "测试基金管理有限公司",
            ["manager.RegisterCapital"] = "5000万元",
        });

        var ops = new List<FillOperator>
        {
            new ScalarOp { Entity = "manager", Property = "Name", Question = "公司名称",
                Location = new DocLocation { TableIndex = 0, RowIndex = 0, ColIndex = 1 } },
            new ScalarOp { Entity = "manager", Property = "RegisterCapital", Question = "注册资本",
                Location = new DocLocation { TableIndex = 0, RowIndex = 1, ColIndex = 1 } },
        };

        var outPath = TempFile("out.docx");
        DocOps.Fill(src, outPath, ops, resolver);

        var result = DocOps.ReadTable(outPath, 0);
        Assert.Contains("测试基金管理有限公司", result);
        Assert.Contains("5000万元", result);
        // 问题列不动
        Assert.Contains("公司名称", result);
        Assert.Contains("注册资本", result);
    }

    // ── Type b: RecommendOp ────────────────────────────

    [Fact]
    public void TypeB_RecommendOp_FillsAndSkipsOutOfBounds()
    {
        var src = TempFile("src.docx");
        TestDocBuilder.Create(src,
            tables: [TestDocBuilder.LQRATable(("产品名称", ""), ("产品名称", ""))],
            paragraphs: []);

        // 只有 1 个 recommend，fund_index=1 应跳过
        var resolver = TestData.Resolver(recommends: new()
        {
            [0] = new() { ["Name"] = "稳健一号", ["Scale"] = "2亿" },
        });

        var ops = new List<FillOperator>
        {
            new RecommendOp { FundIndex = 0, Property = "Name", Question = "产品名称", Table = "要素表",
                Location = new DocLocation { TableIndex = 0, RowIndex = 0, ColIndex = 1 } },
            new RecommendOp { FundIndex = 1, Property = "Name", Question = "产品名称", Table = "要素表",
                Location = new DocLocation { TableIndex = 0, RowIndex = 1, ColIndex = 1 } },
        };

        var outPath = TempFile("out.docx");
        DocOps.Fill(src, outPath, ops, resolver);

        // 直接读单元格内容验证
        using var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(outPath, false);
        var table = doc.MainDocumentPart!.Document.Body!.Elements<DocumentFormat.OpenXml.Wordprocessing.Table>().First();
        var rows = table.Elements<DocumentFormat.OpenXml.Wordprocessing.TableRow>().ToList();

        // fund_index=0 应填入
        var cell01 = rows[0].Elements<DocumentFormat.OpenXml.Wordprocessing.TableCell>().ElementAt(1);
        Assert.Contains("稳健一号", cell01.InnerText);

        // fund_index=1 越界，应保持空
        var cell11 = rows[1].Elements<DocumentFormat.OpenXml.Wordprocessing.TableCell>().ElementAt(1);
        Assert.True(string.IsNullOrEmpty(cell11.InnerText.Trim()), $"期望空单元格，实际: '{cell11.InnerText}'");
    }

    // ── Type c: ListExpandOp — 不扩展（数据少于预分配行） ──

    [Fact]
    public void TypeC_ListExpand_NoExpansionWhenDataFits()
    {
        var src = TempFile("src.docx");
        // 3 列头 + 4 行预分配，数据只有 2 条
        TestDocBuilder.Create(src,
            tables: [TestDocBuilder.ListTable(
                ["姓名", "持股比例"],
                [new string?[2], new string?[2], new string?[2], new string?[2]])],
            paragraphs: []);

        var resolver = TestData.Resolver(lists: new()
        {
            ["shareholder"] = new[]
            {
                new Dictionary<string, string> { ["Name"] = "张三", ["Ratio"] = "60%" },
                new Dictionary<string, string> { ["Name"] = "李四", ["Ratio"] = "40%" },
            },
        });

        // ts = [1,0], te = [4,1]（预分配 4 行）
        var ops = new List<FillOperator>
        {
            new ListExpandOp
            {
                Entity = "shareholder",
                Properties = [new PropItem("Name", "姓名"), new PropItem("Ratio", "持股比例")],
                Ts = new DocLocation { TableIndex = 0, RowIndex = 1, ColIndex = 0 },
                Te = new DocLocation { TableIndex = 0, RowIndex = 4, ColIndex = 1 },
            },
        };

        var outPath = TempFile("out.docx");
        DocOps.Fill(src, outPath, ops, resolver);

        var table = DocOps.ReadTable(outPath, 0);
        Assert.Contains("张三", table);
        Assert.Contains("60%", table);
        Assert.Contains("李四", table);
        Assert.Contains("40%", table);
        // 不应扩展行 — 原始 5 行（1 表头 + 4 数据）
        Assert.Contains("(5 rows)", table);
    }

    // ── Type c: ListExpandOp — 需要扩展行 ──────────────

    [Fact]
    public void TypeC_ListExpand_ExpandsRowsWhenDataExceeds()
    {
        var src = TempFile("src.docx");
        // 2 列头 + 2 行预分配，数据有 4 条 → 需扩展 2 行
        TestDocBuilder.Create(src,
            tables: [TestDocBuilder.ListTable(
                ["姓名", "持股比例"],
                [new string?[2], new string?[2]])],
            paragraphs: []);

        var resolver = TestData.Resolver(lists: new()
        {
            ["shareholder"] = new[]
            {
                new Dictionary<string, string> { ["Name"] = "A", ["Ratio"] = "25%" },
                new Dictionary<string, string> { ["Name"] = "B", ["Ratio"] = "25%" },
                new Dictionary<string, string> { ["Name"] = "C", ["Ratio"] = "25%" },
                new Dictionary<string, string> { ["Name"] = "D", ["Ratio"] = "25%" },
            },
        });

        // ts = [1,0], te = [2,1]（预分配 2 行）
        var ops = new List<FillOperator>
        {
            new ListExpandOp
            {
                Entity = "shareholder",
                Properties = [new PropItem("Name", "姓名"), new PropItem("Ratio", "持股比例")],
                Ts = new DocLocation { TableIndex = 0, RowIndex = 1, ColIndex = 0 },
                Te = new DocLocation { TableIndex = 0, RowIndex = 2, ColIndex = 1 },
            },
        };

        var outPath = TempFile("out.docx");
        DocOps.Fill(src, outPath, ops, resolver);

        var table = DocOps.ReadTable(outPath, 0);
        Assert.Contains("A", table);
        Assert.Contains("B", table);
        Assert.Contains("C", table);
        Assert.Contains("D", table);
        // 原始 3 行 + 扩展 2 行 = 5 行
        Assert.Contains("(5 rows)", table);
    }

    // ── Type c: 同表格多个 ListExpandOp，累计偏移 ──────

    [Fact]
    public void TypeC_MultipleExpands_CumulativeOffset()
    {
        var src = TempFile("src.docx");
        // 嵌套表格：左侧合并列不动，右侧子表格有两个 ListExpand 区域
        // 模拟：6 行表格
        // [0] 表头:  类别 | 姓名 | 职务
        // [1] 高管1  [1,1] [1,2]
        // [2] 高管2  [2,1] [2,2]
        // [3] 风控1  [3,1] [3,2]
        // [4] 风控2  [4,1] [4,2]
        // 第一个 ListExpand: ts=[1,1], te=[2,2]，高管，3 条数据 → 扩展 1 行
        // 第二个 ListExpand: ts=[3,1], te=[4,2]，风控，3 条数据 → 扩展 1 行（原始 te 已偏移）

        var table = new Table();
        AddRow(table, "人员类别", "姓名", "职务");
        AddRow(table, "", "", "");  // row 1
        AddRow(table, "", "", "");  // row 2
        AddRow(table, "", "", "");  // row 3
        AddRow(table, "", "", "");  // row 4

        TestDocBuilder.Create(src, tables: [table], paragraphs: []);

        var resolver = TestData.Resolver(lists: new()
        {
            ["executive"] = new[]
            {
                new Dictionary<string, string> { ["Name"] = "E1", ["Title"] = "CEO" },
                new Dictionary<string, string> { ["Name"] = "E2", ["Title"] = "COO" },
                new Dictionary<string, string> { ["Name"] = "E3", ["Title"] = "CFO" },
            },
            ["riskctrl"] = new[]
            {
                new Dictionary<string, string> { ["Name"] = "R1", ["Title"] = "风控总监" },
                new Dictionary<string, string> { ["Name"] = "R2", ["Title"] = "风控经理" },
                new Dictionary<string, string> { ["Name"] = "R3", ["Title"] = "风控专员" },
            },
        });

        var ops = new List<FillOperator>
        {
            new ListExpandOp
            {
                Entity = "executive",
                Properties = [new PropItem("Name", "姓名"), new PropItem("Title", "职务")],
                Ts = new DocLocation { TableIndex = 0, RowIndex = 1, ColIndex = 1 },
                Te = new DocLocation { TableIndex = 0, RowIndex = 2, ColIndex = 2 },
            },
            new ListExpandOp
            {
                Entity = "riskctrl",
                Properties = [new PropItem("Name", "姓名"), new PropItem("Title", "职务")],
                Ts = new DocLocation { TableIndex = 0, RowIndex = 3, ColIndex = 1 },
                Te = new DocLocation { TableIndex = 0, RowIndex = 4, ColIndex = 2 },
            },
        };

        var outPath = TempFile("out.docx");
        DocOps.Fill(src, outPath, ops, resolver);

        var result = DocOps.ReadTable(outPath, 0);
        // 高管 3 条都在
        Assert.Contains("E1", result);
        Assert.Contains("E2", result);
        Assert.Contains("E3", result);
        // 风控 3 条都在
        Assert.Contains("R1", result);
        Assert.Contains("R2", result);
        Assert.Contains("R3", result);
        // 原始 5 行 + 高管扩展 1 + 风控扩展 1 = 7 行
        Assert.Contains("(7 rows)", result);
    }

    // ── Type d: GridOp EntityPerRow ─────────────────────

    [Fact]
    public void TypeD_GridOp_EntityPerRow_FillsByRowHeader()
    {
        var src = TempFile("src.docx");
        //       | 总资产 | 总负债
        // 2023  |       |
        // 2022  |       |
        TestDocBuilder.Create(src,
            tables: [TestDocBuilder.GridTable(
                ["总资产", "总负债"],
                ["2023", "2022"],
                new string?[2, 2])],
            paragraphs: []);

        var resolver = TestData.Resolver(lists: new()
        {
            ["financialstatement"] = new[]
            {
                new Dictionary<string, string> { ["Year"] = "2023", ["TotalAssets"] = "100亿", ["TotalLiabilities"] = "60亿" },
                new Dictionary<string, string> { ["Year"] = "2022", ["TotalAssets"] = "80亿", ["TotalLiabilities"] = "50亿" },
            },
        });

        // ts = [1,1], te = [2,2]（数据区域，不包括行头列）
        var ops = new List<FillOperator>
        {
            new GridOp
            {
                Entity = "financialstatement",
                Properties = [new PropItem("TotalAssets", "总资产"), new PropItem("TotalLiabilities", "总负债")],
                Ts = new DocLocation { TableIndex = 0, RowIndex = 1, ColIndex = 1 },
                Te = new DocLocation { TableIndex = 0, RowIndex = 2, ColIndex = 2 },
                EntityPerRow = true,
                FilterBy = "Year",
            },
        };

        var outPath = TempFile("out.docx");
        DocOps.Fill(src, outPath, ops, resolver);

        var result = DocOps.ReadTable(outPath, 0);
        Assert.Contains("100亿", result);
        Assert.Contains("60亿", result);
        Assert.Contains("80亿", result);
        Assert.Contains("50亿", result);
        // 验证不扩展
        Assert.Contains("(3 rows)", result); // 1 表头 + 2 数据
    }

    // ── Type e: GridOp EntityPerCol ─────────────────────

    [Fact]
    public void TypeE_GridOp_EntityPerCol_FillsByColHeader()
    {
        var src = TempFile("src.docx");
        //       | 2023  | 2022
        // 总资产 |       |
        // 总负债 |       |
        TestDocBuilder.Create(src,
            tables: [TestDocBuilder.GridTable(
                ["2023", "2022"],
                ["总资产", "总负债"],
                new string?[2, 2])],
            paragraphs: []);

        var resolver = TestData.Resolver(lists: new()
        {
            ["financialstatement"] = new[]
            {
                new Dictionary<string, string> { ["Year"] = "2023", ["TotalAssets"] = "100亿", ["TotalLiabilities"] = "60亿" },
                new Dictionary<string, string> { ["Year"] = "2022", ["TotalAssets"] = "80亿", ["TotalLiabilities"] = "50亿" },
            },
        });

        // ts = [1,1], te = [2,2]
        var ops = new List<FillOperator>
        {
            new GridOp
            {
                Entity = "financialstatement",
                Properties = [new PropItem("TotalAssets", "总资产"), new PropItem("TotalLiabilities", "总负债")],
                Ts = new DocLocation { TableIndex = 0, RowIndex = 1, ColIndex = 1 },
                Te = new DocLocation { TableIndex = 0, RowIndex = 2, ColIndex = 2 },
                EntityPerRow = false,
                FilterBy = "Year",
            },
        };

        var outPath = TempFile("out.docx");
        DocOps.Fill(src, outPath, ops, resolver);

        var result = DocOps.ReadTable(outPath, 0);
        // Type e: 列头匹配 Year，行头匹配 property
        // col 1 (2023): row 1 → TotalAssets=100亿, row 2 → TotalLiabilities=60亿
        // col 2 (2022): row 1 → TotalAssets=80亿, row 2 → TotalLiabilities=50亿
        Assert.Contains("100亿", result);
        Assert.Contains("60亿", result);
        Assert.Contains("80亿", result);
        Assert.Contains("50亿", result);
    }

    // ── Type z: ParagraphOp ────────────────────────────

    [Fact]
    public void TypeF_ParagraphOp_FillsParagraph()
    {
        var src = TempFile("src.docx");
        TestDocBuilder.Create(src,
            tables: [],
            paragraphs: ["请简述投资策略：", ""]);

        var resolver = TestData.Resolver(answers: new()
        {
            ["请简述投资策略："] = "本基金采用量化多因子策略",
        });

        var ops = new List<FillOperator>
        {
            new ParagraphOp
            {
                Question = "请简述投资策略：",
                Location = new DocLocation { ParaIndex = 1 },
            },
        };

        var outPath = TempFile("out.docx");
        DocOps.Fill(src, outPath, ops, resolver);

        var result = DocOps.ReadParagraphs(outPath);
        Assert.Contains("本基金采用量化多因子策略", result);
    }

    // ── 解析结构测试 ────────────────────────────────────

    [Fact]
    public void ParseDocument_DetectsTableStructure()
    {
        var src = TempFile("struct.docx");
        TestDocBuilder.Create(src,
            tables:
            [
                TestDocBuilder.LQRATable(("问题1", ""), ("问题2", "")),
                TestDocBuilder.ListTable(["A", "B"], [new string?[2], new string?[2]]),
            ],
            paragraphs: ["段落一", "段落二"]);

        var parsed = DocOps.ParseDocument(src);

        // 段落存在
        Assert.Contains("段落一", parsed);
        Assert.Contains("段落二", parsed);
        // 两个表格
        Assert.Contains("T[0]", parsed);
        Assert.Contains("T[1]", parsed);
        // 第一个表格 2 行
        Assert.Contains("[0,0]", parsed);
        Assert.Contains("[0,1]", parsed);
        // 第二个表格有表头
        Assert.Contains("[has header]", parsed);
    }

    // ── 辅助 ───────────────────────────────────────────

    private static void AddRow(Table table, params string[] cells)
    {
        var row = new TableRow();
        foreach (var c in cells)
            row.AppendChild(TestDocBuilder.MakeCell(c));
        table.AppendChild(row);
    }
}
