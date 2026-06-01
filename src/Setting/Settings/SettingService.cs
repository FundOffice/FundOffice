
using FMO.Models;
using LiteDB;
using MoT;

namespace FMO.Settings;

public static partial class SettingService
{
    private static ILiteDatabase db = new LiteDatabase(@"FileName=data\settings");

    private static Dictionary<string, AbilityUnit> AbilitySection { get; set; } = [];
    private static Dictionary<string, ISettingFunction> AbilityObject { get; set; } = [];


    private static Dictionary<string, SettingUnit> SettingUnits { get; set; } = [];


    public static void Initialize()
    {
         
  
        SettingUnits = db.GetCollection<SettingUnit>().FindAll().ToDictionary(x => x.Id);


        var u = GetUnits("Order");
        if (!u.Any(x => x.Name == "AllowCreateTemporaryInESigning"))
            RegisterSwitch("Order", "AllowCreateTemporaryInESigning", "允许在电签平台设置开放日", "允许在电签平台设置开放日，即使它不是托管平台中的开放日", true);


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
                try { vr.Init(); } catch (Exception e) { Logg.Error(e); }

            instance.Start();
        }


    }

    public static void RegisterSwitch(string section, string name, string title, string description, bool isenable)
    {
        SettingUnit r;
        string key = $"{section}.{name}";
        if (!SettingUnits.TryGetValue(key, out var rule))
        {
            r = new SwitchUnit { Name = name, Section = section, Title = title, Description = description, IsEnabled = isenable };
            SettingUnits.Add(r.Id, r);
        }
        else r = rule;
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

    public static SettingUnit[] GetUnits(string seciton)
    {
        return SettingUnits.Where(x => x.Key.StartsWith(seciton)).Select(x => x.Value).ToArray();
    }


    public static SettingUnit? GetValue(string section, string name)
    {
        return SettingUnits.TryGetValue($"{section}.{name}", out var unit) ? unit : null;
    }



    public static void Save(SettingUnit unit)
    {
        db.GetCollection<SettingUnit>().Upsert(unit);
        SettingUnits[unit.Id] = unit;
    }


}

