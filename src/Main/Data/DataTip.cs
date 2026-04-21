namespace FMO.Utilities;

public interface IDataTip
{
    long Id { get; }

    string[]? Tags { get; }

    object? Context { get; }

    string? Message { get; }
}


public class DataTip : IDataTip
{
    public long Id { get; } = DateTime.Now.Ticks;

    public object? Context { get; set; }

    public string? Message { get; set; }

    public string[]? Tags { get; set; }
}


public class DataTip<T> : IDataTip
{
    public long Id { get; } = DateTime.Now.Ticks;

    public object? Context => _Context;

    public T? _Context { get; set; }


    public string? Message { get; set; }

    public string[]? Tags { get; set; }
}


public record DataTipRemove(long Id);