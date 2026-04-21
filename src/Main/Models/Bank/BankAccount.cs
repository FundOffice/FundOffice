using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.AccessControl;
using System.Text;
using System.Text.RegularExpressions;

namespace FMO.Models;





public class BankAccountStringConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string);
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        return value is string s ? BankAccount.FromString(s) : base.ConvertFrom(context, culture, value);
    }

    public override bool CanConvertTo(ITypeDescriptorContext? context, [NotNullWhen(true)] Type? destinationType)
    {
        return destinationType == typeof(string);
    }

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        return destinationType == typeof(string) ? value?.ToString() : base.ConvertTo(context, culture, value, destinationType);
    }
}



[TypeConverter(typeof(BankAccountStringConverter))]
public class BankAccount
{
    public int Id { get; set; }


   // public int OwnerId { get; set; }



    /// <summary>
    /// 户名
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// 账号
    /// </summary>
    public string? Number { get; set; }

    /// <summary>
    /// 银行
    /// </summary>
    public string? Bank { get; set; }

    /// <summary>
    /// 银行-支行
    /// </summary>
    public string? Branch { get; set; }

    /// <summary>
    /// 开户行
    /// </summary>
    public string? BankOfDeposit { get => Bank + Branch; set => SetDeposit(value); }


    public string? LargePayNo { get; set; }

    /// <summary>
    /// swift
    /// </summary>
    public string? SwiftCode { get; set; }

    /// <summary>
    /// 银行地址
    /// </summary>
    public string? BankAddress { get; set; }

    /// <summary>
    /// 注销
    /// </summary>
    public bool IsClosed { get; set; }


    private void SetDeposit(string? str)
    {
        if (string.IsNullOrWhiteSpace(str)) return;
        var m = Regex.Match(str, @"(\w+银行)(?:.*公司)?(\w+)?");
        if (!m.Success) return;

        Bank = m.Groups[1].Value;
        if (m.Groups.Count > 2)
            Branch = m.Groups[2].Value;
    }

    public static BankAccount? FromString(string str)
    {
        BankAccount account = new BankAccount();
        var m = Regex.Match(str, @"(?:账\s*号|账\s*户\s*号\s*码)\s*[:：\s]*(\d{5,})");
        if (!m.Success) return null;
        account.Number = m.Groups[1].Value;

        m = Regex.Match(str, @"(?:账\s*)?户\s*名\s*(?:称)?\s*[:：\s]*\s*([\w--]+)");
        if (!m.Success) return null;
        account.Name = m.Groups[1].Value;

        m = Regex.Match(str, @"(\w+银行)(?:.*公司)?(\w+)");
        if (!m.Success) return null;
        account.BankOfDeposit = m.Value;


        m = Regex.Match(str, @"大\s*额\s*支\s*付\s*(?:系统)?号\s*[:：\s]*(\w+)");
        if (m.Success) account.LargePayNo = m.Groups[1].Value;

        return account;
    }


    public void FillFrom(string? str)
    {
        if (string.IsNullOrWhiteSpace(str)) return;

        var m = Regex.Match(str, @"[账号|账户号码]\s*[:：]*(\d+)");
        if (m.Success)
            Number = m.Groups[1].Value;

        m = Regex.Match(str, @"(?:账)?户名(?:称)?\s*[:：]*(\w+)");
        if (m.Success)
            Name = m.Groups[1].Value;

        m = Regex.Match(str, @"(\w+银行)(?:.*公司)?(\w+)");
        if (m.Success)
            BankOfDeposit = m.Value;

        m = Regex.Match(str, @"大额支付号?\s*[:：]*(\w+)");
        if (m.Success) LargePayNo = m.Groups[1].Value;
    }


    public override string ToString()
    {
        StringBuilder builder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(Name))
            builder.Append($"户名：{Name}\n");

        if (!string.IsNullOrWhiteSpace(Number))
            builder.Append($"账号：{Number}\n");

        if (!string.IsNullOrWhiteSpace(BankOfDeposit))
            builder.Append($"开户行：{BankOfDeposit}\n");

        if (!string.IsNullOrWhiteSpace(LargePayNo))
            builder.Append($"大额支付号：{LargePayNo}\n");

        if (!string.IsNullOrWhiteSpace(SwiftCode))
            builder.Append($"SWIFT：{SwiftCode}\n");

        return builder.ToString().Trim();
    }
}



public class InvestorBankAccount : BankAccount
{

    public int OwnerId { get; set; }

}







/// <summary>
/// 账户余额
/// </summary>
public class BankBalance
{
    public int Id { get; set; }

    public required string AccountName { get; set; }

    public required string AccountNo { get; set; }

    public required string Name { get; set; }

    public decimal Balance { get; set; }

    public DateTime Time { get; set; }

    public string Currency { get; set; } = "RMB";
}


public class FundBankBalance : BankBalance
{
    public int FundId { get; set; }

    public string? FundCode { get; set; }

    public string? FundName { get; set; }

}