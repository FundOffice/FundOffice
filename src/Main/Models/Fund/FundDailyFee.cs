namespace FMO.Models;

/// <summary>
/// 每日计提费用
/// </summary>
public class FundDailyFee 
{

    public string Id => $"{FundCode}{Date}";


    public int FundId { get; set; }

    public string? Class { get; set; }

    public required string FundCode { get; set; }


    public DateOnly Date { get; set; }

    /// <summary>
    /// 管理费计提
    /// </summary>
    public decimal ManagerFeeAccrued { get; set; }
    
    /// <summary>
    /// 管理费支付
    /// </summary>
    public decimal ManagerFeePaid { get; set; }
    
    /// <summary>
    /// 管理费余额
    /// </summary>
    public decimal ManagerFeeBalance { get; set; }



    /// <summary>
    /// 托管费计提
    /// </summary>
    public decimal CustodianFeeAccrued { get; set; }

    /// <summary>
    /// 托管费支付
    /// </summary>
    public decimal CustodianFeePaid { get; set; }

    /// <summary>
    /// 托管费余额
    /// </summary>
    public decimal CustodianFeeBalance { get; set; }



    /// <summary>
    /// 外包费计提
    /// </summary>
    public decimal OutsourcingFeeAccrued { get; set; }

    /// <summary>
    /// 外包费支付
    /// </summary>
    public decimal OutsourcingFeePaid { get; set; }

    /// <summary>
    /// 外包费余额
    /// </summary>
    public decimal OutsourcingFeeBalance { get; set; }





    /// <summary>
    /// 业绩报酬费计提
    /// </summary>
    public decimal PerformanceFeeAccrued { get; set; }

    /// <summary>
    /// 业绩报酬费支付
    /// </summary>
    public decimal PerformanceFeePaid { get; set; }

    /// <summary>
    /// 业绩报酬费余额
    /// </summary>
    public decimal PerformanceFeeBalance { get; set; }




    /// <summary>
    /// 销售费计提
    /// </summary>
    public decimal SalesFeeAccrued { get; set; }

    /// <summary>
    /// 销售费支付
    /// </summary>
    public decimal SalesFeePaid { get; set; }

    /// <summary>
    /// 销售费余额
    /// </summary>
    public decimal SalesFeeBalance { get; set; }




    /// <summary>
    /// 投资顾问费计提
    /// </summary>
    public decimal ConsultantFeeAccrued { get; set; }

    /// <summary>
    /// 投资顾问费支付
    /// </summary>
    public decimal ConsultantFeePaid { get; set; }

    /// <summary>
    /// 投资顾问费余额
    /// </summary>
    public decimal ConsultantFeeBalance { get; set; }

     

}