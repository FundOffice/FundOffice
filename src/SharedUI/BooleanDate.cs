using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections;

namespace FMO.Shared;

public partial class BooleanDate : ObservableObject, IEquatable<BooleanDate>, IDisplay<string>
{

    public DateTime Today => DateTime.Today;

    [ObservableProperty]
    public partial DateTime? Date { get; set; }


    [ObservableProperty]
    public partial bool IsLongTerm { get; set; }

    public BooleanDate() { }

    public BooleanDate(DateTime date)
    {
        Date = date;
        IsLongTerm = date.Year > 2099;
    }

    public BooleanDate(DateOnly date)
    {
        Date = new(date, TimeOnly.MaxValue);
        IsLongTerm = date.Year > 2099;
    }



    public bool Equals(BooleanDate? other)
    {
        if (other is null) return false;

        if (IsLongTerm == other.IsLongTerm) return true;
        if (Date != other.Date) return false;
        return true;
    }


    partial void OnIsLongTermChanged(bool value)
    {
        if (!value && Date >= DateTime.MaxValue.Date)
            Date = null;
        else 
            Date = DateTime.MaxValue;
    }

    public string Transfrom() => IsLongTerm ? "长期" : Date?.ToString("yyyy/MM/dd") ?? "未设置";
}
