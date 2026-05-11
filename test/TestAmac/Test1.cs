using FMO.Models;
using FMO.Utilities;
using System.Collections;
using System.Diagnostics;
using System.Reflection;

namespace TestAmac;

[TestClass]
public sealed class Test1
{
    [TestMethod]
    public async Task TestMethod1()
    {

        List<FMO.AMAC.FundBasicInfo> list = [];
        Manager manager = new() { AmacId = "101000048334", Name = "", RegisterNo = "" };
        await FMO.AMAC.AmacHtml.CrawleManagerInfo(manager, list);


        manager.Print();


        foreach (var item in list)
        {
            item.Print();
        }


    }


    private void Print(object o)
    {

    }


}

public static class ObjectPrinter
{
    public static void Print(this object obj)
    {
        PrintObject(obj, 0);
    }

    /// <summary>
    /// 递归打印对象（支持集合、嵌套对象）
    /// </summary>
    private static void PrintObject(object obj, int indentLevel)
    {
        string indent = new string(' ', indentLevel * 2);

        if (obj == null)
        {
            Debug.WriteLine($"{indent}null");
            return;
        }

        Type type = obj.GetType();
        Debug.Write($"{indent}[{type.Name}]");

        // 1. 如果是字符串/数值/布尔等基础类型，直接打印
        if (type.IsPrimitive || obj is string or DateTime or DateOnly or decimal)
        {
            Debug.WriteLine($"{indent}  : {obj}");
            return;
        }

        // 2. 如果是集合（数组、List、Dictionary等）
        if (obj is IEnumerable enumerable && !(obj is string))
        {
            int index = 0;
            foreach (var item in enumerable)
            {
                Debug.WriteLine($"{indent}  [索引 {index++}]");
                PrintObject(item, indentLevel + 2);
            }
            return;
        }

        if (indentLevel > 2) return;

        Debug.WriteLine("");

        // 3. 普通对象：打印所有属性
        PropertyInfo[] properties = type.GetProperties();
        foreach (PropertyInfo prop in properties)
        {
            try
            {
                object value = prop.GetValue(obj);
                Debug.Write($"{indent}  {prop.Name} = ");
                PrintObject(value, indentLevel + 1);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{indent}  {prop.Name} = 获取值失败: {ex.Message}");
            }
        }
    }
}