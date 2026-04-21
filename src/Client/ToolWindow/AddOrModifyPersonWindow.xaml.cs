using FMO.Models;
using FMO.Shared;
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



public partial class PersonViewModel : EditableControlViewModelBase<Person>
{

    public static IDType[] IDTypes { get; } = [IDType.IdentityCard, IDType.PassportChina, IDType.PassportForeign, IDType.TaiwanCompatriotsID, IDType.ForeignPermanentResidentID, IDType.HongKongMacauPass, IDType.HouseholdRegister];

    public ChangeableViewModel<Person, string?> Name { get; }


    public ChangeableViewModel<Person, string?> IdNumber { get; }


    public ChangeableViewModel<Person, IDType?> IdType { get; }


    public ChangeableViewModel<Person, string?> Phone { get; }


    public ChangeableViewModel<Person, string?> Email { get; }


    public ChangeableViewModel<Person, string?> Address { get; }


    public ChangeableViewModel<Person, string?> Profile { get; }



    public ChangeableViewModel<Person, DateEfficientViewModel?> Efficient { get; }

    public PersonViewModel(Person person)
    {
        Id = person.Id;

        Name = new()
        {
            Label = "姓名",
            InitFunc = x => x.Name,
            UpdateFunc = (x, y) => x.Name = y,
            ClearFunc = x => x.Name = string.Empty
        };
        Name.Init(person);

        IdType = new()
        {
            Label = "证件类型",
            InitFunc = x => x.Identity?.Type,
            UpdateFunc = (x, y) => x.Identity = x.Identity is null ? new Identity { Id = "", Type = y ?? default } : x.Identity with { Type = y ?? default },
            ClearFunc = x => x.Identity = x.Identity is null ? null : x.Identity with { Type = default }
        };
        IdType.Init(person);

        IdNumber = new()
        {
            Label = "证件号码",
            InitFunc = x => x.Identity?.Id,
            UpdateFunc = (x, y) => x.Identity = x.Identity is null ? null : x.Identity with { Id = y ?? "" },
            ClearFunc = x => x.Identity = x.Identity is null ? null : x.Identity with { Id = "" }
        };
        IdNumber.Init(person);

        Efficient = new()
        {
            Label = "有效期",
            InitFunc = x => new(x.Efficient),
            UpdateFunc = (x, y) => x.Efficient = y!.Build(),
            ClearFunc = x => x.Efficient = default
        };
        Efficient.Init(person);

        Phone = new()
        {
            Label = "电话",
            InitFunc = x => x.Phone,
            UpdateFunc = (x, y) => x.Phone = y,
            ClearFunc = x => x.Phone = default
        };
        Phone.Init(person);


        Address = new()
        {
            Label = "地址",
            InitFunc = x => x.Address,
            UpdateFunc = (x, y) => x.Address = y,
            ClearFunc = x => x.Address = string.Empty
        };
        Address.Init(person);



        Email = new()
        {
            Label = "Email",
            InitFunc = x => x.Email,
            UpdateFunc = (x, y) => x.Email = y,
            ClearFunc = x => x.Email = string.Empty
        };
        Email.Init(person);


        Profile = new()
        {
            Label = "简介",
            InitFunc = x => x.Profile,
            UpdateFunc = (x, y) => x.Profile = y,
            ClearFunc = x => x.Profile = string.Empty
        };
        Profile.Init(person);




    }

    protected override void ModifyOverride(IPropertyModifier unit)
    {
        base.ModifyOverride(unit);

        // WeakReferenceMessenger.Default.Send(new PersonChangedMessage(Id));
    }





    protected override Person InitNewEntity()
    {
        return new Person { Name = "" };
    }
}