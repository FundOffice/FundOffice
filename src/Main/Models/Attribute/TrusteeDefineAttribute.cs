namespace FMO.Models;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class TrusteeDefineAttribute : Attribute
{
    public Type ViewModelType { get; }
    public TrusteeDefineAttribute(Type viewModelType)
    {
        ViewModelType = viewModelType;
    }
}
