using FMO.Models;

namespace FMO;

public partial class AccountOtherWorkEventViewModel : WorkEventViewModel
{
    public AccountOtherWorkEventViewModel()
    {
    }

    public AccountOtherWorkEventViewModel(AccountOtherWorkEvent workEvent)
    {
        FillFrom(workEvent);
    }

    public override WorkEvent Build()
    {
        var e = new AccountOtherWorkEvent();
        CopyTo(e);
        return e;
    }
}