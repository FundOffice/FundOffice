namespace FMO.Models;

public class PerformanceBenchmark : IEquatable<PerformanceBenchmark>
{
    public bool Has { get; set; }

    public string? Benchmark { get; set; }

    public bool Equals(PerformanceBenchmark? other)
    {
        if (other is null) return false;
        if (Has != other.Has) return false;
        if (!Has) return true; // 无基准 → 全同
        return Benchmark == other.Benchmark;
    }

    public override int GetHashCode() => HashCode.Combine(Has, Benchmark);

    public override string ToString() => Has switch { true when Benchmark?.Length > 0 => Benchmark, _ => "未设置" };
}