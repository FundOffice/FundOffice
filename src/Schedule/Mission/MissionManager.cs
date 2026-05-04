using FMO.Models;
using System.Reflection;

namespace FMO.Schedule;

public record MissionTemplate(string Title, string Description, Type Type, Func<Mission, MissionViewModel> CreateViewModel, Func<Mission> CreateMission);


public static class MissionManager
{
    private static readonly Dictionary<Type, MissionTemplate> _tpl = new();


    public static void Register(Type missionType, Func<Mission> func, Func<Mission, MissionViewModel> vmFactory)
    {
        var titleAttr = missionType.GetCustomAttribute<MissionInfoAttribute>();
        var title = titleAttr?.Title ?? missionType.Name;
        var des = titleAttr?.Description ?? "";

        _tpl[missionType] = new MissionTemplate(title, des, missionType, vmFactory, func);
    }


    public static MissionViewModel GetViewModel(Mission m)
    {
        if(_tpl.TryGetValue(m.GetType(), out var factory))
            return factory.CreateViewModel(m);

        if (m is OnceMission mm)
            return new OnceMissionViewModel(mm);

        return new MissionViewModel(m);
    }

    public static MissionTemplate[] Templates => _tpl.Values.ToArray();
}

