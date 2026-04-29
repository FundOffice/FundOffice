namespace FMO.Models;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ESignDefineAttribute : Attribute
{
    public Type ViewModelType { get; }
    public ESignDefineAttribute(Type viewModelType)
    {
        ViewModelType = viewModelType;
    }
}
