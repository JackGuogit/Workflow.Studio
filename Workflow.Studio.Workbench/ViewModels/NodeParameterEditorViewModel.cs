using System.Collections.ObjectModel;

namespace Workflow.Studio.Workbench.ViewModels;

public sealed class NodeParameterEditorViewModel
{
    public NodeParameterEditorViewModel(NodeViewModel node)
    {
        Node = node;
        Parameters = new ObservableCollection<NodeParameterItemViewModel>(
            node.Model.Parameters.Select(entry => new NodeParameterItemViewModel(entry.Key, entry.Value)));
    }

    public NodeViewModel Node { get; }

    public ObservableCollection<NodeParameterItemViewModel> Parameters { get; }

    public string Title => $"编辑参数 - {Node.Title}";

    public string Description => Node.Description;

    public string NodeTypeText => Node.NodeTypeText;

    public bool HasParameters => Parameters.Count > 0;

    public bool TryCreateParameterSnapshot(out Dictionary<string, object?> parameters, out string? error)
    {
        parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var parameter in Parameters)
        {
            if (!parameter.TryBuildValue(out var value, out error))
            {
                return false;
            }

            parameters[parameter.Key] = value;
        }

        error = null;
        return true;
    }
}
