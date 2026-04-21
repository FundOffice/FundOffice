using System.Text.Json.Serialization;

namespace FMO.Trustee.JsonCSC;


#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。

public class RetJson
{
    [JsonPropertyName("retCode")]
    public string Code { get; set; }


    [JsonPropertyName("retMsg")]
    public string Msg { get; set; }

}




public class RetJson<T>
{
    [JsonPropertyName("retCode")]
    public string Code { get; set; }


    [JsonPropertyName("retMsg")]
    public string Msg { get; set; }

    [JsonPropertyName("data")]
    public required DataJsonWrap<T> Data { get; set; }
}

public class DataJsonWrap<T>
{

    [JsonPropertyName("result")]
    public required List<T> Data { get; set; }

    [JsonPropertyName("rowCount")]
    public int RowCount { get; set; }


    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }
}


public interface IJson<T>
{
    T ToObject();
}







#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。