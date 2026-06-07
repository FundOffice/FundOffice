using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace FMO.TPL;

[JsonDerivedType(typeof(InputFund), nameof(InputFund))]
[JsonDerivedType(typeof(InputDate), nameof(InputDate))]
[JsonDerivedType(typeof(InputDateRange), nameof(InputDateRange))]
[JsonDerivedType(typeof(InputInvestor), nameof(InputInvestor))]

public class InputInfo
{
    public InputInfo()
    {
    }

    [SetsRequiredMembers]
    public InputInfo(string tilte)
    {
        Tilte = tilte;
    }

    public required string Tilte { get; init; }


}
public enum ChooseType
{
    Single,
    Multiple,
    ALL
}



public class InputFund : InputInfo
{

    public ChooseType ChooseType { get; set; }

    public InputFund()
    {
    }

    [SetsRequiredMembers]
    public InputFund(string title, ChooseType chooseType) : base(title)
    {
        ChooseType = chooseType;
    }
}


public class InputInvestor : InputInfo
{

    public ChooseType ChooseType { get; set; }

    public InputInvestor()
    {
    }

    [SetsRequiredMembers]
    public InputInvestor(string title, ChooseType chooseType) : base(title)
    {
        ChooseType = chooseType;
    }
}

public class InputDate : InputInfo
{
    public InputDate()
    {
    }

    [SetsRequiredMembers]
    public InputDate(string title) : base(title)
    {
    }
}

public class InputDateRange : InputInfo
{
    public InputDateRange()
    {
    }

    [SetsRequiredMembers]
    public InputDateRange(string title) : base(title)
    {
    }
}

