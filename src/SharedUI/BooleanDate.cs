using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections;

namespace FMO.Shared;

public partial class BooleanDate : ObservableObject, IEquatable<BooleanDate>
{
    [ObservableProperty]
    public partial DateTime? Date { get; set; }


    [ObservableProperty]
    public partial bool IsLongTerm { get; set; }

    public bool Equals(BooleanDate? other)
    {
        if (other is null) return false;

        //if (Date is null && other.Date is not null) return false;
        //if (Date is not null && other.Date is null) return false;
        if (Date != other.Date) return false;

        if (IsLongTerm != other.IsLongTerm) return false;

        return true;
    }


    partial void OnIsLongTermChanged(bool value)
    {
        if (!value && Date >= DateTime.MaxValue.Date)
            Date = null;
    }
}
