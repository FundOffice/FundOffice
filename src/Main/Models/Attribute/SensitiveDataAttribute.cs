using System;
using System.Collections.Generic;
using System.Text;

namespace FMO.Models;

// 标记需要脱敏的字段/属性
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class SensitiveDataAttribute : Attribute
{
}
