
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


    private static Dictionary<Type, Delegate> _vmMap = [];

    public static void Initialize()
    {

        //InitVerifySection();
        SettingUnits = db.GetCollection<SettingUnit>().FindAll().ToDictionary(x => x.Id);
    }


    public static AbilityUnit[] GetAbilityUnits(string section) => AbilitySection.Where(x => x.Key.StartsWith(section + ".")).Select(x => x.Value).ToArray();



    public static void RegisterViewModel<TEntity, TViewModel>(Func<TEntity, TViewModel> func) where TEntity : SettingUnit
    {
        _vmMap[typeof(TEntity)] = func;
    }


    public static object? CreateViewModel<T>(T obj) where T : SettingUnit
    {
        return _vmMap.TryGetValue(typeof(T),out var func) ? func.DynamicInvoke(obj) : null;
    }


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
        return db.GetCollection<SettingUnit>().Find(x => x.Section == seciton).ToArray();
    }


    public static SettingUnit? GetValue(string section, string name)
    {
        return SettingUnits.TryGetValue($"{section}.{name}", out var unit) ? unit : null;
    }



    public static void Save(SettingUnit unit)
    {
        db.GetCollection<SettingUnit>().Upsert(unit);
    }


}

