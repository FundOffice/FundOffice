using LiteDB;

namespace FMO.Settings;

public static partial class SettingService
{
    private static ILiteDatabase db = new LiteDatabase(@"FileName=data\settings");

     


    public static void Initialize()
    {

        InitVerifySection();
    }











   // public static SettingUnit[] Units { get; set; }

    public static SettingUnit[] Load(string seciton)
    {
        return db.GetCollection<SettingUnit>().Find(x => x.Section == seciton).ToArray();
    }



    public static void Save(SettingUnit unit)
    {
        db.GetCollection<SettingUnit>().Upsert(unit);  
    }

}

