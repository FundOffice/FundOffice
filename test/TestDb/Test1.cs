using FMO.Disclosure;
using FMO.Models;
using LiteDB;

namespace TestDb;

[TestClass]
public sealed class Test1
{
    [TestMethod]
    public void TestIDisclosureFile()
    {
        // 初始化 LiteDB (建议加上 using 确保连接释放)
        using var db = new LiteDatabase(@"FileName=test.db;Connection=Shared");
        var t = db.GetCollection<IDisclosureNotice>();

        // 辅助方法：构造 SimpleFile
        SimpleFile CreateTestFile(string label) => new()
        {
            Label = label,
            File = new FileMeta(
                Id: Guid.NewGuid().ToString("N"),
                Name: $"TestDoc_{label}.pdf",
                Time: DateTime.Now,
                Hash: $"sha256_{label}_hash_value"
            )
        };

        // 1. 生成不同类型的公告实例（均实现 IDisclosureFile）
        var noticeTemp = new TemporaryDisclosureNotice
        {
            FundId = 1001,
            FundName = "测试基金Alpha",
            FundCode = "F001",
            PublishDate = DateOnly.FromDateTime(DateTime.Today),
            Name = "临时公告-测试1",
            File = CreateTestFile("Temp1")
        };

        var noticeMgr = new ManagerDisclosureNotice
        {
            PublishDate = DateOnly.FromDateTime(DateTime.Today),
            Name = "管理人公告-测试2",
            File = CreateTestFile("Mgr2")
        };

        var noticeOpen = new TemporaryOpenNotice
        {
            FundId = 1002,
            FundName = "测试基金Beta",
            FundCode = "F002",
            PublishDate = DateOnly.FromDateTime(DateTime.Today),
            OpenDay = DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            AllowPurchase = true,
            AllowRedemption = false,
            File = CreateTestFile("Open3")
        };

        var noticeHuge = new HugeRedemptionNotice
        {
            FundId = 1003,
            FundName = "测试基金Gamma",
            FundCode = "F003",
            PublishDate = DateOnly.FromDateTime(DateTime.Today),
            OpenDay = DateOnly.FromDateTime(DateTime.Today),
            Ratio = 0.75m,
            IsFullyPaied = false,
            File = CreateTestFile("Huge4")
        };

        // 2. 存入集合
        t.Upsert(noticeTemp);
        t.Upsert(noticeMgr);
        t.Upsert(noticeOpen);
        t.Upsert(noticeHuge);

        // 3. 读取数据
        // 注意：IDisclosureNotice 接口不包含 File 属性，需转换为 IDisclosureFile 访问
        var retrievedTemp = t.FindById(noticeTemp.Id) as IDisclosureFile;
        var retrievedMgr = t.FindById(noticeMgr.Id) as IDisclosureFile;
        var retrievedOpen = t.FindById(noticeOpen.Id) as IDisclosureFile;
        var retrievedHuge = t.FindById(noticeHuge.Id) as IDisclosureFile;

        // 4. 校验读取结果及 File 属性一致性
        Assert.IsNotNull(retrievedTemp, "TemporaryDisclosureNotice 读取失败");
        Assert.IsNotNull(retrievedMgr, "ManagerDisclosureNotice 读取失败");
        Assert.IsNotNull(retrievedOpen, "TemporaryOpenNotice 读取失败");
        Assert.IsNotNull(retrievedHuge, "HugeRedemptionNotice 读取失败");

        AssertFileEqual(noticeTemp.File, retrievedTemp.File);
        AssertFileEqual(noticeMgr.File, retrievedMgr.File);
        AssertFileEqual(noticeOpen.File, retrievedOpen.File);
        AssertFileEqual(noticeHuge.File, retrievedHuge.File);
    }

    /// <summary>
    /// 深度比对 SimpleFile 及其内部 FileMeta 是否一致
    /// </summary>
    private void AssertFileEqual(SimpleFile? expected, SimpleFile? actual)
    {
        Assert.AreEqual(expected?.Exists, actual?.Exists, "Exists 状态不一致");
        Assert.AreEqual(expected?.Label, actual?.Label, "Label 不一致");

        if (expected?.File != null && actual?.File != null)
        {
            Assert.AreEqual(expected.File.Id, actual.File.Id, "FileMeta.Id 不一致");
            Assert.AreEqual(expected.File.Name, actual.File.Name, "FileMeta.Name 不一致");
            double msDiff = Math.Abs((expected.File.Time - actual.File.Time).TotalMilliseconds);
            Assert.IsLessThan(10, msDiff, $"FileMeta.Time 精度丢失或 DateTimeKind 不一致 (差异: {msDiff:F3}ms)");
            Assert.AreEqual(expected.File.Hash, actual.File.Hash, "FileMeta.Hash 不一致");
        }
        else
        {
            Assert.IsNull(expected?.File, "预期 FileMeta 应为 null");
            Assert.IsNull(actual?.File, "实际 FileMeta 应为 null");
        }
    }
}