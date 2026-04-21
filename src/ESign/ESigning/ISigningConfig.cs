namespace FMO.ESigning;

public interface ISigningConfig
{
    string Id { get; }

    bool IsEnable { get; set; }

    bool IsValid { get; set; }
}
