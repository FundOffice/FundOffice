using CommunityToolkit.Mvvm.Messaging;
using FMO.Logging;
using FMO.Models;
using FMO.Utilities;
using System.Collections.ObjectModel;

namespace FMO;


public class TipsObserver : IRecipient<IDataTip>, IRecipient<DataTipRemove>
{

    public static TipsObserver Instance = new TipsObserver();


    public ObservableCollection<IDataTip> Tips { get; } = [];



    public TipsObserver()
    {
        WeakReferenceMessenger.Default.RegisterAll(this);
    }


    public void Receive(IDataTip message)
    {
        Task.Run(async () => await App.Current.Dispatcher.BeginInvoke(() => Tips.Add(message)));
    }

    public void Receive(DataTipRemove message)
    {
        Task.Run(async () => await App.Current.Dispatcher.BeginInvoke(() =>
        {
            if (Tips.FirstOrDefault(x => x.Id == message.Id) is IDataTip tip)
                Tips.Remove(tip);
            else LogEx.Error($"Remove Tip {message.Id} Missing");
        }));
    }
}