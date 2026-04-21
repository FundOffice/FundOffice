using CommunityToolkit.Mvvm.ComponentModel;
using FMO.Models;
using System.ComponentModel;
using System.Text.Json;

namespace FMO.Shared;


public interface IPropertyModifier
{
    bool CanConfirm { get; }
    bool CanDelete { get; }
    bool IsValueChanged { get; }

    void Refresh();

    void Apply();
    void Reset();
}


public interface IEntityModifier<TEntity>
{
    void Init(TEntity entity);
    void RemoveValue(TEntity entity);
    void UpdateEntity(TEntity entity);
}



public interface IDataValidation
{
    bool IsValid();
}


/// <summary>
/// 
/// </summary>
/// <typeparam name="TEntity"></typeparam>
/// <typeparam name="TProperty">自定义类型必须实现IEquatable<TProperty></typeparam>
public partial class ChangeableViewModel<TEntity, TProperty> : ObservableObject, IPropertyModifier, IEntityModifier<TEntity>
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

    //public bool HasUnsavedValue => NewValue is not null && NewValue switch { Enum e => true, _ => !NewValue.Equals(default(TProperty)) };

    /// <summary>
    /// 新值改变了，和旧值不同
    /// </summary>
    [ObservableProperty]
    public partial bool IsValueChanged { get; set; }


    [ObservableProperty]
    public partial bool IsInherited { get; set; }


    public Func<TEntity, TProperty?>? InitFunc { get; set; }

    public Func<TEntity, bool>? InheritedFunc { get; set; }

    public Action<TEntity, TProperty?>? UpdateFunc { get; set; }

    public Action<TEntity>? ClearFunc { get; set; }

    public Func<TProperty?, string?>? DisplayFunc { get; set; }

    public ChangeableViewModel()
    {
        if (!(typeof(TProperty).IsValueType || typeof(TProperty) == typeof(string) || typeof(TProperty).IsAssignableTo(typeof(IEquatable<TProperty>))))
            throw new InvalidOperationException();

        if (typeof(TProperty).IsAssignableTo(typeof(INotifyPropertyChanged)))
            _notnullDefault = (TProperty)Activator.CreateInstance(typeof(TProperty))!;

        DisplayFunc = x => x switch { Enum e => EnumDescriptionTypeConverter.GetEnumDescription(e), _ => x?.ToString() };
    }

    public ChangeableViewModel(TEntity entity, Func<TEntity, TProperty?>? init = null, Action<TEntity, TProperty?>? update = null, Action<TEntity>? clear = null, Func<TProperty?, string?>? display = null)
    {
        if (typeof(TProperty).IsAssignableTo(typeof(ObservableObject)))
            _notnullDefault = (TProperty)Activator.CreateInstance(typeof(TProperty))!;

        InitFunc = init;
        UpdateFunc = update;
        ClearFunc = clear;
        DisplayFunc = display ?? (x => x switch { Enum e => EnumDescriptionTypeConverter.GetEnumDescription(e), _ => x?.ToString() });

        Init(entity);
    }

    partial void OnNewValueChanged(TProperty? oldValue, TProperty? newValue)
    {
        //if (oldValue is not null && oldValue is INotifyPropertyChanged changed)
        //    changed.PropertyChanged -= SubjectPropertyChanged;

        IsValueChanged = NewValue switch { null => OldValue is not null, _ => !EqualityComparer<TProperty>.Default.Equals(NewValue, OldValue) };


        if (newValue is INotifyPropertyChanged changed)
            changed.PropertyChanged += (s, e) => Refresh();
    }

    partial void OnOldValueChanged(TProperty? value)
    {
        IsValueChanged = NewValue switch { null => OldValue is not null, _ => !EqualityComparer<TProperty>.Default.Equals(NewValue, OldValue) };
    }



    public virtual void Apply()
    {
        //OldValue = NewValue;
        if (typeof(TProperty).IsValueType || typeof(TProperty) == typeof(string))
            OldValue = NewValue;
        else
            OldValue = JsonSerializer.Deserialize<TProperty>(JsonSerializer.Serialize(NewValue));

        IsInherited = false;
    }

    public void Reset()
    {
        if (typeof(TProperty).IsValueType || typeof(TProperty) == typeof(string))
            NewValue = OldValue;
        else
            NewValue = JsonSerializer.Deserialize<TProperty>(JsonSerializer.Serialize(OldValue));
    }

    public virtual void Init(TEntity entity)
    {
        if (entity is  null)
            return;
         
        if(InitFunc is not null)
        {
            OldValue = InitFunc(entity);
            NewValue = InitFunc(entity);
        }
        if (InheritedFunc is not null)
            IsInherited = InheritedFunc(entity);
    }

    public virtual void UpdateEntity(TEntity entity)
    {
        if (entity is not null && UpdateFunc is not null && NewValue is not null)
            UpdateFunc(entity, NewValue);
        
    }

    public virtual void RemoveValue(TEntity entity)
    {
        if (entity is not null && ClearFunc is not null)
            ClearFunc(entity);
    }

    public void Refresh()
    {
        IsValueChanged = NewValue switch { null => OldValue is not null, _ => !EqualityComparer<TProperty>.Default.Equals(NewValue, OldValue) };
        OnPropertyChanged(nameof(CanConfirm));
        OnPropertyChanged(nameof(CanDelete));
    }
}
