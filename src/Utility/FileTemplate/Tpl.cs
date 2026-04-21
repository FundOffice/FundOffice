using FMO.Models;
using MiniExcelLibs;
using MiniSoftware;
using Serilog;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace FMO.TPL;

public static class Tpl
{
    public static bool IsExists(string name) => File.Exists(@$"files\tpl\{name}");


    public static string GetPath(string name) => @$"files\tpl\{name}";



    public static bool Generate(string path, string tpl, object obj)
    {
        try
        {
            var di = new FileInfo(path).Directory;
            if (di is not null && !di.Exists) di.Create();

            if (Regex.IsMatch(tpl, @"\.doc|\.docx", RegexOptions.IgnoreCase))
            {
                MiniWord.SaveAsByTemplate(path, tpl, obj);
                return true;
            }
            if (Regex.IsMatch(tpl, @"\.xls|\.xlsx", RegexOptions.IgnoreCase))
            {
                MiniExcel.SaveAsByTemplate(path, tpl, obj);
                XlsxFormula.MakeFormula(path);
                return true;
            }
            return false;
        }
        catch { return false; }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="path"></param>
    /// <param name="tplName">模板文件名</param>
    /// <param name="obj"></param>
    /// <returns></returns>
    public static bool GenerateByPredefined(string path, string tplName, object obj)
    {
        if (string.IsNullOrWhiteSpace(tplName)) return false;
        if (!IsExists(tplName)) return false;

        try { Generate(path, GetPath(tplName), obj); return true; } catch { return false; }
    }


    public static bool GenerateRegisterAnounce(Fund fund, string path)
    {
        // 获取模板
        var fi = new FileInfo(@"files\tpl\备案承诺函.docx");
        if (!fi.Exists)
        {
            Log.Error("生成备案承诺函失败：未找到模板");
            return false;
        }

        try
        {
            MiniWord.SaveAsByTemplate(path, fi.FullName, new Dictionary<string, object> { { "Name", fund.Name }, { "Code", fund.Code! } });
            return true;
        }
        catch (Exception e)
        {
            Log.Error($"生成备案承诺函失败：{e.Message}");
            return false;
        }
    }
}



public class XlsxFormula
{
    public static void MakeFormula(string xlsxPath)
    {
        if (string.IsNullOrEmpty(xlsxPath) || !File.Exists(xlsxPath))
        {
            throw new ArgumentException("Invalid XLSX file path");
        }

        string tempDir = Path.Combine(Path.GetTempPath(), "XlsxConverter_" + Guid.NewGuid().ToString());

        try
        {
            // 解压XLSX文件
            ZipFile.ExtractToDirectory(xlsxPath, tempDir);

            // 处理所有工作表
            string worksheetsDir = Path.Combine(tempDir, "xl", "worksheets");
            if (Directory.Exists(worksheetsDir))
            {
                foreach (string file in Directory.GetFiles(worksheetsDir, "*.xml"))
                {
                    ProcessWorksheetFile(file);
                }
            }

            // 修改工作簿设置，确保Excel打开时重新计算公式
            UpdateWorkbookForRecalculation(tempDir);

            // 创建输出文件名
            string outputFile = xlsxPath;

            // 删除已存在的输出文件
            if (File.Exists(outputFile))
            {
                File.Delete(outputFile);
            }

            // 重新压缩为XLSX文件
            ZipFile.CreateFromDirectory(tempDir, outputFile);
        }
        finally
        {
            // 清理临时文件
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    private static void ProcessWorksheetFile(string filePath)
    {
        // 读取XML内容
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XDocument doc = XDocument.Load(filePath);

        // 查找所有包含$=的单元格
        var cells = doc.Descendants(ns + "c");
        bool modified = false;

        foreach (var cell in cells)
        {
            // 获取单元格的值
            var valueElement = cell.Element(ns + "v");
            var formulaElement = cell.Element(ns + "f");

            // 处理情况1: 单元格有值且以$=开头
            if (valueElement != null && valueElement.Value.StartsWith("$="))
            {
                // 提取公式部分
                string formula = valueElement.Value.Substring(1); // 去掉$符号，保留=开头

                // 移除原有的值元素
                valueElement.Remove();

                // 添加公式元素
                cell.Add(new XElement(ns + "f", formula));

                // 添加特殊计算值，使Excel重新计算
                cell.Add(new XElement(ns + "v", "#CALC!"));

                modified = true;
            }

        }

        // 只有当内容确实发生变化时才保存文件
        if (modified)
        {
            doc.Save(filePath);
        }
    }

    private static void UpdateWorkbookForRecalculation(string tempDir)
    {
        string workbookPath = Path.Combine(tempDir, "xl", "workbook.xml");
        if (!File.Exists(workbookPath))
        {
            Console.WriteLine("Workbook.xml not found, skipping recalculation settings.");
            return;
        }

        // 读取workbook.xml
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XDocument doc = XDocument.Load(workbookPath);

        // 查找或创建calcPr元素
        var calcPr = doc.Root?.Element(ns + "calcPr");
        if (calcPr is null)
        {
            calcPr = new XElement(ns + "calcPr");
            doc.Root?.Add(calcPr);
        }

        // 设置强制重新计算的属性
        calcPr.SetAttributeValue("fullCalcOnLoad", "1");
        calcPr.SetAttributeValue("calcMode", "auto");
        calcPr.SetAttributeValue("calcCompleted", "0"); // 0表示计算未完成

        // 保存修改
        doc.Save(workbookPath);
    }


}