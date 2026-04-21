namespace FMO.Models;

/// <summary>
/// TransactionId、RecordId是唯一的
/// 认申购 order -> request | transaction -> record
/// 赎回 oreder -> request -> record -> transaction
/// 清盘 record -> transaction
/// 补录 request -> record
/// </summary>
public class TransferMapping
{

    //public TAMapping()
    //{

    //}

    //public TAMapping(string transactionId, int orderId, int requestId, int recordId)
    //{
    //    TransactionId = transactionId;
    //    OrderId = orderId;
    //    RequestId = requestId;
    //    RecordId = recordId; 
    //}

    public int Id { get; set; }

    public string? TransactionId { get; set; }

    public int OrderId { get; set; }

    public int RequestId { get; set; }

    public int RecordId { get; set; }

    public bool IsMaunal { get; set; }

    public bool Conflict { get; set; }



    public void Merge(TransferMapping other)
    {
        if(Id == 0) Id = other.Id; 
        if (string.IsNullOrWhiteSpace(TransactionId))
            TransactionId = other.TransactionId;
        if (OrderId == 0) OrderId = other.OrderId;
        if (RequestId == 0) RequestId = other.RequestId;
        if (RecordId == 0) RecordId = other.RecordId;
    }
}