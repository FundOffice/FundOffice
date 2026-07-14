using CommunityToolkit.Mvvm.ComponentModel;
using FMO.Models;

namespace FMO;

public partial class AccountInfoChangeWorkEventViewModel : WorkEventViewModel
{
    public AccountInfoChangeWorkEventViewModel()
    {
    }

    public AccountInfoChangeWorkEventViewModel(AccountInfoChangeWorkEvent workEvent)
    {
        FillFrom(workEvent);
        ChangeType = workEvent.ChangeType;
        OriginalValue = workEvent.OriginalValue;
        NewValue = workEvent.NewValue;
    }

    [ObservableProperty]
    public partial AccountInfoChangeType ChangeType { get; set; }

    [ObservableProperty]
    public partial string? OriginalValue { get; set; }

    [ObservableProperty]
    public partial string? NewValue { get; set; }

    public string ChangeTypeDisplay => ChangeType switch
    {
        AccountInfoChangeType.Name => "名称",
        AccountInfoChangeType.Address => "地址",
        AccountInfoChangeType.Contact => "联系方式",
        AccountInfoChangeType.BankInfo => "银行信息",
        AccountInfoChangeType.Other => "其他",
        _ => ChangeType.ToString(),
    };

    public override WorkEvent Build()
    {
        var e = new AccountInfoChangeWorkEvent();
        CopyTo(e);
        e.ChangeType = ChangeType;
        e.OriginalValue = OriginalValue;
        e.NewValue = NewValue;
        return e;
    }
}
