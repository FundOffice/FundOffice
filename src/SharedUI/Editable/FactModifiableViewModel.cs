using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FMO.Models;
using FMO.Utilities;
using LiteDB;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using Utilities;

namespace FMO.Shared;



public class FactorChangeEventArgs<TProperty>
{

    public FactorChangeEventArgs(FactorModifiableViewModel<TProperty> sender, ValueChangeKind kind)
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

    public FactorChangeEventArgs(ValueChangeKind kind, int shareId, int flowId, int fundId, TProperty? oldValue, TProperty? newValue, TProperty? fallbackValue)
    {
        Kind = kind;
        ShareId = shareId;
        FlowId = flowId;
        FundId = fundId;
        OldValue = oldValue;
        NewValue = newValue;
        FallbackValue = fallbackValue;
    }

    public FactorChangeEventArgs(ValueChangeKind kind, int shareId, int flowId, int fundId, TProperty value)
    {
        Kind = kind;
        ShareId = shareId;
        FlowId = flowId;
        FundId = fundId;
        NewValue = value;
    }

    public ValueChangeKind Kind { get; set; }

    public int ShareId { get; }

    public int FlowId { get; }

    public string FactorId { get; } = null!;

    public int FundId { get; }

    public TProperty? OldValue { get; set; }
    public TProperty? NewValue { get; set; }
    public TProperty? FallbackValue { get; set; }

    private T? CastValue<T>(TProperty? property)
    {
        if (property is T t) return t;
        else if (property is IViewModel<T> vm) return vm.Build();
        return default;
    }

    public FactorChangeEventArgs<T>? Cast<T>()
    {
        return new FactorChangeEventArgs<T>(Kind, ShareId, FlowId, FundId, CastValue<T>(OldValue), CastValue<T>(NewValue), CastValue<T>(FallbackValue));
    }
}

public delegate void FactorChangedHandler<T>(FactorChangeEventArgs<T> e);

public partial class FactorModifiableViewModel<TValue> : ModifiableViewModel<TValue>, IValueModifier
{

    public string Id => $"{FundId}.{FlowId}.{ShareId}.{FactorId}";

    public required int ShareId { get; set; }

    public required int FlowId { get; init; }

    public required int FundId { get; init; }

    public required string FactorId { get; init; }

    public string? ShareName { get; set; }

    protected override void NotifyChanged(ValueChangeKind kind, TValue? value)
    {
        if (kind is ValueChangeKind.Added or ValueChangeKind.Modified)
            SaveChange(FundId, FactorId, FlowId, ShareId, value);
        else if (kind is ValueChangeKind.Deleted)
            RemoveFact(FundId, FactorId, FlowId, ShareId);

        base.NotifyChanged(kind, value);
    }


    void SaveChange<T>(int fundId, string factId, int flowId, int shareId, T data)
    {
        using var db = DbHelper.Base();
        db.GetCollection<IFundFactor>().Upsert(new FundFactor<T>(factId, fundId, flowId, shareId, data));
    }

    void RemoveFact(int fundId, string factId, int flowId, int shareId)
    {
        using var db = DbHelper.Base();
        db.GetCollection<IFundFactor>().Delete($"{fundId}.{flowId}.{shareId}.{factId}");
    }
}

public partial class FactorModifiableViewModel<TValue, TViewModel> : ModifiableViewModel<TValue, TViewModel>, IValueModifier where TViewModel : IViewModel<TValue, TViewModel>
{


    public string Id => $"{FundId}.{FlowId}.{ShareId}.{FactorId}";

    public required int ShareId { get; set; }

    public required int FlowId { get; init; }

    public required int FundId { get; init; }

    public required string FactorId { get; init; }

    public string? ShareName { get; set; }

    protected override void NotifyChanged(ValueChangeKind kind, TViewModel value)
    {
        if (kind is ValueChangeKind.Added or ValueChangeKind.Modified)
            SaveChange(FundId, FactorId, FlowId, ShareId, TViewModel.Trans(value));
        else if (kind is ValueChangeKind.Deleted)
            RemoveFact(FundId, FactorId, FlowId, ShareId);

        base.NotifyChanged(kind, value);
    }


    void SaveChange<T>(int fundId, string factId, int flowId, int shareId, T data)
    {
        using var db = DbHelper.Base();
        db.GetCollection<IFundFactor>().Upsert(new FundFactor<T>(factId, fundId, flowId, shareId, data));
    }

    void RemoveFact(int fundId, string factId, int flowId, int shareId)
    {
        using var db = DbHelper.Base();
        db.GetCollection<IFundFactor>().Delete($"{fundId}.{flowId}.{shareId}.{factId}");
    }
}



/// <summary>
public partial class ShareFactorViewModel<TValue> : ObservableObject, IFactorModifier
{ 

    [SetsRequiredMembers]
    public ShareFactorViewModel(int fundId, int flowId, string factorId, ShareClass[] sc, (TValue? Old, TValue? New)[] data)
    {
        FundId = fundId;
        FlowId = flowId;
        FactorId = factorId;
        Classes = sc;


        // 要素不拆分
        if (data.Length == 1)
        {
            CanDivide = sc.Length > 1;
            CanMerge = false;
            var u = new FactorModifiableViewModel<TValue>
            {
                FundId = fundId,
                FlowId = flowId,
                ShareId = sc[0].Id,
                FactorId = factorId,
                ShareName = null,
                NewValue = (data[0].New),
                OldValue = CloneHelper.CloneValue(data[0].New),
                FallbackValue = data[0].Old
            };
            Data.Add(u);
        }
        else
        {
            CanDivide = false;
            CanMerge = sc.Length > 1;

            for (int i = 0; i < sc.Length; i++)
            {
                var c = sc[i];
                var u = new FactorModifiableViewModel<TValue>
                {
                    FundId = fundId,
                    FlowId = flowId,
                    ShareId = c.Id,
                    FactorId = factorId,
                    ShareName = c.Name,
                    NewValue = data[i].New,
                    OldValue = CloneHelper.CloneValue(data[i].New),
                    FallbackValue = data[i].Old
                };
                Data.Add(u);
            }

        }
    }




    public ObservableCollection<FactorModifiableViewModel<TValue>> Data { get; } = new();

    public int FlowId { get; }
    public string FactorId { get; }
    public required ShareClass[] Classes { get; set; }



    /// <summary>
    /// 可分割
    /// </summary>
    [ObservableProperty]
    public partial bool CanDivide { get; set; }

    /// <summary>
    /// 可合并
    /// </summary>
    [ObservableProperty]
    public partial bool CanMerge { get; set; }


    public int FundId { get; set; }

    [RelayCommand]
    public void Divide()
    {
        if (Classes.Length == 1)
        {
            Toast.Warning("单一份额，不允许拆分要素");
            return;
        }

        CanDivide = false;
        CanMerge = true;

        var sc = Classes;


        var unit = Data[0];
        var v = unit.OldValue!;
        var fallback = unit.FallbackValue;
        bool isInherited = unit.IsInherited;

        // 非继承值，需要实际拆分
        if (!isInherited)
        {
            using var db = DbHelper.Base();
            db.GetCollection<IFundFactor>().Insert(sc.Select(x => new FundFactor<TValue>(FactorId, FundId, FlowId, x.Id, v)));
            db.GetCollection<IFundFactor>().Delete(unit.Id);
        }

        for (int i = 0; i < sc.Length; i++)
        {
            var c = sc[i];
            var u = new FactorModifiableViewModel<TValue>
            {
                FundId = FundId,
                FlowId = FlowId,
                ShareId = c.Id,
                FactorId = FactorId,
                ShareName = c.Name,
                NewValue = CloneHelper.CloneValue(v),
                OldValue = CloneHelper.CloneValue(v),
                FallbackValue = CloneHelper.CloneValue(fallback)
            };
            Data.Add(u);
        }
        Data.RemoveAt(0);

    }

    [RelayCommand]
    public void Unify(FactorModifiableViewModel<TValue> unit)
    {
        CanDivide = true;
        CanMerge = false;

        var sc = Classes;

        ///更新到数据库，删除所有要素
        using var db = DbHelper.Base();
        var e = db.GetCollection<IFundFactor>().DeleteMany(x => x.FlowId == FlowId && x.FactorId == FactorId && x.FundId == FundId);
        db.GetCollection<IFundFactor>().Insert(new FundFactor<TValue>(factorId: FactorId, fundId: FundId, flowId: FlowId, data: unit.OldValue!));


        unit.ShareId = -1;
        unit.ShareName = null;

 
        Data.Clear();
        Data.Add(unit);
    }

    
}



public partial class ShareFactorViewModel<TValue, TViewModel> : ObservableObject, IFactorModifier where TViewModel : IViewModel<TValue, TViewModel>
{ 

    [SetsRequiredMembers]
    public ShareFactorViewModel(int fundId, int flowId, string factorId, ShareClass[] sc, (TValue? Old, TValue? New)[] data)
    {
        FundId = fundId;
        FlowId = flowId;
        FactorId = factorId;
        Classes = sc;


        // 要素不拆分
        if (data.Length == 1)
        {
            CanDivide = sc.Length > 1;
            CanMerge = false;
            var u = new FactorModifiableViewModel<TValue, TViewModel>
            {
                FundId = fundId,
                FlowId = flowId,
                ShareId = sc[0].Id,
                FactorId = factorId,
                ShareName = null,
                NewValue = TViewModel.Trans(data[0].New),
                OldValue = data[0].New,
                FallbackValue = data[0].Old
            };
            Data.Add(u);
        }
        else
        {
            CanDivide = false;
            CanMerge = sc.Length > 1;

            for (int i = 0; i < sc.Length; i++)
            {
                var c = sc[i];
                var u = new FactorModifiableViewModel<TValue, TViewModel>
                {
                    FundId = fundId,
                    FlowId = flowId,
                    ShareId = c.Id,
                    FactorId = factorId,
                    ShareName = c.Name,
                    NewValue = TViewModel.Trans(data[i].New),
                    OldValue = data[i].New,
                    FallbackValue = data[i].Old
                };
                Data.Add(u);
            }

        }
    }


    public ObservableCollection<FactorModifiableViewModel<TValue, TViewModel>> Data { get; } = new();

    public int FlowId { get; }
    public string FactorId { get; }
    public required ShareClass[] Classes { get; set; }



    /// <summary>
    /// 可分割
    /// </summary>
    [ObservableProperty]
    public partial bool CanDivide { get; set; }

    /// <summary>
    /// 可合并
    /// </summary>
    [ObservableProperty]
    public partial bool CanMerge { get; set; }


    public int FundId { get; set; }

    [RelayCommand]
    public void Divide()
    {
        if (Classes.Length == 1)
        {
            Toast.Warning("单一份额，不允许拆分要素");
            return;
            //   throw new InvalidOperationException("唯一份额类型，不允许拆分要素");
        }

        CanDivide = false;
        CanMerge = true;

        var sc = Classes;

        var unit = Data[0];
        var v = unit.OldValue!;
        var fallback = unit.FallbackValue;
        bool isInherited = unit.IsInherited;

        // 非继承值，需要实际拆分
        if (!isInherited)
        {
            using var db = DbHelper.Base();
            db.GetCollection<IFundFactor>().Delete(unit.Id);
            db.GetCollection<IFundFactor>().Insert(sc.Select(x => new FundFactor<TValue>(FactorId, FundId, FlowId, x.Id, v)));
        }

        for (int i = 0; i < sc.Length; i++)
        {
            var c = sc[i];
            var u = new FactorModifiableViewModel<TValue, TViewModel>
            {
                FundId = FundId,
                FlowId = FlowId,
                ShareId = c.Id,
                FactorId = FactorId,
                ShareName = c.Name,
                NewValue = TViewModel.Trans(v),
                OldValue = v,
                FallbackValue = fallback
            };
            Data.Add(u);
        }

        Data.RemoveAt(0);
    }

    [RelayCommand]
    public void Unify(FactorModifiableViewModel<TValue, TViewModel> unit)
    {
        CanDivide = true;
        CanMerge = false;

        var sc = Classes;

        ///更新到数据库，删除所有要素
        using var db = DbHelper.Base();
        var e = db.GetCollection<IFundFactor>().DeleteMany(x=> x.FlowId == FlowId && x.FactorId == FactorId && x.FundId == FundId);
        db.GetCollection<IFundFactor>().Insert(new FundFactor<TValue>(FactorId, FundId, FlowId, unit.OldValue!));



        unit.ShareId = -1;
        unit.ShareName = null;

        Data.Clear();
        Data.Add(unit);
    }
     
}