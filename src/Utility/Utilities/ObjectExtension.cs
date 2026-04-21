using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;

namespace FMO.Utilities;

public static class ObjectExtension
{
    public static string PrintProperties(this object obj)
    {
        if (obj is null) return "Null Object";

        var type = obj.GetType();
        string info = string.Join(",\n\t", type.GetProperties().Select(x => $"{x.Name}:{x.GetValue(obj)?.ToString() ?? "null"}"));
        return $"{type.Name} \n\t {info} \n\n";
    }


    public static void UpdateFrom(this object des, object src, bool ignoreDefault = false)
    {
        if (src is null) return;

        var psd = des.GetType().GetProperties();
        var pss = src.GetType().GetProperties();

        foreach (var p in psd)
        {
            if (!p.CanWrite) continue;

            //是否有同名属性
            var ps = pss.FirstOrDefault(x => x.Name == p.Name && x.CanRead);
            if (ps is null) continue;

            var v = ps.GetValue(src);
            if (!ignoreDefault || v != default)
                p.SetValue(des, v);
        }
    }

    public static IEnumerable<T> Gather<T>(this IEnumerable<T> obj, IEnumerable<int> idx)
    {
        var sort = idx.Order().ToArray();
        foreach (var t in obj.Index())
            if (Array.BinarySearch(sort, t.Index) >= 0)
                yield return t.Item;
    }

    public static IEnumerable<T> TakeRevese<T>(this IEnumerable<T> obj, int[] idx)
    {
        var sort = idx.Order().ToArray();
        foreach (var t in obj.Reverse().Index())
            if (Array.BinarySearch(sort, t.Index) >= 0)
                yield return t.Item;
    }

    /// <summary>
    /// 把一个对象的所有属性展开成一个字典
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public static Dictionary<string, object> ExpandToDictionary(this object obj)
    {
        if (obj == null) return new Dictionary<string, object>();

        var dictionary = new Dictionary<string, object>();
        var type = obj.GetType();

        foreach (var property in GetProperties(type))
        {
            var value = property.GetValue(obj);

            
            if (property.PropertyType == typeof(bool?) && value is null)
            {
                dictionary.Add(property.Name, false);
                continue;
            }
            if (value is null) 
                continue; 

            if (value is IEnumerable enumerable && !(value is string))
            {
                var list = new List<object>();
                foreach (var item in enumerable)
                {
                    if (item is null)
                        continue;
                    else if (NeedExpand(item.GetType()))
                        list.Add(ExpandToDictionary(item));
                    else
                        list.Add((item));
                }
                dictionary.Add($"{property.Name}", list);
            }
            else if (NeedExpand(property.PropertyType))//(!IsSimpleType(property.PropertyType))
            {
                var innerDict = ExpandToDictionary(value);
                dictionary.Add($"{property.Name}", value.ToString() ?? "");
                foreach (var innerPair in innerDict)
                {
                    dictionary.Add($"{property.Name}_{innerPair.Key}", innerPair.Value);
                }
            }

            else
                dictionary.Add(property.Name, value);
        }

        return dictionary;
    }

    private static bool NeedExpand(Type type)
    {
        //if (type == typeof(CoolingPeriodInfo))
        ///     return false;

        if (type.FullName!.StartsWith("FMO"))
            return true;

        return false;
    }

    //private static bool IsSimpleType(Type type)
    //{
    //    // 处理可空类型
    //    if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
    //    {
    //        type = Nullable.GetUnderlyingType(type);
    //    }

    //    // 基本类型
    //    if (type.IsPrimitive ||
    //        type == typeof(string) ||
    //        type == typeof(decimal) ||
    //        type == typeof(DateTime) ||
    //        type == typeof(DateTimeOffset) ||
    //        type == typeof(TimeSpan) ||
    //        type == typeof(Guid) ||
    //        type.IsEnum)
    //    {
    //        return true;
    //    }

    //    // 数组和集合
    //    if (typeof(IEnumerable).IsAssignableFrom(type))
    //    {
    //        return true;
    //    }

    //    return false;
    //}

    private static IEnumerable<PropertyInfo> GetProperties(Type type)
    {
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
    }

    /// <summary>
    /// 把null值替换为指定的占位符
    /// </summary>
    /// <param name="dict"></param>
    /// <param name="mask"></param>
    public static Dictionary<string, object> ReplaceNullsWithPlaceholder(this Dictionary<string, object> dict, string mask = "未设置")
    {
#pragma warning disable CS8603 // 可能返回 null 引用。
        if (dict == null) return dict;
#pragma warning restore CS8603 // 可能返回 null 引用。

        var keys = dict.Keys.ToList(); // 避免修改时集合被修改
        foreach (var key in keys)
        {
            var value = dict[key];

            // 处理 null 值
            if (value == null)
                dict[key] = mask;


            // 递归处理嵌套字典
            else if (value is Dictionary<string, object> nestedDict)
            {
                ReplaceNullsWithPlaceholder(nestedDict, mask);
            }
            // 处理集合（列表、数组等）
            else if (value is IEnumerable enumerable && !(value is string))
            {
                ProcessCollection(enumerable, mask);
            }
        }
        return dict;
    }

    private static void ProcessCollection(IEnumerable collection, string mask)
    {
        foreach (var item in collection)
        {
            if (item == null) continue; // 集合中的 null 不处理（只处理字典中的）

            // 递归处理嵌套字典
            if (item is Dictionary<string, object> nestedDict)
            {
                ReplaceNullsWithPlaceholder(nestedDict, mask);
            }
            // 递归处理嵌套集合
            else if (item is IEnumerable nestedCollection && !(item is string))
            {
                ProcessCollection(nestedCollection, mask);
            }
        }
    }
}

public static class StringHelper
{
    private static readonly Regex BasicEmailRegex = new Regex(
        @"^[a-zA-Z0-9_.+-]+@[a-zA-Z0-9-]+\.[a-zA-Z0-9-.]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);


    public static bool IsMail(this string? mail)
    {
        if (mail is null) return false;
        return BasicEmailRegex.IsMatch(mail);
    }
}
