using FMO.Models;
using FMO.Utilities;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Win32;
using MiniExcelLibs;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Utilities;

namespace FMO.TPL;


public class ExcelTemplateInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Version { get; set; } = "";
    public string Class { get; set; } = "";


}

public class ExcelTemplate
{

    public TemplateMeta Meta { get; private set; } = null!;

    public TemplateScript Script { get; private set; } = null!;


    public static TemplateMeta[] GetTemplates()
    {
        using var db = DbHelper.Template();
        return db.GetCollection<TemplateMeta>().FindAll().ToArray();
    }


    public static ExcelTemplate Load(TemplateMeta meta)
    {
        using var db = DbHelper.Template();
        var script = db.GetCollection<TemplateScript>().FindById(meta.Id);


        return new ExcelTemplate { Meta = meta, Script = script };
    }




    public static async Task<bool> Import(string file)
    {
        // 路径为空/空白 → 抛异常（无效参数）
        if (string.IsNullOrWhiteSpace(file))
            throw new ArgumentException("模板文件路径不能为空或空白字符", nameof(file));

        if (!File.Exists(file))
            return false;

        using var fs = new FileStream(file, FileMode.Open);
        using var zip = await ZipArchive.CreateAsync(fs, ZipArchiveMode.Read, true, Encoding.UTF8);

        var entry = zip.GetEntry(".def");
        using var stream = entry?.Open();
        if (stream is null)
            throw new Exception("模板缺少 meta");

        using var sr = new StreamReader(stream);

        var meta = JsonSerializer.Deserialize<TemplateMeta>(stream);

        if (meta is null)
            throw new Exception("模板定义文件解析失败，可能文件已损坏或格式不正确");

        /////////////////
        entry = zip.GetEntry(".script");
        using var scriptStream = entry?.Open();
        if (scriptStream is null)
            throw new Exception("模板缺少script");

        using var sr2 = new StreamReader(scriptStream);

        string scriptJson;
        try
        {
            var du = GetCode(meta);
            scriptJson = AesHelper.Decrypt(sr2.ReadToEnd(), du);
        }
        catch
        {
            throw new InvalidDataException("script 加载失败，无权限");
        }

        bool verifySuccess = SecurityHelper.Verify(scriptJson, meta.Sign!);
        if (!verifySuccess)
            throw new InvalidDataException("RSA验签失败，文件已被篡改");


        var script = JsonSerializer.Deserialize<TemplateScript>(scriptJson);
        if (script is null)
            throw new InvalidDataException("Script内容异常");


        ////////////////////////////
        entry = zip.GetEntry("tpl.xlsx");
        var di = Directory.CreateDirectory(@$"files\tpl\excel\{meta.Id}");
        string path = Path.Combine(di.FullName, "默认模板.xlsx");
        using (var tps = new FileStream(path, FileMode.Create, FileAccess.Write))
        {
            using var tplStream = entry?.Open();
            if (tplStream is null)
                throw new Exception("模板缺少tpl.xlsx");

            await tplStream.CopyToAsync(tps);
        }

        using var db = DbHelper.Template();
        db.GetCollection<TemplateMeta>().Upsert(meta);
        db.GetCollection<TemplateScript>().Upsert(script);
        db.FileStorage.Upload(meta.Id, path);

        return true;
    }


    private static string GetCode(TemplateMeta meta)
    {
        if (meta.Limit != "vip") return "everyone";

#pragma warning disable CA1416
#if RELEASE
        using (var key = Registry.CurrentUser.OpenSubKey(@$"Software\Nexus"))
#else
        using (var key = Registry.CurrentUser.OpenSubKey(@$"Software\Nexus\Debug"))
#endif
            return AesHelper.Decrypt((key!.GetValue("Code") as string)!);
#pragma warning restore CA1416
    }


    /// <summary>
    /// 准备数据
    /// </summary>
    /// <param name="sg"></param>
    /// <returns></returns>
    public async Task<ScriptGlobal> Prepare(ScriptGlobal sg)
    {
        using var db = DbHelper.Base();

        for (int i = 0; i < Script.Input.Length; i++)
        {
            if (Script.Input[i] is InputFund funds)
                sg.Funds = sg.inputs[i] as Fund[];
            else if(Script.Input[i] is InputInvestor)
                sg.Investors = sg.inputs[i] as Investor[];
        } 



        foreach (var item in Script.Refer)
        {
            switch (item.Field)
            {
                case nameof(ScriptGlobal.Funds):
                    if (sg.Funds is null)
                        sg.Funds = db.GetCollection<Fund>().FindAll().ToArray();
                    break;

                case nameof(ScriptGlobal.Records):
                    sg.Records = await GetData(db.GetCollection<TransferRecord>().Query(), sg, item.Filter);

                    break;

                case nameof(ScriptGlobal.Dailies):
                    await MakeDaily(db, sg, item);
                    break;

                case nameof(ScriptGlobal.Investors):
                    sg.Investors = await GetData(db.GetCollection<Investor>().Query(), sg, item.Filter);
                    break;

                default:
                    break;
            }
        }



        return sg;
    }

    public async Task<object> GetFillData(ScriptGlobal param)
    {
        var option = ScriptOptions.Default
            .AddReferences(Assembly.GetExecutingAssembly())
            .WithImports("System", "System.Collections.Generic", "System.Linq", "FMO.Models");

        return await CSharpScript.EvaluateAsync(Script.Script, option, globals: param, globalsType: typeof(ScriptGlobal));
    }


    private async Task<T[]> GetData<T>(LiteDB.ILiteQueryable<T> queryable, ScriptGlobal param, string qu)
    {
        if (string.IsNullOrWhiteSpace(qu))
            return queryable.ToArray();

        var option = ScriptOptions.Default
            .AddReferences(Assembly.GetExecutingAssembly())
            .WithImports("System", "System.Collections.Generic", "System.Linq", "FMO.Models", "LiteDB");

        var g = new TemplateRefer<T> { query = queryable };
        g.UpdateFrom(param);
        return await CSharpScript.EvaluateAsync<T[]>(qu, option, globals: g, globalsType: typeof(TemplateRefer<T>));
    }

    private async Task MakeDaily(BaseDatabase db, ScriptGlobal inputs, ReferenceInfo item)
    {
        List<DailyValue> v = [];
        foreach (var fd in inputs.Funds!)
        {
            var colnames = db.GetCollectionNames().Where(x => Regex.IsMatch(x, @$"fv_{fd.Id}\b"));
            foreach (var cn in colnames)
            {
                var queryable = db.GetDailyCollection(fd.Id).Query();
                if (string.IsNullOrWhiteSpace(item.Filter))
                    v.AddRange(queryable.ToArray());
                else
                    v.AddRange(await GetData(queryable, inputs, item.Filter));
            }
        }

        inputs.Dailies = [.. v];

        return;
    }


    public async Task SaveTo(string path, ScriptGlobal g, string? tplPath = null)
    {
        var data = await GetFillData(g);
        if (string.IsNullOrWhiteSpace(tplPath) || !File.Exists(tplPath))
            await MiniExcel.SaveAsByTemplateAsync(path, LoadDefault(Meta), data);
        else
            await MiniExcel.SaveAsByTemplateAsync(path, tplPath, data);
    }

    private Stream LoadDefault(TemplateMeta meta)
    {
        using var db = DbHelper.Template();
        var ms = new MemoryStream();
        db.FileStorage.Download(meta.Id, ms);
        ms.Seek(0, SeekOrigin.Begin);
        return ms;
    }
}
