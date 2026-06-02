

namespace System.Runtime.CompilerServices
{
    internal sealed class CompilerFeatureRequiredAttribute : Attribute
    {
        public CompilerFeatureRequiredAttribute(string featureName)
        {
            FeatureName = featureName;
        }
        public string FeatureName { get; } 
        public bool IsOptional { get; init; }
    }

    internal sealed class SetsRequiredMembersAttribute : Attribute { }

    internal sealed class RequiredMemberAttribute : Attribute { }

    internal static class IsExternalInit { }
}