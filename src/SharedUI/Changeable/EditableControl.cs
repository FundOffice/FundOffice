using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace FMO.Shared;


public class EditableControl : HeaderedContentControl
{
    static EditableControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(EditableControl), new FrameworkPropertyMetadata(typeof(EditableControl)));
    }

    public bool IsReadOnly
    {
        get { return (bool)GetValue(IsReadOnlyProperty); }
        set { SetValue(IsReadOnlyProperty, value); }
    }

    // Using a DependencyProperty as the backing store for IsReadOnly.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty IsReadOnlyProperty =
        DependencyProperty.Register("IsReadOnly", typeof(bool), typeof(EditableControl), new PropertyMetadata(false));


}



public partial class EditableViewModel<TProperty> : ObservableObject
{
    [ObservableProperty]
    public partial string? Label { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Display))]
    [NotifyPropertyChangedFor(nameof(CanConfirm))]
    [NotifyPropertyChangedFor(nameof(CanDelete))]
    public partial TProperty? OldValue { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConfirm))]
    [NotifyPropertyChangedFor(nameof(CanDelete))]
    public partial TProperty? NewValue { get; set; }

    private TProperty? _notnullDefault = default;// new TProperty();

    public string? Display => DisplayFunc is not null ? DisplayFunc(OldValue) : OldValue?.ToString();

    public virtual bool CanConfirm => NewValue is not null /*&& !NewValue.Equals(default(TProperty))*/ && IsValueChanged && NewValue switch { IDataValidation d => d.IsValid(), _ => true };

    public bool CanDelete => !IsValueChanged && OldValue is not null && !EqualityComparer<TProperty>.Default.Equals(_notnullDefault, OldValue);

    /// <summary>
    /// 新值改变了，和旧值不同
    /// </summary>
    [ObservableProperty]
    public partial bool IsValueChanged { get; set; }


    [ObservableProperty]
    public partial bool IsInherited { get; set; }


    public Func<TProperty?, string?>? DisplayFunc { get; set; }


    public delegate void ValueChangedHandler(TProperty? oldValue, TProperty? newValue);
    public event ValueChangedHandler? ValueChanged;

    partial void OnNewValueChanged(TProperty? oldValue, TProperty? newValue)
    {
        IsValueChanged = NewValue switch { null => OldValue is not null, _ => !EqualityComparer<TProperty>.Default.Equals(NewValue, OldValue) };

        if (newValue is INotifyPropertyChanged changed)
            changed.PropertyChanged += (s, e) => Refresh();
    }

    partial void OnOldValueChanged(TProperty? value)
    {
        IsValueChanged = NewValue switch { null => OldValue is not null, _ => !EqualityComparer<TProperty>.Default.Equals(NewValue, OldValue) };
    }


    public void Init(TProperty? property)
    {
        OldValue = property;
        NewValue = property;
    }

    public virtual void Apply()
    {
        var old = OldValue;
        if (typeof(TProperty).IsValueType || typeof(TProperty) == typeof(string))
            OldValue = NewValue;
        else
            OldValue = JsonSerializer.Deserialize<TProperty>(JsonSerializer.Serialize(NewValue));

        ValueChanged?.Invoke(old, NewValue);
        IsInherited = false;
    }
    public void Reset()
    {
        if (typeof(TProperty).IsValueType || typeof(TProperty) == typeof(string))
            NewValue = OldValue;
        else
            NewValue = JsonSerializer.Deserialize<TProperty>(JsonSerializer.Serialize(OldValue));
    }

    public void Refresh()
    {
        IsValueChanged = NewValue switch { null => OldValue is not null, _ => !EqualityComparer<TProperty>.Default.Equals(NewValue, OldValue) };
        OnPropertyChanged(nameof(CanConfirm));
        OnPropertyChanged(nameof(CanDelete));
    }
}