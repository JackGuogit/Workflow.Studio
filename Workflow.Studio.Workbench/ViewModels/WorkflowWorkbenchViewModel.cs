using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading;
using System.Windows;
using Workflow.Studio.Core.Models;
using Workflow.Studio.Core.Nodes;
using Workflow.Studio.Core.Services;
using Workflow.Studio.Theme;

namespace Workflow.Studio.Workbench.ViewModels;

public sealed partial class WorkflowWorkbenchViewModel : ObservableObject
{
    private readonly WorkflowEngine _workflowEngine;
    private readonly NodeFactory _nodeFactory;
    private readonly WorkflowEventHub _eventHub;
    private readonly SemaphoreSlim _executionGate = new(1, 1);
    private readonly Dictionary<string, NodeViewModel> _nodeIndex = new(StringComparer.OrdinalIgnoreCase);
    private WorkflowData _workflow;

    [ObservableProperty]
    private string _statusMessage = "就绪";

    [ObservableProperty]
    private string _globalVariablesText = "尚未执行";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WorkflowStateText))]
    [NotifyCanExecuteChangedFor(nameof(ExecuteWorkflowCommand))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ViewportText))]
    private Point _viewportLocation;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ViewportText))]
    private Size _viewportSize;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ViewportText))]
    private double _viewportZoom = 1d;

    [ObservableProperty]
    private NodeViewModel? _selectedNode;

    [ObservableProperty]
    private ConnectionViewModel? _selectedConnection;

    public WorkflowWorkbenchViewModel(WorkflowEngine workflowEngine, NodeFactory nodeFactory, WorkflowEventHub eventHub)
    {
        _workflowEngine = workflowEngine;
        _nodeFactory = nodeFactory;
        _eventHub = eventHub;
        WorkbenchThemeManager.EnsureInitialized();
        WorkbenchThemeManager.ThemeChanged += OnThemeChanged;
        _eventHub.NodeStatusChanged += OnNodeStatusChanged;
        _eventHub.PortValueChanged += OnPortValueChanged;

        Nodes = [];
        Connections = [];
        SelectedNodes = [];
        SelectedConnections = [];
        AvailableNodes = new ObservableCollection<NodeLibraryItemViewModel>(
            _nodeFactory.GetAvailableNodes().Select(descriptor => new NodeLibraryItemViewModel(descriptor)));
        PendingConnection = new PendingConnectionViewModel(this);
        ExecuteWorkflowCommand = new AsyncRelayCommand(ExecuteWorkflowAsync, CanExecuteWorkflow);
        ResetWorkflowCommand = new RelayCommand(ResetWorkflow, CanResetWorkflow);
        DeleteSelectionCommand = new RelayCommand(DeleteSelection, CanDeleteSelection);
        RemoveNodeCommand = new RelayCommand<NodeViewModel?>(RemoveNode, CanRemoveNode);
        RemoveConnectionCommand = new RelayCommand<ConnectionViewModel?>(RemoveConnection, CanRemoveConnection);
        DisconnectConnectorCommand = new RelayCommand<object?>(DisconnectConnector, CanDisconnectConnector);
        AddNodeCommand = new RelayCommand<NodeLibraryItemViewModel?>(AddNode);
        ToggleThemeCommand = new RelayCommand(ToggleTheme);

        SelectedNodes.CollectionChanged += OnSelectionCollectionChanged;
        SelectedConnections.CollectionChanged += OnSelectionCollectionChanged;

        _workflow = CreateDemoWorkflow();
        RebuildViewModels();
    }

    public ObservableCollection<NodeViewModel> Nodes { get; }

    public ObservableCollection<ConnectionViewModel> Connections { get; }

    public ObservableCollection<NodeViewModel> SelectedNodes { get; }

    public ObservableCollection<ConnectionViewModel> SelectedConnections { get; }

    public ObservableCollection<NodeLibraryItemViewModel> AvailableNodes { get; }

    public PendingConnectionViewModel PendingConnection { get; }

    public IAsyncRelayCommand ExecuteWorkflowCommand { get; }

    public IRelayCommand ResetWorkflowCommand { get; }

    public IRelayCommand DeleteSelectionCommand { get; }

    public IRelayCommand<NodeViewModel?> RemoveNodeCommand { get; }

    public IRelayCommand<ConnectionViewModel?> RemoveConnectionCommand { get; }

    public IRelayCommand<object?> DisconnectConnectorCommand { get; }

    public IRelayCommand<NodeLibraryItemViewModel?> AddNodeCommand { get; }

    public IRelayCommand ToggleThemeCommand { get; }

    public string WorkflowStateText => IsBusy ? "运行中" : "就绪";

    public string WorkflowGraphSummary => $"节点 {Nodes.Count} · 连线 {Connections.Count}";

    public string CurrentThemeText => WorkbenchThemeManager.ActiveThemeDisplayName;

    public double MinViewportZoom => 0.2d;

    public double MaxViewportZoom => 2.5d;

    public string ViewportText => $"视口 X {ViewportLocation.X:0} · Y {ViewportLocation.Y:0} · 缩放 {ViewportZoom:P0}";

    public string SelectionText => $"已选节点 {SelectedNodes.Count} · 已选连线 {SelectedConnections.Count}";

    public void NotifyNodeSettingsChanged(NodeViewModel node)
    {
        node.NotifySettingsChanged();
        StatusMessage = $"已更新节点设置: {node.Title}";
    }

    public void Connect(PortViewModel source, PortViewModel target)
    {
        _ = TryAddConnection(source, target);
    }

    public bool RewireConnection(PortViewModel currentTarget, PortViewModel newTarget)
    {
        ArgumentNullException.ThrowIfNull(currentTarget);
        ArgumentNullException.ThrowIfNull(newTarget);

        if (currentTarget.Direction != PortDirection.Input || newTarget.Direction != PortDirection.Input)
        {
            StatusMessage = "仅支持重定向输入端口上的连接。";
            return false;
        }

        if (ReferenceEquals(currentTarget, newTarget))
        {
            return false;
        }

        var incomingConnections = Connections
            .Where(connection => ReferenceEquals(connection.Target, currentTarget))
            .ToList();

        var rewiredCount = 0;

        foreach (var connection in incomingConnections)
        {
            if (!CanConnect(connection.Source, newTarget))
            {
                continue;
            }

            RemoveConnectionInternal(connection, updateVisualState: false);
            TryAddConnection(connection.Source, newTarget, updateStatus: false, updateVisualState: false);
            rewiredCount++;
        }

        if (rewiredCount == 0)
        {
            StatusMessage = "重定向失败：目标端口不支持当前连接。";
            return false;
        }

        RefreshGraphVisualState();
        NotifyToolbarStateChanged();
        StatusMessage = $"已将 {rewiredCount} 条连线重定向到 {newTarget.Owner.Title}.{newTarget.Title}";
        return true;
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
        RefreshGraphVisualState();
        NotifyToolbarStateChanged();
        StatusMessage = $"已添加节点: {item.DisplayName}";
    }

    private bool CanExecuteWorkflow()
    {
        return !IsBusy;
    }

    private bool CanResetWorkflow()
    {
        return !IsBusy;
    }

    private bool CanDeleteSelection()
    {
        return !IsBusy && (SelectedNodes.Count > 0 || SelectedConnections.Count > 0);
    }

    private bool CanRemoveNode(NodeViewModel? node)
    {
        return !IsBusy && node is not null;
    }

    private bool CanRemoveConnection(ConnectionViewModel? connection)
    {
        return !IsBusy && connection is not null;
    }

    private bool CanDisconnectConnector(object? connector)
    {
        return !IsBusy && connector is PortViewModel port && port.ConnectionCount > 0;
    }

    private async Task ExecuteWorkflowAsync()
    {
        var enteredExecutionGate = false;

        try
        {
            enteredExecutionGate = await _executionGate.WaitAsync(0);
            if (!enteredExecutionGate)
            {
                StatusMessage = "执行引擎正在运行，请稍候。";
                return;
            }

            IsBusy = true;
            StatusMessage = "正在执行工作流图...";

            var context = await _workflowEngine.ExecuteAsync(_workflow);
            RefreshGraphVisualState();

            GlobalVariablesText = context.GlobalVariables.Count == 0
                ? "没有全局变量"
                : string.Join(Environment.NewLine, context.GlobalVariables.Select(entry => $"{entry.Key}: {entry.Value}"));

            StatusMessage = $"执行完成，共处理 {context.History.Count} 个节点。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"执行失败：{ex.Message}";
            RefreshGraphVisualState();
        }
        finally
        {
            if (enteredExecutionGate)
            {
                IsBusy = false;
                _executionGate.Release();
            }
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

        RefreshGraphVisualState();
        GlobalVariablesText = "尚未执行";
        StatusMessage = "工作流状态已重置。";
    }

    private void DeleteSelection()
    {
        var removedConnections = 0;
        var removedNodes = 0;

        foreach (var connection in SelectedConnections.ToList())
        {
            if (RemoveConnectionInternal(connection, updateVisualState: false))
            {
                removedConnections++;
            }
        }

        foreach (var node in SelectedNodes.ToList())
        {
            if (RemoveNodeInternal(node, updateVisualState: false))
            {
                removedNodes++;
            }
        }

        RefreshGraphVisualState();
        NotifyToolbarStateChanged();
        StatusMessage = $"已删除 {removedNodes} 个节点和 {removedConnections} 条连线。";
    }

    private void RemoveNode(NodeViewModel? node)
    {
        if (node is null)
        {
            return;
        }

        if (RemoveNodeInternal(node))
        {
            StatusMessage = $"已删除节点: {node.Title}";
        }
    }

    private void RemoveConnection(ConnectionViewModel? connection)
    {
        if (connection is null)
        {
            return;
        }

        if (RemoveConnectionInternal(connection))
        {
            StatusMessage = $"已删除连线: {connection.DisplayName}";
        }
    }

    private void DisconnectConnector(object? connector)
    {
        if (connector is not PortViewModel port)
        {
            return;
        }

        var removedConnections = Connections
            .Where(connection => ReferenceEquals(connection.Source, port) || ReferenceEquals(connection.Target, port))
            .ToList();

        if (removedConnections.Count == 0)
        {
            return;
        }

        foreach (var connection in removedConnections)
        {
            RemoveConnectionInternal(connection, updateVisualState: false);
        }

        RefreshGraphVisualState();
        NotifyToolbarStateChanged();
        StatusMessage = $"已断开端口 {port.Owner.Title}.{port.Title} 的 {removedConnections.Count} 条连线。";
    }

    private void RebuildViewModels()
    {
        Nodes.Clear();
        Connections.Clear();
        SelectedNodes.Clear();
        SelectedConnections.Clear();
        SelectedNode = null;
        SelectedConnection = null;
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

        RefreshGraphVisualState();
        NotifyToolbarStateChanged();
    }

    private void NotifyToolbarStateChanged()
    {
        OnPropertyChanged(nameof(WorkflowGraphSummary));
    }

    private void ToggleTheme()
    {
        WorkbenchThemeManager.SetNextTheme();
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(CurrentThemeText));
        StatusMessage = $"已切换到{CurrentThemeText}主题。";
    }

    partial void OnIsBusyChanged(bool value)
    {
        ResetWorkflowCommand.NotifyCanExecuteChanged();
        DeleteSelectionCommand.NotifyCanExecuteChanged();
        RemoveNodeCommand.NotifyCanExecuteChanged();
        RemoveConnectionCommand.NotifyCanExecuteChanged();
        DisconnectConnectorCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedNodeChanged(NodeViewModel? value)
    {
        RemoveNodeCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedConnectionChanged(ConnectionViewModel? value)
    {
        RemoveConnectionCommand.NotifyCanExecuteChanged();
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

    private bool TryAddConnection(PortViewModel source, PortViewModel target, bool updateStatus = true, bool updateVisualState = true)
    {
        if (!CanConnect(source, target))
        {
            if (updateStatus)
            {
                StatusMessage = "仅支持输出端口连接输入端口，且会忽略重复连接。";
            }

            return false;
        }

        var connection = _workflow.Connect(source.Owner.Id, source.Id, target.Owner.Id, target.Id);
        var connectionViewModel = new ConnectionViewModel(connection, source, target);
        Connections.Add(connectionViewModel);
        source.AttachConnection(connectionViewModel);
        target.AttachConnection(connectionViewModel);

        if (updateVisualState)
        {
            RefreshGraphVisualState();
            NotifyToolbarStateChanged();
        }

        if (updateStatus)
        {
            StatusMessage = $"已连接 {source.Owner.Title}.{source.Title} -> {target.Owner.Title}.{target.Title}";
        }

        return true;
    }

    private Point GetDefaultNodeLocation()
    {
        var index = _workflow.Nodes.Count;
        var x = 120 + (index % 3) * 320;
        var y = 280 + (index / 3) * 220;
        return new Point(x, y);
    }

    private bool RemoveNodeInternal(NodeViewModel node, bool updateVisualState = true)
    {
        var existingNode = Nodes.FirstOrDefault(candidate => ReferenceEquals(candidate, node));
        if (existingNode is null)
        {
            return false;
        }

        foreach (var connection in Connections
                     .Where(connection => ReferenceEquals(connection.Source.Owner, existingNode) || ReferenceEquals(connection.Target.Owner, existingNode))
                     .ToList())
        {
            RemoveConnectionInternal(connection, updateVisualState: false);
        }

        Nodes.Remove(existingNode);
        _workflow.Nodes.Remove(existingNode.Model);
        _nodeIndex.Remove(existingNode.Id);

        if (updateVisualState)
        {
            RefreshGraphVisualState();
            NotifyToolbarStateChanged();
        }

        return true;
    }

    private bool RemoveConnectionInternal(ConnectionViewModel connection, bool updateVisualState = true)
    {
        var existingConnection = Connections.FirstOrDefault(candidate => ReferenceEquals(candidate, connection));
        if (existingConnection is null)
        {
            return false;
        }

        Connections.Remove(existingConnection);
        _workflow.Connections.Remove(existingConnection.Model);
        existingConnection.Source.DetachConnection(existingConnection);
        existingConnection.Target.DetachConnection(existingConnection);

        if (updateVisualState)
        {
            RefreshGraphVisualState();
            NotifyToolbarStateChanged();
        }

        return true;
    }

    private void RefreshGraphVisualState()
    {
        ApplyConnectionState();

        foreach (var node in Nodes)
        {
            node.Refresh();
        }
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

    private void OnSelectionCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        DeleteSelectionCommand.NotifyCanExecuteChanged();
        DisconnectConnectorCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(SelectionText));
    }

    private void OnNodeStatusChanged(object? sender, NodeStatusChangedEventArgs e)
    {
        if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            _ = dispatcher.InvokeAsync(() => OnNodeStatusChanged(sender, e));
            return;
        }

        if (_nodeIndex.TryGetValue(e.NodeId, out var node))
        {
            node.Refresh();
        }
    }

    private void OnPortValueChanged(object? sender, PortValueChangedEventArgs e)
    {
        if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            _ = dispatcher.InvokeAsync(() => OnPortValueChanged(sender, e));
            return;
        }

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
