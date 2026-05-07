using FMO.Models;
using FMO.Utilities;
using LiteDB;
using Microsoft.Win32;

namespace TestLitedb;

[TestClass]
public sealed class Test1
{
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
        using var db = DbHelper.Base();
        var ele =  db.GetCollection<FundElements>().FindById(9);
        var facts = FundElementsFundFactHelper.FromElement(ele);
                 
        using var db2 = new LiteDatabase("FileName=xx.db");
        //db2.DropCollection(nameof(IFundFact));
        db2.GetCollection<IFundFact>().Upsert(facts);

        facts = db2.GetCollection<IFundFact>().FindAll().ToArray();

        var doc = db2.GetCollection(nameof(IFundFact)).FindAll().ToArray();


        var nele = FundElementsFundFactHelper.ToElement(facts, 9);
    }
}


public class sc
{
    public int Id { get; set; }

    public string Name { get; set; }
}