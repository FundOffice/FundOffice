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
public record class TransferFlow
{
    public string Id => Type switch
    {
        TransferFlowType.Order => $"O.{Date.DayNumber}.{FundId}.{InvestorId}",
        TransferFlowType.OrderMissing => $"M.{Date.DayNumber}.{FundId}.{InvestorId}",
        TransferFlowType.Clear => $"C.{Date.DayNumber}.{FundId}",
        TransferFlowType.SetUp => $"P.{Date.DayNumber}.{FundId}",
        TransferFlowType.Dividend => $"D.{Date.DayNumber}.{FundId}",
        TransferFlowType.Desire => $"S.{Date.DayNumber}.{FundId}",
        TransferFlowType.Adjustment => $"A.{Date.DayNumber}.{FundId}",
        TransferFlowType.Transfer => $"T.{Date.DayNumber}.{FundId}",
        TransferFlowType.Convert => $"V.{Date.DayNumber}.{FundId}",
        TransferFlowType.None => "None",
        _ => throw new InvalidDataException($"{Type} Invalid")
    };


    public int FundId { get; set; }



    /// <summary>
    /// 日期
    /// </summary>
    public DateOnly Date { get; set; }


    public TransferFlowType Type { get; set; }


    public int InvestorId { get; set; }



    public static string MakeId(TransferFlowType type, int id, DateOnly date) => type switch
    {
        TransferFlowType.Order => $"O.{date.DayNumber}.{id}",
        TransferFlowType.OrderMissing => $"M.{date.DayNumber}.{id}",
        TransferFlowType.Clear => $"C.{date.DayNumber}.{id}",
        TransferFlowType.SetUp => $"P.{date.DayNumber}.{id}",
        TransferFlowType.Dividend => $"D.{date.DayNumber}.{id}",
        TransferFlowType.Desire => $"S.{date.DayNumber}.{id}",
        TransferFlowType.Adjustment => $"A.{date.DayNumber}.{id}",
        TransferFlowType.Transfer => $"T.{date.DayNumber}.{id}",
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

        if (!int.TryParse(arr[1], out int dayNum))
            throw new FormatException($"日期天数不是数字：{arr[1]}");
        if (!int.TryParse(arr[2], out int numId))
            throw new FormatException($"id字段不是数字：{arr[2]}");

        DateOnly dt = DateOnly.FromDayNumber(dayNum);
        return (flowType, numId, dt);
    }

}
