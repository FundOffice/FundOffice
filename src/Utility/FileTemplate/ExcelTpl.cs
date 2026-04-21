using MiniExcelLibs;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FMO.TPL;



public static class ExcelTpl
{

    /// <summary>
    /// 
    /// </summary>
    /// <param name="path">目标路径</param>
    /// <param name="tpl">模板文件名</param>
    /// <param name="values"></param>
    /// <returns></returns>
    public static bool GenerateFromTemplate(string path, string tpl, object values)
    {
        // 获取模板
        var fi = new FileInfo(@$"files\tpl\{tpl}");
        if (!fi.Exists)
        {
            Log.Error("生成失败：未找到模板");
            return false;
        }

        try
        {
            MiniExcel.SaveAsByTemplate(path, fi.FullName, values);

            return true;
        }
        catch (Exception e)
        {
            Log.Error($"生成失败：{e.Message}");
            return false;
        }
    }




}
