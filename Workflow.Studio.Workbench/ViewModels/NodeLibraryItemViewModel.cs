using Workflow.Studio.Core.Nodes;

namespace Workflow.Studio.Workbench.ViewModels;

public sealed class NodeLibraryItemViewModel
{
    public NodeLibraryItemViewModel(NodeDescriptor descriptor)
    {
        Descriptor = descriptor;
    }

    public NodeDescriptor Descriptor { get; }

    public string NodeTypeId => Descriptor.NodeTypeId;

    public string DisplayName => Descriptor.DisplayName;

    public string Category => Descriptor.Category;

    public string Description => Descriptor.Description;
}
