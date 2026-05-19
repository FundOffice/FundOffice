using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;

namespace FMO.Shared;


public enum ValueChangeKind { None, Added, Modified, Deleted }

public class ValueChangeEventArgs : EventArgs
{
    public IValueModifier Sender { get; }
    public ValueChangeKind Kind { get; }
    public ValueChangeEventArgs(IValueModifier sender, ValueChangeKind kind) => (Sender, Kind) = (sender, kind);
}

public class ValueChangeEventArgs<TProperty> : ValueChangeEventArgs
{
    public ValueChangeEventArgs(IValueModifier sender, ValueChangeKind kind) : base(sender, kind)
    {
        if (sender is ModifiableViewModel<TProperty> changeable)
        {
            OldValue = changeable.OldValue;
            NewValue = changeable.NewValue;
            FallbackValue = changeable.FallbackValue;
        }
    }

    public TProperty? OldValue { get; set; }
    public TProperty? NewValue { get; set; }
    public TProperty? FallbackValue { get; set; }
}


public interface IDisplay<TDisplay>
{
    TDisplay Transfer();
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

public abstract partial class ModifiableViewModel<TValue, TDisplay> : ObservableObject, IValueModifier
{
    public event EventHandler<ValueChangeEventArgs>? Changed;

    [ObservableProperty] public partial string? Label { get; set; }

    [ObservableProperty]
    public partial TValue? OldValue { get; set; }

    [ObservableProperty]
    public partial TValue? NewValue { get; set; }
    [ObservableProperty] public partial TValue? FallbackValue { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanReset))]
    [NotifyPropertyChangedFor(nameof(CanConfirm))]
    [NotifyPropertyChangedFor(nameof(CanClear))]
    public partial ValueChangeKind ChangeKind { get; private set; } = ValueChangeKind.None;


    public abstract TDisplay? DisplayValue { get; }


    // 旧值等于回退值，说明当前状态是“继承/未设置”
    public bool IsInherited => EqualityComparer<TValue?>.Default.Equals(OldValue, FallbackValue);

    public virtual bool CanConfirm => ChangeKind is ValueChangeKind.Added or ValueChangeKind.Modified && (NewValue is not IDataValidation d || d.IsValid());

    public bool CanReset => !EqualityComparer<TValue?>.Default.Equals(OldValue, NewValue);

    // 仅当：无未保存修改，且旧值本身不是回退状态（说明之前确实设置过值）
    public bool CanClear => !CanReset && !IsInherited;

    partial void OnNewValueChanged(TValue? oldValue, TValue? newValue) => UpdateState(oldValue, newValue);
    partial void OnOldValueChanged(TValue? oldValue, TValue? newValue) => UpdateState(oldValue, newValue);
    partial void OnFallbackValueChanged(TValue? oldValue, TValue? newValue) => UpdateState(OldValue, NewValue);

    private void UpdateState(TValue? oldVal, TValue? newVal)
    {
        var eq = EqualityComparer<TValue?>.Default;
        bool oldIsFallback = eq.Equals(OldValue, FallbackValue);
        bool newIsFallback = eq.Equals(NewValue, FallbackValue);
        bool newEqualsOld = eq.Equals(NewValue, OldValue);

        Debug.WriteLine($"{CanClear} {CanReset}");
        // 🔑 核心状态机：以 FallbackValue 为“未设置”基准线
        ChangeKind = newEqualsOld switch
        {
            true => ValueChangeKind.None,                                      // 新旧一致 → 无变更
            false when oldIsFallback && !newIsFallback => ValueChangeKind.Added,   // 继承 → 设置了自定义值
            false when !oldIsFallback && newIsFallback => ValueChangeKind.Deleted, // 自定义值 → 清空回继承
            _ => ValueChangeKind.Modified                                        // 自定义值A → 自定义值B
        };

        DetachDeepNotify(oldVal);
        AttachDeepNotify(newVal);
        Changed?.Invoke(this, new ValueChangeEventArgs(this, ChangeKind));
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
    private void OnDeepPropertyChanged(object? sender, PropertyChangedEventArgs e) => UpdateState(OldValue, NewValue);

    /// <summary>确认修改：新值固化到旧值，状态归零</summary>
    [RelayCommand]
    public void Apply()
    {
        OldValue = CloneValue(NewValue); // 触发 OnOldValueChanged → ChangeKind = None
    }

    /// <summary>还原：放弃当前编辑，回退到已确认的旧值</summary>
    [RelayCommand]
    public void Reset()
    {
        NewValue = CloneValue(OldValue); // 触发 OnNewValueChanged → ChangeKind = None
    }

    /// <summary>清空/删除：将新值设为回退值，标记为 Deleted</summary>
    [RelayCommand]
    public void Clear()
    {
        if (!CanClear) return;
        NewValue = FallbackValue; // 触发 OnNewValueChanged → ChangeKind = Deleted
    }

    private static TValue? CloneValue(TValue? value) => value switch
    {
        null => default,
        ICloneable c => (TValue?)c.Clone(),
        _ => JsonSerializer.Deserialize<TValue>(JsonSerializer.Serialize(value))
    };
}


public class ModifiableViewModel<TValue> : ModifiableViewModel<TValue, string>
{

    //public Func<TValue?, string>? DisplayFunc { get; set; }

    public override string? DisplayValue => NewValue switch { IDisplay<string> t => t.Transfer(), _ => NewValue?.ToString() };
}