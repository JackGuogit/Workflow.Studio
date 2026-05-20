using CommunityToolkit.Mvvm.ComponentModel;
using Workflow.Studio.Core.Models;

namespace Workflow.Studio.Workbench.ViewModels;

public sealed class ConnectionViewModel : ObservableObject
{
    public ConnectionViewModel(ConnectionData model, PortViewModel source, PortViewModel target)
    {
        Model = model;
        Source = source;
        Target = target;
    }

    public ConnectionData Model { get; }

    public PortViewModel Source { get; }

    public PortViewModel Target { get; }

    public string DisplayName => $"{Source.Owner.Title}.{Source.Title} -> {Target.Owner.Title}.{Target.Title}";
}
