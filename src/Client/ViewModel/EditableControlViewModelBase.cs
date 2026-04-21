using CommunityToolkit.Mvvm.Messaging;
using FMO.Shared;
using FMO.Utilities;
using LiteDB;
using System.Reflection;

namespace FMO;




public abstract partial class EditableControlViewModelBase<T> : ChangeableEntityViewModel where T : class//, new()
{
    protected virtual void NotifyChanged() { }// WeakReferenceMessenger.Default.Send(this);

    private Debouncer _debouncer { get; set; }


    public EditableControlViewModelBase()
    {
        _debouncer = new Debouncer(() => NotifyChanged(), 500);
    }

    public virtual T? EntityOverride(ILiteDatabase db)
    {
        return db.GetCollection<T>().FindById(Id);
    }


    public virtual void UpdateOverride(ILiteDatabase db, T v)
    {
        db.GetCollection<T>().Upsert(v);
    }

    protected override void DeleteOverride(IPropertyModifier unit)
    {

        if (unit is IEntityModifier<T> entity)
        {
            using var db = DbHelper.Base();
            var v = EntityOverride(db);

            if (v is not null)
            {
                entity.RemoveValue(v);
                entity.Init(v);
                db.GetCollection<T>().Upsert(v);

                WeakReferenceMessenger.Default.Send(v);
            }
        }
    }

    protected override void ResetOverride(IPropertyModifier unit)
    {
        unit.Reset();
    }


    protected override void ModifyOverride(IPropertyModifier unit)
    {
        if (unit is IEntityModifier<T> property)
        {
            T? v = default;
            if (Id == 0)
            {
                v = InitNewEntity();
            }
            else
            {
                using var db = DbHelper.Base();
                v = EntityOverride(db);
                if (v is null)
                    v = InitNewEntity();
            }

            property.UpdateEntity(v);

            if (v is not null)
            {
                using var db = DbHelper.Base();
                UpdateOverride(db, v);
                if (Id == 0 && v.GetType().GetProperty("Id") is PropertyInfo pi && pi.PropertyType == typeof(int))
                    Id = (int)pi.GetValue(v)!;                

                _debouncer.Invoke();
            }
        }
        unit.Apply();
    }

    protected override void SaveOverride()
    {
        var ps = GetType().GetProperties();
        foreach (var p in ps)
        {
            if (p.PropertyType.IsAssignableTo(typeof(IPropertyModifier)) && p.GetValue(this) is IPropertyModifier v && v.IsValueChanged)
                Modify(v);
        }

        //NotifyChanged();
        IsReadOnly = true;
    }


    protected abstract T InitNewEntity();
}
