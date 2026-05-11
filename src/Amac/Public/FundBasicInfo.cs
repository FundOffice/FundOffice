namespace FMO.AMAC;

/// <summary>
/// 此类用于初始化程序，从AMAC获取公示中的基金基本信息
/// </summary>
public class FundBasicInfo
{
    public string? Name { get; set; }

    /// <summary>
    /// 暂行办法前的基金
    /// </summary>
    public bool IsPreRule { get; set; }


    /// <summary>
    /// 投顾
    /// </summary>
    public bool IsAdvisor { get; set; }

    /// <summary>
    /// /fund/.....html
    /// </summary>
    public string? Url { get; set; }


    public int Monthly { get; set; }

    public int Quarterly { get; set; }

    public int SemiAnnally { get; set; }

    public int Annally { get; set; }

    public double InvestorAccountRate { get; set; }
}