namespace FMO.Models;

public class ManagerFlow
{
    public int Id { get; set; }

    public DateOnly Date { get; set; }

    public string? Title { get; set; }

    public string? Description { get; set; }

    public bool IsFinished { get; set; }
}