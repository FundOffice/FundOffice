using CommunityToolkit.Mvvm.ComponentModel;
using FMO.Models;
using FMO.Shared;
using FMO.Utilities;
using System.Windows;

namespace FMO;

/// <summary>
/// AddOrModifyPersonWindow.xaml 的交互逻辑
/// </summary>
public partial class AddOrModifyPersonWindow : Window
{
    public AddOrModifyPersonWindow()
    {
        InitializeComponent();
    }
}


[EntityModifiable(typeof(Person))]
public partial class PersonViewModel : ObservableObject
{
    private readonly Person _person;

    public static IDType[] IDTypes { get; } = [IDType.IdentityCard, IDType.PassportChina, IDType.PassportForeign, IDType.TaiwanCompatriotsID, IDType.ForeignPermanentResidentID, IDType.HongKongMacauPass, IDType.HouseholdRegister];


    public int Id { get; }


    public ModifiableViewModel<DateEfficientViewModel> Efficient { get; private set; } = null!;

    public PersonViewModel(Person person)
    {
        Id = person.Id;
        _person = person;

        FillBy(person);
    }

    public partial void OnEntityChanged()
    {
        using var db = DbHelper.Base();
        db.GetCollection<Person>().Upsert(_person);
    }
}