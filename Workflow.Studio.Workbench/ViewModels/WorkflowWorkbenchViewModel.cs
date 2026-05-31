using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows;
using Workflow.Studio.Core.Models;
using Workflow.Studio.Core.Nodes;
using Workflow.Studio.Core.Services;

namespace Workflow.Studio.Workbench.ViewModels;

public sealed partial class WorkflowWorkbenchViewModel : ObservableObject
{
    private readonly WorkflowEngine _workflowEngine;
    private readonly NodeFactory _nodeFactory;
    private readonly WorkflowEventHub _eventHub;
    private readonly Dictionary<string, NodeViewModel> _nodeIndex = new(StringComparer.OrdinalIgnoreCase);
    private WorkflowData _workflow;

    [ObservableProperty]
    private string _statusMessage = "就绪";

    [ObservableProperty]
    private string _globalVariablesText = "尚未执行";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteWorkflowCommand))]
    private bool _isBusy;

    public WorkflowWorkbenchViewModel(WorkflowEngine workflowEngine, NodeFactory nodeFactory, WorkflowEventHub eventHub)
    {
        _workflowEngine = workflowEngine;
        _nodeFactory = nodeFactory;
        _eventHub = eventHub;
        _eventHub.NodeStatusChanged += OnNodeStatusChanged;
        _eventHub.PortValueChanged += OnPortValueChanged;

        Nodes = [];
        Connections = [];
        AvailableNodes = new ObservableCollection<NodeLibraryItemViewModel>(
            _nodeFactory.GetAvailableNodes().Select(descriptor => new NodeLibraryItemViewModel(descriptor)));
        PendingConnection = new PendingConnectionViewModel(this);
        ExecuteWorkflowCommand = new AsyncRelayCommand(ExecuteWorkflowAsync, CanExecuteWorkflow);
        ResetWorkflowCommand = new RelayCommand(ResetWorkflow);
        AddNodeCommand = new RelayCommand<NodeLibraryItemViewModel?>(AddNode);

        _workflow = CreateDemoWorkflow();
        RebuildViewModels();
    }

    public ObservableCollection<NodeViewModel> Nodes { get; }

    public ObservableCollection<ConnectionViewModel> Connections { get; }

    public ObservableCollection<NodeLibraryItemViewModel> AvailableNodes { get; }

    public PendingConnectionViewModel PendingConnection { get; }

    public IAsyncRelayCommand ExecuteWorkflowCommand { get; }

    public IRelayCommand ResetWorkflowCommand { get; }

    public IRelayCommand<NodeLibraryItemViewModel?> AddNodeCommand { get; }

    public void NotifyNodeSettingsChanged(NodeViewModel node)
    {
        node.NotifySettingsChanged();
        StatusMessage = $"已更新节点设置: {node.Title}";
    }

    public void Connect(PortViewModel source, PortViewModel target)
    {
        if (!CanConnect(source, target))
        {
            StatusMessage = "仅支持输出端口连接输入端口，且会忽略重复连接。";
            return;
        }

        var connection = _workflow.Connect(source.Owner.Id, source.Id, target.Owner.Id, target.Id);
        source.Model.MarkConnected();
        target.Model.MarkConnected();
        source.Refresh();
        target.Refresh();

        var connectionViewModel = new ConnectionViewModel(connection, source, target);
        Connections.Add(connectionViewModel);
        source.AttachConnection(connectionViewModel);
        target.AttachConnection(connectionViewModel);
        StatusMessage = $"已连接 {source.Owner.Title}.{source.Title} -> {target.Owner.Title}.{target.Title}";
    }

    private void AddNode(NodeLibraryItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        AddNode(item, GetDefaultNodeLocation());
    }

    public void AddNode(NodeLibraryItemViewModel item, Point location)
    {
        ArgumentNullException.ThrowIfNull(item);

        var node = _nodeFactory.CreateNode(item.NodeTypeId, location.X, location.Y);
        _workflow.AddNode(node);

        var viewModel = new NodeViewModel(node);
        Nodes.Add(viewModel);
        _nodeIndex[node.Metadata.Id] = viewModel;
        StatusMessage = $"已添加节点: {item.DisplayName}";
    }

    private bool CanExecuteWorkflow()
    {
        return !IsBusy;
    }

    private async Task ExecuteWorkflowAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "正在执行工作流图...";

            var context = await _workflowEngine.ExecuteAsync(_workflow);

            foreach (var node in Nodes)
            {
                node.Refresh();
            }

            GlobalVariablesText = context.GlobalVariables.Count == 0
                ? "没有全局变量"
                : string.Join(Environment.NewLine, context.GlobalVariables.Select(entry => $"{entry.Key}: {entry.Value}"));

            StatusMessage = $"执行完成，共处理 {context.History.Count} 个节点。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"执行失败：{ex.Message}";
            foreach (var node in Nodes)
            {
                node.Refresh();
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ResetWorkflow()
    {
        foreach (var node in _workflow.Nodes)
        {
            node.SetStatus(NodeStatus.Ready);

            foreach (var port in node.InputPorts.Concat(node.OutputPorts))
            {
                port.Clear();
            }
        }

        ApplyConnectionState();

        foreach (var node in Nodes)
        {
            node.Refresh();
        }

        GlobalVariablesText = "尚未执行";
        StatusMessage = "工作流状态已重置。";
    }

    private void RebuildViewModels()
    {
        Nodes.Clear();
        Connections.Clear();
        _nodeIndex.Clear();

        ApplyConnectionState();

        foreach (var node in _workflow.Nodes)
        {
            var viewModel = new NodeViewModel(node);
            Nodes.Add(viewModel);
            _nodeIndex[node.Metadata.Id] = viewModel;
        }

        foreach (var connection in _workflow.Connections)
        {
            var source = _nodeIndex[connection.SourceNodeId].FindPort(connection.SourcePortId);
            var target = _nodeIndex[connection.TargetNodeId].FindPort(connection.TargetPortId);

            if (source is not null && target is not null)
            {
                var connectionViewModel = new ConnectionViewModel(connection, source, target);
                Connections.Add(connectionViewModel);
                source.AttachConnection(connectionViewModel);
                target.AttachConnection(connectionViewModel);
            }
        }
    }

    private bool CanConnect(PortViewModel source, PortViewModel target)
    {
        return source.Direction == PortDirection.Output
            && target.Direction == PortDirection.Input
            && !ReferenceEquals(source, target)
            && !Connections.Any(connection =>
                string.Equals(connection.Source.Owner.Id, source.Owner.Id, StringComparison.OrdinalIgnoreCase)
                && string.Equals(connection.Source.Id, source.Id, StringComparison.OrdinalIgnoreCase)
                && string.Equals(connection.Target.Owner.Id, target.Owner.Id, StringComparison.OrdinalIgnoreCase)
                && string.Equals(connection.Target.Id, target.Id, StringComparison.OrdinalIgnoreCase));
    }

    private Point GetDefaultNodeLocation()
    {
        var index = _workflow.Nodes.Count;
        var x = 120 + (index % 3) * 320;
        var y = 280 + (index / 3) * 220;
        return new Point(x, y);
    }

    private void ApplyConnectionState()
    {
        foreach (var node in _workflow.Nodes)
        {
            foreach (var port in node.InputPorts.Concat(node.OutputPorts))
            {
                if (port.Status != PortStatus.HasData)
                {
                    port.Clear();
                }
            }
        }

        foreach (var connection in _workflow.Connections)
        {
            _workflow.Nodes.First(node => string.Equals(node.Metadata.Id, connection.SourceNodeId, StringComparison.OrdinalIgnoreCase))
                .FindPort(connection.SourcePortId)?
                .MarkConnected();

            _workflow.Nodes.First(node => string.Equals(node.Metadata.Id, connection.TargetNodeId, StringComparison.OrdinalIgnoreCase))
                .FindPort(connection.TargetPortId)?
                .MarkConnected();
        }
    }

    private void OnNodeStatusChanged(object? sender, NodeStatusChangedEventArgs e)
    {
        if (_nodeIndex.TryGetValue(e.NodeId, out var node))
        {
            node.Refresh();
        }
    }

    private void OnPortValueChanged(object? sender, PortValueChangedEventArgs e)
    {
        if (_nodeIndex.TryGetValue(e.NodeId, out var node))
        {
            node.FindPort(e.PortId)?.Refresh();
            node.Refresh();
        }
    }

    private WorkflowData CreateDemoWorkflow()
    {
        var workflow = new WorkflowData();
        workflow.GlobalVariables["GlobalVariablesSeedText"] = "DevWorkflow Studio";

        var sourceNode = _nodeFactory.CreateNode("demo.node.text-source", 80, 120, "node-source");
        var transformNode = _nodeFactory.CreateNode("demo.node.uppercase-transform", 420, 120, "node-transform");
        var previewNode = _nodeFactory.CreateNode("demo.node.preview", 760, 120, "node-preview");

        workflow.AddNode(sourceNode);
        workflow.AddNode(transformNode);
        workflow.AddNode(previewNode);

        workflow.Connect("node-source", "content", "node-transform", "incoming");
        workflow.Connect("node-transform", "result", "node-preview", "incoming");

        return workflow;
    }
}
