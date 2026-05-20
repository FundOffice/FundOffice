using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FMO.Models;
using FMO.Shared;
using FMO.Utilities;
using LiteDB;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace FMO;




public partial class ShareElementsViewModel<TProperty, TViewModel> : ObservableObject where TProperty : notnull
{


    [SetsRequiredMembers]
    public ShareElementsViewModel(int fundid, int flowId, FundElements elements, ShareClass[] sc, Func<FundElements, PortionMutable<TProperty>> property, Func<TProperty?, TViewModel> o2v, Func<TViewModel?, TProperty> v2o, Func<TViewModel?, string?>? desp = null)
    {
        FundId = fundid;
        FlowId = flowId;
        Classes = sc;
        ObjectToViewModel = o2v;
        ViewModelToObject = v2o;
        Property = property;
        DisplayFunc = desp;

        var prop = Property(elements);
        var (id, dic) = prop.GetValue(flowId);

        //单一份额
        if (dic is null || dic.Count == 0 || (dic!.Count == 1 && dic.First().Key == -1))
        {
            UnifiedClass = true;
            var u = new ChangeableViewModel<FundElements, TViewModel>
            {
                InitFunc = x => ObjectToViewModel(dic?.Count switch { > 0 => dic!.First().Value, _ => default }),
                UpdateFunc = (x, y) => Property(x).SetValue(-1, ViewModelToObject(y), flowId),
                ClearFunc = x => Property(x).RemoveValue(flowId),
                DisplayFunc = DisplayFunc
            };
            u.Init(elements);
            Data.Add(u);
        }
        else
        {
            UnifiedClass = false;

            foreach (var c in sc)
            {
                var v = prop.GetValue(c.Id, flowId);

                var u = new ChangeableViewModel<FundElements, TViewModel>
                {
                    Label = c.Name,
                    InitFunc = x => ObjectToViewModel(v.Value),
                    UpdateFunc = (x, y) => Property(x).SetValue(-1, ViewModelToObject(y), flowId),
                    ClearFunc = x => Property(x).RemoveValue(flowId),
                    DisplayFunc = DisplayFunc
                };
                u.Init(elements);
                Data.Add(u);
            }

        }
    }


    public ObservableCollection<ChangeableViewModel<FundElements, TViewModel>> Data { get; } = new();

    public int FlowId { get; }

    public required ShareClass[] Classes { get; set; }

    /// <summary>
    /// 单一份额
    /// </summary>
    [ObservableProperty]
    public partial bool UnifiedClass { get; set; }


    public Func<FundElements, int, (int, TProperty)>? InitFunc { get; set; }



    public required Func<FundElements, PortionMutable<TProperty>> Property { get; set; }

    public required Func<TProperty?, TViewModel> ObjectToViewModel { get; set; }

    public required Func<TViewModel?, TProperty> ViewModelToObject { get; set; }

    public Func<TViewModel?, string?>? DisplayFunc { get; set; }
    public int FundId { get; set; }

    [RelayCommand]
    public void Divide()
    {
        UnifiedClass = false;
        var sc = Classes;

        var v = Data[0].OldValue;

        foreach (var c in sc)
        {
            var u = new ChangeableViewModel<FundElements, TViewModel>
            {
                Label = c.Name,
                InitFunc = x => JsonSerializer.Deserialize<TViewModel>(JsonSerializer.Serialize(v)),
                UpdateFunc = (x, y) => Property(x).SetValue(c.Id, ViewModelToObject(y), FlowId),
                ClearFunc = x => Property(x).RemoveValue(FlowId),
                DisplayFunc = DisplayFunc
            };
            u.NewValue = JsonSerializer.Deserialize<TViewModel>(JsonSerializer.Serialize(v));
            u.OldValue = JsonSerializer.Deserialize<TViewModel>(JsonSerializer.Serialize(v));
            Data.Add(u);
        }
        Data.RemoveAt(0);

        ///更新到数据库
        using var db = DbHelper.Base();
        var e = db.GetCollection<FundElements>().FindById(FundId);
        PortionMutable<TProperty> prop = Property(e);
        var (id, dic) = prop.GetValue(FlowId);
        var obj = ViewModelToObject(v);

        foreach (var c in sc)
            prop.SetValue(c.Id, obj, FlowId);
        prop.RemoveValue(-1, FlowId);
        db.GetCollection<FundElements>().Update(e);
    }

    [RelayCommand]
    public void Unify(ChangeableViewModel<FundElements, TViewModel> unit)
    {
        UnifiedClass = true;
        var sc = Classes;

        var v = unit.OldValue;

        var u = new ChangeableViewModel<FundElements, TViewModel>
        {
            InitFunc = x => JsonSerializer.Deserialize<TViewModel>(JsonSerializer.Serialize(v)),
            UpdateFunc = (x, y) => Property(x).SetValue(-1, ViewModelToObject(y), FlowId),
            ClearFunc = x => Property(x).RemoveValue(FlowId),
            DisplayFunc = DisplayFunc
        };
        u.NewValue = v;
        u.OldValue = JsonSerializer.Deserialize<TViewModel>(JsonSerializer.Serialize(v));
        Data.Clear();
        Data.Add(u);

        ///更新到数据库
        using var db = DbHelper.Base();
        var e = db.GetCollection<FundElements>().FindById(FundId);
        PortionMutable<TProperty> prop = Property(e);
        var (id, dic) = prop.GetValue(FlowId);
        var obj = ViewModelToObject(v);

        foreach (var c in sc)
            prop.RemoveValue(c.Id, FlowId);
        prop.SetValue(-1, obj, FlowId);
        db.GetCollection<FundElements>().Update(e);
    }
}



public partial class ShareElementsViewModel2<TValue, TViewModel> : ObservableObject
{

    [SetsRequiredMembers]
    public ShareElementsViewModel2(int fundId, int flowId, string factorId, ShareClass[] sc, (TViewModel? Old, TViewModel? New)[] data)
    {
        FundId = fundId;
        FlowId = flowId;
        FactorId = factorId;
        Classes = sc;

        // 要素不拆分
        if (data.Length == 1)
        {
            UnifiedClass = true;
            var u = new FactModifiableViewModel<TViewModel>
            {
                FundId = fundId,
                FlowId = flowId,
                ShareId = sc[0].Id,
                FactorId = factorId,
                ShareName = sc[0].Name,
                NewValue = (data[0].New),
                OldValue = CloneHelper.CloneValue(data[0].New),
                FallbackValue = data[0].Old
            };
            u.Changed += U_Changed;
            Data.Add(u);
        }
        else
        {
            UnifiedClass = false;

            for (int i = 0; i < sc.Length; i++)
            {
                var c = sc[i];
                var u = new FactModifiableViewModel<TViewModel>
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
                u.Changed += U_Changed;
                Data.Add(u);
            }

        }
    }

    private void U_Changed(object? sender, FactChangeEventArgs<TViewModel> e)
    {
        if (e.Kind is ValueChangeKind.Added or ValueChangeKind.Modified)
            SaveChange(e.FundId, e.FactorId, e.FlowId, e.ShareId, e.NewValue);
        else if (e.Kind is ValueChangeKind.Deleted)
            RemoveFact(e.FundId, e.FactorId, e.FlowId, e.ShareId);
    }

    void SaveChange(int fundId, string factId, int flowId, int shareId, TViewModel? data)
    {
        if (data is TValue good)
        {
            using var db = DbHelper.Base();
            db.GetCollection<IFundFactor>().Upsert(new FundFactor<TValue>(factId, fundId, flowId, shareId, good));
        }
        else if (data is IViewModel<TValue> vm)
        {
            using var db = DbHelper.Base();
            db.GetCollection<IFundFactor>().Upsert(new FundFactor<TValue>(factId, fundId, flowId, shareId, vm.Build()));
        }
    }
    void RemoveFact(int fundId, string factId, int flowId, int shareId)
    {
        using var db = DbHelper.Base();
        db.GetCollection<IFundFactor>().Delete($"{fundId}.{flowId}.{shareId}.{factId}");
    }



    public ObservableCollection<FactModifiableViewModel<TViewModel>> Data { get; } = new();

    public int FlowId { get; }
    public string FactorId { get; }
    public required ShareClass[] Classes { get; set; }

    /// <summary>
    /// 单一份额
    /// </summary>
    [ObservableProperty]
    public partial bool UnifiedClass { get; set; }


    public int FundId { get; set; }

    [RelayCommand]
    public void Divide()
    {
        if (Classes.Length == 1)
            throw new InvalidOperationException("唯一份额类型，不允许拆分要素");

        UnifiedClass = false;
        var sc = Classes;

        var v = Data[0].OldValue;
        var fallback = Data[0].FallbackValue;

        for (int i = 0; i < sc.Length; i++)
        {
            var c = sc[i];
            var u = new FactModifiableViewModel<TViewModel>
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
            u.Changed += U_Changed;
            Data.Add(u);
        }
        Data.RemoveAt(0);

    }

    [RelayCommand]
    public void Unify(FactModifiableViewModel<TViewModel> unit)
    {
        UnifiedClass = true;
        var sc = Classes;
         
        ///更新到数据库
        using var db = DbHelper.Base();
        var e = db.GetCollection<IFundFactor>().DeleteMany(Query.In("_id", Data.Select(x => new BsonValue(x.Id))));

     

        unit.ShareId = -1;
        unit.ShareName = null;

        SaveChange(FundId, FactorId, FlowId, -1, unit.OldValue);

        Data.Clear();
        Data.Add(unit);
    }
}

