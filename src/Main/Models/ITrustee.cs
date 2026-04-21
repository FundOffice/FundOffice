
using FMO.Models;

namespace FMO.Trustee;











/// <summary>
/// 
/// </summary>
public interface ITrustee
{
    /// <summary>
    /// https://xxx.com no /
    /// </summary>
    string TestDomain { get; }

    /// <summary>
    /// https://xxx.com no /
    /// </summary>
    string Domain { get; }

    bool IsValid { get; }

    bool Prepare();

    string Title { get; }


    string Identifier { get; }

    Task<ReturnWrap<TransferRequest>> QueryTransferRequests(DateOnly begin, DateOnly end, string? fundCode = null);

    /// <summary>
    /// 获取交易确认记录
    /// </summary>
    /// <param name="begin"></param>
    /// <param name="end"></param>
    /// <returns></returns>
    Task<ReturnWrap<TransferRecord>> QueryTransferRecords(DateOnly begin, DateOnly end, string? fundCode = null);

    /// <summary>
    /// 获取基金固定费用
    /// </summary>
    /// <param name="begin"></param>
    /// <param name="end"></param>
    /// <returns></returns>
    Task<ReturnWrap<FundDailyFee>> QueryFundDailyFee(DateOnly begin, DateOnly end);


    /// <summary>
    /// 托管户流水
    /// </summary>
    /// <param name="begin"></param>
    /// <param name="end"></param>
    /// <returns></returns>
    Task<ReturnWrap<BankTransaction>> QueryCustodialAccountTransction(DateOnly begin, DateOnly end, string? fundCode = null);


    /// <summary>
    /// 募集户流水
    /// </summary>
    /// <param name="begin"></param>
    /// <param name="end"></param>
    /// <returns></returns>
    Task<ReturnWrap<RaisingBankTransaction>> QueryRaisingAccountTransction(DateOnly begin, DateOnly end, string? fundCode = null);



    /// <summary>
    /// 获取分级产品映射关系
    /// </summary>
    /// <returns></returns>
    Task<ReturnWrap<SubjectFundMapping>> QuerySubjectFundMappings();


    /// <summary>
    /// 获取募集户余额
    /// </summary>
    /// <param name="fundCode"></param>
    /// <returns></returns>
    Task<ReturnWrap<FundBankBalance>> QueryRaisingBalance();


    Task<ReturnWrap<DailyValue>> QueryNetValue(DateOnly begin, DateOnly end, string? fundCode = null);

    Task<bool> VerifyConfig();

    public bool IsSuit(string? comapny);
}




public record ReturnWrap<T>(ReturnCode Code, IList<T>? Data);

public record TrusteeCallHistory(string Identifier, string Method, DateTime Time, string Params, string? Json);