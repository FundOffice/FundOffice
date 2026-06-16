using CommunityToolkit.Mvvm.ComponentModel;
using FMO.Models;

namespace FMO;

public partial class AccountOpeningWorkEventViewModel : WorkEventViewModel
{
    public AccountOpeningWorkEventViewModel()
    {
    }

    public AccountOpeningWorkEventViewModel(AccountOpeningWorkEvent workEvent)
    {
        FillFrom(workEvent);
        AccountType = workEvent.AccountType;
        OpenDate = workEvent.OpenDate;
        Institution = workEvent.Institution;
    }

    [ObservableProperty]
    public partial AccountOpeningType AccountType { get; set; }

    [ObservableProperty]
    public partial DateTime? OpenDate { get; set; }

    [ObservableProperty]
    public partial string? Institution { get; set; }

    public string AccountTypeDisplay => AccountType switch
    {
        AccountOpeningType.Securities => "证券",
        AccountOpeningType.Futures => "期货",
        AccountOpeningType.Bank => "银行",
        AccountOpeningType.Other => "其他",
        _ => AccountType.ToString(),
    };

    public override WorkEvent Build()
    {
        var e = new AccountOpeningWorkEvent();
        CopyTo(e);
        e.AccountType = AccountType;
        e.OpenDate = OpenDate;
        e.Institution = Institution;
        return e;
    }
}
