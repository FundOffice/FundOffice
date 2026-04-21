using System.Diagnostics.CodeAnalysis;

namespace FMO.Models;


public class DataExtra<T> where T : struct
{
    public T? Data { get; set; }

    public string? Extra { get; set; }
}



public class Mutable<T> where T : notnull
{
    /// <summary>
    /// 名字
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// 描述
    /// </summary>
    //public string? Description { get; set; }



    public SortedDictionary<int, T> Changes { get; set; } = new();

    public T Value => Changes.LastOrDefault().Value;

    [SetsRequiredMembers]
    public Mutable(string name, T value)
    {
        Name = name;
        //Description = description;
        //_changes = new();

        //_changes.Add(-1, value);
    }

    [SetsRequiredMembers]
    public Mutable(string name)
    {
        Name = name;
        //_changes = new();
    }

    public void SetValue(T value, int flowid)
    {
        Changes[flowid] = value;
    }

    public void RemoveValue(int flowid)
    {
        Changes.Remove(flowid);
    }

    public (int FlowId, T? Value) GetValue(int flowid)
    {
        foreach (var x in Changes.Reverse())
        {
            if (x.Key <= flowid)
                return (x.Key, x.Value);
        }
        return (-1, default);
    }

    public bool HasValue(T? val)
    {
        return val is null ? false : Changes.Any(x => x.Value is not null && x.Value.Equals(val));
    }

    public static implicit operator T?(Mutable<T>? mutable)
    {
        return mutable is null ? default : mutable.Value;
    }

    public override string? ToString()
    {
        return Value?.ToString() ?? base.ToString();
    }
}


//public class LabledValue<T>
//{
//    public string Label { get; set; }
//    public T Value { get; set; }
//}


public interface IPortionMutable
{
    void RemoveValue(int share, int flowid);

    void SwitchToSingle(int share, int flowid);

}

public class PortionMutable<T> : Mutable<Dictionary<int, T>>, IPortionMutable where T : notnull
{
    [SetsRequiredMembers]
    public PortionMutable(string name) : base(name, new())
    {
    }

    public void SetValue(int share, T value, int flowid)
    {
        if (!Changes.ContainsKey(flowid))
            Changes.Add(flowid, new());

        var dic = Changes[flowid];

        dic[share] = value;
    }

    public (int FlowId, T? Value) GetValue(int share, int flowid )
    {
        foreach (var x in Changes.Reverse())
        {
            if (x.Key > flowid) continue;

            if (x.Value.ContainsKey(share))
                return (x.Key, x.Value[share]);

            /// 如果只有一个值
            if (x.Value.Count == 1 && x.Value.First().Key == -1)
                return (x.Key, x.Value.First().Value);
        }

        return (-1, default);
    }

    public void RemoveValue(int share, int flowid)
    {
        if (!Changes.ContainsKey(flowid))
            return;

        var dic = Changes[flowid];
        dic.Remove(share);
    }

    /// <summary>
    /// share变成单一份额，id 改成-1
    /// </summary>
    /// <param name="share"></param>
    /// <param name="flowid"></param>
    public void SwitchToSingle(int share, int flowid)
    {
        if (!Changes.ContainsKey(flowid))
            return;

        var dic = Changes[flowid];

        if (dic.ContainsKey(share))
        {
            dic[-1] = dic[share];
            dic.Remove(share);
        }
    }
}