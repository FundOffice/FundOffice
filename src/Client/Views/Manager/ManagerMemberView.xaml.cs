using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using FMO.Models;
using FMO.Shared;
using System.Windows.Controls;

namespace FMO;

/// <summary>
/// ManagerMemberView.xaml 的交互逻辑
/// </summary>
public partial class ManagerMemberView : UserControl
{
    public ManagerMemberView()
    {
        InitializeComponent();
    }
}

public partial class ManagerMemberViewModel : EditableControlViewModelBase<Participant>
{

    public static IDType[] IDTypes { get; } = [IDType.IdentityCard, IDType.PassportChina, IDType.PassportForeign, IDType.TaiwanCompatriotsID, IDType.ForeignPermanentResidentID, IDType.HongKongMacauPass, IDType.HouseholdRegister];


    //public static PersonRole[] Roles { get; } = [PersonRole.Legal, PersonRole.ActualController, PersonRole.InvestmentManager, PersonRole.Agent, PersonRole.OrderPlacer, PersonRole.FundTransferor, PersonRole.ConfirmationPerson];

    public RoleViewModel[] Roles { get; }


    public ChangeableViewModel<Participant, string?> Name { get; }


    public ChangeableViewModel<Participant, string?> IdNumber { get; }


    public ChangeableViewModel<Participant, IDType?> IdType { get; }


    public ChangeableViewModel<Participant, string?> Phone { get; }


    public ChangeableViewModel<Participant, string?> Email { get; }


    public ChangeableViewModel<Participant, string?> Address { get; }


    public ChangeableViewModel<Participant, string?> Profile { get; }


    public ChangeableViewModel<Participant, PersonRole?> Role { get; }

    public ChangeableViewModel<Participant, DateEfficientViewModel?> Efficient { get; }

    public ManagerMemberViewModel(Participant person)
    {
        Id = person?.Id ?? 0;

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
            UpdateFunc = (x, y) => x.Identity = x.Identity is null ? new Identity { Id = y! } : x.Identity with { Id = y ?? "" },
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


        Role = new()
        {
            Label = "角色",
            InitFunc = x => x.Role,
            UpdateFunc = (x, y) => x.Role = y ?? default,
            ClearFunc = x => x.Role = default
        };
        Role.Init(person);

        PersonRole[] arr = [PersonRole.Legal, PersonRole.ActualController, PersonRole.InvestmentManager, PersonRole.Agent, PersonRole.OrderPlacer, PersonRole.FundTransferor, PersonRole.ConfirmationPerson];
        Roles = arr.Select(x => new RoleViewModel { Role = x, IsSelected = person.Role.HasFlag(x) }).ToArray();

        foreach (var item in Roles)
        {
            item.PropertyChanged += (s, e) => Role.NewValue = UnionRole();
        }

    }

    private PersonRole UnionRole()
    {
        PersonRole role = default;
        foreach (var item in Roles)
        {
            if (item.IsSelected)
                role |= item.Role;
        }
        return role;
    }

    protected override void ModifyOverride(IPropertyModifier unit)
    {
        base.ModifyOverride(unit);

        WeakReferenceMessenger.Default.Send(new ParticipantChangedMessage(Id));
    }


    protected override Participant InitNewEntity()
    {
        return new Participant();
    }


    public partial class RoleViewModel : ObservableObject
    {
        [ObservableProperty]
        public partial PersonRole Role { get; set; }

        [ObservableProperty]
        public partial bool IsSelected { get; set; }
    }
}