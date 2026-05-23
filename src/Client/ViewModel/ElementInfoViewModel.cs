using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FMO.Models;
using FMO.Shared;
using System.Collections.ObjectModel;
using System.Text;
using System.Text.RegularExpressions;

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
public partial class SealingInfoViewModel : ObservableObject, IViewModel<SealingRule?, SealingInfoViewModel>, IDisplay<string>, IDataValidation
{
    public bool IsValid()
    {
        return Type switch { SealingType.Has => Month > 0, SealingType.Other => Extra?.Length > 0, null => false, _ => true };
    }

    public string Transfrom()
    {
        return Type switch { SealingType.Has => $"{Month}个月", SealingType.No => "无", _ => Extra ?? "未设置" };
    }
}

[AutoChangeableViewModel(typeof(FundInvestmentManager))]
public partial class InvestmentManagerInfoViewModel;



[AutoChangeableViewModel(typeof(BankAccount))]
public partial class BankAccountInfoViewModel : IDataValidation, IViewModel<BankAccount>
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
}

public partial class BankChangeableViewModel<T> : ChangeableViewModel<T, BankAccountInfoViewModel>
{
    //public override bool CanConfirm => base.CanConfirm && (NewValue?.IsValid() ?? false);
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


    public PartFeeViewModel(PartRedemptionFee obj)
    {
        Month = obj.Month;
        Include = obj.Include;
        Fee = obj.Fee;
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


public partial class TemporarilyOpenInfoViewModel : IDataValidation, IViewModel<TemporarilyOpenInfo?, TemporarilyOpenInfoViewModel>
{
    public bool IsValid() => !IsAllowed || (AllowPurchase || AllowRedemption);

    public override string ToString() => !IsAllowed ? "不允许临开" : (IsLimited ? "仅合同变更、法规变更时，" : "") + $"允许{(AllowPurchase ? "申购" : "")}{(AllowRedemption ? "赎回" : "")}";
}

[AutoChangeableViewModel(typeof(PerformanceBenchmark))]
public partial class PerformanceBenchmarkViewModel
{
}


//[ForceNull(nameof(FundPurchaseRule.MinDeposit))]
public partial class FundPurchaseRuleViewModel : ObservableObject, IDataValidation, IViewModel<FundPurchaseRule?, FundPurchaseRuleViewModel>, IDisplay<string>
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

    public string Transfrom()
    {
        var a = MinDeposit is null ? null : $"{MinDeposit / 10000}万起投" + (AdditionalDeposit > 0 ? $"，追加{AdditionalDeposit / 10000}万起" : "") + (HasRequirement ? Statement : "");
        var b = HasFee ? $"   " + PayMethod switch { FundFeePayType.Out => "价外收取", FundFeePayType.Extra => "额外收取", FundFeePayType.Other => PayOther, _ => "" } + Type switch { FundFeeType.Ratio => $"{Fee}%", FundFeeType.Fix => $"{Fee}元", FundFeeType.Other => Other, _ => "未知费用" } : null;
        var c = HasGuaranteedFee ? $"  保底 {GuaranteedFee}元" : null;
        return (a + b + c) switch { null or "" => "未设置", var x => x };
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
    public override string? ToString()
    {
        return IsRequired ? "需要" : "不适用";
    }
}


public partial class FundDurationViewModel : ObservableObject, IViewModel<int?, FundDurationViewModel>, IDisplay<string>
{


    public string Transfrom()
    {
        return Value switch { >= 999 => "无固定期限", var m when m > 0 && m % 12 == 0 => $"{Value / 12}年", > 0 => $"{Value}个月", _ => "未设置" };
    }
}


public partial class FundModeViewModel : ObservableObject, IViewModel<FundModeInfo?, FundModeViewModel>, IDisplay<string>
{


    public string Transfrom()
    {
        return Mode switch { FundMode.Open => "开放式", FundMode.Close => "封闭式", FundMode.Other => Other ?? "未设置", _ => "未设置" };
    }
}

public partial class FundExpireDateViewModel : ObservableObject, IViewModel<DateOnly?, FundExpireDateViewModel>, IDisplay<string>
{
    public string Transfrom() => Value switch { DateOnly d => d > DateOnly.MinValue ? $"{d: yyyy/M/d}" : "未设置", _ => "未设置" };
}


public partial class HugeRedemptionRuleViewModel : IViewModel<HugeRedemptionRule?, HugeRedemptionRuleViewModel>, IDisplay<string>
{
    public string Transfrom() => Has switch
    {
        true when Ratio > 0 => $"{Ratio * 100}%",
        _ => "未设置"
    };
}