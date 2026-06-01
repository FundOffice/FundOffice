using FMO.Trustee;
using FMO.Utilities;
using Initial;
using System.Net;

namespace TestXYZQ;




[TestClass]
public sealed class TestOpenDay
{

    [TestInitialize]
    public void TestInit()
    {
        // This method is called before each test method.
        DataInject.SetAsDebug();

        using var pdb = DbHelper.Platform();
        var config = pdb.GetCollection<TrusteeUnifiedConfig>().FindOne(_ => true) ?? new();


        //更新到trustee
        TrusteeApiBase.SetProxy(config.UseProxy ? new WebProxy(config.ProxyUrl) { Credentials = string.IsNullOrWhiteSpace(config.ProxyUser) ? null : new NetworkCredential(config.ProxyUser, config.ProxyPassword) } : null);
    }

    [TestMethod]
    public async Task QueryOpenDay()
    {
        var assist = new XYZQ();
        assist.LoadConfig();
        assist.Initialize();

        var result = await assist.QueryOpenDays(new DateOnly(2026, 1, 1), new DateOnly(2026, 5, 1));



        Assert.AreEqual(ReturnCode.Success, result.Code);
    }
}
