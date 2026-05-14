using FMO.Models;
using FMO.TPL;
using FMO.Utilities;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;

namespace TestScript;

[TestClass]
public sealed class ScriptExec
{
    [TestMethod]
    public async Task TestMethod1()
    {
        Initial.TestInit.SetAsDebug();

        var def =
           """
            input Fund 
            using Fund Daily


            return Funds[0].Id;
            var obj = new
            {
                Fund = Funds[0]

            };
            return obj;

            """;

        var reader = new StringReader(def);


        string input = reader.ReadLine()!;
        string refer = reader.ReadLine()!;

        var script = reader.ReadToEnd()!;


        using var db = DbHelper.Base();
        var funds = db.GetCollection<Fund>().Query().Limit(4).ToArray();

        var option = ScriptOptions.Default
            .AddReferences(Assembly.GetExecutingAssembly())
            .WithImports("System", "System.Collections.Generic", "FMO.Models");


        var param = new TemplateUsing();
        param.Funds = funds;
        var obj = await CSharpScript.EvaluateAsync(script, option, globals: param, globalsType: typeof(TemplateUsing));

        Debug.WriteLine($"执行结果：{obj}"); // 输出 1001

        
    }


    [TestMethod]
    public async Task TestTpl()
    {
        //var zip = ZipFile.OpenRead("sh.zip");
        //zip.ExtractToDirectory(AppDomain.CurrentDomain.BaseDirectory, true); 
        using var sr = new StreamReader(@"sharehold\def");
        var def = sr.ReadToEnd();

        var parts = def.Split("---");
        var script = parts[1];
        
        Debug.WriteLine(script);

        //using var xstream = new StreamReader("default.xlsx");


        int fid = 9;
        var date = new DateOnly(2025, 5, 4);

        Initial.TestInit.SetAsDebug();
        using var db = DbHelper.Base();
        var ta = db.GetCollection<TransferRecord>().Query().Where(x => x.FundId == fid).ToArray();

        var param = new TemplateUsing
        {
            Dates = [date],
            Records = ta,
            Dailies = db.GetDailyCollection(fid).Query().OrderByDescending(x=>x.Date).Where(x => x.Date <= date).Limit(1).ToArray(),
        };


        var option = ScriptOptions.Default
            .AddReferences(Assembly.GetExecutingAssembly())
            .WithImports("System", "System.Collections.Generic", "System.Linq", "FMO.Models");

         
        var obj = await CSharpScript.EvaluateAsync(script, option, globals: param, globalsType: typeof(TemplateUsing));

        Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

        bool suc = Tpl.Generate("ddd.xlsx", @"sharehold\default.xlsx", obj);

        Assert.IsTrue(suc);


    }

}

/*
input a b c
reference a b c d e

func()
{
return object;
}

// a b c 等是一组固定可选的class，宿主程序解析后，给UI，让用户选择a类中的哪些数据，其它同理
// ref 根据选择，比如 a 选了id=1，2，3这些，生成一个object{ a=[], b=[] ,c =[]}，这些是宿主的部分
// func 是用户写的脚本
 */

