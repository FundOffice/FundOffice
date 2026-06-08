using CommunityToolkit.Mvvm.ComponentModel;
using FMO.Models;

namespace FMO.Shared;

public partial class BooleanDate : ObservableObject, IViewModel<DateOnly, BooleanDate>
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
        if (date == default) Date = null;
        else
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

    public DateOnly Build() => Trans(this);

    public override string ToString() => IsLongTerm ? "长期" : Date?.ToString("yyyy/MM/dd") ?? "未设置";

    public static DateOnly Trans(BooleanDate vm)
    {
        return vm.IsLongTerm ? DateOnly.MaxValue : vm.Date is null ? default : DateOnly.FromDateTime(vm.Date.Value);
    }

    public static BooleanDate Trans(DateOnly vm)
    {
        return new BooleanDate(vm);
    }

    public bool Equals(DateOnly other)
    {
        return Date is not null && DateOnly.FromDateTime(Date.Value) == other;
    }
}
