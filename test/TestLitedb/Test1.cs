using FMO.Models;
using FMO.Utilities;
using LiteDB;
using Microsoft.Win32;
using System.Diagnostics;
using System.Linq.Expressions;

namespace TestLitedb;

[TestClass]
public sealed class Test1
{
    public Test1()
    {
        using (var key = Registry.CurrentUser.OpenSubKey(@$"Software\Nexus\Debug"))
        {
            if (key != null)
            {
                var workFolder = key.GetValue("WorkingFolder") as string;
                if (!string.IsNullOrWhiteSpace(workFolder))
                {
                    var di = new DirectoryInfo(workFolder);
                    if (di.Exists)
                        Directory.SetCurrentDirectory(di.FullName);
                }
            }
        }
    }

    [TestMethod]
    public void TestMethod1()
    {
        using var db = new LiteDatabase("FileName=xx.db");
        for (int i = 0; i < 10; i++)
            db.GetCollection<sc>().Upsert(new sc { Id = i, Name = $"{i}" });

        db.GetCollection<sc>().Update(5, new sc { Name = "fdslkf" });


        foreach (var item in db.GetCollection<sc>().FindAll().ToArray())
        {
            Console.WriteLine($"{item.Id} {item.Name}");
        }

    }

    [TestMethod]
    public void TestFact()
    {

        using var db = DbHelper.Base();
        var ele = db.GetCollection<FundElements>().FindById(9);
        var facts = FundElementsFundFactHelper.FromElement(ele);

        using var db2 = new LiteDatabase("FileName=xx.db");
        //db2.DropCollection(nameof(IFundFact));
        db2.GetCollection<IFundFactor>().Upsert(facts);

        facts = db2.GetCollection<IFundFactor>().FindAll().ToArray();

        var doc = db2.GetCollection(nameof(IFundFactor)).FindAll().ToArray();


        var nele = FundElementsFundFactHelper.ToElement(facts, 9);
    }



    [TestMethod]
    public void TestLinq()
    {
        using var db = DbHelper.Base();

        Expression<Func<ILiteCollection<Fund>, object>> expression = x => x.FindAll().ToArray();


        Serialize.Linq.Serializers.ExpressionSerializer serializer = new Serialize.Linq.Serializers.ExpressionSerializer(new Serialize.Linq.Serializers.JsonSerializer());

        var text = serializer.SerializeText(expression);

        var ex = serializer.DeserializeText(text) as Expression<Func<ILiteCollection<Fund>, object>>;

        var obj = ex.Compile().Invoke(db.GetCollection<Fund>());

    }

    [TestMethod]
    public void MyTestMethod()
    {


        var typen = $"{typeof(LiquidationFlow).FullName},{typeof(LiquidationFlow).Assembly.GetName().Name}";

        Debug.WriteLine(typen);

        using var db = DbHelper.Base();
        var types = db.GetCollection(nameof(FundFlow)).FindAll().ToArray();

        foreach (var item in types)
        {
            Debug.WriteLine(item["_type"]);
        }

    }


}


public class sc
{
    public int Id { get; set; }

    public string Name { get; set; }
}