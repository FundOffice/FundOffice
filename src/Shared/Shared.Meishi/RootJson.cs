
using System.Text.Json.Nodes;

namespace FMO.Shared.MeiShi;



internal class RootJson
{
    public int code { get; set; }


    public JsonNode? data { get; set; }

    public string? message { get; set; }
}
