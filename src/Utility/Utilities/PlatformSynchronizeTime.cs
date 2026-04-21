namespace FMO.Utilities;

public class PlatformSynchronizeTime
{
    public int Id { get; set; }

    public required string Identifier { get; set; }

    public required string Method { get; set; }

    public DateTime Time { get; set; }

}
