using FMO.Models;

namespace FMO.Utilities;

public static class TAHelper
{

    public static bool IsBuy(this TransferRequestType type)
    {
        return type switch { TransferRequestType.Purchase or TransferRequestType.Subscription => true, _ => false };
    }
    public static bool IsSell(this TransferRequestType type)
    {
        return type switch { TransferRequestType.Redemption or TransferRequestType.ForceRedemption => true, _ => false };
    }
    public static bool RequiredOrder(this TransferRequestType type)
    {
        return type switch { TransferRequestType.Purchase or TransferRequestType.Subscription or TransferRequestType.Redemption or TransferRequestType.ForceRedemption => true, _ => false };
    }

    public static bool IsBuy(this TransferRequest r) => r.RequestType.IsBuy();

    public static bool IsSell(this TransferRequest r) => r.RequestType.IsSell();

    public static bool RequiredOrder(this TransferRequest r) => r.RequestType.RequiredOrder();


    public static bool IsBuy(this TransferRecordType type)
    {
        return type switch { TransferRecordType.Purchase or TransferRecordType.Subscription => true, _ => false };
    }
    public static bool IsSell(this TransferRecordType type)
    {
        return type switch { TransferRecordType.Redemption or TransferRecordType.ForceRedemption => true, _ => false };
    }

    public static bool RequiredOrder(this TransferRecordType type)
    {
        return type switch { TransferRecordType.Purchase or TransferRecordType.Subscription or TransferRecordType.Redemption or TransferRecordType.ForceRedemption => true, _ => false };
    }


    public static bool IsBuy(this TransferRecord r) => r.Type.IsBuy();

    public static bool IsSell(this TransferRecord r) => r.Type.IsSell();

    public static bool RequiredOrder(this TransferRecord r) => r.Type.RequiredOrder();


    public static bool IsBuy(this TransferOrderType r) => r switch { TransferOrderType.FirstTrade or TransferOrderType.Buy => true, _ => false };

    public static bool IsSell(this TransferOrderType r) => r switch { TransferOrderType.RemainAmout or TransferOrderType.Share or TransferOrderType.Amount => true, _ => false };


    public static bool IsBuy(this TransferOrder r) => r.Type.IsBuy();

    public static bool IsSell(this TransferOrder r) => r.Type.IsSell();

    public static bool IsCompatible(this TransferOrder o, TransferRequest r) => (o.IsBuy() && r.IsBuy()) || (o.IsSell() && r.IsSell());
    public static bool IsCompatible(this TransferRequest o, TransferOrder r) => (o.IsBuy() && r.IsBuy()) || (o.IsSell() && r.IsSell());

    public static bool IsCompatible(this TransferOrder o, TransferRecord r) => (o.IsBuy() && r.IsBuy()) || (o.IsSell() && r.IsSell());
    public static bool IsCompatible(this TransferRecord o, TransferOrder r) => (o.IsBuy() && r.IsBuy()) || (o.IsSell() && r.IsSell());


    public static void ReplaceAndKeep(this TransferRecord r, TransferRecord old)
    {
        if (r.Id == 0) r.Id = old.Id;
        if (r.RequestId == 0) r.RequestId = old.RequestId;
        if (r.OrderId == 0) r.OrderId = old.OrderId;
        if (!r.IsLiquidating) r.IsLiquidating = old.IsLiquidating;
        if (r.ConfirmFile is null) r.ConfirmFile = old.ConfirmFile;
    }

    public static void ReplaceAndKeep(this TransferRequest r, TransferRequest old)
    {
        if (r.Id == 0) r.Id = old.Id;
        if (r.OrderId == 0) r.OrderId = old.OrderId;
        if (!r.IsLiquidating) r.IsLiquidating = old.IsLiquidating;
    }
}
