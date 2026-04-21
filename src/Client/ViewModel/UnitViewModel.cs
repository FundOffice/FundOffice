using CommunityToolkit.Mvvm.ComponentModel;
using FMO.Models;
using System.ComponentModel;
using System.Reflection;
using System.Windows;

namespace FMO;





/// <summary>
/// 单个项的vm，提供新旧值的
/// 需要有3个功能
/// 初始化
/// 修改
/// 保存
/// </summary>
//public abstract class UnitViewModel : ObservableObject
//{
//    public const string UnsetValue = "-";

//    /// <summary>
//    /// 此项的标签
//    /// </summary>
//    public string? Label { get; set; }

//    public abstract bool CanConfirm { get; }

//    public abstract bool CanDelete { get; }

//    /// <summary>
//    /// 已修改未保存
//    /// </summary>
//    public abstract bool HasUnsavedValue { get; }

//    /// <summary>
//    /// 显示值
//    /// </summary>
//    public string? Display => DisplayOverride();



//    /// <summary>
//    /// 订阅所有属性变动
//    /// </summary>
//    protected virtual void SubscribeChanges()
//    {
//        foreach (var p in GetType().GetProperties())
//        {
//            if (p.PropertyType.IsAssignableTo(typeof(INotifyPropertyChanged)))
//                ((INotifyPropertyChanged)p.GetValue(this)!).PropertyChanged += ItemPropertyChanged;

//            if (p.PropertyType.IsGenericType && (p.PropertyType.GetGenericTypeDefinition() == typeof(ValueViewModel<>) || p.PropertyType.GetGenericTypeDefinition() == typeof(RefrenceViewModel<>)) && p.PropertyType.GetGenericArguments()[0].IsAssignableTo(typeof(INotifyPropertyChanged)))
//            {
//                var ty = typeof(WeakEventManager<,>).MakeGenericType([p.PropertyType, typeof(PropertyChangedEventArgs)]);
              
//                var m = ty.GetMethod("AddHandler", BindingFlags.Static | BindingFlags.Public);
//                var pobj = p.GetValue(this);
//                if(m is not null)
//                m.Invoke(null, [pobj, nameof(PropertyChanged), new EventHandler<PropertyChangedEventArgs>(OnNewChanged)]);
//                 //WeakEventManager<ValueViewModel<TProperty>, PropertyChangedEventArgs>.AddHandler(Data, nameof(INotifyPropertyChanged.PropertyChanged), OnDataPropertyChanged);

//            }
//        }
//    }

//    private void OnNewChanged(object? sender, PropertyChangedEventArgs e)
//    {
//        if(e.PropertyName == nameof(ValueViewModel<>.New) && sender?.GetType()?.GetProperty(nameof(ValueViewModel<>.New)) is PropertyInfo info && info.PropertyType.IsAssignableTo(typeof(INotifyPropertyChanged)))
//        {
//            var ty = typeof(WeakEventManager<,>).MakeGenericType([info.PropertyType, typeof(PropertyChangedEventArgs)]);

//            var m = ty.GetMethod("AddHandler", BindingFlags.Static | BindingFlags.Public); 
//            if (m is not null)
//                m.Invoke(null, [sender, nameof(PropertyChanged), new EventHandler<PropertyChangedEventArgs>(ItemPropertyChanged)]);
//        }
//    }

//    protected virtual string? DisplayOverride() => ToString();

//    protected void ItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
//    {
//        if (sender is IModifiableValue x)
//        {
//            OnPropertyChanged(nameof(CanConfirm));
//            OnPropertyChanged(nameof(CanDelete));
//            OnPropertyChanged(nameof(HasUnsavedValue));
//        }
//    }

//    public virtual void ApplyOverride()
//    {
//        foreach (var p in GetType().GetProperties())
//        {
//            if (p.PropertyType.IsAssignableTo(typeof(IModifiableValue)))
//                ((IModifiableValue)p.GetValue(this)!).Apply();
//        }

//        OnPropertyChanged(nameof(CanConfirm));
//        OnPropertyChanged(nameof(CanDelete));
//        OnPropertyChanged(nameof(HasUnsavedValue));
//    }

//    public void Apply()
//    {
//        ApplyOverride();

//        OnPropertyChanged(nameof(Display));
//    }


//    public void Reset()
//    {
//        foreach (var p in GetType().GetProperties())
//        {
//            if (p.PropertyType.IsAssignableTo(typeof(IModifiableValue)))
//                ((IModifiableValue)p.GetValue(this)!).Clear();
//        }
//    }

//}

/// <summary>
/// 更新到实体
/// </summary>
/// <typeparam name="T"></typeparam>
//public interface IEntityViewModel<T> where T : notnull
//{
//    public void Init(T param);

//    public void UpdateEntity(T obj);

//    public Action<T>? ClearFunc { get; set; }

//    public void RemoveValue(T obj)
//    {
//        if (obj is not null && ClearFunc is not null)
//            ClearFunc(obj);
//    }
//}


/// <summary>
/// 用于struct
/// </summary>
/// <typeparam name="TEntity"></typeparam>
/// <typeparam name="TProperty"></typeparam>
//public partial class EntityValueViewModel<TEntity, TProperty> : UnitViewModel, IEntityViewModel<TEntity> where TProperty : struct where TEntity : notnull
//{
//    public ValueViewModel<TProperty> Data { get; } = new();

//    public override bool CanConfirm => Data.IsChanged;

//    public override bool CanDelete => !HasUnsavedValue && Data.Old is not null;

//    public override bool HasUnsavedValue => Data.IsChanged;

//    public Func<TEntity, TProperty?>? InitFunc { get; set; }

//    public Action<TEntity, TProperty?>? UpdateFunc { get; set; }

//    public Action<TEntity>? ClearFunc { get; set; }

//    public Func<TProperty?, string?>? DisplayFunc { get; set; }

//    protected override string? DisplayOverride()
//    {
//        if (DisplayFunc is not null)
//            return DisplayFunc(Data.Old);

//        return Data.Old switch { Enum e => EnumDescriptionTypeConverter.GetEnumDescription(e), null => null, var a => a.ToString() };
//    }

//    public void Init(TEntity param)
//    {
//        if (param is not null && InitFunc is not null)
//        {
//            Data.Old = InitFunc(param);
//            Data.New = InitFunc(param);
//        }

//        WeakEventManager<ValueViewModel<TProperty>, PropertyChangedEventArgs>.AddHandler(Data, nameof(INotifyPropertyChanged.PropertyChanged), OnDataPropertyChanged);

//        SubscribeChanges();
//    }

//    private void OnDataPropertyChanged(object? sender, PropertyChangedEventArgs e)
//    {
//        if (e.PropertyName == nameof(Data.New) && Data.New is INotifyPropertyChanged changed)
//            WeakEventManager<TProperty, PropertyChangedEventArgs>.AddHandler(Data.New.Value, nameof(INotifyPropertyChanged.PropertyChanged), ItemPropertyChanged);
//    }

//    public void UpdateEntity(TEntity obj)
//    {
//        if (obj is not null && UpdateFunc is not null && Data.New is not null)
//            UpdateFunc(obj, Data.New);
//    }

//}



/// <summary>
/// 用于class
/// </summary>
/// <typeparam name="TEntity"></typeparam>
/// <typeparam name="TProperty"></typeparam>
//public partial class EntityRefrenceViewModel<TEntity, TProperty> : UnitViewModel, IEntityViewModel<TEntity> where TProperty : class where TEntity : notnull
//{
//    public RefrenceViewModel<TProperty> Data { get; } = new();

//    public override bool CanConfirm => Data.IsChanged;

//    public override bool CanDelete => !HasUnsavedValue && Data.Old is not null;

//    public override bool HasUnsavedValue => Data.IsChanged;

//    public Func<TEntity, TProperty?>? InitFunc { get; set; }

//    public Action<TEntity, TProperty?>? UpdateFunc { get; set; }

//    public Action<TEntity>? ClearFunc { get; set; }

//    public Func<TProperty?, string?>? DisplayFunc { get; set; }

//    protected override string? DisplayOverride()
//    {
//        if (DisplayFunc is not null)
//            return DisplayFunc(Data.Old);

//        return Data.Old switch { Enum e => EnumDescriptionTypeConverter.GetEnumDescription(e), null => null, var a => a.ToString() };
//    }

//    public void Init(TEntity param)
//    {
//        SubscribeChanges();
//        if (param is not null && InitFunc is not null)
//        {
//            Data.Old = InitFunc(param);
//            Data.New = InitFunc(param);
//        }
//    }

//    public void UpdateEntity(TEntity obj)
//    {
//        if (obj is not null && UpdateFunc is not null && Data.New is not null)
//            UpdateFunc(obj, Data.New);
//    }

//}






