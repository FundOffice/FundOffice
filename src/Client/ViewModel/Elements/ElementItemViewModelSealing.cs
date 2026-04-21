using FMO.Models;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace FMO;

public class ElementItemViewModelSealing : ElementItemViewModel
{

    public ValueViewModel<SealingType> Type { get; } = new();

    public ValueViewModel<int> Month { get; } = new();

    public RefrenceViewModel<string> Extra { get; } = new();

    public override void UpdateEntity(FundElements elements, int fid)
    {
        SealingRule? sealingRule = Type.New is null || Month.New is null ? null : new SealingRule { Type = Type.New.Value, Month = Month.New.Value, Extra = Extra.New };
        if (sealingRule is null)
            elements.SealingRule!.RemoveValue(fid);
        else
            elements.SealingRule!.SetValue(sealingRule, fid);
    }

    public override void RemoveValue(FundElements elements, int flowid)
    {
        elements.SealingRule!.RemoveValue(flowid);
        Type.New = null;
        Month.New = null;
        Extra.New = null;
    }

    public override bool CanConfirm => Type.New switch { SealingType.No => Type.New != Type.Old, SealingType.Has => Month.New > 0 && Month.New != Month.Old, SealingType.Other => !string.IsNullOrWhiteSpace(Extra.New) && Extra.New != Extra.Old, _ => false };


    public override bool CanDelete => (Type.IsSetted || Month.IsSetted || Extra.IsSetted) && !CanConfirm;


    public override bool HasUnsavedValue => Type.IsChanged || Month.IsChanged || Extra.IsChanged;


    [SetsRequiredMembers]
    public ElementItemViewModelSealing(FundElements elements, string property, int flowid, string v)
    {
        Property = property;
        var mutable = elements.SealingRule!;//property switch { nameof(FundElements.SealingRule) => elements.SealingRule!, nameof(FundElements.LockingRule) => elements.LockingRule!, _ => throw new Exception() };

        Label = v;// mutable.Description!;
        (int fid, var dec) = mutable.GetValue(flowid);
        Type.Old = fid == flowid ? dec?.Type : fid == -1 ? null : dec?.Type;
        Type.New = fid == flowid ? dec?.Type : fid == -1 ? null : dec?.Type;

        Month.Old = fid == flowid ? dec?.Month ?? 0 : dec?.Month;
        Month.New = Month.Old;

        Extra.Old = fid == flowid ? dec?.Extra : fid == -1 ? null : dec?.Extra;
        Extra.New = fid == flowid ? dec?.Extra : fid == -1 ? null : dec?.Extra;

        Type.PropertyChanged += ItemPropertyChanged;
        Month.PropertyChanged += ItemPropertyChanged;
        Extra.PropertyChanged += ItemPropertyChanged;
    }

    public override void Init(FundElements elements, int flowid)
    {
        (int fid, var obj) = elements.SealingRule!.GetValue(flowid);
        if (obj is null) return;

        Type.Old = obj.Type;
        Type.New = obj.Type;

        Month.Old = obj.Month;
        Month.New = obj.Month;

        Extra.Old = obj.Extra;
        Extra.New = obj.Extra;
    }


    protected override string? DisplayOverride()
    {
        return $"{Type.Old switch { SealingType.No => "无", SealingType.Has => $"{Month.Old}个月", _ => Extra.Old }} ";
    } 
}
