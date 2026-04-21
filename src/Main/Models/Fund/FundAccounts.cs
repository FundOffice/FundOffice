namespace FMO.Models;



public class AccountEvent
{
    public required string Name { get; set; }


}


public class AccountCredentialEvent : AccountEvent
{
    /// <summary>
    /// 资金账号
    /// </summary>
    public string? Account { get; set; }

    /// <summary>
    /// 交易密码
    /// </summary>
    public string? Password { get; set; }


}

/// <summary>
/// 开户事件
/// </summary>
public class OpenAccountEvent : AccountEvent
{
    /// <summary>
    /// 资金账号
    /// </summary>
    public string? Account { get; set; }

    /// <summary>
    /// 交易密码
    /// </summary>
    public string? TradePassword { get; set; }

    /// <summary>
    /// 资金密码
    /// </summary>
    public string? CapitalPassword { get; set; }

    /// <summary>
    /// 银证、银期等
    /// </summary>
    public SimpleFile? BankLetter { get; set; }


    /// <summary>
    /// 经服
    /// </summary>
    public SimpleFile? ServiceAgreement { get; set; }

    /// <summary>
    /// 开户信息
    /// </summary>
    public SimpleFile? AccountLetter { get; set; }
}


public record SecurityCardInfo(string? Card, bool Connected);

public record SecurityCardLink(int Id, SecurityCardType Type, string Card, int Account, bool Detatch = false);


/// <summary>
/// 股票账户
/// </summary>
public class StockAccount
{
    public int Id { get; set; }

    public int FundId { get; set; }

    /// <summary>
    /// 对应股卡组
    /// </summary>
    public int Group { get; set; }

    public string? Company { get; set; }

    /// <summary>
    /// 注销
    /// </summary>
    public bool IsClosed { get; set; }

    /// <summary>
    /// 上海股东卡
    /// </summary>
    //public SecurityCardInfo? SHCard { get; set; }

    /// <summary>
    /// 深圳股东卡
    /// </summary>
    //public SecurityCardInfo? SZCard { get; set; }

    /// <summary>
    /// 基本户
    /// </summary>
    public OpenAccountEvent? Common { get; set; }

    /// <summary>
    /// 信用户
    /// </summary>
    public OpenAccountEvent? Credit { get; set; }


    public List<AccountEvent> Events { get; set; } = [];

}

public class FutureAccount
{
    public int Id { get; set; }

    public int FundId { get; set; }

    public string? Company { get; set; }

    /// <summary>
    /// 注销
    /// </summary>
    public bool IsClosed { get; set; }

    /// <summary>
    /// 基本户
    /// </summary>
    public OpenAccountEvent? Common { get; set; }



    public List<AccountEvent>? Events { get; set; }

}






/// <summary>
/// 每个基金只有一个的账户
/// </summary>
public class FundSingletonAccounts
{
    public int Id { get; set; }

    /// <summary>
    /// 一码通
    /// </summary>
    public string? UniversalNo { get; set; }

    /// <summary>
    /// 统一开户编码
    /// </summary>
    public string? FutureNo { get; set; }

    /// <summary>
    /// 中债登债券账户
    /// </summary>
    public string? CCDCBondAccount { get; set; }

    /// <summary>
    /// 上清所债券账户
    /// </summary>
    public string? SHCBondAccount { get; set; }







}



/// <summary>
/// 募集、托管户等
/// </summary>
public class FundBankAccount : BankAccount
{
    public new string Id => Number ?? "";

    public int FundId { get; set; }

    public string? FundCode { get; set; }


    public FundAccountType Type { get; set; }

    /// <summary>
    /// 销户
    /// </summary>
    public bool IsCanceled { get; set; }


}