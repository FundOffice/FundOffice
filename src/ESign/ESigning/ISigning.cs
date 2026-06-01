using CommunityToolkit.Mvvm.Messaging;
using FMO.Models;
using FMO.Utilities;
using LiteDB;
using MoT;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("MeiShi")]


namespace FMO.ESigning;

public record ESigningStatus(string Id, bool IsValid);



/// <summary>
/// 在线资格认证信息
/// </summary>
/// <param name="Id"></param>
/// <param name="Date"></param>
/// <param name="CustomerName"></param>
/// <param name="CustomerIdentityNumber"></param>
/// <param name="Finished"></param>
public record QualficationInfo(string Id, DateTime Time, string CustomerName, string CustomerIdentityNumber, bool Finished);

public interface ISigning
{
    string Id { get; }

    string Name { get; }


    /// <summary>
    /// 从托管外包机构同步客户资料，单向
    /// </summary>
    /// <returns></returns>
    Task<Investor[]> QueryCustomerAsync(DateTime from = default, DateTime end = default);

    /// <summary>
    /// 获取合投列表
    /// </summary>
    /// <param name="from"></param>
    /// <param name="end"></param>
    /// <returns></returns>
    Task<InvestorQualification[]> QueryQualificationAsync(DateTime from = default, DateTime end = default);

    /// <summary>
    /// 获取合投详情
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    Task<bool> QueryQualificationAsync(InvestorQualification q);

    Task<TransferOrder[]> QueryOrderAsync(DateTime from = default, DateTime end = default);

    Task<ErrorReturn> QueryOrderAsync(TransferOrder order);


     

    /// <summary>
    /// 创建临时开放日
    /// </summary>
    /// <param name="fundId">Fund Id</param>
    /// <param name="shareCode">份额类别</param>
    /// <param name="date">开放日</param>
    /// <param name="flag">申购标志</param>
    /// <param name="notify">通知投资人</param>
    /// <returns></returns>
    Task<ErrorReturn> CreateTemporaryOpenDay(int fundId, string? shareCode, DateOnly date, OpenTradeType flag, bool notify);

    /// <summary>
    /// 创建临时开放日
    /// </summary>
    /// <param name="fundName">基金名称</param>
    /// <param name="shareCode">份额类别</param>
    /// <param name="date">开放日</param>
    /// <param name="flag">申购标志</param>
    /// <param name="notify">通知投资人</param>
    /// <returns></returns>
    Task<ErrorReturn> CreateTemporaryOpenDay(string fundName, string? shareCode, DateOnly date, OpenTradeType flag, bool notify);


    /// <summary>
    /// 查找当前可用的基金开放日
    /// </summary>
    /// <param name="fundId"></param>
    /// <param name="shareCode"></param>
    /// <returns></returns>
    Task<Return<DateOnly[]>> QueryAvaliableOpenDay(int fundId, string? shareCode, OpenTradeType flag);

    Task<Return<DateOnly[]>> QueryAvaliableOpenDayAsync(string fundName, string? shareCode, OpenTradeType flag);

    /// <summary>
    /// 获取在签约平台中的基金信息
    /// </summary>
    /// <returns></returns>
    Task<EsigningFundInfo[]> QueryFundInfo();


    void OnConfig(ISigningConfig config);

    Task<ErrorReturn> PushOrder(SigningOrder order);
}

internal static class ISigningExtensions
{
    public static void SetStatus(this ISigning s, bool valid)
    {
        WeakReferenceMessenger.Default.Send(new ESigningStatus(s.Id, valid));
        using var db = DbHelper.Platform();
        var cf = db.GetCollection<ISigningConfig>().FindById(s.Id);
        if (cf is null) return;
        cf.IsValid = valid;
        db.GetCollection<ISigningConfig>().Update(cf);
    }
}

public class SigningOrder
{ 

    public int FundId { get; set; }

    public int ShareId { get; set; }

    public int InvestorId { get; set; }


    public TransferOrderType OrderType { get; set; }

    public decimal? Number { get; set; }

    public decimal? Fee { get; set; }

    public DateOnly OpenDate { get; set; }
}

public record SigningCallHistory(string Identifier, string Method, DateTime Time, string Params, string? Json);

public record SigningWorkerLoopHistory(DateTime Time, string Method);

public static class SigningLoger
{ 
    public static void LogRun(this ISigning signing, string method, string param, string json)
    {
        Logg.Write(new SigningCallHistory(signing.Id, method, DateTime.Now, param, json));
    }


    public static void LogWorker(string method)
    {
        Logg.Write(new SigningWorkerLoopHistory(DateTime.Now, method));
    }
}