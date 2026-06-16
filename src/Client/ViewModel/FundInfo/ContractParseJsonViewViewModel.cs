using CommunityToolkit.Mvvm.ComponentModel;
using FMO.Models;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace FMO;

public partial class ContractParseJsonViewViewModel : ObservableObject
{
    public string Provider { get; }

    public DateTime ParsedAt { get; }

    public string FileHash { get; }

    [ObservableProperty]
    public partial ObservableCollection<ContractParseProperty> Properties { get; set; }

    public ContractParseJsonViewViewModel(ContractParseHistory history)
    {
        Provider = history.Provider;
        ParsedAt = history.ParsedAt;
        FileHash = history.FileHash;
        Properties = BuildProperties(history.FundInfoJson);
    }

    private static ObservableCollection<ContractParseProperty> BuildProperties(string json)
    {
        var properties = new ObservableCollection<ContractParseProperty>();
        if (string.IsNullOrWhiteSpace(json)) return properties;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in root.EnumerateObject())
                {
                    var (value, confidence) = TryUnwrapConfidence(prop.Value);
                    properties.Add(new ContractParseProperty
                    {
                        Title = prop.Name,
                        Confidence = confidence,
                        Items = BuildItems(prop.Name, value)
                    });
                }
            }
            else
            {
                properties.Add(new ContractParseProperty
                {
                    Title = "(root)",
                    Items = BuildItems("(root)", root)
                });
            }
        }
        catch
        {
            properties.Add(new ContractParseProperty
            {
                Title = "(解析失败)",
                Items = [[new ContractParseProperty.PropertyWithValue("原始文本", json)]]
            });
        }

        return properties;
    }

    private static (JsonElement value, decimal confidence) TryUnwrapConfidence(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return (element, 0);

        JsonElement? valueProp = null;
        JsonElement? confidenceProp = null;

        foreach (var prop in element.EnumerateObject())
        {
            if (prop.NameEquals("Value"))
                valueProp = prop.Value;
            else if (prop.NameEquals("Confidence"))
                confidenceProp = prop.Value;
            else
                return (element, 0);
        }

        if (valueProp is null || confidenceProp is null || confidenceProp.Value.ValueKind != JsonValueKind.Number)
            return (element, 0);

        return (valueProp.Value, confidenceProp.Value.GetDecimal());
    }

    private static ContractParseProperty.PropertyWithValue[][] BuildItems(string title, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                return
                [
                    element.EnumerateObject()
                        .Select(p => new ContractParseProperty.PropertyWithValue(p.Name, JsonValueToString(p.Value)))
                        .ToArray()
                ];

            case JsonValueKind.Array:
                var items = element.EnumerateArray().ToList();
                if (items.Count == 0)
                    return [[new ContractParseProperty.PropertyWithValue(title, "[]")]];

                // 数组元素全是简单值时，用 string.Join 合并成一行
                if (items.All(e => !IsComplex(e)))
                {
                    return [[new ContractParseProperty.PropertyWithValue(title, string.Join(", ", items.Select(JsonValueToString)))]];
                }

                // 否则每个复杂元素占一行
                return items.Select(item =>
                {
                    if (item.ValueKind == JsonValueKind.Object)
                    {
                        return item.EnumerateObject()
                            .Select(p => new ContractParseProperty.PropertyWithValue(p.Name, JsonValueToString(p.Value)))
                            .ToArray();
                    }
                    return new[] { new ContractParseProperty.PropertyWithValue(title, JsonValueToString(item)) };
                }).ToArray();

            default:
                return [[new ContractParseProperty.PropertyWithValue(title, JsonValueToString(element))]];
        }
    }

    private static bool IsComplex(JsonElement element) =>
        element.ValueKind == JsonValueKind.Object || element.ValueKind == JsonValueKind.Array;

    private static string JsonValueToString(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                return element.GetString() ?? "";
            case JsonValueKind.Number:
                return element.GetRawText();
            case JsonValueKind.True:
                return "true";
            case JsonValueKind.False:
                return "false";
            case JsonValueKind.Null:
                return "(空值)";
            case JsonValueKind.Object:
            case JsonValueKind.Array:
                return JsonSerializer.Serialize(element);
            default:
                return element.GetRawText();
        }
    }
}

public class ContractParseProperty
{
    public required string Title { get; set; }

    public decimal Confidence { get; set; }

    public PropertyWithValue[][] Items { get; set; } = [];

    public record PropertyWithValue(string Property, string Value);
}
