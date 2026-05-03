namespace FMO.Todo;

public class Todo : ITodo
{
    public int Id { get; init; }

    /// <summary>
    /// 特定的类型下，唯一标识一个待办事项的字符串。可以用来去重。
    /// </summary>
    public virtual string? UniqueId { get; }

    public virtual bool JustNotify => false;


    public DateTime CreateTime { get; init; }

    public DateTime FinishTime { get; set; }

    public TotoStatus Status { get; set; }

}


public enum TotoStatus
{
    None,

    Ignored,

    Finished
}

public record TodoStatusMessage(int Id, TotoStatus Status);