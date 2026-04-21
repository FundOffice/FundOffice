using FMO.Utilities;
using LiteDB;
using System.Net;

namespace FMO.Trustee;

public static class TrusteeGallay
{
    public static ITrustee[] Trustees { get; }


    public static TrusteeViewModelBase[] TrusteeViewModels { get; }


    public static TrusteeWorker Worker { get; }

    static TrusteeGallay()
    {
        using var pdb = DbHelper.Platform();
        var config = pdb.GetCollection<TrusteeUnifiedConfig>().FindOne(_ => true);
        if (config is not null)
        {
            TrusteeApiBase.SetProxy(config.UseProxy ? new WebProxy(config.ProxyUrl) { Credentials = string.IsNullOrWhiteSpace(config.ProxyUser) ? null : new NetworkCredential(config.ProxyUser, config.ProxyPassword) } : null);
        }


        TrusteeViewModels = [new CMSViewModel(), new CITISCViewModel(), new CSCViewModel(), new XYZQViewModel()];

        Trustees = TrusteeViewModels.OfType<ITrusteeViewModel>().Select(x => x.Assist).ToArray();


        Worker = new TrusteeWorker(Trustees);
    }

    public static ITrustee? Find(int id) => Worker.Find(id);

    /// <summary>
    /// 放到首页中
    /// </summary>
    public static void Initialize()
    {
#if !DEBUG
        Worker.Start();
#endif
    }
}