using FMO.Models;
using FMO.Shared;

namespace FMO;

[AutoChangeableViewModel(typeof(RaisingBankTransaction))]
partial class RaisingBankTranscationViewModel
{
    public RaisingBankTranscationViewModel(RaisingBankTransaction? instance, (int Id, string Name, string Code, DateOnly ClearDate)[] funds) : this(instance)
    {
        if (instance is null) return;
        var fund = funds.FirstOrDefault(x => x.Id == instance.FundId);
        FundName = fund.Name;

        This = string.IsNullOrWhiteSpace(FundName) ? instance.AccountName : FundName;
    }

    public string FundName { get; }


    public string This { get; }
}