namespace FMO.Todo;

public interface ITodo
{
    int Id { get; init; }

    DateTime CreateTime { get; }
    
    DateTime FinishTime { get; set; }
    

    bool JustNotify { get; }
    
    TotoStatus Status { get; set; }
    
    string? UniqueId { get; }
}