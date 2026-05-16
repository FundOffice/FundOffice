namespace FMO.TPL;






public record ReferenceInfo(string Field, string Filter);


internal class TemplateMetaDTO
{
    public InputInfo[]? Input { get; set; }

    public ReferenceInfo[]? Refer { get; set; }

    public string? Script { get; set; }
}


public class TplInputs
{
    public const string Fund = "Fund";

    public const string Date = "Date";
}




public class TemplateMeta
{

    public required string Id { get; set; }


    public required string Name { get; set; }


    public required string Description { get; set; }


    public required string Version { get; set; }

    public string Class { get; set; } = "";


    public string Limit { get; set; } = "everyone";


    public string Sign { get; set; } = "";



}


public class TemplateScript
{
    public required string Id { get; set; }

    public InputInfo[] Input { get; set; } = [];

    public ReferenceInfo[] Refer { get; set; } = [];


    public string? Script { get; set; }
}
