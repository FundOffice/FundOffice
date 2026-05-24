using CommunityToolkit.Mvvm.ComponentModel;
using FMO.Models;
using FMO.Utilities;
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


[EntityModifiable(typeof(Participant))]
public partial class ManagerMemberViewModel : ObservableObject
{
    private readonly Participant _person;

    public static IDType[] IDTypes { get; } = [IDType.IdentityCard, IDType.PassportChina, IDType.PassportForeign, IDType.TaiwanCompatriotsID, IDType.ForeignPermanentResidentID, IDType.HongKongMacauPass, IDType.HouseholdRegister];


    //public static PersonRole[] Roles { get; } = [PersonRole.Legal, PersonRole.ActualController, PersonRole.InvestmentManager, PersonRole.Agent, PersonRole.OrderPlacer, PersonRole.FundTransferor, PersonRole.ConfirmationPerson];

    public RoleViewModel[] Roles { get; }

    [ObservableProperty]
    public partial bool IsReadOnly { get; set; }

    public int Id { get; set; }


    public ManagerMemberViewModel(Participant person)
    {
        _person = person;
        Id = person.Id;

        FillBy(_person);

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

    public partial void OnEntityChanged()
    {
        using var db = DbHelper.Base();
        db.GetCollection<Participant>().Upsert(_person);
    }


    public partial class RoleViewModel : ObservableObject
    {
        [ObservableProperty]
        public partial PersonRole Role { get; set; }

        [ObservableProperty]
        public partial bool IsSelected { get; set; }
    }
}