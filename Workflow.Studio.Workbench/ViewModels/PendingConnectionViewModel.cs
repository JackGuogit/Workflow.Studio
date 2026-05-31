using CommunityToolkit.Mvvm.Input;

namespace Workflow.Studio.Workbench.ViewModels;

public sealed class PendingConnectionViewModel
{
    private PortViewModel? _source;

    public PendingConnectionViewModel(WorkflowWorkbenchViewModel workbench)
    {
        StartCommand = new RelayCommand<PortViewModel?>(source => _source = source);
        FinishCommand = new RelayCommand<PortViewModel?>(target =>
        {
            if (_source is null || target is null)
            {
                return;
            }

            workbench.Connect(_source, target);
            _source = null;
        });
    }

    public IRelayCommand<PortViewModel?> StartCommand { get; }

    public IRelayCommand<PortViewModel?> FinishCommand { get; }
}
