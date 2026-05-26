using FMO.Logging;
using FMO.Models;
using LiteDB;

namespace FMO.Settings;

public static partial class SettingService
{
    private static ILiteDatabase db = new LiteDatabase(@"FileName=data\settings");

    private static Dictionary<string, AbilityUnit> AbilitySection { get; set; } = [];
    private static Dictionary<string, ISettingFunction> AbilityObject { get; set; } = [];


    public static void Initialize()
    {

        //InitVerifySection();
         
    }


    public static AbilityUnit[] GetAbilityUnits(string section) => AbilitySection.Where(x => x.Key.StartsWith(section + ".")).Select(x => x.Value).ToArray();



    public static void RegisterAbility(string section, string name, string title, string description, bool isenable, ISettingFunction instance)
    {
        AbilityUnit r;
        string key = $"{section}.{name}";
        if (!AbilitySection.TryGetValue(key, out var rule))
        {
            r = new AbilityUnit { Name = name, Section = section, Title = title, Description = description, IsEnabled = isenable };
            AbilitySection.Add(key, r);
        }
        else r = rule;

        AbilityObject[key] = instance;

        if (r.IsEnabled)
        {
            if (instance is ISettingFunction vr)
                try { vr.Init(); } catch(Exception e) { LogEx.Error(e); }

            instance.Start();
        }


    }

    public static void DisableAbility(string key)
    {
        if (AbilitySection.TryGetValue(key, out var u))
        {
            u.IsEnabled = false;

            if (AbilityObject.TryGetValue(key, out var obj))
            {
                obj.Stop();
            }

            Save(u);
        }
    }

    public static void EnableAbility(string key)
    {
        if (AbilitySection.TryGetValue(key, out var u))
        {
            u.IsEnabled = true;

            if (AbilityObject.TryGetValue(key, out var obj))
            {
                if (obj is IVerifyRule r)
                    r.Init();
                obj.Start();
            }

            Save(u);
        }
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

