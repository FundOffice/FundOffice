using System;
using System.Collections.Generic;

namespace FMO.Models;

/// <summary>
/// 季度更新上报状态
/// </summary>
public class QuarterlyUpdateStatus
{
    public int Id { get; set; }


    public int FundId { get; set; }

    public DateOnly PeriodEnd { get; set; }


    /// <summary>
    /// 已提交
    /// </summary>
    public bool IsSubmitted { get; set; }

    /// <summary>
    /// 投资人信息报送部分（详细结构）
    /// </summary>
    public FillSection InvestorFill { get; set; } = new();

    /// <summary>
    /// 运行信息报送部分（详细结构）
    /// </summary>
    public FillSection OperationFill { get; set; } = new();

 
}

/// <summary>
/// 单个报送部分的状态结构
/// </summary>
public class FillSection
{
    /// <summary>
    /// 是否已完成该部分报送
    /// </summary>
    public bool IsFilled { get; set; }

    /// <summary>
    /// 汇总错误信息（简短文本）
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// 详细错误列表（便于界面展示或日志）
    /// </summary>
    public List<string> ErrorDetails { get; set; } = new();

    /// <summary>
    /// 最后更新时间（UTC）
    /// </summary>
    public DateTime? LastUpdated { get; set; }

    public void AddError(string error)
    {
        if (string.IsNullOrWhiteSpace(error)) return;
        ErrorDetails.Add(error);
        Error = string.Join("; ", ErrorDetails);
        LastUpdated = DateTime.UtcNow;
    }

    public void ClearErrors()
    {
        ErrorDetails.Clear();
        Error = null;
        LastUpdated = DateTime.UtcNow;
    }
}
