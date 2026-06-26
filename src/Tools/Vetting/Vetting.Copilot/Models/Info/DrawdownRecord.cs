namespace Vetting.Copilot.Models.Info;

public class DrawdownRecord : IResolve
{
    public int Id { get; set; }
    public string? ProductName { get; set; }
    public string? Date { get; set; }
    public string? Amplitude { get; set; }
    public string? Reason { get; set; }
    public string? Countermeasures { get; set; }
    public string? RecoveryDays { get; set; }

    public object? Resolve(string propertyName) => propertyName switch
    {
        nameof(ProductName) => ProductName,
        nameof(Date) => Date,
        nameof(Amplitude) => Amplitude,
        nameof(Reason) => Reason,
        nameof(Countermeasures) => Countermeasures,
        nameof(RecoveryDays) => RecoveryDays,
        _ => null,
    };
}
