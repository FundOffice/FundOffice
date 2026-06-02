using FMO.AI;
using FMO.Utilities;
using Initial;

namespace TestAI;

[TestClass]
public sealed class Test1
{
    [TestInitialize]
    public void TestInit()
    {
        // This method is called before each test method.
        DataInject.SetAsDebug();
    }

    [TestMethod]
    public void TestMiMo()
    {
        using var db = DbHelper.Base();
        db.GetCollection<TokenProvider>().FindById()
    }
}
