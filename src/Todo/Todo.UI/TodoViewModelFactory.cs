namespace FMO.Todo;

public static partial class TodoViewModelFactory
{
    private static Dictionary<Type, Func<TodoViewModel>> _creators = [];

    private static Dictionary<Type, Func<Todo,TodoViewModel>> _creators2 = [];


    public static void Register<T>(Func<TodoViewModel> func) where T : Todo
    {
        _creators.Add(typeof(T), func);
    }

    public static void Register<T>(Func<Todo, TodoViewModel> func) where T : Todo
    {
        _creators2.Add(typeof(T), func);
    }

    public static TodoViewModel? Create(Type type) => _creators.TryGetValue(type, out var viewModel) ? viewModel() : null;


    public static TodoViewModel? Create<T>(T Todo) where T : Todo => _creators2.TryGetValue(Todo.GetType(), out var viewModel) ? viewModel(Todo) : null;

     
}