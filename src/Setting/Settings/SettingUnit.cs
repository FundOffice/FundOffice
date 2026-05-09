using FMO.Trigger;
using LiteDB;
using System.Collections.Immutable;

namespace FMO.Settings;

public class SettingUnit
{
    public string Id => $"{Section}.{Name}";

    public required string Section { get; set; }

    public required string Name { get; set; }

    public required string Title { get; set; }

    public string? Description { get; set; }




}

public static class SettingSections
{
    public const string VerifyRule = "VerifyRule";
}

public class SettingUnit<T> : SettingUnit
{
    public required T Data { get; set; }
}



public class VerifyRuleUnitData
{
    public bool IsEnabled { get; set; }

}




public class VerifyRuleUnit : SettingUnit<VerifyRuleUnitData>
{


}



public partial class SettingService
{
    private ILiteDatabase db;

    public SettingService()
    {
        db = new LiteDatabase(@"FileName=data\settings");

        Units = db.GetCollection<SettingUnit>().FindAll().ToArray();

        
    }

    public SettingUnit[] Units { get; set; }

    public SettingUnit[] Load(string seciton)
    {
        return db.GetCollection<SettingUnit>().Find(x => x.Section == seciton).ToArray();
    }



    public void Save(SettingUnit unit)
    {
        db.GetCollection<SettingUnit>().Upsert(unit);  
    }

}

