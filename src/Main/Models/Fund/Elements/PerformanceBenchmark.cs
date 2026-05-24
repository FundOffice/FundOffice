namespace FMO.Models;

public class PerformanceBenchmark
{
    public bool Has { get; set; }

    public string? Benchmark { get; set; }

    public override string ToString() => Has switch { true when Benchmark?.Length > 0 => Benchmark, _ => "未设置" };
}