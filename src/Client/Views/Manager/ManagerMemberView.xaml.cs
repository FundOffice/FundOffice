using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FMO.Models;
using FMO.Shared;
using FMO.Utilities;
using Microsoft.Win32;
using MoT;
using System.IO;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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

    [ObservableProperty]
    public partial bool StayOpen { get; set; }

    public int Id { get; set; }

    public ModifiableViewModel<Identity, IdentityViewMdoel> Identity { get; private set; } = null!;

    [ObservableProperty]
    public partial ImageSource? Photo { get; set; }

    public ManagerMemberViewModel(Participant person)
    {
        _person = person;
        Id = person.Id;

        FillBy(_person);

        PersonRole[] arr = [PersonRole.Legal, PersonRole.ActualController, PersonRole.InvestmentManager, PersonRole.Agent, PersonRole.OrderPlacer, PersonRole.FundTransferor, PersonRole.ConfirmationPerson];
        Roles = arr.Select(x => new RoleViewModel { Role = x, IsSelected = person.Role.HasFlag(x) }).ToArray();

        using var db = DbHelper.Base();
        string fileId = $"Photo.Participant.{Id}";
        if (db.FileStorage.Exists(fileId))
        {
            try
            {
                var ms = new MemoryStream();
                db.FileStorage.Download(fileId, ms);

                BitmapImage image = new BitmapImage();
                image.BeginInit();
                ms.Seek(0, SeekOrigin.Begin);
                image.StreamSource = ms;
                image.EndInit();
                Photo = image;
            }
            catch (Exception e)
            {
                Logg.Error(e);
            }
        }

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


    [RelayCommand]
    public void SetPhoto()
    {
        StayOpen = true;
        var fd = new OpenFileDialog();
        fd.Filter = "图片|*.png;*.jpg;*.jpeg;*.bmp";
        if (fd.ShowDialog() is true)
        {
            using var db = DbHelper.Base();
            db.FileStorage.Upload($"Photo.Participant.{Id}", fd.FileName);

            // 更新UI
            try
            {
                if (Photo is BitmapImage img && img.StreamSource is not null)
                {
                    img.StreamSource.Dispose();
                    img.StreamSource = null;
                }
                var bytes = File.ReadAllBytes(fd.FileName);

                BitmapImage image = new BitmapImage();
                image.BeginInit();
                image.StreamSource = new MemoryStream(bytes);
                image.EndInit();
                Photo = image;
            }
            catch (Exception e)
            {
                Logg.Error(e);
            }
        }

        Task.Run(async () =>
        {
            await Task.Delay(1000);
            StayOpen = false;
        });
    }


    public partial class RoleViewModel : ObservableObject
    {
        [ObservableProperty]
        public partial PersonRole Role { get; set; }

        [ObservableProperty]
        public partial bool IsSelected { get; set; }
    }
}