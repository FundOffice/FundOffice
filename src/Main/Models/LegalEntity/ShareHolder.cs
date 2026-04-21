namespace FMO.Models;

 

public class Ownership
{
    public int Id { get; set; }

    /// <summary>
    /// 持有人
    /// </summary>
    public int HolderId { get; set; }


    /// <summary>
    /// 持股的机构、公司
    /// 0 表示 管理人
    /// </summary>
    public int InstitutionId { get; set; }


    public decimal Share { get; set; }

    public decimal Ratio { get; set; }

}