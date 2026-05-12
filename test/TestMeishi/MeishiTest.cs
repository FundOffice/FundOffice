using FMO.Models;
using FMO.Utilities;
using Initial;
using System.Text.Json.Serialization;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace TestMeishi;

[TestClass]
public sealed class MeishiTest
{
    public MeishiTest()
    {
        TestInit.SetAsDebug();
    }
        
    [TestMethod]
    public async Task CreateTemporaryOpenDay_FundNotFound()
    { 

        var mei = new FMO.ESigning.MeiShi.MeiShiAssit();

        var date = DateOnly.FromDateTime(DateTime.Now);
        var result = await mei.CreateTemporaryOpenDay(2, null, date.AddDays(6), OpenFlag.Buy, true);

       
    }

    [TestMethod]
    public void TestTimeRange()
    {
        Console.WriteLine(new TimeRange(new(2026, 5, 12)));
    }

    [TestMethod]
    public void TestNotice()
    {
        var date = DateOnly.FromDateTime(DateTime.Now).AddDays(5);
        var d = Math.Max(0, date.DayNumber - DateOnly.FromDateTime(DateTime.Now).DayNumber);


        Console.WriteLine(d);
    }
}


internal class TimeRange
{
    public TimeRange(DateOnly date)
    {
        StartTime = new DateTime(date, default).ToUniversalTime().TimeStampByMilliseconds();
        EndTime = StartTime + 86399000;
    }

    /// <summary>
    /// 开始时间戳（毫秒）
    /// </summary>
    [JsonPropertyName("startTime")]
    public long StartTime { get; set; }

    /// <summary>
    /// 结束时间戳（毫秒）
    /// </summary>
    [JsonPropertyName("endTime")]
    public long EndTime { get; set; }

    public override string ToString()
    {
        return $"{StartTime} {EndTime}";
    }
}