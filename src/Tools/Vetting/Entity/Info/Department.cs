namespace Vetting.Models.Entities;

/// <summary>
/// 部门信息
/// </summary>
public class Department
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Headcount { get; set; }
    public string? MainFunction { get; set; }
    public string? Head { get; set; }
    public string? RecruitmentPlan { get; set; }
    public string? HasPartTime { get; set; }
}
