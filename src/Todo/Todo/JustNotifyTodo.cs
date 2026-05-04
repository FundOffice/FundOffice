namespace FMO.Todo;

public class JustNotifyTodo : ITodo
{ 
    public bool JustNotify => true; 

    public string? UniqueId { get; set; }

    public string? Message { get; set; }

    public DateTime CreateTime { get; set; } = DateTime.Now;

    public DateTime FinishTime { get; set; }

    public int Id { get; init; }

    public TotoStatus Status { get; set; }
}