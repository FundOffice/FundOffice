using CommunityToolkit.Mvvm.ComponentModel;
using FMO.Models;
using FMO.Utilities;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FMO;

/// <summary>
/// OwnershipMapView.xaml 的交互逻辑
/// </summary>
public partial class OwnershipMapView : UserControl
{
    public OwnershipMapView()
    {
        InitializeComponent();
    }

    private void UserControl_DataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (DataContext is not OwnershipMapViewModel vm) return;

        int heightOfDepth = 60; //每层高

        var root = FindName("root") as Canvas;
        root.Children.Clear();

        var tb = new TextBlock { Text = vm.Ownership.Name };
        root.Children.Add(tb); 
    }
}



public partial class OwnershipMapViewModel : ObservableObject
{

    public OwnershipItem Ownership { get;  }

    public int Depth { get; set; }

    [SetsRequiredMembers]
    public OwnershipMapViewModel(int institutionId)
    {
        using var db = DbHelper.Base();
        var per = db.GetCollection<IEntity>().FindAll().ToList();
        per.Insert(0, db.GetCollection<Manager>().FindById(1));


        var os = db.GetCollection<Ownership>().FindAll().ToArray();
        // 解析股权结构图
        List<OwnershipItem> data = new();

        var ins = per.FirstOrDefault(x => x.Id == institutionId);
        var oi = new OwnershipItem { Name = ins!.Name };
        Parse(institutionId, per, os, oi);

        int dep = 0;
        dep = GetDepth(oi);

        Ownership = oi;
        Depth = dep;
    }



    private OwnershipItem Parse(int institutionId, IList<IEntity> per, IEnumerable<Ownership> os, OwnershipItem oi)
    {
        foreach (var item in os.Where(x => x.InstitutionId == institutionId))
        {
            var ins = per.FirstOrDefault(x => x.Id == item.InstitutionId);
            var holder = per.FirstOrDefault(x => x.Id == item.HolderId);

            if (holder is Person p)
            {
                oi.Childs.Add(new OwnershipItem { Name = p.Name, Ratio = item.Ratio });
                continue;
            }
            else if (holder is Institution)
                oi.Childs.Add(Parse(holder.Id, per, os, new OwnershipItem { Name = holder.Name, Ratio = item.Ratio }));


        }

        return oi;
    }

    private int GetDepth(OwnershipItem o)
    {
        if (o.Childs.Count == 0) return 0;

        int max = 0;
        foreach (var item in o.Childs)
        {
            var d = GetDepth(item);
            max = Math.Max(max, d);
        }

        return max + 1;
    }


    public class OwnershipItem
    {
        public int Id { get; set; }

        public required string Name { get; set; }

        public decimal Ratio { get; set; }


        public List<OwnershipItem> Childs { get; } = new();
    }

}

