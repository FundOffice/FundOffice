using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Vetting.Copilot.Models.Info;
using Xunit;
using Xunit.Abstractions;

namespace Vetting.Copilot.Tests;

public class DictEntity : IResolve
{
    private readonly Dictionary<string, string> _props;
    public DictEntity(Dictionary<string, string> props) => _props = props;
    public object? Resolve(string propertyName) =>
        _props.TryGetValue(propertyName, out var v) ? v : null;
}

public class ObjectEntity : IResolve
{
    private readonly Dictionary<string, object?> _props;
    public ObjectEntity(Dictionary<string, object?> props) => _props = props;
    public object? Resolve(string propertyName) =>
        _props.TryGetValue(propertyName, out var v) ? v : null;
}

public static class TestData
{
    public static DataResolver Resolver(
        Dictionary<string, string>? scalars = null,
        Dictionary<string, Dictionary<string, string>[]>? lists = null,
        Dictionary<int, Dictionary<string, string>>? recommends = null,
        Dictionary<string, string>? answers = null)
    {
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
        return new DataResolver(scalarObjs, lists ?? new(), recommendFunds, answers ?? new(), null, new());
    }
}

public static class TestDocBuilder
{
    public static string Create(string path, List<Table>? tables = null, List<string>? paragraphs = null)
    {
        using var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        var mainPart = doc.AddMainDocumentPart();
        mainPart.Document = new Document();
        var body = new Body();
        mainPart.Document.Body = body;
        var paraQueue = new Queue<string>(paragraphs ?? []);
        var tableQueue = new Queue<Table>(tables ?? []);
        while (paraQueue.Count > 0 || tableQueue.Count > 0)
        {
            if (paraQueue.Count > 0) body.AppendChild(new Paragraph(new Run(new Text(paraQueue.Dequeue()))));
            if (tableQueue.Count > 0) body.AppendChild(tableQueue.Dequeue());
        }
        mainPart.Document.Save();
        return path;
    }

    public static Table MakeTable(int rows, int cols, string[,]? data = null)
    {
        var table = new Table();
        for (int r = 0; r < rows; r++)
        {
            var tr = new TableRow();
            for (int c = 0; c < cols; c++)
            {
                var tc = new TableCell();
                var text = data?[r, c] ?? "R" + r + "C" + c;
                tc.AppendChild(new Paragraph(new Run(new Text(text))));
                tr.AppendChild(tc);
            }
            table.AppendChild(tr);
        }
        return table;
    }

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

    public static Table ListTable(string[] headers, string?[][] dataRows, bool headerRow = true)
    {
        var table = new Table();
        if (headerRow)
        {
            var hRow = new TableRow();
            foreach (var h in headers)
                hRow.AppendChild(MakeCell(h, bold: true));
            table.AppendChild(hRow);
        }
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

    public static Table GridTable(string[] colHeaders, string[] rowHeaders, string?[,] data)
    {
        var table = new Table();
        var hRow = new TableRow();
        hRow.AppendChild(MakeCell("", bold: true));
        foreach (var ch in colHeaders)
            hRow.AppendChild(MakeCell(ch, bold: true));
        table.AppendChild(hRow);
        for (int ri = 0; ri < rowHeaders.Length; ri++)
        {
            var row = new TableRow();
            row.AppendChild(MakeCell(rowHeaders[ri]));
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

public class DocOpsFillTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ITestOutputHelper _output;

    public DocOpsFillTests(ITestOutputHelper output)
    {
        _output = output;
        _tempDir = Path.Combine(Path.GetTempPath(), $"vetting_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private string TempFile(string name) => Path.Combine(_tempDir, name);

    [Fact]
    public void ScalarOp_FillsCellValue()
    {
        var src = TempFile("src.docx");
        TestDocBuilder.Create(src,
            tables: [TestDocBuilder.LQRATable(("公司名称", ""), ("注册资本", ""))]);

        var resolver = TestData.Resolver(scalars: new()
        {
            ["manager.Name"] = "测试基金管理有限公司",
            ["manager.RegisterCapital"] = "5000万元",
        });

        var ops = new List<FillOperator>
        {
            new ScalarOp { Entity = "manager", Property = "Name", Question = "公司名称",
                Location = new Location { Table = 0, Row = 0, Col = 1 } },
            new ScalarOp { Entity = "manager", Property = "RegisterCapital", Question = "注册资本",
                Location = new Location { Table = 0, Row = 1, Col = 1 } },
        };

        var outPath = TempFile("out.docx");
        DocOps.Fill(src, outPath, ops, resolver);

        using var doc = WordprocessingDocument.Open(outPath, false);
        var table = doc.MainDocumentPart!.Document.Body!.Elements<Table>().First();
        var rows = table.Elements<TableRow>().ToList();
        Assert.Contains("测试基金管理有限公司", rows[0].Elements<TableCell>().ElementAt(1).InnerText);
        Assert.Contains("5000万元", rows[1].Elements<TableCell>().ElementAt(1).InnerText);
    }

    [Fact]
    public void TypeB_RecommendOp_FillsAndSkipsOutOfBounds()
    {
        var src = TempFile("src.docx");
        TestDocBuilder.Create(src,
            tables: [TestDocBuilder.LQRATable(("产品名称", ""), ("产品名称", ""))]);

        var resolver = TestData.Resolver(recommends: new()
        {
            [0] = new() { ["Name"] = "稳健一号", ["Scale"] = "2亿" },
        });

        var ops = new List<FillOperator>
        {
            new RecommendOp
            {
                Range = new Range { Table = 0, Start = new Location { Row = 0, Col = 1 }, End = new Location { Row = 0, Col = 1 } },
                FundIndex = 0, Table = "要素表",
                Props = [new RecommendPropItem { Row = 0, Col = 1, Prop = "Name", Header = "产品名称" }]
            },
            new RecommendOp
            {
                Range = new Range { Table = 0, Start = new Location { Row = 1, Col = 1 }, End = new Location { Row = 1, Col = 1 } },
                FundIndex = 1, Table = "要素表",
                Props = [new RecommendPropItem { Row = 1, Col = 1, Prop = "Name", Header = "产品名称" }]
            },
        };

        var outPath = TempFile("out.docx");
        DocOps.Fill(src, outPath, ops, resolver);

        using var doc = WordprocessingDocument.Open(outPath, false);
        var table = doc.MainDocumentPart!.Document.Body!.Elements<Table>().First();
        var rows = table.Elements<TableRow>().ToList();

        Assert.Contains("稳健一号", rows[0].Elements<TableCell>().ElementAt(1).InnerText);
        Assert.True(string.IsNullOrEmpty(rows[1].Elements<TableCell>().ElementAt(1).InnerText.Trim()),
            $"期望空单元格，实际: '{rows[1].Elements<TableCell>().ElementAt(1).InnerText}'");
    }

    [Fact]
    public void TypeC_ListExpand_NoExpansionWhenDataFits()
    {
        var src = TempFile("src.docx");
        TestDocBuilder.Create(src,
            tables: [TestDocBuilder.ListTable(
                ["姓名", "持股比例"],
                [new string?[2], new string?[2], new string?[2], new string?[2]])]);

        var resolver = TestData.Resolver(lists: new()
        {
            ["shareholder"] = new[]
            {
                new Dictionary<string, string> { ["Name"] = "张三", ["Ratio"] = "60%" },
                new Dictionary<string, string> { ["Name"] = "李四", ["Ratio"] = "40%" },
            },
        });

        var ops = new List<FillOperator>
        {
            new ListExpandOp
            {
                Entity = "shareholder",
                Properties = [new PropItem("Name", "姓名", 1, 0), new PropItem("Ratio", "持股比例", 1, 1)],
                Range = new Range { Table = 0, Start = new Location { Row = 1, Col = 0 }, End = new Location { Row = 4, Col = 1 } },
            },
        };

        var outPath = TempFile("out.docx");
        DocOps.Fill(src, outPath, ops, resolver);

        using var doc = WordprocessingDocument.Open(outPath, false);
        var table = doc.MainDocumentPart!.Document.Body!.Elements<Table>().First();
        var rows = table.Elements<TableRow>().ToList();
        var text = string.Join(" ", rows.Select(r => r.InnerText));
        Assert.Contains("张三", text);
        Assert.Contains("60%", text);
        Assert.Contains("李四", text);
        Assert.Contains("40%", text);
        Assert.Equal(5, rows.Count); // 1 header + 4 data
    }

    [Fact]
    public void TypeC_ListExpand_ExpandsRowsWhenDataExceeds()
    {
        var src = TempFile("src.docx");
        TestDocBuilder.Create(src,
            tables: [TestDocBuilder.ListTable(
                ["姓名", "持股比例"],
                [new string?[2], new string?[2]])]);

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

        var ops = new List<FillOperator>
        {
            new ListExpandOp
            {
                Entity = "shareholder",
                Properties = [new PropItem("Name", "姓名", 1, 0), new PropItem("Ratio", "持股比例", 1, 1)],
                Range = new Range { Table = 0, Start = new Location { Row = 1, Col = 0 }, End = new Location { Row = 2, Col = 1 } },
            },
        };

        var outPath = TempFile("out.docx");
        DocOps.Fill(src, outPath, ops, resolver);

        using var doc = WordprocessingDocument.Open(outPath, false);
        var table = doc.MainDocumentPart!.Document.Body!.Elements<Table>().First();
        var rows = table.Elements<TableRow>().ToList();
        Assert.Equal(5, rows.Count); // 1 header + 2 original + 2 expanded
        Assert.Contains("C", rows[3].Elements<TableCell>().First().InnerText);
    }

    [Fact]
    public void TypeC_MultipleExpands_CumulativeOffset()
    {
        var table = new Table();
        AddRow(table, "人员类别", "姓名", "职务");
        AddRow(table, "", "", "");
        AddRow(table, "", "", "");
        AddRow(table, "", "", "");
        AddRow(table, "", "", "");

        var src = TempFile("src.docx");
        TestDocBuilder.Create(src, tables: [table]);

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
                Properties = [new PropItem("Name", "姓名", 1, 1), new PropItem("Title", "职务", 1, 2)],
                Range = new Range { Table = 0, Start = new Location { Row = 1, Col = 1 }, End = new Location { Row = 2, Col = 2 } },
            },
            new ListExpandOp
            {
                Entity = "riskctrl",
                Properties = [new PropItem("Name", "姓名", 1, 1), new PropItem("Title", "职务", 1, 2)],
                Range = new Range { Table = 0, Start = new Location { Row = 3, Col = 1 }, End = new Location { Row = 4, Col = 2 } },
            },
        };

        var outPath = TempFile("out.docx");
        DocOps.Fill(src, outPath, ops, resolver);

        using var doc = WordprocessingDocument.Open(outPath, false);
        var resultTable = doc.MainDocumentPart!.Document.Body!.Elements<Table>().First();
        var rows = resultTable.Elements<TableRow>().ToList();
        var text = string.Join(" ", rows.Select(r => r.InnerText));
        Assert.Contains("E1", text);
        Assert.Contains("E2", text);
        Assert.Contains("E3", text);
        Assert.Contains("R1", text);
        Assert.Contains("R2", text);
        Assert.Contains("R3", text);
        Assert.Equal(7, rows.Count); // 1 header + 2 original exec + 2 original risk + 1 exec expanded + 1 risk expanded
    }

    [Fact]
    public void TypeD_GridOp_EntityPerRow_FillsByRowHeader()
    {
        var src = TempFile("src.docx");
        TestDocBuilder.Create(src,
            tables: [TestDocBuilder.GridTable(
                ["总资产", "总负债"],
                ["2023", "2022"],
                new string?[2, 2])]);

        var resolver = TestData.Resolver(lists: new()
        {
            ["financialstatement"] = new[]
            {
                new Dictionary<string, string> { ["Year"] = "2023", ["TotalAssets"] = "100亿", ["TotalLiabilities"] = "60亿" },
                new Dictionary<string, string> { ["Year"] = "2022", ["TotalAssets"] = "80亿", ["TotalLiabilities"] = "50亿" },
            },
        });

        var ops = new List<FillOperator>
        {
            new GridOp
            {
                Entity = "financialstatement",
                Properties = [new PropItem("TotalAssets", "总资产", 1, 1), new PropItem("TotalLiabilities", "总负债", 2, 2)],
                Range = new Range { Table = 0, Start = new Location { Row = 1, Col = 1 }, End = new Location { Row = 2, Col = 2 } },
                EntityPerRow = true,
                FilterBy = "Year",
            },
        };

        var outPath = TempFile("out.docx");
        DocOps.Fill(src, outPath, ops, resolver);

        using var doc = WordprocessingDocument.Open(outPath, false);
        var resultTable = doc.MainDocumentPart!.Document.Body!.Elements<Table>().First();
        var rows = resultTable.Elements<TableRow>().ToList();
        var text = string.Join(" ", rows.Select(r => r.InnerText));
        Assert.Contains("100亿", text);
        Assert.Contains("60亿", text);
        Assert.Contains("80亿", text);
        Assert.Contains("50亿", text);
    }

    [Fact]
    public void TypeE_GridOp_EntityPerCol_FillsByColHeader()
    {
        var src = TempFile("src.docx");
        TestDocBuilder.Create(src,
            tables: [TestDocBuilder.GridTable(
                ["2023", "2022"],
                ["总资产", "总负债"],
                new string?[2, 2])]);

        var resolver = TestData.Resolver(lists: new()
        {
            ["financialstatement"] = new[]
            {
                new Dictionary<string, string> { ["Year"] = "2023", ["TotalAssets"] = "100亿", ["TotalLiabilities"] = "60亿" },
                new Dictionary<string, string> { ["Year"] = "2022", ["TotalAssets"] = "80亿", ["TotalLiabilities"] = "50亿" },
            },
        });

        var ops = new List<FillOperator>
        {
            new GridOp
            {
                Entity = "financialstatement",
                Properties = [new PropItem("TotalAssets", "总资产", 1, 1), new PropItem("TotalLiabilities", "总负债", 2, 2)],
                Range = new Range { Table = 0, Start = new Location { Row = 1, Col = 1 }, End = new Location { Row = 2, Col = 2 } },
                EntityPerRow = false,
                FilterBy = "Year",
            },
        };

        var outPath = TempFile("out.docx");
        DocOps.Fill(src, outPath, ops, resolver);

        using var doc = WordprocessingDocument.Open(outPath, false);
        var resultTable = doc.MainDocumentPart!.Document.Body!.Elements<Table>().First();
        var rows = resultTable.Elements<TableRow>().ToList();
        var text = string.Join(" ", rows.Select(r => r.InnerText));
        Assert.Contains("100亿", text);
        Assert.Contains("60亿", text);
        Assert.Contains("80亿", text);
        Assert.Contains("50亿", text);
    }

    [Fact]
    public void TypeF_ParagraphOp_FillsParagraph()
    {
        var src = TempFile("src.docx");
        TestDocBuilder.Create(src, paragraphs: ["请简述投资策略：", ""]);

        var resolver = TestData.Resolver(answers: new()
        {
            ["请简述投资策略："] = "本基金采用量化多因子策略",
        });

        var ops = new List<FillOperator>
        {
            new ParagraphOp { Question = "请简述投资策略：", Location = new Location { Para = 1 } },
        };

        var outPath = TempFile("out.docx");
        DocOps.Fill(src, outPath, ops, resolver);

        var result = DocOps.ReadParagraphs(outPath);
        Assert.Contains("本基金采用量化多因子策略", result);
    }

    [Fact]
    public void TypeF_ParagraphOp_InsertsNewParaWhenTargetNotEmpty()
    {
        // 问题段落后面没有空行，AI 误将 Type z 定位到问题段落本身
        // 问题文本超过6个字，应插入新段落保留问题
        var src = TempFile("src.docx");
        TestDocBuilder.Create(src, paragraphs: ["请简述投资策略："]);

        var resolver = TestData.Resolver(answers: new()
        {
            ["请简述投资策略："] = "本基金采用量化多因子策略",
        });

        var ops = new List<FillOperator>
        {
            new ParagraphOp { Question = "请简述投资策略：", Location = new Location { Para = 0 } },
        };

        var outPath = TempFile("out.docx");
        DocOps.Fill(src, outPath, ops, resolver);

        var result = DocOps.ReadParagraphs(outPath);
        // 问题文本必须保留（超过6个字）
        Assert.Contains("请简述投资策略：", result);
        // 答案也必须出现
        Assert.Contains("本基金采用量化多因子策略", result);
    }

    [Fact]
    public void TypeF_ParagraphOp_OverwritesShortParagraph()
    {
        // 段落少于6个字，视为空段落，直接覆盖
        var src = TempFile("src.docx");
        TestDocBuilder.Create(src, paragraphs: ["简介："]);  // 3个字

        var resolver = TestData.Resolver(answers: new()
        {
            ["简介："] = "某基金管理有限公司成立于2020年",
        });

        var ops = new List<FillOperator>
        {
            new ParagraphOp { Question = "简介：", Location = new Location { Para = 0 } },
        };

        var outPath = TempFile("out.docx");
        DocOps.Fill(src, outPath, ops, resolver);

        var result = DocOps.ReadParagraphs(outPath);
        // 原短文本被覆盖，不再出现
        Assert.DoesNotContain("简介：", result);
        // 答案直接写入该段落
        Assert.Contains("某基金管理有限公司成立于2020年", result);
    }

    [Fact]
    public void TypeA_ScalarOp_InsertsNewParaWhenTargetNotEmpty()
    {
        // Type a 段落定位：目标段落已有内容时，应插入新段落写入
        // 问题文本超过6个字，应插入新段落保留问题
        var src = TempFile("src.docx");
        TestDocBuilder.Create(src, paragraphs: ["公司简介描述："]);  // 7个字，超过6字阈值

        var resolver = TestData.Resolver(scalars: new()
        {
            ["manager.Description"] = "某基金管理有限公司成立于2020年",
        });

        var ops = new List<FillOperator>
        {
            new ScalarOp { Entity = "manager", Property = "Description", Question = "公司简介",
                Location = new Location { Para = 0 } },
        };

        var outPath = TempFile("out.docx");
        DocOps.Fill(src, outPath, ops, resolver);

        var result = DocOps.ReadParagraphs(outPath);
        // 问题文本必须保留（超过6个字）
        Assert.Contains("公司简介描述：", result);
        // 答案也必须出现
        Assert.Contains("某基金管理有限公司成立于2020年", result);
    }

    [Fact]
    public void TypeA_ScalarOp_OverwritesShortParagraph()
    {
        // 段落少于6个字，视为空段落，直接覆盖
        var src = TempFile("src.docx");
        TestDocBuilder.Create(src, paragraphs: ["名称："]);  // 3个字

        var resolver = TestData.Resolver(scalars: new()
        {
            ["manager.Name"] = "测试基金管理有限公司",
        });

        var ops = new List<FillOperator>
        {
            new ScalarOp { Entity = "manager", Property = "Name", Question = "名称",
                Location = new Location { Para = 0 } },
        };

        var outPath = TempFile("out.docx");
        DocOps.Fill(src, outPath, ops, resolver);

        var result = DocOps.ReadParagraphs(outPath);
        // 原短文本被覆盖，不再出现
        Assert.DoesNotContain("名称：", result);
        // 答案直接写入该段落
        Assert.Contains("测试基金管理有限公司", result);
    }

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

        Assert.Contains("段落一", parsed);
        Assert.Contains("段落二", parsed);
        Assert.Contains("T[0]", parsed);
        Assert.Contains("T[1]", parsed);
        Assert.Contains("[0,0]", parsed);
        Assert.Contains("[0,1]", parsed);
        Assert.Contains("[has header]", parsed);
    }

    private static void AddRow(Table table, params string[] cells)
    {
        var row = new TableRow();
        foreach (var c in cells)
            row.AppendChild(TestDocBuilder.MakeCell(c));
        table.AppendChild(row);
    }

    [Fact]
    public void ResolvePlaceholders_ScalarEntity()
    {
        var resolver = TestData.Resolver(scalars: new()
        {
            ["manager.Name"] = "某某基金管理有限公司",
        });
        var result = resolver.ResolvePlaceholders("公司名称：{{manager.Name}}，欢迎咨询。");
        Assert.Equal("公司名称：某某基金管理有限公司，欢迎咨询。", result);
    }

    [Fact]
    public void ResolvePlaceholders_FundById()
    {
        var fund = new FundInfo { Id = 42, Name = "优选成长基金", Code = "001234" };
        var resolver = new DataResolver(
            scalars: new Dictionary<string, IResolve>(),
            lists: new(),
            recommendFunds: new Dictionary<int, FundInfo> { [0] = fund },
            answersByQuestion: new(),
            fileName: null,
            fundBindings: new());
        var result = resolver.ResolvePlaceholders("推荐产品：{{fund#42.Name}}（{{fund#42.Code}}）");
        Assert.Equal("推荐产品：优选成长基金（001234）", result);
    }

    [Fact]
    public void ResolvePlaceholders_WithFormat()
    {
        var resolver = new DataResolver(
            scalars: new Dictionary<string, IResolve>
            {
                ["manager"] = new ObjectEntity(new() { ["EstablishDate"] = new DateTime(2020, 3, 15) }),
            },
            lists: new(),
            recommendFunds: new(),
            answersByQuestion: new(),
            fileName: null,
            fundBindings: new());
        var result = resolver.ResolvePlaceholders("成立日期：{{manager.EstablishDate:yyyy}}年");
        Assert.Equal("成立日期：2020年", result);
    }

    [Fact]
    public void FillParagraph_WithPlaceholders_ResolvesValues()
    {
        var src = TempFile("src.docx");
        TestDocBuilder.Create(src, paragraphs: ["基金简介：", ""]);

        var resolver = TestData.Resolver(
            scalars: new() { ["manager.Name"] = "某某基金" },
            answers: new() { ["基金简介："] = "本基金由{{manager.Name}}管理。" });

        var ops = new List<FillOperator>
        {
            new ParagraphOp { Question = "基金简介：", Location = new Location { Para = 1 } },
        };

        var outPath = TempFile("out.docx");
        DocOps.Fill(src, outPath, ops, resolver);

        var result = DocOps.ReadParagraphs(outPath);
        Assert.Contains("本基金由某某基金管理。", result);
    }
}