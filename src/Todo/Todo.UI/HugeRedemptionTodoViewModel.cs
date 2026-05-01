using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FMO.Models;
using FMO.Utilities;

namespace FMO.Todo;

[AutoViewModel(typeof(Todo))]
public partial class TodoViewModel
{


    [RelayCommand]
    public void Ignore()
    {
        Status = TotoStatus.Ignored;
        Save();
        WeakReferenceMessenger.Default.Send(new TodoStatusMessage(Id, Status));
    }

    protected void Save()
    {
        using var db = DbHelper.Base();
        var obj = Build();
        db.GetCollection<Todo>().Upsert(obj); 
    }
}












[AutoViewModel(typeof(HugeRedemptionTodo))]
public partial class HugeRedemptionTodoViewModel : TodoViewModel
{



    [RelayCommand]
    public void GenerateNotice()
    {
        // 生成赎回公告的逻辑
    }
}
