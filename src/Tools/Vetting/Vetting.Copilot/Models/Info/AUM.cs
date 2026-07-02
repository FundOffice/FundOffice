namespace Vetting.Copilot.Models.Info;

public class AUM : IResolve
{
    public int Id { get; set; }
    public string? Year { get; set; }
    public string? Scale { get; set; }

    public object? Resolve(string propertyName) => propertyName switch
    {
        nameof(Year) => Year,
        nameof(Scale) => Scale,
        _ => null,
    };
}
