using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FMO.Models;
using FMO.Utilities;

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
public partial class JustNotifyTodoViewModel : TodoViewModel
{

}


[AutoViewModel(typeof(PeriodicalUnreportedTodo))]
public partial class PeriodicalUnreportedTodoViewModel : TodoViewModel
{
    //  Message = "以下报告未报送，今日报送的也会显示，不必紧张\n" + string.Join("\n", messages)

    public string Message => Info();

    private string Info()
    {
        string msg = "";

        if (Monthly?.Length is > 0 and < 5)
            msg += "月报：" + string.Join("、", Monthly.Select(x => Fund.GetDefaultShortName(x)));
        else if (Monthly?.Length >= 5)
            msg += $"月报：{Monthly?.Length}个产品未报";

        if (Quarterly?.Length is > 0 and < 5)
            msg += "\n季报：" + string.Join("、", Quarterly.Select(x => Fund.GetDefaultShortName(x)));
        else if (Quarterly?.Length >= 5)
            msg += $"\n季报：{Quarterly?.Length}个产品未报";

        if (SemiAnnually?.Length is > 0 and < 5)
            msg += "\n半年报：" + string.Join("、", SemiAnnually.Select(x => Fund.GetDefaultShortName(x)));
        else if (SemiAnnually?.Length >= 5)
            msg += $"\n半年报：{SemiAnnually?.Length}个产品未报";

        if (Annually?.Length is > 0 and < 5)
            msg += "\n年报：" + string.Join("、", Annually.Select(x => Fund.GetDefaultShortName(x)));
        else if (Annually?.Length >= 5)
            msg += $"\n年报：{Annually?.Length}个产品未报";

        return msg;
    }


    [RelayCommand]
    public void Open()
    {
        WeakReferenceMessenger.Default.Send(new OpenPageMessage("Disclosure"));
    }
}