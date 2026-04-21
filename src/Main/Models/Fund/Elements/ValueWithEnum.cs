using System.ComponentModel;

namespace FMO.Models;



/// <summary>
/// 基金费用
/// </summary>
[TypeConverter(typeof(EnumDescriptionTypeConverter))]
public enum FundFeeType
{

    [Description("固定比例")] Ratio,

    [Description("固定金额")] Fix,

    [Description("按持有时间")] ByTime,


    [Description("其它")] Other
}


[TypeConverter(typeof(EnumDescriptionTypeConverter))]
public enum FundFeePayType
{

    [Description("额外收取")] Extra,

    [Description("价外法")] Out,

    [Description("其它")] Other
}




/// <summary>
/// 带枚举及值的类型
/// </summary>
/// <typeparam name="TEnum"></typeparam>
/// <typeparam name="TValue"></typeparam>
public class ValueWithEnum<TEnum, TValue> where TEnum : struct, Enum
{
    public TEnum Type { get; set; }

    public TValue? Value { get; set; }

    public string? Extra { get; set; }
}


public class ValueWithBoolean<T>
{
    /// <summary>
    /// 是否采用
    /// </summary>
    public bool IsAdopted { get; set; }

    public T? Value { get; set; }
}



/*
/// <summary>
/// 收费规则
/// </summary>
public class FeeRule
{

}

/// <summary>
/// 数轴
/// left .... right
/// </summary>
public class TieredRatesFeeRule : FeeRule
{
    public class Tier<T> where T : struct
    {
        public T Stop { get; set; }

        public decimal Value { get; set; }
    }

    /// <summary>
    /// true -> 从小到大排列 区间为 >= stop，对应value
    /// false -> 从大到小排列
    /// </summary>
    public bool IsAccending { get; set; } = true;

    /// <summary>
    /// 标记点
    /// </summary>
    public List<int> Stops { get; set; }


    /// <summary>
    /// 区间值，count = stops.count - 1
    /// </summary>
    public List<decimal> Values { get; set; }



    /// <summary>
    /// 
    /// </summary>
    public decimal? Left { get; set; }

    public decimal? Right { get; set; }
}*/