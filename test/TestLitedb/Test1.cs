using LiteDB;

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
}


public class sc
{
    public int Id { get; set; }

    public string Name { get; set; }
}