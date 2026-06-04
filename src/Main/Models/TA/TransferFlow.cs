using System.ComponentModel;

namespace FMO.Models;


[TypeConverter(typeof(EnumDescriptionTypeConverter))]
public enum TransferFlowType
{
    None,

    [Description("基金成立")] SetUp,

    [Description("申赎")] Order,

    [Description("申赎")] OrderMissing,

    [Description("清盘")] Clear,

    [Description("分红")] Dividend,

    [Description("分红方式")] Desire,

    [Description("调增调减")] Adjustment,

    [Description("转让受让")] Transfer,

    [Description("份额转换")] Convert
}

/// <summary>
/// 订单流
/// </summary>
public class TransferFlow
{
    //public string Id => Type switch
    //{
    //    TransferFlowType.Order => $"O.{OrderId}",
    //    TransferFlowType.Clear => $"C.{FundId}",
    //    TransferFlowType.Dividend => $"D.{FundId}.{Date}",
    //    TransferFlowType.Desire => $"S.{FundId}.{Date}",
    //    TransferFlowType.Adjustment => $"A.{FundId}.{Date}",
    //    TransferFlowType.Transfer => $"T.{FundId}.{Date}",
    //    _ => throw new InvalidOperationException()
    //};


    //public TransferFlowType Type { get; set; }

    ///// <summary>
    ///// 订单
    ///// =0 缺少订单
    ///// </summary>
    //public int OrderId { get; set; }


    ///// <summary>
    ///// 申请和确认记录
    ///// </summary>
    //public (int Request, int Comfirm)[] Records { get; set; } = [];


    //public int FundId { get; set; }



    ///// <summary>
    ///// 开放日
    ///// </summary>
    //public DateOnly Date { get; set; }

    public static string MakeId(TransferFlowType type, int id, DateOnly date) => type switch
    {
        TransferFlowType.Order => $"O.{id}.{date.DayNumber}",
        TransferFlowType.OrderMissing => $"M.{id}.{date.DayNumber}",
        TransferFlowType.Clear => $"C.{id}.{date.DayNumber}",
        TransferFlowType.SetUp => $"P.{id}.{date.DayNumber}",
        TransferFlowType.Dividend => $"D.{id}.{date.DayNumber}",
        TransferFlowType.Desire => $"S.{id}.{date.DayNumber}",
        TransferFlowType.Adjustment => $"A.{id}.{date.DayNumber}",
        TransferFlowType.Transfer => $"T.{id}.{date.DayNumber}",
        _ => throw new InvalidDataException($"{type} Invalid")
    };


    public static (TransferFlowType type, int id, DateOnly date) Parse(string id)
    {
        var arr = id.Split('.');

        var prefix = arr[0];
        TransferFlowType flowType = prefix switch
        {
            "O" => TransferFlowType.Order,
            "R" => TransferFlowType.OrderMissing,
            "C" => TransferFlowType.Clear,
            "P" => TransferFlowType.SetUp,
            "D" => TransferFlowType.Dividend,
            "S" => TransferFlowType.Desire,
            "A" => TransferFlowType.Adjustment,
            "T" => TransferFlowType.Transfer,
            _ => TransferFlowType.None
        };

        if (flowType == TransferFlowType.None)
            return default;

        if (!int.TryParse(arr[1], out int numId))
            throw new FormatException($"id字段不是数字：{arr[1]}");
        if (!int.TryParse(arr[2], out int dayNum))
            throw new FormatException($"日期天数不是数字：{arr[2]}");

        DateOnly dt = DateOnly.FromDayNumber(dayNum);
        return (flowType, numId, dt);
    }

}
