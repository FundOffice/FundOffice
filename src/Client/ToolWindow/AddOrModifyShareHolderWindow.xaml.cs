using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FMO.Models;
using FMO.Utilities;
using System.Collections.ObjectModel;
using System.Windows;

namespace FMO;

/// <summary>
/// AddOrModifyShareHolderWindow.xaml 的交互逻辑
/// </summary>
public partial class AddOrModifyShareHolderWindow : Window
{
    public AddOrModifyShareHolderWindow()
    {
        InitializeComponent();
    }
}



public partial class AddOrModifyShareHolderWindowViewModel : ObservableObject
{
    public AddOrModifyShareHolderWindowViewModel(Institution institution)
    {
        using var db = DbHelper.Base();
        var per = db.GetCollection<IEntity>().FindAll().ToList();

        Institution = institution;
        Entities = new(per); 

        Entities.CollectionChanged += (s, e) => OnPropertyChanged(nameof(Candidates));
    }

     


    public ObservableCollection<IEntity> Entities { get; }


    [ObservableProperty]
    public partial bool ShowAddEntity { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial IEntity? Holder { get; set; }


    [ObservableProperty]
    public partial string? HolderName { get; set; }

    public IEnumerable<IEntity>? Candidates => Institution is null ? Entities : Entities.Where(x=>x.Id != Institution.Id);


    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Candidates))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial Institution? Institution { get; set; }


    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial decimal? ShareAmount { get; set; }

    public bool CanConfirm => Institution is not null && Holder is not null && (ShareAmount ?? 0) > 0;


    partial void OnHolderNameChanged(string? value)
    {

        ShowAddEntity = !string.IsNullOrWhiteSpace(value) && (Holder is null || Holder.Name != value);

        if (string.IsNullOrWhiteSpace(value)) Holder = null;
    }

    [RelayCommand]
    public void AddInstitution()
    {
        Institution obj = new Institution { Name = HolderName! };
        Entities.Add(obj);
        Holder = obj;
        ShowAddEntity = false;
    }

    [RelayCommand]
    public void AddPerson()
    {
        Person obj = new Person { Name = HolderName! };
        Entities.Add(obj);
        Holder = obj;
        ShowAddEntity = false;
    }


    [RelayCommand(CanExecute = nameof(CanConfirm))]
    public void Confirm(Window wnd)
    {
        if (Holder is null || Institution is null || ShareAmount is null) return;

        var iid = Institution is Manager ? 0 : Institution.Id;

        using var db = DbHelper.Base();

        // 检测是否有相同的数据
        var old = db.GetCollection<Ownership>().FindOne(x => x.HolderId == Holder.Id && x.InstitutionId == iid);
        if (old is not null)
            Holder.Id = old.Id;

        db.GetCollection<IEntity>().Upsert(Holder);

        db.GetCollection<Ownership>().Insert(new Ownership { HolderId = Holder.Id, InstitutionId = iid, Share = ShareAmount.Value });

        App.Current.Dispatcher.BeginInvoke(() => wnd.Close());
    }

}