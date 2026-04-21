using CommunityToolkit.Mvvm.ComponentModel;
using FMO.Models;

namespace FMO;

public abstract class ElementItemViewModel : ObservableObject
{
    public const string UnsetValue = "-";

    public required string Label { get; set; }

    public abstract bool CanConfirm { get; }

    public abstract bool CanDelete { get; }

    /// <summary>
    /// 已修改未保存
    /// </summary>
    public abstract bool HasUnsavedValue { get; }

    public required string Property { get; set; }


    public string? Display => DisplayOverride();

    public abstract void UpdateEntity(FundElements elements, int fid);

    public abstract void RemoveValue(FundElements elements, int flowid);


    protected virtual void Init()
    {
        foreach (var p in GetType().GetProperties())
        {
            if (p.PropertyType.IsAssignableTo(typeof(ObservableObject)))
                ((ObservableObject)p.GetValue(this)!).PropertyChanged += ItemPropertyChanged;
        }
    }

    public abstract void Init(FundElements elements, int flowid);


    protected virtual string? DisplayOverride() => ToString();

    protected void ItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {  
        if (sender is IModifiableValue x)
        {
            OnPropertyChanged(nameof(CanConfirm));
            OnPropertyChanged(nameof(CanDelete));
            OnPropertyChanged(nameof(HasUnsavedValue));
        }
    }

    public virtual void ApplyOverride()
    {
        foreach (var p in GetType().GetProperties())
        {
            if (p.PropertyType.IsAssignableTo(typeof(IModifiableValue)))
                ((IModifiableValue)p.GetValue(this)!).Apply();
        }

        OnPropertyChanged(nameof(CanConfirm));
        OnPropertyChanged(nameof(CanDelete));
        OnPropertyChanged(nameof(HasUnsavedValue));
    }

    public void Apply()
    {
        ApplyOverride();

        OnPropertyChanged(nameof(Display));
    }
}
