namespace FMO.Models;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public class HookDataAttribute : Attribute
{

}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public class HookableAttribute : Attribute
{

}