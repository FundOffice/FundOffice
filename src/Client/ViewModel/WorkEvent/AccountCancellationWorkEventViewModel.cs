using FMO.Models;

namespace FMO;

public partial class AccountCancellationWorkEventViewModel : WorkEventViewModel
{
    public AccountCancellationWorkEventViewModel()
    {
    }

    public AccountCancellationWorkEventViewModel(AccountCancellationWorkEvent workEvent)
    {
        FillFrom(workEvent);
    }

    public override WorkEvent Build()
    {
        var e = new AccountCancellationWorkEvent();
        CopyTo(e);
        return e;
    }
}
