namespace FMO.Settings;

public interface IUnitViewModel
{
}


/// <summary>
/// 已通过生成器自动注册 SettingUnit 对应的 ViewModel 基类
/// 标记以下特性的类会被自动注册到 UnitViewModel 的工厂方法中，创建时会传入对应的 SettingUnit 实例
/// [AutoViewModel(typeof(SwitchUnit))]
/// </summary>
public class SettingViewModels
{


    private static Dictionary<Type, Delegate> _vmMap = [];

    public static void RegisterViewModel<TEntity, TViewModel>(Func<TEntity, TViewModel> func) where TEntity : SettingUnit where TViewModel : IUnitViewModel, new()
    {
        _vmMap[typeof(TEntity)] = func;
    }


    public static IUnitViewModel? CreateViewModel<T>(T obj) where T : SettingUnit
    {
        return _vmMap.TryGetValue(obj.GetType(), out var func) ? func.DynamicInvoke(obj) as IUnitViewModel : null;
    }

    public static T2? CreateViewModel<T2, T>(T obj) where T : SettingUnit where T2 : class, IUnitViewModel
    {
        return _vmMap.TryGetValue(obj.GetType(), out var func) ? func.DynamicInvoke(obj) as T2 : null;
    }
}
