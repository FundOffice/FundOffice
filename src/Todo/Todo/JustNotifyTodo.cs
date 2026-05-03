namespace FMO.Todo;

public class JustNotifyTodo : Todo
{
    public override bool JustNotify => true;

    public string? Message { get; set; }
}