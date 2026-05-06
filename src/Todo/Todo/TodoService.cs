using CommunityToolkit.Mvvm.Messaging;
using FMO.Utilities;
using LiteDB;
using System.Collections.Concurrent;

namespace FMO.Todo;

public static class TodoService
{
    private static ConcurrentDictionary<int, ITodo> _Todos = [];

    public static ITodo[]? GetAll()
    {
        List<ITodo> todoList = [];
        using var db = DbHelper.Base();
        var col = db.GetCollection<BsonDocument>("ITodo");
        foreach (var doc in col.Find($"$.{nameof(ITodo.Status)}='{nameof(TotoStatus.None)}'"))
        {
            try
            {
                ITodo todo = BsonMapper.Global.Deserialize<ITodo>(doc);

                todoList.Add(todo);
            }
            catch
            {
                // 类型不对、数据损坏 → 直接跳过
                continue;
            }
        }
        return todoList.ToArray();
    }

    public static void Register<T>(T todo) where T : ITodo
    {
        using var db = DbHelper.Base();

        // 如果UniqueId不为null，说明这是一个具有唯一标识的Todo，需要先将之前的同类Todo标记为已忽略
        var id = 0;
        if (todo.UniqueId is not null)
            id = db.GetCollection<ITodo>().FindOne(x => x.UniqueId == todo.UniqueId)?.Id ?? 0;
        //db.GetCollection<ITodo>().UpdateMany($"{{ '{nameof(ITodo.Status)}':'{nameof(TotoStatus.Ignored)}' }}", $"$.{nameof(ITodo.UniqueId)}='{todo.UniqueId}'");

        if (id > 0)
            db.GetCollection<ITodo>().Update(id, todo);
        else 
            db.GetCollection<ITodo>().Insert(todo);

        WeakReferenceMessenger.Default.Send((ITodo)todo);
    }

    public static void Unregister(int id)
    {
        using var db = DbHelper.Base();

        // 如果UniqueId不为null，说明这是一个具有唯一标识的Todo，需要先将之前的同类Todo标记为已忽略
        db.GetCollection<ITodo>().UpdateMany($"{{ '{nameof(ITodo.Status)}':'{nameof(TotoStatus.Ignored)}' }}", $"$.{nameof(ITodo.Id)}={id}");

        WeakReferenceMessenger.Default.Send(new TodoStatusMessage(id, TotoStatus.Ignored));
    }

    public static void Unregister(string uid)
    {
        using var db = DbHelper.Base();

        // 如果UniqueId不为null，说明这是一个具有唯一标识的Todo，需要先将之前的同类Todo标记为已忽略
        db.GetCollection<ITodo>().UpdateMany($"{{ '{nameof(ITodo.Status)}':'{nameof(TotoStatus.Ignored)}' }}", $"$.{nameof(ITodo.UniqueId)}='{uid}'");

        WeakReferenceMessenger.Default.Send(new TodoGroupStatusMessage(uid, TotoStatus.Ignored));
    }


    public static void Initialize()
    {
        using var db = DbHelper.Base();
        var col = db.GetCollection<BsonDocument>("ITodo");
        foreach (var doc in col.Find($"$.{nameof(ITodo.Status)}='{nameof(TotoStatus.None)}'"))
        {
            try
            {
                ITodo todo = BsonMapper.Global.Deserialize<ITodo>(doc);

                _Todos.TryAdd(todo.Id, todo);
            }
            catch
            {
                // 类型不对、数据损坏 → 直接跳过
                continue;
            }
        }

    }




}