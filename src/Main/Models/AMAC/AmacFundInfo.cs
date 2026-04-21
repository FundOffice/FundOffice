using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FMO.Models;

public class AmacFundInfo
{

    // ================= 基本信息 =================
    #region 基本信息
    /// <summary>
    /// 产品名称（全称）
    /// </summary>
    public required string FullName { get; set; }

    /// <summary>
    /// 产品简称
    /// </summary>
    public required string ShortName { get; set; }

    /// <summary>
    /// 产品编码 (通常是自动生成或只读)
    /// </summary>
    public required string Code { get; set; }

    /// <summary>
    /// 基金类型 (例如: "0" 代表私募证券投资基金)
    /// </summary>
    public FundType FundType { get; set; }

    /// <summary>
    /// 业务模式 (例如: "0" 代表既募集又投资)
    /// </summary>
    public ConductType Conduct { get; set; }

    /// <summary>
    /// 产品类型 (例如: "5" 代表权益类)
    /// </summary>
    public SecurityFundType ProductType { get; set; }

    /// <summary>
    /// 是否量化/对冲基金 (例如: "PA01" 代表量化)
    /// </summary>
    public QuantHedgeType ProAttributeOfValue { get; set; }

    // ================= 投资策略 =================
    /// <summary>
    /// 投资策略（可多选）
    /// </summary>
    public List<AmacInvestmentStrategy> InvestStrategy { get; set; } = [];


    // ================= 组织与日期信息 =================
    /// <summary>
    /// 组织形式 (例如: "0" 代表契约型)
    /// </summary>
    public OrganizationForm OrgForm { get; set; }

    /// <summary>
    /// 基金成立日期
    /// </summary>
    public DateOnly FoundDate { get; set; }

    /// <summary>
    /// 基金到期日
    /// </summary>
    public DateOnly DueDate { get; set; }

    /// <summary>
    /// 募集完成日
    /// </summary>
    public DateOnly RaiseEndDate { get; set; }

    /// <summary>
    /// 币种 (例如: "0" 代表人民币)
    /// </summary>
    public CurrencyType Currency { get; set; }

    /// <summary>
    /// 是否涉及跨境投资
    /// </summary>
    public bool IsCrossBorderInvt { get; set; }

    /// <summary>
    /// 基金份额（万份） - 仅当组织形式为契约型时适用
    /// </summary>
    public decimal? CapitailContribution { get; set; }

    // ================= 合规与特殊情形 =================
    /// <summary>
    /// 是否存在保本情形
    /// </summary>
    public bool IsGuaranSituation { get; set; }

    /// <summary>
    /// 是否存在保收益情形
    /// </summary>
    public bool IsGuaranIncome { get; set; }

    /// <summary>
    /// 是否仅投资单一标的
    /// </summary>
    public bool IsInvestInSingleTarget { get; set; }

    /// <summary>
    /// 是否涉及关联交易
    /// </summary>
    public bool IsConnectedTransaction { get; set; }

    /// <summary>
    /// 是否通过SPV进行投资
    /// </summary>
    public bool IsSpvInvest { get; set; }

    // ================= 描述性信息 =================
    /// <summary>
    /// 主要投资方向
    /// </summary>
    public string? InvestDirection { get; set; }

    /// <summary>
    /// 管理人（投顾）认为需要说明的其他问题
    /// </summary>
    public string? OtherProblems { get; set; }

    // ================= 联系人信息 =================
    /// <summary>
    /// 备案主要联系人
    /// </summary>
    public AmacContactInfo? ProductMainContacts { get; set; }

    /// <summary>
    /// 备案备用联系人
    /// </summary>
    public AmacContactInfo? ProductMinorContacts { get; set; }
    #endregion

    #region 结构化
    /// <summary>
    /// 是否结构化
    /// </summary>
    public bool IsStructured { get; set; }

    /// <summary>
    /// 杠杆比例 (如: 1.5, 2.0)
    /// </summary>
    public decimal LeverageRatio { get; set; }

    /// <summary>
    /// 份额类别信息列表
    /// </summary>
    public List<AmacShareClassInfo> ShareClasses { get; set; } = new List<AmacShareClassInfo>();

    /// <summary>
    /// 关于结构化安排的其他说明	
    /// </summary>
    public string? StructureStatement { get; set; } 
    #endregion
}




/// <summary>
/// 联系人信息嵌套类
/// </summary>
/// <param name="ContactName"> 姓名 </param>
/// <param name="ContactPhone"> 座机 </param>
/// <param name="ContactMobile"> 手机 </param>
/// <param name="ContactEmail"> 邮箱 </param>
public record AmacContactInfo(string ContactName, string ContactPhone, string ContactMobile, string ContactEmail);

