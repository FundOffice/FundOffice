using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FMO.Models;
using FMO.Shared;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;

namespace FMO;

public partial class ManageFeeInfoViewModel : ObservableObject, IEquatable<ManageFeeInfoViewModel>
{
    [ObservableProperty]
    public partial FundFeeType? Type { get; set; }

    [ObservableProperty]
    public partial decimal? Value { get; set; }

    [ObservableProperty]
    public partial string? Other { get; set; }


    public ManageFeeInfoViewModel() { }

    public ManageFeeInfoViewModel(FundFeeInfo? info)
    {
        Type = info?.Type;
        Value = info?.Fee;
        Other = info?.Other;
    }

    public bool Equals(ManageFeeInfoViewModel? other)
    {
        return Type == other?.Type && Value == other?.Value;
    }

    internal FundFeeInfo Build()
    {
        return new FundFeeInfo { Fee = Value ?? default, Type = Type ?? default };
    }
}

public partial class DataExtraViewModel<T> : ObservableObject, IEquatable<DataExtraViewModel<T>> where T : struct
{
    [ObservableProperty]
    public partial T? Data { get; set; }


    [ObservableProperty]
    public partial string? Other { get; set; }


    public DataExtraViewModel() { }

    public DataExtraViewModel(DataExtra<T>? info)
    {
        Data = info?.Data;
        Other = info?.Extra;
    }

    public bool Equals(DataExtraViewModel<T>? other)
    {
        return EqualityComparer<T?>.Default.Equals(Data, other?.Data) && Other == other?.Other;
    }

    internal DataExtra<T> Build()
    {
        return new DataExtra<T> { Data = Data, Extra = Other };
    }
}




[ForceNull(nameof(SealingRule.Type))]
public partial class SealingInfoViewModel : ObservableObject, IViewModel<SealingRule?, SealingInfoViewModel>, IDataValidation
{
    public bool IsValid()
    {
        return Type switch { SealingType.Has => Month > 0, SealingType.Other => Extra?.Length > 0, null => false, _ => true };
    }


}

[AutoChangeableViewModel(typeof(FundInvestmentManager))]
public partial class InvestmentManagerInfoViewModel;



public partial class BankAccountInfoViewModel : ObservableObject, IDataValidation, IViewModel<BankAccount?, BankAccountInfoViewModel>
{

    private string? _Deposit;

    public string? Deposit
    {
        get { if (string.IsNullOrWhiteSpace(_Deposit)) _Deposit = BankOfDeposit; return _Deposit; }
        set { _Deposit = value; BankOfDeposit = value; SetDeposit(value); }
    }

    private void SetDeposit(string? str)
    {
        if (string.IsNullOrWhiteSpace(str)) return;
        var m = Regex.Match(str, @"(\w+银行)(?:.*公司)?(\w+)?");
        if (!m.Success) return;

        Bank = m.Groups[1].Value;
        if (m.Groups.Count > 2)
            Branch = m.Groups[2].Value;
    }

    public bool IsValid() => Bank?.Length > 3 && Name?.Length > 1 && Number?.Length > 5;

    public override string ToString()
    {
        StringBuilder builder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(Name))
            builder.Append($"户名：{Name}\n");

        if (!string.IsNullOrWhiteSpace(Number))
            builder.Append($"账号：{Number}\n");

        if (!string.IsNullOrWhiteSpace(BankOfDeposit))
            builder.Append($"开户行：{BankOfDeposit}\n");

        if (!string.IsNullOrWhiteSpace(LargePayNo))
            builder.Append($"大额支付号：{LargePayNo}\n");

        if (!string.IsNullOrWhiteSpace(SwiftCode))
            builder.Append($"SWIFT：{SwiftCode}\n");

        return builder.ToString();
    }

    [RelayCommand]
    public void CopyAll()
    {
        Clipboard.SetText(ToString());
    }
}



public partial class FundFeeInfoViewModel : IDataValidation, IViewModel<FundFeeInfo?, FundFeeInfoViewModel>
{
    public bool IsValid() => Type switch { FundFeeType.Ratio or FundFeeType.Fix => Fee > 0, FundFeeType.Other => Other?.Length > 0, _ => false };

    public override string ToString()
    {
        return !HasFee ? "无" : Type switch { FundFeeType.Fix => $"固定费用：{Fee}元 / 年", FundFeeType.Ratio => $"{Fee}% / 年", FundFeeType.Other => Other, _ => $"未设置" } + (GuaranteedFee > 0 ? $" 有保底：{GuaranteedFee} / 年" : "");
    }
}

public partial class RedemptionFeeInfoViewMdoel : ObservableObject, IDataValidation, IViewModel<RedemptionFeeInfo?, RedemptionFeeInfoViewMdoel>
{
    public RedemptionFeeInfoViewMdoel()
    {
        Parts = new();
    }

    public RedemptionFeeInfoViewMdoel(RedemptionFeeInfo? fee)
    {
        Type = fee?.Type;
        HasFee = fee?.HasFee ?? false;
        Fee = fee?.Fee;
        Other = fee?.Other;
        Parts = fee?.Parts is null ? new() : new(fee.Parts.Select(x => new PartFeeViewModel(x)));
        if (Parts.Count > 0)
            Parts[^1].IsLast = true;
    }

    [ObservableProperty]
    public partial FundFeeType? Type { get; set; }


    [ObservableProperty]
    public partial bool HasFee { get; set; }

    [ObservableProperty]
    public partial decimal? Fee { get; set; }

    /// <summary>
    /// 特殊类型
    /// </summary>
    [ObservableProperty]
    public partial string? Other { get; set; }

    public ObservableCollection<PartFeeViewModel> Parts { get; private set; }

    public RedemptionFeeInfo Build()
    {
        return new RedemptionFeeInfo
        {
            Fee = Fee ?? default,
            Other = Other,
            Type = Type ?? default,
            HasFee = HasFee,
            Parts = Parts.Select(x => new PartRedemptionFee { Fee = x.Fee ?? 0, Include = x.Include, Month = x.Month ?? 0 }).ToList()
        };
    }

    public bool Equals(RedemptionFeeInfoViewMdoel? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return EqualityComparer<decimal?>.Default.Equals(Fee, other.Fee)
            && EqualityComparer<FundFeeType?>.Default.Equals(Type, other.Type)
            && EqualityComparer<bool>.Default.Equals(HasFee, other.HasFee)
            && EqualityComparer<string?>.Default.Equals(Other, other.Other)
            && Parts.SequenceEqual(other.Parts);
    }

    public bool IsValid() => Type switch { FundFeeType.ByTime => Parts?.Count > 1, _ => true };

    public override string? ToString()
    {
        return !HasFee ? "-" : Type switch
        {
            FundFeeType.Fix => $"固定费用：{Fee}元 / 年",
            FundFeeType.Ratio => $"{Fee}% / 年",
            FundFeeType.ByTime => $"持有时间T：" + FeeByTimeString(),
            FundFeeType.Other => Other,
            _ => $"未设置"
        };
    }


    private string FeeByTimeString()
    {
        string s = "";
        for (int i = 0; i < Parts.Count; i++)
        {
            var p = Parts[i];
            if (i == 0)
                s += $"T{(!p.Include ? '<' : '≤')}{p.Month}月, {p.Fee}%";
            else if (i == Parts.Count - 1)
                s += $"；T{(Parts[i - 1].Include ? '>' : '≥')}{Parts[i - 1].Month}月, {p.Fee}%";
            else s += $"；{Parts[i - 1].Month}月{(Parts[i - 1].Include ? '<' : '≤')}T{(!p.Include ? '<' : '≤')}{p.Month}月, {p.Fee}%";
        }
        return s;
    }


    [RelayCommand]
    public void AddPart()
    {
        Parts.Add(new());
        if (Parts.Count == 1) Parts.Add(new() { IsLast = true });

        foreach (var item in Parts.SkipLast(1))
            item.IsLast = false;
        Parts[^1].IsLast = true;
        OnPropertyChanged(nameof(Parts));
    }

    [RelayCommand]
    public void DeletePart(PartFeeViewModel obj)
    {
        Parts.Remove(obj);
        Parts[^1].IsLast = true;
        OnPropertyChanged(nameof(Parts));
    }



    public bool Equals(global::FMO.Models.RedemptionFeeInfo? other)
    {
        if (other is null)
        {
            return EqualityComparer<global::FMO.Models.FundFeeType?>.Default.Equals(Type, default) &&
                   EqualityComparer<bool>.Default.Equals(HasFee, default) &&
                   EqualityComparer<decimal?>.Default.Equals(Fee, default) &&
                   EqualityComparer<string>.Default.Equals(Other, default) &&
                   (Parts is null || !Parts.Any());
        }
        if (!EqualityComparer<global::FMO.Models.FundFeeType?>.Default.Equals(Type, other.Type)) return false;
        if (!EqualityComparer<bool>.Default.Equals(HasFee, other.HasFee)) return false;
        if (!EqualityComparer<decimal?>.Default.Equals(Fee, other.Fee)) return false;
        if (!EqualityComparer<string>.Default.Equals(Other, other.Other)) return false;
        if ((Parts?.Count ?? 0) != (other?.Parts?.Count ?? 0)) return false;

        if (Parts?.Count > 0)
            for (int i = 0; i < Parts.Count; i++)
            {
                var a = Parts[i];
                var b = other!.Parts![i];

                if (!a.Equals(b)) return false;
            }


        return true;
    }




    public RedemptionFeeInfoViewMdoel FillBy(global::FMO.Models.RedemptionFeeInfo? obj)
    {
        if (obj is null)
        {
            Type = default;
            HasFee = default;
            Fee = default;
            Other = default;
            Parts = [];
            return this;
        }
        Type = obj.Type;
        HasFee = obj.HasFee;
        Fee = obj.Fee;
        Other = obj.Other;
        Parts = obj?.Parts is null ? new() : new(obj.Parts.Select(x => new PartFeeViewModel(x)));
        if (Parts.Count > 0)
            Parts[^1].IsLast = true;
        return this;
    }
}


public partial class PartFeeViewModel : ObservableObject, IViewModel<PartRedemptionFee, PartFeeViewModel>
{


    public PartFeeViewModel(PartRedemptionFee? obj)
    {
        Month = obj?.Month;
        Include = obj?.Include ?? false;
        Fee = obj?.Fee;
    }



    [ObservableProperty]
    public partial bool IsLast { get; set; }

    public bool Equals(PartFeeViewModel? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Month == other.Month && Include == other.Include && Fee == other.Fee;
    }


    public bool Equals(PartRedemptionFee? other)
    {
        if (other is null)
            return Month is null or 0 && !Include && Fee is null or 0;

        if ((Month ?? 0) != (other.Month ?? 0)) return false;
        if (Include != other.Include) return false;
        if ((Fee ?? 0) != (other.Fee ?? 0)) return false;
        return true;
    }
}


[AutoChangeableViewModel(typeof(SealingRule))]
public partial class LockingRuleViewModel;


public partial class AgencyInfoViewModel : IDataValidation, IViewModel<AgencyInfo?, AgencyInfoViewModel>
{
    public bool IsValid() => !HasAgency || !string.IsNullOrWhiteSpace(Name);

    public override string ToString() => HasAgency switch { true when !string.IsNullOrWhiteSpace(Name) => Name!, false => "-", _ => "未设置" };
}


public partial class TemporarilyOpenInfoViewModel : ObservableObject, IDataValidation, IViewModel<TemporarilyOpenInfo?, TemporarilyOpenInfoViewModel>
{
    public bool IsValid() => !IsAllowed || (AllowPurchase || AllowRedemption);

    public override string ToString() => !IsAllowed ? "不允许临开" : (IsLimited ? "仅合同变更、法规变更时，" : "") + $"允许{(AllowPurchase ? "申购" : "")}{(AllowRedemption ? "赎回" : "")}";

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.PropertyName == nameof(IsLimited))
            AllowPurchase = false;
    }
}


public partial class PerformanceBenchmarkViewModel : IViewModel<PerformanceBenchmark?, PerformanceBenchmarkViewModel>, IDataValidation
{
    public bool IsValid() => !Has || Benchmark?.Length > 2;

}


//[ForceNull(nameof(FundPurchaseRule.MinDeposit))]
public partial class FundPurchaseRuleViewModel : ObservableObject, IDataValidation, IViewModel<FundPurchaseRule?, FundPurchaseRuleViewModel>
{
    [ObservableProperty]
    public partial int? MinDeposit { get; set; } = 1000000;

    public string? FeeName { get; set; }

    public bool IsValid()
    {
        if (MinDeposit is null or < 10000) return false;
        if (HasFee && Type is FundFeeType.Ratio or FundFeeType.Fix && Fee <= 0) return false;
        if (Type is FundFeeType.Other && Other?.Length == 0) return false;

        return true;
    }



    [RelayCommand]
    public void SetDefault()
    {
        MinDeposit = 1000000;
    }
}

//[AutoChangeableViewModel(typeof(FeePayInfo))]
public partial class FeePayInfoViewModel : IViewModel<FeePayInfo?, FeePayInfoViewModel>
{
    public override string? ToString()
    {
        return Type switch { FeePayFrequency.Month => "按月支付", FeePayFrequency.Quarter => "按季支付", FeePayFrequency.Other => Other, _ => "未设置" };
    }


}



public partial class CoolingPeriodInfoViewModel : IViewModel<CoolingPeriodInfo?, CoolingPeriodInfoViewModel>
{
    public override string? ToString()
    {
        return Type switch { CoolingPeriodType.OneDay => "24小时", CoolingPeriodType.Other => Other, _ => "未设置" };
    }
}




public partial class CallbackInfoViewModel : IViewModel<CallbackInfo?, CallbackInfoViewModel>
{

    public bool Equals(CallbackInfo? other)
    {
        if (other is null) return false;
        if (IsRequired != other.IsRequired) return false;
        if (!IsRequired) return true; // 不回访时 OnlyAfterMandatory 无意义
        return OnlyAfterMandatory == other.OnlyAfterMandatory;
    }

    public override int GetHashCode()
    {
        if (!IsRequired) return HashCode.Combine(IsRequired);
        return HashCode.Combine(IsRequired, OnlyAfterMandatory);
    }

    public override string ToString() => IsRequired && !OnlyAfterMandatory ? "需要回访" : IsRequired ? "在强制要求前不回访" : "不适用";
}


public partial class FundDurationViewModel : ObservableObject, IViewModel<FundDuration?, FundDurationViewModel>
{



}


public partial class FundModeViewModel : ObservableObject, IViewModel<FundModeInfo?, FundModeViewModel>
{



}

public partial class FundExpireDateViewModel : ObservableObject, IViewModel<DateOnly?, FundExpireDateViewModel>
{
    public override string ToString() => Value switch { DateOnly d => d > DateOnly.MinValue ? $"{d: yyyy/M/d}" : "未设置", _ => "未设置" };
}


public partial class HugeRedemptionRuleViewModel : IViewModel<HugeRedemptionRule?, HugeRedemptionRuleViewModel>
{

}


public partial class FundOpenRuleViewModel : ObservableObject, IViewModel<OpenRule[]?, FundOpenRuleViewModel>
{
    [ObservableProperty]
    public partial ObservableCollection<OpenRule> Rules { get; set; } = [];

    public static OpenRule[]? Trans(FundOpenRuleViewModel vm)
    {
        return vm.Rules.ToArray();
    }

    public static FundOpenRuleViewModel Trans(OpenRule[]? vm)
    {
        return new FundOpenRuleViewModel { Rules = vm is null ? [] : [.. vm.Select(x => (OpenRule)x.Clone())] };
    }

    public bool Equals(OpenRule[]? other)
    {
        if (Rules.Count != (other?.Length ?? 0)) return false;

        if (Rules.Count == 0) return true;

        return Rules.Select(x => x.ToString()).Order().SequenceEqual(other!.Select(x => x.ToString()).Order());

    }


    [RelayCommand]
    public void AddRule()
    {
        Rules.Add(new());
    }


    [RelayCommand]
    public void DeleteRule(OpenRule rule)
    {

        Rules.Remove(rule);
    }

    [RelayCommand]
    public void SetOpenRule(OpenRule rule)
    {
        OpenRuleViewModel openRuleViewModel = new();
        openRuleViewModel.Init(rule);

        var wnd = new OpenRuleEditor
        {
            Height = 930,
            Width = 1200,
            DataContext = openRuleViewModel,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = App.Current.MainWindow
        };
        if (wnd.ShowDialog() is true)
        {
            var id = Rules.IndexOf(rule);
            Rules.RemoveAt(id);
            Rules.Insert(id, openRuleViewModel.Rule);
            //rule.UpdateFrom(openRuleViewModel.Rule);

            OnPropertyChanged(nameof(Rules));
        }
    }
}

public partial class PerformanceFeeTierViewModel : ObservableObject, IViewModel<PerformanceFeeTier, PerformanceFeeTierViewModel>
{
    [ObservableProperty]
    public partial decimal? UpperBound { get; set; }

    [ObservableProperty]
    public partial bool Include { get; set; }

    [ObservableProperty]
    public partial decimal Rate { get; set; }

    [ObservableProperty]
    public partial bool IsLast { get; set; }

    public static PerformanceFeeTier Trans(PerformanceFeeTierViewModel vm)
    {
        return new PerformanceFeeTier
        {
            UpperBound = vm.IsLast ? null : vm.UpperBound,
            Include = vm.IsLast ? false : vm.Include,
            Rate = vm.Rate
        };
    }

    public static PerformanceFeeTierViewModel Trans(PerformanceFeeTier? model)
    {
        return new PerformanceFeeTierViewModel { UpperBound = model?.UpperBound, Include = model?.Include ?? false, Rate = model?.Rate ?? 0m };
    }

    public bool Equals(PerformanceFeeTier? other)
    {
        if (other is null) return false;
        return UpperBound == other.UpperBound && Include == other.Include && Rate == other.Rate;
    }
}



public partial class PerformanceFeeStandardViewModel : ObservableObject, IViewModel<PerformanceFeeStandard?, PerformanceFeeStandardViewModel>, IDataValidation
{
    [ObservableProperty]
    public partial bool Has { get; set; }

    [ObservableProperty]
    public partial PerformanceFeeReturnType ReturnType { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<PerformanceFeeTierViewModel> Tiers { get; set; } = [];

    public static PerformanceFeeReturnType[] ReturnTypes { get; } = Enum.GetValues<PerformanceFeeReturnType>();

    public static PerformanceFeeStandard? Trans(PerformanceFeeStandardViewModel vm)
    {
        if (vm is null) return null;
        return new PerformanceFeeStandard
        {
            Has = vm.Has,
            ReturnType = vm.ReturnType,
            Tiers = vm.Tiers.Count == 0 ? null : [.. vm.Tiers.Select(t => PerformanceFeeTierViewModel.Trans(t))],
        };
    }

    public static PerformanceFeeStandardViewModel Trans(PerformanceFeeStandard? model)
    {
        return new PerformanceFeeStandardViewModel
        {
            Has = model?.Has ?? false,
            ReturnType = model?.ReturnType ?? PerformanceFeeReturnType.Actual,
            Tiers = (model?.Tiers?.Count is null or 0) ? [new PerformanceFeeTierViewModel()] : new(model.Tiers.Select(t => PerformanceFeeTierViewModel.Trans(t))),
        };
    }

    public bool Equals(PerformanceFeeStandard? other)
    {
        if (other is null) return false;// !Has;
        if (Has != other.Has) return false;
        if (ReturnType != other.ReturnType) return false;
        if (Tiers.Count != (other.Tiers?.Count ?? 0)) return false;
        for (int i = 0; i < Tiers.Count; i++)
        {
            var t = Tiers[i];
            var o = other.Tiers![i];
            var isLast = i == Tiers.Count - 1;
            var tUpper = isLast ? null : t.UpperBound;
            var tInclude = isLast ? false : t.Include;
            if (tUpper != o.UpperBound || tInclude != o.Include || t.Rate != o.Rate) return false;
        }
        return true;
    }

    public override string ToString()
    {
        return Trans(this)?.ToString() ?? "不计提";
    }

    [RelayCommand]
    public void AddTier(PerformanceFeeTierViewModel? from)
    {
        if (from is not null && Tiers.IndexOf(from) is int idx && idx >= 0)
            Tiers.Insert(idx + 1, new());
        else
            Tiers.Add(new PerformanceFeeTierViewModel());
    }

    [RelayCommand]
    public void RemoveTier(PerformanceFeeTierViewModel tier)
    {
        if (IsSingleTier)
        {
            tier.UpperBound = null;
            tier.Include = false;
            tier.Rate = 0;
            OnPropertyChanged(nameof(Has));
        }
        else
        {
            Tiers.Remove(tier);
        }
    }

    public bool IsSingleTier => Tiers.Count == 1;

    partial void OnTiersChanged(ObservableCollection<PerformanceFeeTierViewModel> value)
    {
        void AttachTierNotify(PerformanceFeeTierViewModel tier)
        {
            tier.PropertyChanged += (_, _) => OnPropertyChanged(nameof(Has));
        }

        foreach (var tier in value)
            AttachTierNotify(tier);

        value.CollectionChanged += (_, e) =>
        {
            OnPropertyChanged(nameof(IsSingleTier));
            UpdateTierIsLast(value);

            if (e.NewItems is not null)
                foreach (var item in e.NewItems)
                {
                    if (item is PerformanceFeeTierViewModel tier)
                        AttachTierNotify(tier);
                }
        };
        OnPropertyChanged(nameof(IsSingleTier));
        UpdateTierIsLast(value);
    }

    private static void UpdateTierIsLast(ObservableCollection<PerformanceFeeTierViewModel> tiers)
    {
        for (int i = 0; i < tiers.Count; i++)
            tiers[i].IsLast = i == tiers.Count - 1;
    }

    public bool IsValid()
    {
        if (!Has) return true;

        if (Tiers.Count < 1) return false;

        if (Tiers.Count == 1 && Tiers[0].Rate == 0) return false;

        if (Tiers.SkipLast(1).Any(x => x.UpperBound is not > 0)) return false;

        return true;
    }
}


public partial class PerformanceFeeRuleViewModel : ObservableObject, IViewModel<PerformanceFeeRule?, PerformanceFeeRuleViewModel>, IDataValidation
{
    [ObservableProperty]
    public partial PerformanceFeeMethod Method { get; set; } = PerformanceFeeMethod.HighWaterMark;

    [ObservableProperty]
    public partial PerformanceFeeDeductionType DeductionType { get; set; }

    [ObservableProperty]
    public partial bool TriggerRedemption { get; set; } = true;
    [ObservableProperty]
    public partial bool TriggerDistribution { get; set; } = true;
    [ObservableProperty]
    public partial bool TriggerLiquidation { get; set; } = true;
    [ObservableProperty]
    public partial bool TriggerOpenDay { get; set; }

    [ObservableProperty]
    public partial string? SpecialMethod { get; set; }

    [ObservableProperty]
    public partial string? Remark { get; set; }

    public static PerformanceFeeMethod[] Methods { get; } = Enum.GetValues<PerformanceFeeMethod>();
    public static PerformanceFeeDeductionType[] DeductionTypes { get; } = Enum.GetValues<PerformanceFeeDeductionType>();

    public static PerformanceFeeRule? Trans(PerformanceFeeRuleViewModel vm)
    {
        if (vm is null) return null;
        var trigger = PerformanceFeeTrigger.None;
        if (vm.TriggerRedemption) trigger |= PerformanceFeeTrigger.Redemption;
        if (vm.TriggerDistribution) trigger |= PerformanceFeeTrigger.Distribution;
        if (vm.TriggerLiquidation) trigger |= PerformanceFeeTrigger.Liquidation;
        if (vm.TriggerOpenDay) trigger |= PerformanceFeeTrigger.OpenDay;
        return new PerformanceFeeRule
        {
            Method = vm.Method,
            DeductionType = vm.DeductionType,
            Trigger = trigger,
            SpecialMethod = vm.SpecialMethod,
            Remark = vm.Remark,
        };
    }

    public static PerformanceFeeRuleViewModel Trans(PerformanceFeeRule? model)
    {
        return new PerformanceFeeRuleViewModel
        {
            Method = model?.Method ?? PerformanceFeeMethod.HighWaterMarkPerInvestor,
            DeductionType = model?.DeductionType ?? PerformanceFeeDeductionType.NavDeduction,
            TriggerRedemption = model?.Trigger.HasFlag(PerformanceFeeTrigger.Redemption) ?? true,
            TriggerDistribution = model?.Trigger.HasFlag(PerformanceFeeTrigger.Distribution) ?? true,
            TriggerLiquidation = model?.Trigger.HasFlag(PerformanceFeeTrigger.Liquidation) ?? true,
            TriggerOpenDay = model?.Trigger.HasFlag(PerformanceFeeTrigger.OpenDay) ?? false,
            SpecialMethod = model?.SpecialMethod,
            Remark = model?.Remark,
        };
    }

    public bool Equals(PerformanceFeeRule? other)
    {
        if (other is null) return false;
        if (Method != other.Method) return false;
        if (DeductionType != other.DeductionType) return false;
        var trigger = PerformanceFeeTrigger.None;
        if (TriggerRedemption) trigger |= PerformanceFeeTrigger.Redemption;
        if (TriggerDistribution) trigger |= PerformanceFeeTrigger.Distribution;
        if (TriggerLiquidation) trigger |= PerformanceFeeTrigger.Liquidation;
        if (TriggerOpenDay) trigger |= PerformanceFeeTrigger.OpenDay;
        if (trigger != other.Trigger) return false;
        if (SpecialMethod != other.SpecialMethod) return false;
        if (Remark != other.Remark) return false;
        return true;
    }

    public override string ToString()
    {
        return Trans(this)?.ToString() ?? "";
    }

    public bool IsValid()
    {
        return Method switch
        {
            PerformanceFeeMethod.Special => !string.IsNullOrWhiteSpace(SpecialMethod),
            _ => true,
        };
    }
}
