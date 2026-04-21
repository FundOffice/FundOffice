using CommunityToolkit.Mvvm.ComponentModel;

namespace FMO;


public interface IModifiableValue
{
    bool IsChanged { get; }

    bool IsSetted { get; }

    public void Apply();

    void Clear();
}


