using FMO.Models;

namespace FMO.Trustee;

#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
public class SubjectFundMappingJson
{
    /// <summary>
    /// 产品代码
    /// </summary>
    public string fundcode { get; set; }

    /// <summary>
    /// 产品名称
    /// </summary>
    public string fundname { get; set; }

    /// <summary>
    /// 成立日期
    /// </summary>
    public string setupdate { get; set; }

    /// <summary>
    /// 产品状态
    /// </summary>
    public string pdtstatus { get; set; }

    /// <summary>
    /// 协会备案代码
    /// </summary>
    public string xhbadm { get; set; }

    /// <summary>
    /// 分级类型
    /// </summary>
    public string investfunds { get; set; }

    /// <summary>
    /// 是否分级
    /// </summary>
    public string ifgrading { get; set; }

    /// <summary>
    /// 母基金代码
    /// </summary>
    public string parenttacode { get; set; }

    /// <summary>
    /// 母基金名称
    /// </summary>
    public string parentfundname { get; set; }

    public SubjectFundMapping ToObject()
    {
        var sc = ifgrading == "1" ? fundname.Replace(parentfundname, "") : "";
        return new SubjectFundMapping { AmacCode = xhbadm, FundCode = fundcode, FundName = fundname, MasterCode = parenttacode, MasterName = parentfundname, ShareClass = sc, Status = ParseStauts(pdtstatus) };
    }

    private FundStatus ParseStauts(string pdtstatus)
    {
        switch (pdtstatus)
        {
            case "1": return FundStatus.Setup;
            case "2": return FundStatus.Normal;
            case "3":
            case "4":
                return FundStatus.Liquidation;
            default: return FundStatus.Unk;
        }
    }
}


#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。