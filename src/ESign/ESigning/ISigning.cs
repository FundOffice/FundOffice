using CommunityToolkit.Mvvm.Messaging;
using FMO.Models;
using FMO.Utilities;
using LiteDB;
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

    Task<bool> QueryOrderAsync(TransferOrder order);

    /// <summary>
    /// 批量上传信批文件
    /// </summary>
    /// <param name="filePath"></param>
    /// <returns></returns>
    //Task<bool> BatchUploadDisclosureFiles(string filePath, DateTime time, string type);


    Task<bool> UploadDisclosureFile(string fundName, string fundCode, string shareClass, DateTime time, string announceName, string file);



    /// <summary>
    /// 获取在签约平台中的基金信息
    /// </summary>
    /// <returns></returns>
    Task<EsigningFundInfo[]> QueryFundInfo();


    void OnConfig(ISigningConfig config);


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

public record SigningCallHistory(string Identifier, string Method, DateTime Time, string Params, string? Json);

public record SigningWorkerLoopHistory(DateTime Time, string Method);

public static class SigningLoger
{
    static ILiteDatabase db { get; } = new LiteDatabase(@$"FileName=data\platformlog.db;Connection=Shared");

    public static void LogRun(this ISigning signing, string method, string param, string json)
    {
        db.GetCollection<SigningCallHistory>().Insert(new SigningCallHistory(signing.Id, method, DateTime.Now, param, json));
    }


    public static void LogWorker(string method)
    {
        db.GetCollection<SigningWorkerLoopHistory>().Insert(new SigningWorkerLoopHistory(DateTime.Now, method));
    }
}