using CommunityToolkit.Mvvm.Messaging;
using FMO.Models;
using System.Collections.ObjectModel;

namespace FMO.Utilities;


public class DataObserver : IRecipient<EntityChangedMessage<Fund, DateOnly>>
{
    public ObservableCollection<IDataTip> Tips { get; } = [];



    public DataObserver()
    {
        WeakReferenceMessenger.Default.RegisterAll(this);
    }


    public void AddTip(IDataTip tip)
    {
        
        Tips.Add(tip);
    }



    public void Receive(EntityChangedMessage<Fund, DateOnly> message)
    {
        if (message.PropertyName == nameof(Fund.ClearDate))
        {
            // DataTracker.VerifyFundClearDateRule
        }
    }
}