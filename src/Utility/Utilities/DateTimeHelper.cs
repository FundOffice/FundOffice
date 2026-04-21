using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace FMO.Utilities;

public static class DateTimeHelper
{
    private static DateTime start = new DateTime(1970, 1, 1);

    public static long TimeStampBySeconds(this DateTime dateTime)
    {
        return (long)(dateTime - start).TotalSeconds;
    }
    public static long TimeStampByMilliseconds(this DateTime dateTime)
    {
        return (long)(dateTime - start).TotalMilliseconds;
    }

    public static bool TryParse(string? s, out DateOnly d)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            d = default;
            return false;
        }

        if (DateOnly.TryParse(s, out d))
            return true;
        else if (DateOnly.TryParseExact(s, "yyyyMMdd", out d))
            return true;
        else if (DateOnly.TryParseExact(s, "yyyy-MM-dd", out d))
            return true;


        //var m = Regex.Match(s, @"(?<!\d)\s*(\d{4})[-/]?(\d{2})[-/]?(\d{2})\s*(?!\d)");
        //if (m.Success && int.Parse(m.Groups[1].Value) is int y && int.Parse(m.Groups[2].Value) is int mm && mm <= 12 && int.Parse(m.Groups[3].Value) is int dd)
        //{
        //    d = new DateOnly(y, mm, dd);
        //    return true;
        //}

        return false;
    }


    public static DateOnly? TryFindDate(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var m = Regex.Match(s, @"(?<!\d)\s*(\d{4})\s*[-/年]?\s*(\d{2})\s*[-/月]?\s*(\d{2})\s*(?!\d)");
        if (m.Success && int.Parse(m.Groups[1].Value) is int y && int.Parse(m.Groups[2].Value) is int mm && mm <= 12 && int.Parse(m.Groups[3].Value) is int dd)
        {
            return new DateOnly(y, mm, dd);
        }

        return null;
    }


}



public class DateTimeConverterUsingDateTimeParse : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return DateTime.Parse(reader!.GetString() ?? "");
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString("yyyy-MM-dd HH:mm:ss"));
    }
}