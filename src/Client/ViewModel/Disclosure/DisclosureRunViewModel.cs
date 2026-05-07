using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FMO.Disclosure;
using FMO.Models;
using FMO.Utilities;
using System.Diagnostics.CodeAnalysis;

namespace FMO;

public partial class DisclosureRunViewModel : ObservableObject, IRecipient<DisclosureInstance>, IRecipient<DisclosureRunMessage>
{

    public string Channel { get; }

    public string ChannelName { get; }

    public bool HasInstance => !string.IsNullOrWhiteSpace(InstanceId);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasInstance))]
    public partial string? InstanceId { get; set; }

    public bool IsStopped => Status == DisclosureStatus.Stopped;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStopped))]
    public partial DisclosureStatus Status { get; set; }

    [ObservableProperty]
    public partial DateTime StartedTime { get; set; }

    [ObservableProperty]
    public partial DateTime LastRunTime { get; set; }

    [ObservableProperty]
    public partial DateTime CompletedTime { get; set; }

    [ObservableProperty]
    public partial int FailedTimes { get; set; }

    [ObservableProperty]
    public partial string? Error { get; set; }

    [ObservableProperty]
    public partial bool AutoRun { get; set; }

    public required IDisclosureNotice Notice { get; init; }

    public required DisclosureWorkflow Workflow { get; init; }

    [RelayCommand]
    public void CreateInstance()
    {
        var inst = DisclosureService.CreateInstance(Workflow, Notice);
        Fill(inst);
    }

    [SetsRequiredMembers]
    public DisclosureRunViewModel(IDisclosureNotice notice, DisclosureWorkflow workflow, DisclosureInstance? instance)
    {
        WeakReferenceMessenger.Default.RegisterAll(this);

        Notice = notice;
        Workflow = workflow;

        Channel = workflow.Channel;
        ChannelName = DisclosureService.GetChannel(workflow.Channel)?.Name ?? workflow.Channel;

        if (instance is not null)
            Fill(instance);
    }

    public void Fill(DisclosureInstance instance)
    {
        InstanceId = instance.Id;
        Status = instance.Status;
        StartedTime = instance.StartedTime;
        LastRunTime = instance.LastRunTime;
        CompletedTime = instance.CompletedTime;
        FailedTimes = instance.FailedTimes;
        Error = instance.Error;
        AutoRun = instance.AutoRun;
    }

    [RelayCommand]
    public void StartRun()
    {
        if (!HasInstance) return;

        using var db = DbHelper.Base();
        var inst = db.GetCollection<DisclosureInstance>().FindById(InstanceId);
        DisclosureService.AddToQueue(inst);
    }


    [RelayCommand]
    public void StopRun()
    {
        if (!HasInstance) return;
        DisclosureService.RemoveFromQueue(InstanceId!);
        Status = DisclosureStatus.Stopped;
    }

    public void Receive(DisclosureInstance message)
    {
        if (message.Id == InstanceId) Fill(message);
    }

    public void Receive(DisclosureRunMessage message)
    {
        if (message.Id != InstanceId) return;

        Error = message.Message;
    }
}
