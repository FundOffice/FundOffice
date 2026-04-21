namespace FMO.Models;

public class Participant
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public PersonRole Role { get; set; }

    public Identity? Identity { get; set; }

    public DateEfficient Efficient { get; set; }

    /// <summary>
    /// 证书编号
    /// </summary>
    public string? CertCode { get; set; }

    public string? Post { get; set; }

    /// <summary>
    /// 称谓
    /// </summary>
    public string? Title { get; set; }
     
    /// <summary>
    /// 
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// 地址
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// 简介
    /// </summary>
    public string? Profile { get; set; }
}


public record ParticipantChangedMessage(int Id);