namespace Vetting.Copilot.Models.Info;

public class DrawdownRecord
{
    public int Id { get; set; }
    public string? ProductName { get; set; }
    public string? Date { get; set; }
    public string? Amplitude { get; set; }
    public string? Reason { get; set; }
    public string? Countermeasures { get; set; }
    public string? RecoveryDays { get; set; }
}
