using FMO.Models;
using System.Diagnostics.CodeAnalysis;

namespace FMO;

public class ElementValueViewModel<T> : ElementItemViewModel where T : struct
{
    public ValueViewModel<T> Data { get; } = new();


    public override bool CanConfirm => Data.IsChanged;

    public override bool CanDelete => !HasUnsavedValue && Data.New is not null;


    public override bool HasUnsavedValue => Data.IsChanged;


    public Func<T?, string?>? DisplayGenerator { get; set; }


    [SetsRequiredMembers]
    public ElementValueViewModel(FundElements elements, string property, int flowid, string label)
    {
        Property = property;
        var obj = elements.GetType().GetProperty(property)?.GetValue(elements);

        if (obj is Mutable<T> mutable)
        {
            Label = label;
            (int fid, T? dec) = mutable.GetValue(flowid);
            Data.Old = fid == flowid ? dec : fid == -1 ? default : dec;
            Data.New = fid == flowid ? dec : default;
        }
        else
            throw new NotImplementedException();

        Data.PropertyChanged += ItemPropertyChanged;
    }


    public override void Init(FundElements elements, int flowid)
    {
        var obj = elements.GetType().GetProperty(Property)?.GetValue(elements);

        if (obj is Mutable<T> mutable)
        {
            (int fid, T? dec) = mutable.GetValue(flowid);
            Data.Old = fid == flowid ? dec : fid == -1 ? default : dec;
            Data.New = fid == flowid ? dec : default;
        }
        else if (obj is Mutable<T?> mutable2)
        {
            (int fid, T? dec) = mutable2.GetValue(flowid);
            Data.Old = fid == flowid ? dec : fid == -1 ? default : dec;
            Data.New = fid == flowid ? dec : default;
        }

        Init();
    }

    public override void UpdateEntity(FundElements elements, int flowid)
    {
        var obj = elements.GetType().GetProperty(Property)?.GetValue(elements);

        if (Data.New is null) return;

        if (obj is Mutable<T> mutable)
            mutable.SetValue(Data.New.Value, flowid);
    }


    public override void RemoveValue(FundElements elements, int flowid)
    {
        var obj = elements.GetType().GetProperty(Property)?.GetValue(elements);

        if (Data.New is null) return;

        if (obj is Mutable<T> mutable)
            mutable.RemoveValue(flowid);

        Data.New = null;
    }


    public override void ApplyOverride()
    {
        Data.Apply();
        OnPropertyChanged(nameof(CanConfirm));
    }

    protected override string? DisplayOverride()
    {
        return DisplayGenerator is not null ? DisplayGenerator(Data.Old) : Data.Old?.ToString();
    }
}


public class ElementRefrenceViewModel<T> : ElementItemViewModel where T : class
{
    public RefrenceViewModel<T> Data { get; } = new();


    public override bool CanConfirm => Data.IsChanged;

    public override bool CanDelete => !HasUnsavedValue && Data.New is not null;

    public override bool HasUnsavedValue => Data.IsChanged;


    public Func<T?, string?>? DisplayGenerator { get; set; }

    [SetsRequiredMembers]
    public ElementRefrenceViewModel(FundElements elements, string property, int flowid, string label)
    {
        Property = property;
        var obj = elements.GetType().GetProperty(property)?.GetValue(elements);

        if (obj is Mutable<T> mutable)
        {
            Label = label;
            (int fid, T? dec) = mutable.GetValue(flowid);
            Data.Old = fid == flowid ? dec : fid == -1 ? default : dec;
            Data.New = Data.Old;
        }
        else
            throw new NotImplementedException();

        Data.PropertyChanged += ItemPropertyChanged;
    }


    public override void Init(FundElements elements, int flowid)
    {
        var obj = elements.GetType().GetProperty(Property)?.GetValue(elements);

        if (obj is Mutable<T> mutable)
        {
            (int fid, T? dec) = mutable.GetValue(flowid);
            Data.Old = fid == flowid ? dec : fid == -1 ? default : dec;
            Data.New = fid == flowid ? dec : default;
        }

        Init();
    }

    public override void UpdateEntity(FundElements elements, int flowid)
    {
        var obj = elements.GetType().GetProperty(Property)?.GetValue(elements);

        if (Data.New is null) return;

        if (obj is Mutable<T> mutable)
            mutable.SetValue(Data.New, flowid);
    }

    public override void RemoveValue(FundElements elements, int flowid)
    {
        var obj = elements.GetType().GetProperty(Property)?.GetValue(elements);

        if (Data.New is null) return;

        if (obj is Mutable<T> mutable)
            mutable.RemoveValue(flowid);


        Data.New = null;
    }


    public override void ApplyOverride()
    {
        Data.Apply();
        OnPropertyChanged(nameof(CanConfirm));
    }

    protected override string? DisplayOverride()
    {
        return DisplayGenerator is not null ? DisplayGenerator(Data.Old) : Data.Old?.ToString();
    } 
}


public class ElementItemWithEnumViewModel<TEnum, TValue> : ElementItemViewModel where TEnum : struct, Enum where TValue : struct
{
    public ValueViewModel<TEnum> Type { get; } = new();

    public ValueViewModel<TValue> Data { get; } = new();

    public RefrenceViewModel<string> Extra { get; } = new();



    public override bool CanConfirm => Data.IsChanged;

    public override bool CanDelete => !HasUnsavedValue && (Data.New is not null || Type.New is not null);

    public override bool HasUnsavedValue => Type.IsChanged || Data.IsChanged || Extra.IsChanged;


    [SetsRequiredMembers]
    public ElementItemWithEnumViewModel(FundElements elements, string property, int flowid, string label) //: base(elements, property, flowid, label)
    {
        Property = property;
        var obj = elements.GetType().GetProperty(property)?.GetValue(elements) as dynamic;

        if (obj is Mutable<ValueWithEnum<TEnum, TValue>> mutable)
        {
            Label = label;
            (int fid, ValueWithEnum<TEnum, TValue>? dec) = mutable.GetValue(flowid);

            Type.Old = fid == -1 ? null : fid == flowid && dec is not null ? dec.Type : null;
            Type.New = Type.Old;

            Data.Old = fid == -1 ? null : fid == flowid && dec is not null ? dec.Value : null;
            Data.New = Data.Old;

            Extra.Old = fid == -1 ? null : fid == flowid && dec is not null ? dec.Extra : null;
            Extra.New = Extra.Old;

        }
        else if (obj is Mutable<ValueWithEnum<TEnum, TValue?>> mutable2)
        {
            Label = label;
            (int fid, ValueWithEnum<TEnum, TValue?>? dec) = mutable2.GetValue(flowid);

            Type.Old = fid == -1 ? default : fid == flowid && dec is not null ? dec.Type : default;
            Type.New = Type.Old;

            Data.Old = fid == -1 ? null : fid == flowid && dec is not null ? dec.Value : null;
            Data.New = Data.Old;

            Extra.Old = fid == -1 ? null : fid == flowid && dec is not null ? dec.Extra : null;
            Extra.New = Extra.Old;
        }
        else
            throw new NotImplementedException();

        Type.PropertyChanged += ItemPropertyChanged;
        Data.PropertyChanged += ItemPropertyChanged;
        Extra.PropertyChanged += ItemPropertyChanged;
    }

    public override void UpdateEntity(FundElements elements, int flowid)
    {
        var obj = elements.GetType().GetProperty(Property)?.GetValue(elements);

        if (Data.New is null || Type.New is null) return;

        if (obj is Mutable<ValueWithEnum<TEnum, TValue>> mutable)
            mutable.SetValue(new ValueWithEnum<TEnum, TValue> { Type = Type.New.Value, Value = Data.New.Value, Extra = Extra.New }, flowid);

        //else if (obj is Mutable<T?> mutable2)
        //    mutable.SetValue(new ValueWithEnum<TEnum, TValue> { Type = Type.New, Value = Data.New, Extra = Extra.New }, flowid);

        else throw new NotImplementedException();
    }


    public override void RemoveValue(FundElements elements, int flowid)
    {
        var obj = elements.GetType().GetProperty(Property)?.GetValue(elements);

        if (Data.New is null) return;

        if (obj is Mutable<ValueWithEnum<TEnum, TValue>> mutable)
            mutable.RemoveValue(flowid);


        Type.New = null;
        Data.New = null;
        Extra.New = null;
    }


    public override void Init(FundElements elements, int flowid)
    {
        throw new NotImplementedException();
    }

    public override void ApplyOverride()
    {
        Type.Apply();
        Data.Apply();
        Extra.Apply();
    }


    protected override string? DisplayOverride()
    {
        return Type.ToString() == "Other" ? Extra.Old : Type.Old is null ? null : $"{EnumDescriptionTypeConverter.GetEnumDescription(Type.Old)} {Data.Old}";
    }
}




public class ElementRefItemWithEnumViewModel<TEnum, TValue> : ElementItemViewModel where TEnum : struct, Enum where TValue : class
{
    public ValueViewModel<TEnum> Type { get; } = new();

    public RefrenceViewModel<TValue> Data { get; } = new();

    public RefrenceViewModel<string> Extra { get; } = new();



    public override bool CanConfirm => Data.IsChanged;

    public override bool CanDelete => !HasUnsavedValue && (Data.New is not null || Type.New is not null);

    public override bool HasUnsavedValue => Type.IsChanged || Data.IsChanged || Extra.IsChanged;

    [SetsRequiredMembers]
    public ElementRefItemWithEnumViewModel(FundElements elements, string property, int flowid, string label) //: base(elements, property, flowid, label)
    {
        Property = property;
        var obj = elements.GetType().GetProperty(property)?.GetValue(elements) as dynamic;

        if (obj is Mutable<ValueWithEnum<TEnum, TValue>> mutable)
        {
            Label = label;
            (int fid, ValueWithEnum<TEnum, TValue>? dec) = mutable.GetValue(flowid);

            Type.Old = fid == -1 ? null : fid == flowid && dec is not null ? dec.Type : null;
            Type.New = Type.Old;

            Data.Old = fid == -1 ? null : fid == flowid && dec is not null ? dec.Value : null;
            Data.New = Data.Old;

            Extra.Old = fid == -1 ? null : fid == flowid && dec is not null ? dec.Extra : null;
            Extra.New = Extra.Old;

        }
        else if (obj is Mutable<ValueWithEnum<TEnum, TValue?>> mutable2)
        {
            Label = label;
            (int fid, ValueWithEnum<TEnum, TValue?>? dec) = mutable2.GetValue(flowid);

            Type.Old = fid == -1 ? null : fid == flowid && dec is not null ? dec.Type : null;
            Type.New = Type.Old;

            Data.Old = fid == -1 ? null : fid == flowid && dec is not null ? dec.Value : null;
            Data.New = Data.Old;

            Extra.Old = fid == -1 ? null : fid == flowid && dec is not null ? dec.Extra : null;
            Extra.New = Extra.Old;
        }
        else
            throw new NotImplementedException();

        Type.PropertyChanged += ItemPropertyChanged;
        Data.PropertyChanged += ItemPropertyChanged;
        Extra.PropertyChanged += ItemPropertyChanged;
    }

    public override void UpdateEntity(FundElements elements, int flowid)
    {
        var obj = elements.GetType().GetProperty(Property)?.GetValue(elements);

        if (Data.New is null || Type.New is null) return;

        if (obj is Mutable<ValueWithEnum<TEnum, TValue>> mutable)
            mutable.SetValue(new ValueWithEnum<TEnum, TValue> { Type = Type.New.Value, Value = Data.New, Extra = Extra.New }, flowid);

        //else if (obj is Mutable<T?> mutable2)
        //    mutable.SetValue(new ValueWithEnum<TEnum, TValue> { Type = Type.New, Value = Data.New, Extra = Extra.New }, flowid);

        else throw new NotImplementedException();
    }

    public override void RemoveValue(FundElements elements, int flowid)
    {
        var obj = elements.GetType().GetProperty(Property)?.GetValue(elements);

        if (Data.New is null) return;

        if (obj is Mutable<ValueWithEnum<TEnum, TValue>> mutable)
            mutable.RemoveValue(flowid);


        Type.New = null;
        Data.New = null;
        Extra.New = null;
    }


    public override void Init(FundElements elements, int flowid)
    {
        throw new NotImplementedException();
    }

    public override void ApplyOverride()
    {
        Type.Apply();
        Data.Apply();
        Extra.Apply();
    }


    protected override string? DisplayOverride()
    {
        return Type.ToString() == "Other" ? Extra.Old : Type.Old is null ? null : $"{EnumDescriptionTypeConverter.GetEnumDescription(Type.Old)} {Data.Old}";
    }
}



public class ElementWithBooleanViewModel<T> : ElementItemViewModel where T : struct
{
    /// <summary>
    /// 是否采用
    /// </summary>
    public ValueViewModel<bool> IsAdopted { get; } = new();

    public ValueViewModel<T> Data { get; } = new();



    public override bool CanConfirm => (IsAdopted.New.HasValue && IsAdopted.New.Value && Data.IsChanged) || (!(IsAdopted.New ?? false) && (IsAdopted.Old ?? false));

    /// <summary>
    /// 当不采用或者采用且有值时
    /// </summary>
    public override bool CanDelete => !HasUnsavedValue && ((!IsAdopted.New ?? false) || ((IsAdopted.New ?? false) && Data.New.HasValue));


    public override bool HasUnsavedValue => IsAdopted.IsChanged || Data.IsChanged;


    [SetsRequiredMembers]
    public ElementWithBooleanViewModel(FundElements elements, string property, int flowid, string label) //: base(elements, property, flowid, label)
    {
        Property = property;
        var obj = elements.GetType().GetProperty(property)?.GetValue(elements);

        if (obj is Mutable<ValueWithBoolean<T>> mutable)
        {
            Label = label;
            (int fid, ValueWithBoolean<T>? dec) = mutable.GetValue(flowid);

            IsAdopted.Old = fid == -1 ? default : fid == flowid && dec is not null ? dec.IsAdopted : default;
            IsAdopted.New = IsAdopted.Old;

            Data.Old = fid == -1 ? null : fid == flowid && dec is not null ? dec.Value : null;
            Data.New = Data.Old;

        }
        else if (obj is Mutable<ValueWithBoolean<T?>> mutable2)
        {
            Label = label;
            (int fid, ValueWithBoolean<T?>? dec) = mutable2.GetValue(flowid);

            IsAdopted.Old = fid == -1 ? default : fid == flowid && dec is not null ? dec.IsAdopted : default;
            IsAdopted.New = IsAdopted.Old;

            Data.Old = fid == -1 ? null : fid == flowid && dec is not null ? dec.Value : null;
            Data.New = Data.Old;
        }
        else
            throw new NotImplementedException();

        IsAdopted.PropertyChanged += ItemPropertyChanged;
        Data.PropertyChanged += ItemPropertyChanged;
    }

    public override void UpdateEntity(FundElements elements, int flowid)
    {
        var obj = elements.GetType().GetProperty(Property)?.GetValue(elements);

        if (Data.New is null || IsAdopted.New is null) return;

        if (obj is Mutable<ValueWithBoolean<T>> mutable)
            mutable.SetValue(new ValueWithBoolean<T> { IsAdopted = IsAdopted.New.Value, Value = Data.New.Value }, flowid);
        else if (obj is Mutable<ValueWithBoolean<T?>> mutable2)
            mutable2.SetValue(new ValueWithBoolean<T?> { IsAdopted = IsAdopted.New.Value, Value = Data.New.Value }, flowid);
        else throw new NotImplementedException();
    }

    public override void RemoveValue(FundElements elements, int flowid)
    {
        var obj = elements.GetType().GetProperty(Property)?.GetValue(elements);

        if (Data.New is null) return;

        if (obj is Mutable<ValueWithBoolean<T>> mutable)
            mutable.RemoveValue(flowid);


        Data.New = null;
        IsAdopted.New = null;
    }


    public override void Init(FundElements elements, int flowid)
    {
        throw new NotImplementedException();
    }

    public override void ApplyOverride()
    {
        IsAdopted.Apply();
        Data.Apply();

    }

}


public class ElementRefrenceWithBooleanViewModel<T> : ElementItemViewModel where T : class
{
    /// <summary>
    /// 是否采用
    /// </summary>
    public ValueViewModel<bool> IsAdopted { get; } = new();

    public RefrenceViewModel<T> Data { get; } = new();



    public override bool CanConfirm => (IsAdopted.Old is null && (!IsAdopted.New??false)) || (IsAdopted.New.HasValue && IsAdopted.New.Value && Data.IsChanged) || ((IsAdopted.New ?? false) && (IsAdopted.Old ?? true));

    /// <summary>
    /// 当不采用或者采用且有值时
    /// </summary>
    public override bool CanDelete => !HasUnsavedValue && IsAdopted.New is not null && ((!IsAdopted.New ?? false) || ((IsAdopted.New ?? false) && Data.New is not null));


    public override bool HasUnsavedValue => IsAdopted.IsChanged || Data.IsChanged;

    public Func<bool?, T?, string?>? DisplayGenerator { get; set; }

    [SetsRequiredMembers]
    public ElementRefrenceWithBooleanViewModel(FundElements elements, string property, int flowid, string label) //: base(elements, property, flowid, label)
    {
        Property = property;
        var obj = elements.GetType().GetProperty(property)?.GetValue(elements);

        if (obj is Mutable<ValueWithBoolean<T>> mutable)
        {
            Label = label;
            (int fid, ValueWithBoolean<T>? dec) = mutable.GetValue(flowid);

            IsAdopted.Old = fid == -1 ? null : fid == flowid && dec is not null ? dec.IsAdopted : null;
            IsAdopted.New = IsAdopted.Old;

            Data.Old = fid == -1 ? null : fid == flowid && dec is not null ? dec.Value : null;
            Data.New = Data.Old;

        }
        else if (obj is Mutable<ValueWithBoolean<T?>> mutable2)
        {
            Label = label;
            (int fid, ValueWithBoolean<T?>? dec) = mutable2.GetValue(flowid);

            IsAdopted.Old = fid == -1 ? null : fid == flowid && dec is not null ? dec.IsAdopted : null;
            IsAdopted.New = IsAdopted.Old;

            Data.Old = fid == -1 ? null : fid == flowid && dec is not null ? dec.Value : null;
            Data.New = Data.Old;
        }
        else
            throw new NotImplementedException();

        IsAdopted.PropertyChanged += ItemPropertyChanged;
        Data.PropertyChanged += ItemPropertyChanged;
    }

    public override void UpdateEntity(FundElements elements, int flowid)
    {
        var obj = elements.GetType().GetProperty(Property)?.GetValue(elements);

        if (Data.New is null || IsAdopted.New is null) return;

        if (obj is Mutable<ValueWithBoolean<T>> mutable)
            mutable.SetValue(new ValueWithBoolean<T> { IsAdopted = IsAdopted.New.Value, Value = Data.New }, flowid);
        else if (obj is Mutable<ValueWithBoolean<T?>> mutable2)
            mutable2.SetValue(new ValueWithBoolean<T?> { IsAdopted = IsAdopted.New.Value, Value = Data.New }, flowid);
        else throw new NotImplementedException();
    }

    public override void RemoveValue(FundElements elements, int flowid)
    {
        var obj = elements.GetType().GetProperty(Property)?.GetValue(elements);

        if (Data.New is null) return;

        if (obj is Mutable<ValueWithBoolean<T>> mutable)
            mutable.RemoveValue(flowid);


        Data.New = null;
        IsAdopted.New = null;
    }


    public override void Init(FundElements elements, int flowid)
    {
        throw new NotImplementedException();
    }

    public override void ApplyOverride()
    {
        IsAdopted.Apply();
        Data.Apply();

    }


    protected override string? DisplayOverride()
    {
        return DisplayGenerator is not null ? DisplayGenerator(IsAdopted.Old, Data.Old) : Data.Old?.ToString();
    }
}

/// <summary>
/// 带保底的费用
/// </summary>
/// <typeparam name="TEnum"></typeparam>
/// <typeparam name="T1"></typeparam>
/// <typeparam name="T2"></typeparam>
public class ElementItemWithEnumViewModel<TEnum, T1, T2> : ElementItemViewModel where TEnum : struct, Enum where T1 : struct where T2 : struct
{
    public ValueViewModel<TEnum> Type { get; } = new();

    public ValueViewModel<T1> Data { get; } = new();

    public RefrenceViewModel<string> Extra { get; } = new();


    /// <summary>
    /// 是否采用
    /// </summary>
    public ValueViewModel<bool> IsAdopted { get; } = new();

    public ValueViewModel<T2> Data2 { get; } = new();



    public override bool CanConfirm => (Type.IsChanged && Type.ToString() != "Other" ? Data.IsSetted : Extra.IsSetted) || (Type.IsSetted && Data.IsSetted && ((IsAdopted.IsChanged && !(IsAdopted.New ?? false)) || ((IsAdopted.New ?? false) && Data2.IsSetted)));

    public override bool CanDelete => IsNotEmpty() && !HasUnsavedValue && (Data.New is not null || Type.New is not null);


    public override bool HasUnsavedValue => Type.IsChanged || Data.IsChanged || Extra.IsChanged;

    public string Property2 { get; }


    public Func<TEnum, T1?, string?, bool, T2?, string?>? DisplayGenerator { get; set; }


    [SetsRequiredMembers]
    public ElementItemWithEnumViewModel(FundElements elements, string property, string property2, int flowid, string label)
    {
        Property = property;
        Property2 = property2;
        var obj = elements.GetType().GetProperty(property)?.GetValue(elements);

        if (obj is Mutable<ValueWithEnum<TEnum, T1>> mutable)
        {
            Label = label;
            (int fid, ValueWithEnum<TEnum, T1>? dec) = mutable.GetValue(flowid);

            Type.Old = fid == -1 ? null : fid == flowid && dec is not null ? dec.Type : null;
            Type.New = Type.Old;

            Data.Old = fid == -1 ? null : fid == flowid && dec is not null ? dec.Value : null;
            Data.New = Data.Old;

            Extra.Old = fid == -1 ? null : fid == flowid && dec is not null ? dec.Extra : null;
            Extra.New = Extra.Old;

        }
        else if (obj is Mutable<ValueWithEnum<TEnum, T1?>> mutable2)
        {
            Label = label;
            (int fid, ValueWithEnum<TEnum, T1?>? dec) = mutable2.GetValue(flowid);

            Type.Old = fid == -1 ? default : fid == flowid && dec is not null ? dec.Type : default;
            Type.New = Type.Old;

            Data.Old = fid == -1 ? default : fid == flowid && dec is not null ? dec.Value : default;
            Data.New = Data.Old;

            Extra.Old = fid == -1 ? default : fid == flowid && dec is not null ? dec.Extra : default;
            Extra.New = Extra.Old;
        }
        else
            throw new NotImplementedException();

        obj = elements.GetType().GetProperty(property2)?.GetValue(elements);

        if (obj is Mutable<ValueWithBoolean<T2>> mutable3)
        {
            Label = label;
            (int fid, ValueWithBoolean<T2>? dec) = mutable3.GetValue(flowid);

            IsAdopted.Old = fid == -1 ? default : fid == flowid && dec is not null ? dec.IsAdopted : default;
            IsAdopted.New = IsAdopted.Old;

            Data2.Old = fid == -1 ? null : fid == flowid && dec is not null ? dec.Value : null;
            Data2.New = Data2.Old;

        }
        else if (obj is Mutable<ValueWithBoolean<T2?>> mutable4)
        {
            Label = label;
            (int fid, ValueWithBoolean<T2?>? dec) = mutable4.GetValue(flowid);

            IsAdopted.Old = fid == -1 ? default : fid == flowid && dec is not null ? dec.IsAdopted : default;
            IsAdopted.New = IsAdopted.Old;

            Data2.Old = fid == -1 ? null : fid == flowid && dec is not null ? dec.Value : null;
            Data2.New = Data2.Old;
        }
        else
            throw new NotImplementedException();



        Type.PropertyChanged += ItemPropertyChanged;
        Data.PropertyChanged += ItemPropertyChanged;
        Extra.PropertyChanged += ItemPropertyChanged;
        IsAdopted.PropertyChanged += IsAdopted_PropertyChanged; ;
        Data2.PropertyChanged += ItemPropertyChanged;
    }

    private void IsAdopted_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        Data2.New = null;
        ItemPropertyChanged(sender, e);
    }

    public override void UpdateEntity(FundElements elements, int flowid)
    {
        var obj = elements.GetType().GetProperty(Property)?.GetValue(elements);

        if (Data.New is null || Type.New is null) return;

        if (obj is Mutable<ValueWithEnum<TEnum, T1>> mutable)
            mutable.SetValue(new ValueWithEnum<TEnum, T1> { Type = Type.New.Value, Value = Data.New.Value, Extra = Extra.New }, flowid);

        //else if (obj is Mutable<T?> mutable2)
        //    mutable.SetValue(new ValueWithEnum<TEnum, TValue> { Type = Type.New, Value = Data.New, Extra = Extra.New }, flowid);

        else throw new NotImplementedException();

        if (!(IsAdopted.New ?? false) && Data2.IsSetted)
        {
            obj = elements.GetType().GetProperty(Property2)?.GetValue(elements);
            if (obj is Mutable<ValueWithBoolean<T2>> mutable2)
                mutable2.SetValue(new ValueWithBoolean<T2> { IsAdopted = IsAdopted.New ?? false, Value = Data2.New ?? default }, flowid);
        }
    }

    public override void RemoveValue(FundElements elements, int flowid)
    {
        var obj = elements.GetType().GetProperty(Property)?.GetValue(elements);

        if (obj is Mutable<ValueWithEnum<TEnum, T1>> mutable)
            mutable.RemoveValue(flowid);


        Type.New = null;
        Data.New = null;
        Extra.New = null;
        IsAdopted.New = false;
        Data2.New = null;
    }


    public override void Init(FundElements elements, int flowid)
    {
        throw new NotImplementedException();
    }

    public override void ApplyOverride()
    {
        Type.Apply();
        Data.Apply();
        Extra.Apply();
        IsAdopted.Apply();
        Data2.Apply();
    }

    private bool IsNotEmpty() => Type.IsSetted || Data.IsSetted || IsAdopted.IsSetted || Data2.IsSetted || Extra.IsSetted;


    protected override string? DisplayOverride()
    {
        if (Type.Old is null) return null;
        if (DisplayGenerator is null) return null;

        return DisplayGenerator(Type.Old.Value, Data.Old, Extra.Old, IsAdopted.Old ?? false, Data2.Old);
    }
}



public class InvestmentManagerViewModel
{

}



public class PortionElementItemWithEnumViewModel<TEnum, TValue> : ElementItemViewModel where TEnum : struct, Enum where TValue : struct
{
    public ValueViewModel<TEnum> Type { get; } = new();

    public ValueViewModel<TValue> Data { get; } = new();

    public RefrenceViewModel<string> Extra { get; } = new();



    public override bool CanConfirm => Data.IsChanged;

    public override bool CanDelete => !HasUnsavedValue && (Type.New is not null || Data.New is not null);


    public override bool HasUnsavedValue => Type.IsChanged || Data.IsChanged || Extra.IsChanged;

    public string ShareName { get; }

    public int ShareId { get; set; }

    public Func<TEnum, TValue?, string?, string?>? DisplayGenerator { get; set; }


    [SetsRequiredMembers]
    public PortionElementItemWithEnumViewModel(FundElements elements, int shareId, string share, string property, int flowid, string label) //: base(elements, property, flowid, label)
    {
        Property = property;
        var obj = elements.GetType().GetProperty(property)?.GetValue(elements);

        if (obj is PortionMutable<ValueWithEnum<TEnum, TValue>> mutable)
        {
            Label = label;
            (int fid, ValueWithEnum<TEnum, TValue>? dec) = mutable.GetValue(shareId, flowid);

            Type.Old = fid == -1 ?  null: fid == flowid && dec is not null ? dec.Type : null;
            Type.New = Type.Old;

            Data.Old = fid == -1 ? null : fid == flowid && dec is not null ? dec.Value : null;
            Data.New = Data.Old;

            Extra.Old = fid == -1 ? null : fid == flowid && dec is not null ? dec.Extra : null;
            Extra.New = Extra.Old;

        }
        else if (obj is PortionMutable<ValueWithEnum<TEnum, TValue>> mutable2)
        {
            Label = label;
            (int fid, ValueWithEnum<TEnum, TValue>? dec) = mutable2.GetValue(shareId, flowid);

            Type.Old = fid == -1 ? null : fid == flowid && dec is not null ? dec.Type : null;
            Type.New = Type.Old;

            Data.Old = fid == -1 ? null : fid == flowid && dec is not null ? dec.Value : null;
            Data.New = Data.Old;

            Extra.Old = fid == -1 ? null : fid == flowid && dec is not null ? dec.Extra : null;
            Extra.New = Extra.Old;
        }
        else
            throw new NotImplementedException();

        Type.PropertyChanged += ItemPropertyChanged;
        Data.PropertyChanged += ItemPropertyChanged;
        Extra.PropertyChanged += ItemPropertyChanged;
        ShareId = shareId;
        ShareName = share;
    }


    /*[SetsRequiredMembers]
    public PortionElementItemWithEnumViewModel(FundElements elements, int shareId, string property, int flowid, string label) //: base(elements, property, flowid, label)
    {
        Property = property;
        var obj = elements.GetType().GetProperty(property)?.GetValue(elements) as dynamic;

        if (obj is Mutable<ValueWithEnum<TEnum, TValue>> mutable)
        {
            Label = label;
            (int fid, ValueWithEnum<TEnum, TValue>? dec) = mutable.GetValue(flowid);

            Type.Old = fid == -1 ? default : fid == flowid && dec is not null ? dec.Type : default;
            Type.New = Type.Old;

            Data.Old = fid == -1 ? default : fid == flowid && dec is not null ? dec.Value : default;
            Data.New = Data.Old;

            Extra.Old = fid == -1 ? default : fid == flowid && dec is not null ? dec.Extra : default;
            Extra.New = Extra.Old;

        }
        else if (obj is Mutable<ValueWithEnum<TEnum, TValue?>> mutable2)
        {
            Label = label;
            (int fid, ValueWithEnum<TEnum, TValue?>? dec) = mutable2.GetValue(flowid);

            Type.Old = fid == -1 ? default : fid == flowid && dec is not null ? dec.Type : default;
            Type.New = Type.Old;

            Data.Old = fid == -1 ? default : fid == flowid && dec is not null ? dec.Value : default;
            Data.New = Data.Old;

            Extra.Old = fid == -1 ? default : fid == flowid && dec is not null ? dec.Extra : default;
            Extra.New = Extra.Old;
        }
        else
            throw new NotImplementedException();

        Type.PropertyChanged += ItemPropertyChanged;
        Data.PropertyChanged += ItemPropertyChanged;
        Extra.PropertyChanged += ItemPropertyChanged;
        ShareId = FundElements.SingleShareKey;
    }*/

    public override void UpdateEntity(FundElements elements, int flowid)
    {
        var obj = elements.GetType().GetProperty(Property)?.GetValue(elements);

        if (Data.New is null || Type.New is null) return;

        if (obj is PortionMutable<ValueWithEnum<TEnum, TValue>> mutable)
            mutable.SetValue(ShareId, new ValueWithEnum<TEnum, TValue> { Type = Type.New.Value, Value = Data.New.Value, Extra = Extra.New }, flowid);

        // else if (obj is PortionMutable<ValueWithEnum<TEnum, TValue>?> mutable2)
        //     mutable2.SetValue(Share, new ValueWithEnum<TEnum, TValue> { Type = Type.New.Value, Value = Data.New.Value, Extra = Extra.New }, flowid);

        else throw new NotImplementedException();
    }

    public override void RemoveValue(FundElements elements, int flowid)
    {
        var obj = elements.GetType().GetProperty(Property)?.GetValue(elements);

        if (Data.New is null) return;

        if (obj is PortionMutable<ValueWithEnum<TEnum, TValue>> mutable)
            mutable.RemoveValue(flowid);


        Type.New = null;
        Data.New = null;
        Extra.New = null;
    }

    public override void Init(FundElements elements, int flowid)
    {
        throw new NotImplementedException();
    }

    public override void ApplyOverride()
    {
        Type.Apply();
        Data.Apply();
        Extra.Apply();
    }


    protected override string? DisplayOverride()
    {
        if (DisplayGenerator is null)
            return Type.ToString() == "Other" ? Extra.Old : Type.Old is null ? null : $"{EnumDescriptionTypeConverter.GetEnumDescription(Type.Old)} {Data.Old}";

        return Type.Old is null ? null : DisplayGenerator(Type.Old.Value, Data.Old, Extra.Old);
    }
}
