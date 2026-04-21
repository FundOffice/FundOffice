using FMO.Models;
using MiniSoftware;
using Serilog;

namespace FMO.TPL;

public static class WordTpl
{

    /// <summary>
    /// 生成备案承诺函
    /// </summary>
    /// <returns></returns>
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
            MiniWord.SaveAsByTemplate(path, fi.FullName, values);

            return true;
        }
        catch (Exception e)
        {
            Log.Error($"生成失败：{e.Message}");
            return false;
        }
    }
}
