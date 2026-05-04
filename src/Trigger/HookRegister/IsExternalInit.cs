// Compatibility shim for init-only setters / record support on older target frameworks
namespace System.Runtime.CompilerServices
{
    // The compiler looks for this type when compiling records or init-only properties
    // Provide a simple public static class so projects targeting older frameworks build successfully.
    public static class IsExternalInit { }
}
