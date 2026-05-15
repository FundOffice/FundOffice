using FMO.Models;
using FMO.Utilities;
using LiteDB;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Utilities;

namespace FMO.TPL;

public enum ChooseType
{
    Single,
    Multiple,
    ALL
}

public record InputInfo(string Field, ChooseType ChooseType);

public record ReferenceInfo(string Field, string Filter);

public class TemplateMeta
{
    public string? Name { get; set; }

    public string? Description { get; set; }

    public string? Version { get; set; }

    public string? Limit { get; set; }

    public InputInfo[] Input { get; set; } = [];

    public ReferenceInfo[] ReferenceInfo { get; set; } = [];


    public string? Script { get; set; }

}

public class ExcelTemplate
{

    public TemplateMeta Meta { get; private set; } = null!;

    public MemoryStream Excel { get; private set; } = null!;


    public static async Task<ExcelTemplate?> Load(string file)
    {
        // 路径为空/空白 → 抛异常（无效参数）
        if (string.IsNullOrWhiteSpace(file))
            throw new ArgumentException("模板文件路径不能为空或空白字符", nameof(file));

        if (!File.Exists(file))
            return null;

        using var fs = new FileStream(file, FileMode.Open);
        using var zip = await ZipArchive.CreateAsync(fs, ZipArchiveMode.Read, true, Encoding.UTF8);

        var entry = zip.GetEntry("def");
        if (entry is null) return null;

        using var defStream = entry.Open();
        if (defStream is null) return null;


        var bytes = new byte[entry.Length];
        defStream.ReadExactly(bytes, 0, bytes.Length);


        var ti = Parse(bytes);
        if (ti is null) return null;


        using var tplStream = zip.GetEntry("tpl.xlsx")?.Open();
        if (tplStream is null) return null;

        MemoryStream ms = new MemoryStream();
        tplStream.CopyTo(ms);

        return new ExcelTemplate { Meta = ti, Excel = ms };

    }

    public static TemplateMeta? Parse(byte[] def)
    {
        var encr = AesHelper.Decrypt(def);
        var dec = Encoding.UTF8.GetString(encr);

        var parts = dec.Split("---");
        if (parts.Length != 3) return null;


        var meta = new TemplateMeta();

        ParseBaseMetadata(meta, parts[0]);
        ParseInputAndReference(meta, parts[1]);


        meta.Script = parts[2].Trim();


        return meta;
    }


    /// <summary>
    /// 准备数据
    /// </summary>
    /// <param name="inputs"></param>
    /// <returns></returns>
    public async Task<TemplateGlobal> Prepare(TemplateGlobal inputs)
    {
        using var db = DbHelper.Base();

        foreach (var item in Meta.ReferenceInfo)
        {
            switch (item.Field)
            {
                case nameof(TemplateGlobal.Funds):
                    if (inputs.Funds is null)
                        inputs.Funds = db.GetCollection<Fund>().FindAll().ToArray();
                    break;

                case nameof(TransferRecord):
                    inputs.Records = await Make<TransferRecord>(db.GetCollection<TransferRecord>().Query(), inputs, item.Filter);

                    break;

                case nameof(DailyValue):
                    await MakeDaily(db, inputs, item);
                    break;

                default:
                    break;
            }
        }



        return inputs;
    }

    public async Task<object> Execute(TemplateGlobal param)
    {
        var option = ScriptOptions.Default
            .AddReferences(Assembly.GetExecutingAssembly())
            .WithImports("System", "System.Collections.Generic", "System.Linq", "FMO.Models");

        return await CSharpScript.EvaluateAsync(Meta.Script, option, globals: param, globalsType: typeof(TemplateGlobal));
    }


    private async Task<T[]> Make<T>(ILiteQueryable<T> queryable, TemplateGlobal param, string qu)
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

    private async Task MakeDaily(BaseDatabase db, TemplateGlobal inputs, ReferenceInfo item)
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
                    v.AddRange(await Make(queryable, inputs, item.Filter));
            }
        }

        inputs.Dailies = [.. v];

        return;
    }

     



    #region 核心解析辅助方法
    /// <summary>
    /// 解析基础元数据
    /// </summary>
    private static void ParseBaseMetadata(TemplateMeta meta, string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return;

        // 静态编译正则，提升性能
        Regex _metaRegex = new Regex(@"(\w+)\s+(.+)$", RegexOptions.Compiled | RegexOptions.Multiline);


        foreach (var line in content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var match = _metaRegex.Match(line);
            if (!match.Success) continue;

            var key = match.Groups[1].Value.ToLower();
            var value = match.Groups[2].Value.Trim();

            switch (key)
            {
                case "name": meta.Name = value; break;
                case "desc": meta.Description = value; break;
                case "version": meta.Version = value; break;
                case "limit": meta.Limit = value; break;
            }
        }
    }

    /// <summary>
    /// 解析输入参数(Input)和数据源引用(ReferenceInfo)
    /// 支持格式：
    /// Input：Fund / Fund[s] / Fund[m] / Fund[a]
    /// Reference：Daily / Daily[Fund,Date]
    /// 行内支持 ; 分隔多个项，取第一个有效项赋值
    /// </summary>
    private static void ParseInputAndReference(TemplateMeta meta, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return;

        // 按行拆分
        var lines = content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        // 通用正则：匹配 字段[可选参数] 格式，支持无括号
        Regex _optionalBracketRegex = new(@"^(\w+)(?:\[([\.]*)\])?$", RegexOptions.Compiled);

        foreach (var line in lines)
        {
            // --------------------
            // 解析 Input：按 ; 分割多个项
            // --------------------
            if (line.StartsWith("input", StringComparison.OrdinalIgnoreCase))
            {
                // 提取input内容后，按;拆分多个参数项
                var inputContent = line["input".Length..].Trim();
                var inputItems = inputContent.Split([';', '；'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

                meta.Input = [..inputItems.Select(x => _optionalBracketRegex.Match(x)).Where(x => x.Success).Select(match =>
                {
                    var field = match.Groups[1].Value;
                    var typeFlag = match.Groups[2].Value.ToLower();
                    // 无括号默认单选
                    var chooseType = typeFlag switch
                    {
                        "m" => ChooseType.Multiple,
                        "a" => ChooseType.ALL,
                        _ => ChooseType.Single
                    };
                    return new InputInfo(field, chooseType);

                })];
            }

            // --------------------
            // 解析 Reference：按 ; 分割多个项
            // --------------------
            else if (line.StartsWith("using", StringComparison.OrdinalIgnoreCase))
            {
                // 提取using内容后，按;拆分多个引用项
                var refContent = line["using".Length..].Trim();
                var refItems = refContent.Split([';', '；'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

                meta.ReferenceInfo =[.. refItems.Select(x=> x.Split('#', StringSplitOptions.RemoveEmptyEntries| StringSplitOptions.TrimEntries)).Select(x=> new ReferenceInfo(x[0], x.Length > 0 ?x[1]:""))];

            }
        }
    }



    #endregion
}