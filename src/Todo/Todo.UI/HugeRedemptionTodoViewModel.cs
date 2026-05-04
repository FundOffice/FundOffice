using CommunityToolkit.Mvvm.Input;
using FMO.Models;

namespace FMO.Todo;












[AutoViewModel(typeof(HugeRedemptionTodo))]
public partial class HugeRedemptionTodoViewModel : TodoViewModel
{



    [RelayCommand]
    public void GenerateNotice()
    {
        // 生成赎回公告的逻辑
    }
}



[AutoViewModel(typeof(FundElementFillTodo))]
public partial class FundElementFillTodoViewModel : TodoViewModel
{
    // 这里可以添加特定于 FundFundElementFillTodo 的属性和方法
}


[AutoViewModel(typeof(FundElementMissingTodo))]
public partial class FundElementMissingTodoViewModel : TodoViewModel
{
    // 这里可以添加特定于 FundFundElementFillTodo 的属性和方法
}

[AutoViewModel(typeof(JustNotifyTodo))]
public partial class JustNotifyTodoViewModel:TodoViewModel
{

}