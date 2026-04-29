namespace FMO.Models;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class DisclosureDefineAttribute : Attribute
{
    public DisclosureDefineAttribute(Type configType, Type viewModelType)
    {
        ConfigType = configType;
        ViewModelType = viewModelType;
    }

    public Type ConfigType { get; set; }

    public Type ViewModelType { get; }
    
     
}
