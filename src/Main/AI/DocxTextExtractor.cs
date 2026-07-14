using System.IO.Compression;
using System.Text;
using System.Xml;

namespace FMO.AI;

/// <summary>
/// Docx 文档文本提取器
/// 从 docx 文件中提取纯文本，支持表格和公式（OMML → LaTeX）
/// </summary>
public static class DocxTextExtractor
{
    /// <summary>
    /// 从 docx 文件提取纯文本
    /// 使用 ZipFile + XmlDocument 直接读取，容错性好
    /// 正确处理表格：按行提取，单元格用 " | " 分隔
    /// 公式转为 LaTeX 格式
    /// </summary>
    public static string ExtractTextFromDocx(string docxPath)
    {
        try
        {
            using var zip = ZipFile.OpenRead(docxPath);
            var entry = zip.GetEntry("word/document.xml")
                ?? throw new FileNotFoundException("docx 文件中缺少 word/document.xml");

            using var entryStream = entry.Open();
            var xmlDoc = new XmlDocument();
            xmlDoc.Load(entryStream);

            var nsMgr = new XmlNamespaceManager(xmlDoc.NameTable);
            nsMgr.AddNamespace("w", "http://schemas.openxmlformats.org/wordprocessingml/2006/main");
            nsMgr.AddNamespace("m", "http://schemas.openxmlformats.org/officeDocument/2006/math");

            var body = xmlDoc.SelectSingleNode("//w:body", nsMgr);
            if (body == null) return "";

            var sb = new StringBuilder();
            foreach (XmlNode child in body.ChildNodes)
            {
                if (child.LocalName == "p")
                {
                    var text = ExtractXmlNodeText(child, nsMgr);
                    if (!string.IsNullOrEmpty(text))
                        sb.AppendLine(text);
                }
                else if (child.LocalName == "tbl")
                {
                    var rows = child.SelectNodes("w:tr", nsMgr);
                    if (rows == null) continue;
                    foreach (XmlNode row in rows)
                    {
                        var cells = row.SelectNodes("w:tc", nsMgr);
                        if (cells == null) continue;
                        var cellTexts = new List<string>();
                        foreach (XmlNode cell in cells)
                            cellTexts.Add(ExtractXmlNodeText(cell, nsMgr));
                        sb.AppendLine(string.Join(" | ", cellTexts));
                    }
                }
            }
            return sb.Length > 0 ? sb.ToString() : "";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AI] docx 文本提取失败: {ex.Message}");
            return "";
        }
    }

    /// <summary>
    /// 提取 XML 节点下所有 w:t 和 m:t（公式）文本，按文档顺序拼接
    /// </summary>
    private static string ExtractXmlNodeText(XmlNode node, XmlNamespaceManager nsMgr)
    {
        var sb = new StringBuilder();
        CollectTextInOrder(node, nsMgr, sb);
        return sb.ToString().Trim();
    }

    /// <summary>
    /// 按文档顺序递归收集文本，公式转为 LaTeX
    /// </summary>
    private static void CollectTextInOrder(XmlNode node, XmlNamespaceManager nsMgr, StringBuilder sb)
    {
        foreach (XmlNode child in node.ChildNodes)
        {
            // OMML 公式容器 → LaTeX
            if ((child.LocalName == "oMath" || child.LocalName == "oMathPara")
                && child.NamespaceURI == "http://schemas.openxmlformats.org/officeDocument/2006/math")
            {
                var latex = OmmlNodeToLatex(child, nsMgr);
                sb.Append(latex);
                continue;
            }

            if (child.NodeType == XmlNodeType.Text)
                sb.Append(child.Value);
            else if (child.LocalName == "t" && child.NamespaceURI == "http://schemas.openxmlformats.org/wordprocessingml/2006/main")
                sb.Append(child.InnerText);
            else if (child.HasChildNodes)
                CollectTextInOrder(child, nsMgr, sb);
        }
    }

    /// <summary>
    /// 将 OMML XmlElement 转为 LaTeX 字符串
    /// </summary>
    private static string OmmlNodeToLatex(XmlNode node, XmlNamespaceManager nsMgr)
    {
        var sb = new StringBuilder();
        foreach (XmlNode child in node.ChildNodes)
        {
            if (child.NamespaceURI != "http://schemas.openxmlformats.org/officeDocument/2006/math")
            {
                // 非 math 命名空间的元素（如 w:r），用 CollectTextInOrder 提取 w:t 文本
                CollectTextInOrder(child, nsMgr, sb);
                continue;
            }

            switch (child.LocalName)
            {
                case "f":
                    var num = child.SelectSingleNode("m:num", nsMgr);
                    var den = child.SelectSingleNode("m:den", nsMgr);
                    sb.Append($"\\frac{{{OmmlNodeToLatex(num!, nsMgr)}}}{{{OmmlNodeToLatex(den!, nsMgr)}}}");
                    break;

                case "sSup":
                    var supE = child.SelectSingleNode("m:e", nsMgr);
                    var supS = child.SelectSingleNode("m:sup", nsMgr);
                    sb.Append($"{{{OmmlNodeToLatex(supE!, nsMgr)}}}^{{{OmmlNodeToLatex(supS!, nsMgr)}}}");
                    break;

                case "sSub":
                    var subE = child.SelectSingleNode("m:e", nsMgr);
                    var subS = child.SelectSingleNode("m:sub", nsMgr);
                    sb.Append($"{{{OmmlNodeToLatex(subE!, nsMgr)}}}_{{{OmmlNodeToLatex(subS!, nsMgr)}}}");
                    break;

                case "sSubSup":
                    var ssE = child.SelectSingleNode("m:e", nsMgr);
                    var ssSub = child.SelectSingleNode("m:sub", nsMgr);
                    var ssSup = child.SelectSingleNode("m:sup", nsMgr);
                    sb.Append($"{{{OmmlNodeToLatex(ssE!, nsMgr)}}}_{{{OmmlNodeToLatex(ssSub!, nsMgr)}}}^{{{OmmlNodeToLatex(ssSup!, nsMgr)}}}");
                    break;

                case "rad":
                    var radE = child.SelectSingleNode("m:e", nsMgr);
                    var radDeg = child.SelectSingleNode("m:deg", nsMgr);
                    var degText = radDeg != null ? OmmlNodeToLatex(radDeg, nsMgr) : "";
                    if (string.IsNullOrEmpty(degText))
                        sb.Append($"\\sqrt{{{OmmlNodeToLatex(radE!, nsMgr)}}}");
                    else
                        sb.Append($"\\sqrt[{degText}]{{{OmmlNodeToLatex(radE!, nsMgr)}}}");
                    break;

                case "d":
                    var dPr = child.SelectSingleNode("m:dPr", nsMgr);
                    var beg = dPr?.SelectSingleNode("m:begChr/@m:val", nsMgr)?.Value ?? "(";
                    var end = dPr?.SelectSingleNode("m:endChr/@m:val", nsMgr)?.Value ?? ")";
                    var dContent = new List<string>();
                    foreach (XmlNode de in child.SelectNodes("m:e", nsMgr)!)
                        dContent.Add(OmmlNodeToLatex(de, nsMgr));
                    sb.Append($"\\left{beg}{string.Join(", ", dContent)}\\right{end}");
                    break;

                case "nary":
                    var naryChr = child.SelectSingleNode("m:naryPr/m:chr/@m:val", nsMgr)?.Value ?? "∑";
                    var naryOp = naryChr switch
                    {
                        "∑" => "\\sum",
                        "∏" => "\\prod",
                        "∫" => "\\int",
                        "∮" => "\\oint",
                        _ => naryChr
                    };
                    var narySub = child.SelectSingleNode("m:sub", nsMgr);
                    var narySup = child.SelectSingleNode("m:sup", nsMgr);
                    var naryE = child.SelectSingleNode("m:e", nsMgr);
                    sb.Append(naryOp);
                    if (narySub != null) sb.Append($"_{{{OmmlNodeToLatex(narySub, nsMgr)}}}");
                    if (narySup != null) sb.Append($"^{{{OmmlNodeToLatex(narySup, nsMgr)}}}");
                    if (naryE != null) sb.Append($" {OmmlNodeToLatex(naryE, nsMgr)}");
                    break;

                case "limLow":
                    var llE = child.SelectSingleNode("m:e", nsMgr);
                    var llLim = child.SelectSingleNode("m:lim", nsMgr);
                    sb.Append($"{{{OmmlNodeToLatex(llE!, nsMgr)}}}_{{{OmmlNodeToLatex(llLim!, nsMgr)}}}");
                    break;

                case "limUpp":
                    var luE = child.SelectSingleNode("m:e", nsMgr);
                    var luLim = child.SelectSingleNode("m:lim", nsMgr);
                    sb.Append($"{{{OmmlNodeToLatex(luE!, nsMgr)}}}^{{{OmmlNodeToLatex(luLim!, nsMgr)}}}");
                    break;

                case "func":
                    var funcF = child.SelectSingleNode("m:fName", nsMgr);
                    var funcE = child.SelectSingleNode("m:e", nsMgr);
                    sb.Append($"{OmmlNodeToLatex(funcF!, nsMgr)}\\left({OmmlNodeToLatex(funcE!, nsMgr)}\\right)");
                    break;

                case "bar":
                    var barE = child.SelectSingleNode("m:e", nsMgr);
                    sb.Append($"\\overline{{{OmmlNodeToLatex(barE!, nsMgr)}}}");
                    break;

                case "acc":
                    var accE = child.SelectSingleNode("m:e", nsMgr);
                    sb.Append($"\\hat{{{OmmlNodeToLatex(accE!, nsMgr)}}}");
                    break;

                case "groupChr":
                    var gcE = child.SelectSingleNode("m:e", nsMgr);
                    sb.Append($"\\underbrace{{{OmmlNodeToLatex(gcE!, nsMgr)}}}");
                    break;

                case "eqArr":
                    foreach (XmlNode eqE in child.SelectNodes("m:e", nsMgr)!)
                        sb.AppendLine(OmmlNodeToLatex(eqE, nsMgr) + " \\\\");
                    break;

                case "oMath":
                case "oMathPara":
                    sb.Append(OmmlNodeToLatex(child, nsMgr));
                    break;

                default:
                    // 未知 math 元素（m:r、m:e、m:sub 等）→ 收集所有子文本
                    CollectTextInOrder(child, nsMgr, sb);
                    break;
            }
        }
        return sb.ToString();
    }
}
