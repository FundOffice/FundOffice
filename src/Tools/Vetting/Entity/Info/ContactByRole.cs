namespace Vetting.Models.Entities;

/// <summary>
/// 按职能联系人
/// </summary>
public class ContactByRole
{
    public int Id { get; set; }
    public string? Role { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
}
