using System.Text.Json.Serialization;

public class SortItem
{
    /// <summary>
    /// 
    /// </summary>
    [JsonPropertyName("direction")]
    public string? Direction { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [JsonPropertyName("property")]
    public string? Property { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [JsonPropertyName("ignoreCase")]
    public bool IgnoreCase { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [JsonPropertyName("nullHandling")]
    public string? NullHandling { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [JsonPropertyName("ascending")]
    public bool Ascending { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [JsonPropertyName("descending")]
    public bool Descending { get; set; }
}
