namespace FMO.FeeCalc;

/// <summary>
/// 分配方案
/// </summary>
public class DividendPlan
{

    public List<ProfitAllocation> Plan { get; set; } = [];


}


/// <summary>
/// 分配方案项
/// </summary>
/// <param name="TargetId"> 投资人  </param>
/// <param name="Name">员工、代销、公司等</param>
/// <param name="Ratio">分配比例</param>
public record ProfitAllocation(int TargetId, string Name, decimal Ratio);
