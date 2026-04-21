namespace FMO.Models;

public interface IEntity
{
    public int Id { get; set; }

    public string Name { get; set; }

    public Identity ? Identity { get; set; }

    public DateEfficient Efficient { get; set; }

    public string? Phone { get; set; }
}
