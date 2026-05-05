using FMO.Models;
using FMO.Shared;

namespace FMO;

[AutoChangeableViewModel(typeof(RaisingBankTransaction))]
partial class RaisingBankTranscationViewModel
{
    public RaisingBankTranscationViewModel(RaisingBankTransaction? instance, (int Id, string Name, string? Code, DateOnly ClearDate) fund) : this(instance)
    {
        if (instance is null) return;
        FundName = fund.Name ?? "未知基金";

        This = fund.Name?? instance.AccountName;
    }

    public RaisingBankTranscationViewModel(RaisingBankTransaction? instance, string? fundName) : this(instance)
    {
        if (instance is null) return;
        FundName = fundName ?? "未知基金";

        This = fundName ?? instance.AccountName;
    }

    public string FundName { get; }


    public string This { get; }
}