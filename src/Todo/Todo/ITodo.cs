namespace FMO.Todo;

public interface ITodo
{
    DateTime CreateTime { get; }
    
    DateTime FinishTime { get; set; }
    
    int Id { get; }

    bool JustNotify { get; }
    
    TotoStatus Status { get; set; }
    
    string? UniqueId { get; }
}