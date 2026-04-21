using LiteDB;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace FMO.Models;






public enum DailySource
{
    [Description("手工")]
    Manual,

    [Description("托管")]
    Custodian,

    [Description("估值表")]
    Sheet
}


public class DailyValue
{
    /// <summary>
    /// 默认-1，避免被赋给第一个
    /// </summary>
    public int FundId { get; set; }

    public string? Class { get; set; }

    [BsonId] public long Id => Date.DayNumber;// GenerateId(FundId, Class, Date);


    public DateOnly Date { get; set; }

    public decimal NetValue { get; set; }

    public decimal CumNetValue { get; set; }

    public decimal Asset { get; set; }

    public decimal NetAsset { get; set; }

    /// <summary>
    /// 负债
    /// </summary>
    public decimal Liability => Asset - NetAsset;

    public decimal Share { get; set; }

    public DailySource Source { get; set; }

    /// <summary>
    /// 估值表路径
    /// </summary>
    public string?  SheetPath { get; set; }

    public bool IsAvailiable() => NetValue > 0;

    public long GenerateId(int fundId, string? @class, DateOnly date) => ((long)fundId << 48) | ((long)date.DayNumber << 16) | ComputeStableHash16(@class);

    private const uint FnvPrime = 16777619;
    private const uint FnvOffsetBasis = 2166136261;
    private static ushort ComputeStableHash16(string? input)
    {
        if (string.IsNullOrEmpty(input)) return 0;

        // 使用 UTF-8 编码确保跨平台一致性
        byte[] bytes = Encoding.UTF8.GetBytes(input);
        uint hash = FnvOffsetBasis;

        foreach (byte b in bytes)
        {
            hash ^= b;
            hash *= FnvPrime;
        }

        // 折叠 32 位哈希到 16 位 (XOR folding)
        return (ushort)((hash >> 16) ^ (hash & 0xFFFF));
    }
}


/// <summary>
/// 每日管理规模
/// </summary>
/// <param name="Date"></param>
/// <param name="Scale"></param>
public record DailyManageSacle(DateOnly Date, decimal Scale)
{
    public int Id => Date.DayNumber;
}