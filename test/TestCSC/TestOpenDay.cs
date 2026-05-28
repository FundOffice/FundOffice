using FMO.Models;
using FMO.Trustee;
using FMO.Utilities;
using Initial;
using System.Net;

namespace TestCSC;

[TestClass]
public sealed class TestOpenDay
{
    public Fund Fund { get; set; } = null!;

    [TestInitialize]
    public void TestInit()
    {
        // This method is called before each test method.
        DataInject.SetAsDebug();

        using var pdb = DbHelper.Platform();
        var config = pdb.GetCollection<TrusteeUnifiedConfig>().FindOne(_ => true) ?? new();

        Fund = DbHelper.Base().GetCollection<Fund>().FindById(16);

        //更新到trustee
        TrusteeApiBase.SetProxy(config.UseProxy ? new WebProxy(config.ProxyUrl) { Credentials = string.IsNullOrWhiteSpace(config.ProxyUser) ? null : new NetworkCredential(config.ProxyUser, config.ProxyPassword) } : null);
    }

    [TestMethod]
    public async Task QueryOpenDay()
    {
        var assist = new CSC();
        assist.LoadConfig();
        assist.Initialize();

        var result = await assist.QueryOpenDays(new DateOnly(2026, 1, 1), new DateOnly(2026, 5, 1), Fund.Code);


        Assert.AreEqual(ReturnCode.Success, result.Code);
    }
}
