using FMO.Utilities;
using LiteDB;
using System.Net;

namespace FMO.Trustee;

public static class TrusteeGallay
{
    private static Dictionary<string, ITrustee> _plat = [];
    private static Dictionary<string, TrusteeViewModelBase> _viewModels = [];

    public static ITrustee[] Trustees => _plat.Values.ToArray();


    public static TrusteeViewModelBase[] TrusteeViewModels => _viewModels.Values.OrderBy(x => x.Idenitifier).ToArray();


    public static TrusteeWorker Worker { get; private set; } = null!;

    static TrusteeGallay()
    {
        using var pdb = DbHelper.Platform();
        var config = pdb.GetCollection<TrusteeUnifiedConfig>().FindOne(_ => true);
        if (config is not null)
        {
            TrusteeApiBase.SetProxy(config.UseProxy ? new WebProxy(config.ProxyUrl) { Credentials = string.IsNullOrWhiteSpace(config.ProxyUser) ? null : new NetworkCredential(config.ProxyUser, config.ProxyPassword) } : null);
        }


        //TrusteeViewModels = [new CMSViewModel(), new CITICSViewModel(), new CSCViewModel(), new XYZQViewModel()];

        //Trustees = TrusteeViewModels.OfType<ITrusteeViewModel>().Select(x => x.Assist).ToArray();


    }

    public static ITrustee? Find(int id) => Worker.Find(id);

    public static void Register(TrusteeViewModelBase viewModel)
    {
        //obj.LoadConfig();
        var obj = (viewModel as ITrusteeViewModel)!.Assist;
        _plat[obj.Identifier] = obj;
        _viewModels[viewModel.Idenitifier] = viewModel;
        //Worker.AddTrustee(obj);
    }


    /// <summary>
    /// 放到首页中
    /// </summary>
    public static void Initialize()
    {
        Worker = new TrusteeWorker(Trustees);
#if !DEBUG
        Worker.Start();
#endif
    }
}