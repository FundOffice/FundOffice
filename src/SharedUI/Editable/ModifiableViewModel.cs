using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FMO.Models;
using System.ComponentModel;
using System.Text.Json;

namespace FMO.Shared;


public enum ValueChangeKind { None, Added, Modified, Deleted }

public class ValueChangeEventArgs
{

    public ValueChangeKind Kind { get; }

    public ValueChangeEventArgs(ValueChangeKind kind) => (Kind) = (kind);
}

public class ValueChangeEventArgs<TProperty> : ValueChangeEventArgs
{
    public ValueChangeEventArgs(IValueModifier sender, ValueChangeKind kind) : base(kind)
    {
        if (sender is ModifiableViewModel<TProperty> changeable)
        {
            OldValue = changeable.OldValue;
            NewValue = changeable.NewValue;
            FallbackValue = changeable.FallbackValue;
        }
    }

    public ValueChangeEventArgs(ValueChangeKind kind, TProperty? newValue) : base(kind)
    {
        NewValue = newValue;
    }



    public TProperty? OldValue { get; set; }
    public TProperty? NewValue { get; set; }
    public TProperty? FallbackValue { get; set; }
}

public interface IDisplay
{
    object Transfrom();
}

public interface IDisplay<TDisplay> : IDisplay where TDisplay : notnull
{
    new TDisplay Transfrom();

    object IDisplay.Transfrom() => this.Transfrom();
}

public interface IValueModifier
{
    string? Label { get; set; }

    ValueChangeKind ChangeKind { get; }

    bool IsInherited { get; }
    bool CanConfirm { get; }
    bool CanClear { get; }

    void Apply();
    void Reset();
    void Clear();
}

public interface IFactorModifier
{
    bool CanDivide { get; set; }
    bool CanMerge { get; set; }

    IRelayCommand DivideCommand { get; }

    //IRelayCommand UnifyCommand { get; }
}


public static class CloneHelper
{
    /// <summary>
    /// 克隆值：值类型直接返回，引用类型深克隆
    /// </summary>
    public static T? CloneValue<T>(T? value)
    {
        // 值类型 → 直接返回
        if (typeof(T).IsValueType)
            return value;

        // 引用类型 → 正常克隆逻辑
        return value switch
        {
            null => default,
            ICloneable c => (T?)c.Clone(),
            _ => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))
        };
    }
}

public delegate void ValueChangedHandler<T>(ValueChangeEventArgs<T> args);


public partial class ModifiableViewModel<TValue, TViewModel> : ObservableObject, IValueModifier where TViewModel : IViewModel<TValue, TViewModel>
{
    public event ValueChangedHandler<TValue>? Changed;

    [ObservableProperty] public partial string? Label { get; set; }

    [ObservableProperty]
    public partial TValue? OldValue { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInherited))]
    public partial TViewModel NewValue { get; set; }
    [ObservableProperty] public partial TValue? FallbackValue { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanReset))]
    [NotifyPropertyChangedFor(nameof(CanConfirm))]
    [NotifyPropertyChangedFor(nameof(CanClear))]
    public partial ValueChangeKind ChangeKind { get; private set; } = ValueChangeKind.None;


    //public abstract TDisplay? DisplayValue { get; }


    // 旧值等于回退值，说明当前状态是“继承/未设置”
    public bool IsInherited => CheckInherited();

    private bool NewValueIsWell => NewValue is not IDataValidation d || d.IsValid();

    public virtual bool CanConfirm => ChangeKind is not ValueChangeKind.None && NewValueIsWell;

    public bool CanReset => !NewValue?.Equals(OldValue) ?? OldValue is not null;

    // 仅当：无未保存修改，且旧值本身不是回退状态（说明之前确实设置过值）
    public bool CanClear => !CanReset && !IsInherited;

    partial void OnNewValueChanged(TViewModel oldValue, TViewModel newValue)
    {
        DetachDeepNotify(oldValue);
        AttachDeepNotify(newValue);
        UpdateState();
    }
    partial void OnOldValueChanged(TValue? oldValue, TValue? newValue) => UpdateState();
    partial void OnFallbackValueChanged(TValue? oldValue, TValue? newValue) => UpdateState();

    private bool CheckInherited()
    {
        // FallbackValue 是null / 默认值，不认为是继承
        if (FallbackValue is string s && string.IsNullOrWhiteSpace(s)) return false;
        else if (FallbackValue is not { }) return false;

        return EqualityComparer<TValue?>.Default.Equals(OldValue, FallbackValue);
    }

    private void UpdateState()
    {
        var eq = EqualityComparer<TValue?>.Default;
        bool oldIsFallback = eq.Equals(OldValue, FallbackValue);
        bool newIsFallback = NewValue.Equals(FallbackValue);
        bool newEqualsOld = NewValue.Equals(OldValue);

        // 🔑 核心状态机：以 FallbackValue 为“未设置”基准线
        ChangeKind = newEqualsOld switch
        {
            true => ValueChangeKind.None,                                      // 新旧一致 → 无变更
            false when oldIsFallback && !newIsFallback => ValueChangeKind.Added,   // 继承 → 设置了自定义值
            false when !oldIsFallback && newIsFallback => ValueChangeKind.Deleted, // 自定义值 → 清空回继承
            _ => ValueChangeKind.Modified                                        // 自定义值A → 自定义值B
        };
    }

    private void AttachDeepNotify(TViewModel? value)
    {
        if (value is INotifyPropertyChanged npc)
            npc.PropertyChanged += OnDeepPropertyChanged;
    }
    private void DetachDeepNotify(TViewModel? value)
    {
        if (value is INotifyPropertyChanged npc)
            npc.PropertyChanged -= OnDeepPropertyChanged;
    }
    private void OnDeepPropertyChanged(object? sender, PropertyChangedEventArgs e) => UpdateState();

    /// <summary>确认修改：新值固化到旧值，状态归零</summary>
    [RelayCommand]
    public void Apply()
    {
        OldValue = TViewModel.Trans(NewValue); // 触发 OnOldValueChanged → ChangeKind = None
        NotifyChanged(ChangeKind, NewValue);
    }

    /// <summary>还原：放弃当前编辑，回退到已确认的旧值</summary>
    [RelayCommand]
    public void Reset()
    {
        NewValue = TViewModel.Trans(OldValue); // 触发 OnNewValueChanged → ChangeKind = None
        NotifyChanged(ChangeKind, NewValue);
    }

    /// <summary>清空/删除：将新值设为回退值，标记为 Deleted</summary>
    [RelayCommand]
    public void Clear()
    {
        if (!CanClear) return;

        NewValue = TViewModel.Trans(FallbackValue); // 触发 OnNewValueChanged → ChangeKind = Deleted
        NotifyChanged(ChangeKind, NewValue);
    }

    protected virtual void NotifyChanged(ValueChangeKind kind, TViewModel value) => Changed?.Invoke(new ValueChangeEventArgs<TValue>(ChangeKind, TViewModel.Trans(value)));

}

public partial class ModifiableViewModel<TValue> : ObservableObject, IValueModifier
{
    public event ValueChangedHandler<TValue>? Changed;

    [ObservableProperty] public partial string? Label { get; set; }

    [ObservableProperty]
    public partial TValue? OldValue { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInherited))]
    public partial TValue? NewValue { get; set; }

    [ObservableProperty] public partial TValue? FallbackValue { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanReset))]
    [NotifyPropertyChangedFor(nameof(CanConfirm))]
    [NotifyPropertyChangedFor(nameof(CanClear))]
    public partial ValueChangeKind ChangeKind { get; private set; } = ValueChangeKind.None;


    //public abstract TDisplay? DisplayValue { get; }


    // 旧值等于回退值，说明当前状态是“继承/未设置”
    public bool IsInherited => CheckInherited();

    private bool NewValueIsWell => NewValue is not IDataValidation d || d.IsValid();

    public virtual bool CanConfirm => ChangeKind is ValueChangeKind.Added or ValueChangeKind.Modified && NewValueIsWell;

    public bool CanReset => !EqualityComparer<TValue?>.Default.Equals(OldValue, NewValue);

    // 仅当：无未保存修改，且旧值本身不是回退状态（说明之前确实设置过值）
    public bool CanClear => !CanReset && !IsInherited;

    partial void OnNewValueChanged(TValue? oldValue, TValue? newValue)
    {
        DetachDeepNotify(oldValue);
        AttachDeepNotify(newValue);
        UpdateState();
    }
    partial void OnOldValueChanged(TValue? oldValue, TValue? newValue) => UpdateState();
    partial void OnFallbackValueChanged(TValue? oldValue, TValue? newValue) => UpdateState();

    private bool CheckInherited()
    {
        // FallbackValue 是null / 默认值，不认为是继承
        if (FallbackValue is string s && string.IsNullOrWhiteSpace(s)) return false;
        else if (FallbackValue is not { }) return false;

        return EqualityComparer<TValue?>.Default.Equals(OldValue, FallbackValue);
    }

    private void UpdateState()
    {
        var eq = EqualityComparer<TValue?>.Default;
        bool oldIsFallback = eq.Equals(OldValue, FallbackValue);
        bool newIsFallback = eq.Equals(NewValue, FallbackValue);
        bool newEqualsOld = eq.Equals(NewValue, OldValue);

        // 🔑 核心状态机：以 FallbackValue 为“未设置”基准线
        ChangeKind = newEqualsOld switch
        {
            true => ValueChangeKind.None,                                      // 新旧一致 → 无变更
            false when oldIsFallback && !newIsFallback => ValueChangeKind.Added,   // 继承 → 设置了自定义值
            false when !oldIsFallback && newIsFallback => ValueChangeKind.Deleted, // 自定义值 → 清空回继承
            _ => ValueChangeKind.Modified                                        // 自定义值A → 自定义值B
        };
    }

    private void AttachDeepNotify(TValue? value)
    {
        if (value is INotifyPropertyChanged npc)
            npc.PropertyChanged += OnDeepPropertyChanged;
    }
    private void DetachDeepNotify(TValue? value)
    {
        if (value is INotifyPropertyChanged npc)
            npc.PropertyChanged -= OnDeepPropertyChanged;
    }
    private void OnDeepPropertyChanged(object? sender, PropertyChangedEventArgs e) => UpdateState();



    /// <summary>确认修改：新值固化到旧值，状态归零</summary>
    [RelayCommand]
    public void Apply()
    {
        OldValue = CloneHelper.CloneValue(NewValue); // 触发 OnOldValueChanged → ChangeKind = None
        NotifyChanged(ChangeKind, NewValue);
    }

    /// <summary>还原：放弃当前编辑，回退到已确认的旧值</summary>
    [RelayCommand]
    public void Reset()
    {
        NewValue = CloneHelper.CloneValue(OldValue);// 触发 OnNewValueChanged → ChangeKind = None
        NotifyChanged(ChangeKind, NewValue);
    }

    /// <summary>清空/删除：将新值设为回退值，标记为 Deleted</summary>
    [RelayCommand]
    public void Clear()
    {
        if (!CanClear) return;

        NewValue = FallbackValue; // 触发 OnNewValueChanged → ChangeKind = Deleted
        NotifyChanged(ChangeKind, NewValue);
    }

    protected virtual void NotifyChanged(ValueChangeKind kind, TValue? value) => Changed?.Invoke(new ValueChangeEventArgs<TValue>(ChangeKind, value));
}


//public class ModifiableViewModel<TValue> : ModifiableViewModel<TValue, string>
//{

//    //public Func<TValue?, string>? DisplayFunc { get; set; }

//    public override string? DisplayValue => NewValue switch
//    {
//        IDisplay<string> t => t.Transfrom(),
//        Enum e => EnumDescriptionTypeConverter.GetEnumDescription(e),
//        _ => NewValue?.ToString()
//    };
//}

