using System.Text.Json.Serialization;

public class QueryManagers
{
    /// <summary>
    /// 
    /// </summary>
    [JsonPropertyName("content")]
    public List<ManagerInfo>? Content { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [JsonPropertyName("last")]
    public bool Last { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [JsonPropertyName("totalElements")]
    public int TotalElements { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [JsonPropertyName("totalPages")]
    public int TotalPages { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [JsonPropertyName("numberOfElements")]
    public int NumberOfElements { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [JsonPropertyName("sort")]
    public List<SortItem>? Sort { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [JsonPropertyName("first")]
    public bool First { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [JsonPropertyName("size")]
    public int Size { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [JsonPropertyName("number")]
    public int Number { get; set; }
}