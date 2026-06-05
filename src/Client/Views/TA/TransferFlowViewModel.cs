using CommunityToolkit.Mvvm.ComponentModel;
using FMO.Models;
using System.Diagnostics.Contracts;

namespace FMO;

public interface ITransferFlowViewModel
{
    DateOnly Date { get; set; }
    string Id { get; }
    TransferFlowType Type { get; }
}


public partial class TransferFlowViewModel : ObservableObject, ITransferFlowViewModel
{
    public TransferFlowViewModel(string id)//, TransferOrder? order, TransferRequest[] requests, TransferRecord[] records)
    {
        Id = id;

        (Type, var idd, Date) = TransferFlow.Parse(id);


        if (Type == TransferFlowType.Order)
            OrderId = idd;
        else if (Type == TransferFlowType.OrderMissing)
            RequestId = idd;
        else FundId = idd;
    }

 

    public TransferFlowViewModel(TransferFlow f, (int Id, string Name, string Code, DateOnly ClearDate) fund, Investor investor, IEnumerable<TransferOrder> enumerable1, IEnumerable<TransferRequest> enumerable2, IEnumerable<TransferRecord> enumerable3)
    {
        Id = f.Id;
        Date = f.Date;
        Type = f.Type;
        FundId = f.FundId;
        FundName = fund.Name;
        Code = fund.Code;
        InvestorId = f.InvestorId;

        Orders = enumerable1.ToArray();
        Requests = enumerable2.ToArray();
        Records = enumerable3.ToArray();
    }

    public string Id { get; }

    public DateOnly Date { get; set; }

    public TransferFlowType Type { get; }

    public int FundId { get; set; }

    public string FundName { get; set; }

    public string Code { get; set; }
    

    public int InvestorId { get; }

    public int OrderId { get; set; }

    public int RequestId { get; set; }


    public TransferOrder[] Orders { get; set; }

    public TransferRequest[] Requests { get; set; }

    public TransferRecord[] Records { get; set; }

}




public class OrderTransferFlowViewModel : TransferFlowViewModel
{
    public OrderTransferFlowViewModel(string id, string shareCode, TransferOrder order, TransferRequest[] requests, TransferRecord[] records) : base(id)
    {
        FundName = order.FundName;
        FundCode = order.FundCode;
        ShareName = order.ShareClass;
        ShareCode = shareCode;

        InvestorName = order.InvestorName;
        InvestorCard = order.InvestorIdentity;
        OrderType = order.Type;
        Number = order.Number;
        Fee = order.Fee;
        Contract = order.Contract;
        RiskDiscloure = order.RiskDiscloure;
        OrderSheet = order.OrderSheet;
        RiskPair = order.RiskPair;
        Videotape = order.Videotape;
        Review = order.Review;
    }


    public string FundName { get; }

    public string FundCode { get; }

    public string? ShareName { get; }

    public string ShareCode { get; }

    public string InvestorName { get; set; }

    public string InvestorCard { get; set; }

    public TransferOrderType OrderType { get; set; }

    public decimal Number { get; set; }

    public decimal Fee { get; set; }



    /// <summary>
    /// 合同
    /// </summary>
    public SimpleFile? Contract { get; set; }

    /// <summary>
    /// 风险揭示
    /// </summary>
    public SimpleFile? RiskDiscloure { get; set; }

    /// <summary>
    /// 认申购、赎回单
    /// </summary>
    public SimpleFile? OrderSheet { get; set; }

    /// <summary>
    /// 风险匹配
    /// </summary>
    public SimpleFile? RiskPair { get; set; }

    /// <summary>
    /// 双录
    /// </summary>
    public SimpleFile? Videotape { get; set; }


    /// <summary>
    /// 回访
    /// </summary>
    public SimpleFile? Review { get; set; }
}