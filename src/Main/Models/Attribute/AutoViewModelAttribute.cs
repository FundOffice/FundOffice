using System;
using System.Collections.Generic;
using System.Text;

namespace FMO.Models;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class AutoViewModelAttribute : Attribute
{
    public Type SourceType { get; }

    public AutoViewModelAttribute(Type sourceType)
    {
        SourceType = sourceType;
    }
}