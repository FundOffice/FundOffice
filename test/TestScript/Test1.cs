using CSScriptLib;
using FMO.Models;
using FMO.TPL;
using FMO.Utilities;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using System.Diagnostics;
using System.Dynamic;
using System.Reflection;

namespace TestScript;

[TestClass]
public sealed class ScriptExec
{
    [TestMethod]
    public async Task TestMethod1()
    {
        Initial.DataInject.SetAsDebug();

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


        var param = new TemplateGlobal();
        param.Funds = funds;
        var obj = await CSharpScript.EvaluateAsync(script, option, globals: param, globalsType: typeof(TemplateGlobal));

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

        Initial.DataInject.SetAsDebug();
        using var db = DbHelper.Base();
        var ta = db.GetCollection<TransferRecord>().Query().Where(x => x.FundId == fid).ToArray();

        var param = new TemplateGlobal
        {
            Dates = [date],
            Records = ta,
            Dailies = db.GetDailyCollection(fid).Query().OrderByDescending(x => x.Date).Where(x => x.Date <= date).Limit(1).ToArray(),
        };


        var option = ScriptOptions.Default
            .AddReferences(Assembly.GetExecutingAssembly())
            .WithImports("System", "System.Collections.Generic", "System.Linq", "FMO.Models");


        var obj = await CSharpScript.EvaluateAsync(script, option, globals: param, globalsType: typeof(TemplateGlobal));

        Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

        bool suc = Tpl.Generate("ddd.xlsx", @"sharehold\default.xlsx", obj);

        Assert.IsTrue(suc);


    }


    [TestMethod]
    public async Task MyTestMethod()
    {
        // 1. 动态全局对象（不变）
        dynamic globalParams = new ExpandoObject();
        globalParams.UserName = "张三";
        globalParams.Age = 28;
        globalParams.Score = 95.5;
        globalParams.CalcTotal = (Func<int, int, int>)((a, b) => a + b);

        // 2. ✅ 关键修复：必须添加【程序集引用】+ 命名空间
        var options = ScriptOptions.Default
            .AddReferences(typeof(Enumerable).Assembly) // 核心：加载系统核心程序集
            .AddImports("System");

        // 3. ✅ 关键修复：弃用 EvaluateAsync，改用 Create + RunAsync
        string script1 = $"globals.UserName + \"，年龄：\" + Age + \"，得分：\" + Score";
        var script = CSharpScript.Create<string>(script1, options, typeof(ExpandoObject)); // 创建脚本
        var state = await script.RunAsync(globals: globalParams);   // 运行脚本
        var result1 = state.ReturnValue; // 获取结果

        Debug.WriteLine(result1); // 正常输出：张三，年龄：28，得分：95.5
    }


    [TestMethod]
    public async Task TestTplType2()
    {
        //var zip = ZipFile.OpenRead("sh.zip");
        //zip.ExtractToDirectory(AppDomain.CurrentDomain.BaseDirectory, true); 
        using var sr = new StreamReader(@"sharehold\def2");
        var def = sr.ReadToEnd();

        var parts = def.Split("---");
        var script = parts[1];

        Debug.WriteLine(script);

        //using var xstream = new StreamReader("default.xlsx");


        int fid = 9;
        var date = new DateOnly(2025, 5, 4);

        Initial.DataInject.SetAsDebug();
        using var db = DbHelper.Base();
        var ta = db.GetCollection<TransferRecord>().Query().Where(x => x.FundId == fid).ToArray();

        var param = new Wrap
        {
            Data = new
            {
                //Dates = [date],
                Date = date,
                Records = ta,
                Dailies = db.GetDailyCollection(fid).Query().OrderByDescending(x => x.Date).Where(x => x.Date <= date).Limit(1).ToArray(),
            }
        };


        var option = ScriptOptions.Default
            .AddReferences(Assembly.GetExecutingAssembly())
            .WithImports("System", "System.Collections.Generic", "System.Linq", "FMO.Models");


        var obj = await CSharpScript.EvaluateAsync(script, option, globals: param, globalsType: typeof(Wrap));

        Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

        bool suc = Tpl.Generate("ddd.xlsx", @"sharehold\default.xlsx", obj);

        Assert.IsTrue(suc);


    }



    [TestMethod]
    public async Task TestCSScriptLib()
    {
        //var zip = ZipFile.OpenRead("sh.zip");
        //zip.ExtractToDirectory(AppDomain.CurrentDomain.BaseDirectory, true); 
        using var sr = new StreamReader(@"sharehold\def3");
        var def = sr.ReadToEnd();

        var parts = def.Split("---");
        var script = parts[1];

        Debug.WriteLine(script);

        //using var xstream = new StreamReader("default.xlsx");


        int fid = 9;
        DateOnly[] dates = [new DateOnly(2025, 5, 4)];
        var date = dates[0];
        Initial.DataInject.SetAsDebug();
        using var db = DbHelper.Base();
        var ta = db.GetCollection<TransferRecord>().Query().Where(x => x.FundId == fid).ToArray();

        var param = new
        {
            Dates = dates,
            Records = ta,
            Dailies = db.GetDailyCollection(fid).Query().OrderByDescending(x => x.Date).Where(x => x.Date <= date).Limit(1).ToArray(),
        };


        var eval = CSScript.Evaluator.Clone()
            .ReferenceAssemblyOf<Fund>()
            .ReferenceAssembly(typeof(System.Linq.Enumerable).Assembly)
            .ReferenceAssembly(typeof(List<>).Assembly)
            .ReferenceAssemblyByNamespace("System")
            .ReferenceAssemblyByNamespace("System.Collections.Generic")
            .ReferenceAssemblyByNamespace("System.Linq");


        dynamic method = eval.LoadMethod(script);
        var obj = method.Build();


        Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

        bool suc = Tpl.Generate("ddd.xlsx", @"sharehold\default.xlsx", obj);

        Assert.IsTrue(suc);


    }
}

public class Wrap
{
    public dynamic Data { get; set; }
}