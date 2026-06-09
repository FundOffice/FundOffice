using System;

namespace SG;

/// <summary>
/// 两个生成器共享的属性元数据
/// </summary>
public readonly record struct PropertyMeta(
    string Name,
    string GenericArg,
    bool IsFactorItem,
    string? FactFieldName,
    bool IsValueType = false)
{
    /// <summary>
    /// FactorFields 中使用的常量名
    /// </summary>
    public string FieldKey => FactFieldName ?? Name;

    /// <summary>
    /// __AutoInitializeCtor 初始化代码片段
    /// </summary>
    public string InitCode => IsFactorItem
        ? $"{Name} = new(Filter<{GenericArg.Replace("?", "")}>(FactorFields.{FieldKey}, g), _shares, _shareConfigMap);"
        : $"{Name} = new(Filter<{GenericArg.Replace("?", "")}>(FactorFields.{FieldKey}, g));";
}
