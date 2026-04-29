using FMO.Models;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace FMO.ESigning.MeiShi;



internal class RootJson
{
    public int code { get; set; }


    public JsonNode? data { get; set; }

    public string? message { get; set; }
}
