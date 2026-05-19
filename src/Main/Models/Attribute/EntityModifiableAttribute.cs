using System;
using System.Collections.Generic;
using System.Text;

namespace FMO.Models;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class EntityModifiableAttribute: Attribute
{
    public EntityModifiableAttribute(Type entityType)
    {
        EntityType = entityType;
    }

    public Type EntityType { get; set; }


}
