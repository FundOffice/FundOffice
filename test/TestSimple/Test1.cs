using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace TestSimple;

[TestClass]
public sealed class Test1
{
    [TestMethod]
    public async Task TestMethod1()
    {

        Throttle throttle = new Throttle(TimeSpan.FromMilliseconds(100));

        long start = Stopwatch.GetTimestamp();

        // 绝对不要用 Parallel.For + async！
        // 正确方案：并发任务 + 限流，用 Semaphore 或直接 Task.WhenAll
        var tasks = new List<Task>();

        // 模拟并发请求（100个足够测试，10万会卡死UI）
        for (int i = 1; i <= 10000; i++)
        {
            int idx = i;

           
            
            tasks.Add(Task.Run(async () =>
            {
                await Task.Delay(Random.Shared.Next(1000)); // 模拟业务
                throttle.Execute(() =>
                {
                    double ms = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
                    Console.WriteLine($"[{idx}] 输出：{ms:F2} ms");
                });
            }));
        }

        // 等待所有任务执行完成！！！
        await Task.WhenAll(tasks);

    }
}


