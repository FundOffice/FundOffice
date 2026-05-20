using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FMO.Models;
using System.ComponentModel;

namespace FMO.Shared;



public class FactChangeEventArgs<TProperty>
{
    public FactChangeEventArgs(FactModifiableViewModel<TProperty> sender, ValueChangeKind kind)
    {
        Kind = kind;
        ShareId = sender.ShareId;
        FlowId = sender.FlowId;
        FundId = sender.FundId;
        FactorId = sender.FactorId;
        NewValue = sender.NewValue;
        OldValue = sender.OldValue;
        FallbackValue = sender.FallbackValue;
    }

    public ValueChangeKind Kind { get; set; }

    public int ShareId { get; }

    public int FlowId { get; }

    public string FactorId { get; }

    public int FundId { get; }

    public TProperty? OldValue { get; set; }
    public TProperty? NewValue { get; set; }
    public TProperty? FallbackValue { get; set; }
}


public   partial class FactModifiableViewModel<TValue> : ObservableObject, IValueModifier
{
    public event EventHandler<FactChangeEventArgs<TValue>>? Changed;

    [ObservableProperty] public partial string? Label { get; set; }


    public string Id => $"{FundId}.{FlowId}.{ShareId}.{FactorId}";

    public required int ShareId { get; set; }

    public required int FlowId { get; init; }

    public required int FundId { get; init; }

    public required string FactorId { get; init; }

    public string? ShareName { get; set; }

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



    // 旧值等于回退值，说明当前状态是“继承/未设置”
    public bool IsInherited => CheckInherited();

    public virtual bool CanConfirm => ChangeKind is ValueChangeKind.Added or ValueChangeKind.Modified && NewValueIsWell;

    public bool CanReset => !EqualityComparer<TValue?>.Default.Equals(OldValue, NewValue);

    // 仅当：无未保存修改，且旧值本身不是回退状态（说明之前确实设置过值）
    public bool CanClear => !CanReset && !IsInherited;

    /// <summary>
    /// 新值可用
    /// </summary>
    private bool NewValueIsWell => NewValue is not IDataValidation d || d.IsValid();

    partial void OnNewValueChanged(TValue? oldValue, TValue? newValue)
    {
        UpdateState(oldValue, newValue);

        DetachDeepNotify(newValue);
        AttachDeepNotify(newValue);
    }

    partial void OnOldValueChanged(TValue? oldValue, TValue? newValue)
    {
        UpdateState(oldValue, newValue);
        DetachDeepNotify(oldValue);
        AttachDeepNotify(oldValue);
    }

    //partial void OnFallbackValueChanged(TValue? oldValue, TValue? newValue) => UpdateState(OldValue, NewValue);

    private bool CheckInherited()
    {
        // FallbackValue 是null / 默认值，不认为是继承
        if (FallbackValue is string s && string.IsNullOrWhiteSpace(s)) return false;
        else if (FallbackValue is not { }) return false;

        return EqualityComparer<TValue?>.Default.Equals(OldValue, FallbackValue);
    }

    private void UpdateState(TValue? oldVal, TValue? newVal)
    {
        var eq = EqualityComparer<TValue?>.Default;
        bool oldIsFallback = eq.Equals(OldValue, FallbackValue);
        bool newIsFallback = eq.Equals(NewValue, FallbackValue);
        bool newEqualsOld = eq.Equals(NewValue, OldValue);

        // 🔑 核心状态机：以 FallbackValue 为“未设置”基准线
        ChangeKind = newEqualsOld switch
        {
            true => ValueChangeKind.None,                                      // 新旧一致 → 无变更
            false when oldIsFallback && !newIsFallback && NewValueIsWell => ValueChangeKind.Added,   // 继承 → 设置了自定义值
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
    private void OnDeepPropertyChanged(object? sender, PropertyChangedEventArgs e) => UpdateState(OldValue, NewValue);

    /// <summary>确认修改：新值固化到旧值，状态归零</summary>
    [RelayCommand]
    public void Apply()
    {
        FactChangeEventArgs<TValue> e = new(this, ChangeKind);
        OldValue = CloneHelper.CloneValue(NewValue); // 触发 OnOldValueChanged → ChangeKind = None
        Changed?.Invoke(this, e);
    }

    /// <summary>还原：放弃当前编辑，回退到已确认的旧值</summary>
    [RelayCommand]
    public void Reset()
    {
        NewValue = CloneHelper.CloneValue(OldValue); // 触发 OnNewValueChanged → ChangeKind = None
        FactChangeEventArgs<TValue> e = new(this, ChangeKind);
        Changed?.Invoke(this, e);
    }

    /// <summary>清空/删除：将新值设为回退值，标记为 Deleted</summary>
    [RelayCommand]
    public void Clear()
    {
        if (!CanClear) return;
        NewValue = FallbackValue; // 触发 OnNewValueChanged → ChangeKind = Deleted
        FactChangeEventArgs<TValue> e = new(this, ChangeKind);
        Changed?.Invoke(this, e);
    }


}
