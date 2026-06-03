using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Threading;
using System.Windows;
using Workflow.Studio.Core.Models;
using Workflow.Studio.Core.Nodes;
using Workflow.Studio.Core.Services;
using Workflow.Studio.Theme;
using Workflow.Studio.Workbench.Services;

namespace Workflow.Studio.Workbench.ViewModels;

public sealed partial class WorkflowWorkbenchViewModel : ObservableObject
{
    private const double ViewportPadding = 120d;
    private const double EstimatedNodeWidth = 220d;
    private const double EstimatedNodeHeight = 164d;
    private readonly WorkflowEngine _workflowEngine;
    private readonly NodeFactory _nodeFactory;
    private readonly WorkflowEventHub _eventHub;
    private readonly IWorkflowConnectionValidator _connectionValidator;
    private readonly IWorkflowDebugController _debugController;
    private readonly IWorkflowPersistenceService _workflowPersistenceService;
    private readonly IWorkflowDocumentPickerService _documentPickerService;
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
    [NotifyPropertyChangedFor(nameof(WorkflowStateText))]
    private bool _isDocumentBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WorkflowDocumentText))]
    private string? _currentWorkflowFilePath;

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

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ResumeExecutionCommand))]
    private bool _isExecutionPaused;

    [ObservableProperty]
    private string? _pausedNodeName;

    public WorkflowWorkbenchViewModel(
        WorkflowEngine workflowEngine,
        NodeFactory nodeFactory,
        WorkflowEventHub eventHub,
        IWorkflowConnectionValidator connectionValidator,
        IWorkflowDebugController debugController,
        IWorkflowPersistenceService workflowPersistenceService,
        IWorkflowDocumentPickerService documentPickerService)
    {
        _workflowEngine = workflowEngine;
        _nodeFactory = nodeFactory;
        _eventHub = eventHub;
        _connectionValidator = connectionValidator;
        _debugController = debugController;
        _workflowPersistenceService = workflowPersistenceService;
        _documentPickerService = documentPickerService;
        WorkbenchThemeManager.EnsureInitialized();
        WorkbenchThemeManager.ThemeChanged += OnThemeChanged;
        _eventHub.NodeStatusChanged += OnNodeStatusChanged;
        _eventHub.PortValueChanged += OnPortValueChanged;
        _debugController.LogEmitted += OnLogEmitted;
        _debugController.BreakpointHit += OnBreakpointHit;

        Nodes = [];
        Connections = [];
        SelectedNodes = [];
        SelectedConnections = [];
        ExecutionLogs = [];
        AvailableNodes = new ObservableCollection<NodeLibraryItemViewModel>(
            _nodeFactory.GetAvailableNodes().Select(descriptor => new NodeLibraryItemViewModel(descriptor)));
        PendingConnection = new PendingConnectionViewModel(this);
        ExecuteWorkflowCommand = new AsyncRelayCommand(ExecuteWorkflowAsync, CanExecuteWorkflow);
        SaveWorkflowCommand = new AsyncRelayCommand(SaveWorkflowAsync, CanManageWorkflowDocument);
        LoadWorkflowCommand = new AsyncRelayCommand(LoadWorkflowAsync, CanManageWorkflowDocument);
        ResetWorkflowCommand = new RelayCommand(ResetWorkflow, CanResetWorkflow);
        DeleteSelectionCommand = new RelayCommand(DeleteSelection, CanDeleteSelection);
        RemoveNodeCommand = new RelayCommand<NodeViewModel?>(RemoveNode, CanRemoveNode);
        RemoveConnectionCommand = new RelayCommand<ConnectionViewModel?>(RemoveConnection, CanRemoveConnection);
        DisconnectConnectorCommand = new RelayCommand<object?>(DisconnectConnector, CanDisconnectConnector);
        AddNodeCommand = new RelayCommand<NodeLibraryItemViewModel?>(AddNode);
        ToggleThemeCommand = new RelayCommand(ToggleTheme);
        ResumeExecutionCommand = new RelayCommand(ResumeExecution, CanResumeExecution);
        ToggleBreakpointCommand = new RelayCommand(ToggleBreakpoint, CanToggleBreakpoint);
        FitGraphCommand = new RelayCommand(FitGraphToViewport, CanFitGraphToViewport);
        FitSelectionCommand = new RelayCommand(FitSelectionToViewport, CanFitSelectionToViewport);
        ResetViewportCommand = new RelayCommand(ResetViewport);
        SelectAllNodesCommand = new RelayCommand(SelectAllNodes, CanSelectAllNodes);
        ClearExecutionLogCommand = new RelayCommand(ClearExecutionLog, CanClearExecutionLog);

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

    public ObservableCollection<ExecutionLogItemViewModel> ExecutionLogs { get; }

    public PendingConnectionViewModel PendingConnection { get; }

    public IAsyncRelayCommand ExecuteWorkflowCommand { get; }

    public IAsyncRelayCommand SaveWorkflowCommand { get; }

    public IAsyncRelayCommand LoadWorkflowCommand { get; }

    public IRelayCommand ResetWorkflowCommand { get; }

    public IRelayCommand DeleteSelectionCommand { get; }

    public IRelayCommand<NodeViewModel?> RemoveNodeCommand { get; }

    public IRelayCommand<ConnectionViewModel?> RemoveConnectionCommand { get; }

    public IRelayCommand<object?> DisconnectConnectorCommand { get; }

    public IRelayCommand<NodeLibraryItemViewModel?> AddNodeCommand { get; }

    public IRelayCommand ToggleThemeCommand { get; }

    public IRelayCommand ResumeExecutionCommand { get; }

    public IRelayCommand ToggleBreakpointCommand { get; }

    public IRelayCommand FitGraphCommand { get; }

    public IRelayCommand FitSelectionCommand { get; }

    public IRelayCommand ResetViewportCommand { get; }

    public IRelayCommand SelectAllNodesCommand { get; }

    public IRelayCommand ClearExecutionLogCommand { get; }

    public string WorkflowStateText => IsBusy ? "执行中" : IsDocumentBusy ? "处理中" : "就绪";

    public string WorkflowDocumentText => string.IsNullOrWhiteSpace(CurrentWorkflowFilePath)
        ? "当前文档: 内置 Demo"
        : $"当前文档: {Path.GetFileName(CurrentWorkflowFilePath)}";

    public string WorkflowGraphSummary => $"节点 {Nodes.Count} · 连线 {Connections.Count}";

    public string CurrentThemeText => WorkbenchThemeManager.ActiveThemeDisplayName;

    public double MinViewportZoom => 0.2d;

    public double MaxViewportZoom => 2.5d;

    public string ViewportText => $"视口 X {ViewportLocation.X:0} · Y {ViewportLocation.Y:0} · 缩放 {ViewportZoom:P0}";

    public string SelectionText => $"已选节点 {SelectedNodes.Count} · 已选连线 {SelectedConnections.Count}";

    public string DebugStateText => IsExecutionPaused
        ? $"断点暂停于 {PausedNodeName}"
        : "调试会话空闲";

    public string ExecutionLogSummary => $"日志 {ExecutionLogs.Count}";

    public void NotifyNodeSettingsChanged(NodeViewModel node)
    {
        node.NotifySettingsChanged();
        StatusMessage = $"已更新节点设置: {node.Title}";
    }

    public void Connect(PortViewModel source, PortViewModel target)
    {
        if (IsWorkbenchBusy)
        {
            StatusMessage = "工作台正在处理任务，请稍候后再连接端口。";
            return;
        }

        _ = TryAddConnection(source, target);
    }

    public bool RewireConnection(PortViewModel currentTarget, PortViewModel newTarget)
    {
        ArgumentNullException.ThrowIfNull(currentTarget);
        ArgumentNullException.ThrowIfNull(newTarget);

        if (IsWorkbenchBusy)
        {
            StatusMessage = "工作台正在处理任务，请稍候后再重定向连线。";
            return false;
        }

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

        if (IsWorkbenchBusy)
        {
            StatusMessage = "工作台正在处理任务，请稍候后再添加节点。";
            return;
        }

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
        return !IsWorkbenchBusy;
    }

    private bool CanManageWorkflowDocument()
    {
        return !IsWorkbenchBusy;
    }

    private bool CanResetWorkflow()
    {
        return !IsWorkbenchBusy;
    }

    private bool CanDeleteSelection()
    {
        return !IsWorkbenchBusy && (SelectedNodes.Count > 0 || SelectedConnections.Count > 0);
    }

    private bool CanResumeExecution()
    {
        return IsExecutionPaused;
    }

    private bool CanToggleBreakpoint()
    {
        return !IsWorkbenchBusy && SelectedNode is not null;
    }

    private bool CanFitGraphToViewport()
    {
        return Nodes.Count > 0;
    }

    private bool CanFitSelectionToViewport()
    {
        return SelectedNodes.Count > 0;
    }

    private bool CanSelectAllNodes()
    {
        return Nodes.Count > 0;
    }

    private bool CanClearExecutionLog()
    {
        return ExecutionLogs.Count > 0;
    }

    private bool CanRemoveNode(NodeViewModel? node)
    {
        return !IsWorkbenchBusy && node is not null;
    }

    private bool CanRemoveConnection(ConnectionViewModel? connection)
    {
        return !IsWorkbenchBusy && connection is not null;
    }

    private bool CanDisconnectConnector(object? connector)
    {
        return !IsWorkbenchBusy && connector is PortViewModel port && port.ConnectionCount > 0;
    }

    private async Task SaveWorkflowAsync()
    {
        var filePath = _documentPickerService.PickSaveFilePath(CurrentWorkflowFilePath);
        if (string.IsNullOrWhiteSpace(filePath))
        {
            StatusMessage = "已取消保存工作流。";
            return;
        }

        try
        {
            IsDocumentBusy = true;
            StatusMessage = "正在保存工作流...";
            await _workflowPersistenceService.SaveAsync(_workflow, filePath);
            CurrentWorkflowFilePath = filePath;
            StatusMessage = $"已保存工作流: {Path.GetFileName(filePath)}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"保存工作流失败：{ex.Message}";
        }
        finally
        {
            IsDocumentBusy = false;
        }
    }

    private async Task LoadWorkflowAsync()
    {
        var filePath = _documentPickerService.PickOpenFilePath(CurrentWorkflowFilePath);
        if (string.IsNullOrWhiteSpace(filePath))
        {
            StatusMessage = "已取消加载工作流。";
            return;
        }

        try
        {
            IsDocumentBusy = true;
            StatusMessage = "正在加载工作流...";

            var workflow = await _workflowPersistenceService.LoadAsync(filePath);
            _workflow = workflow;
            CurrentWorkflowFilePath = filePath;
            RebuildViewModels();
            GlobalVariablesText = BuildGlobalVariablesText(_workflow.GlobalVariables, "没有全局变量");
            StatusMessage = $"已加载工作流: {Path.GetFileName(filePath)}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载工作流失败：{ex.Message}";
        }
        finally
        {
            IsDocumentBusy = false;
        }
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
            IsExecutionPaused = false;
            PausedNodeName = null;
            ExecutionLogs.Clear();
            ClearNodeRuntimeHighlights();
            StatusMessage = "正在执行工作流图...";

            // Offload workflow execution so synchronous work inside nodes does not block the UI thread.
            var context = await Task.Run(
                () => _workflowEngine.ExecuteAsync(_workflow),
                CancellationToken.None);
            RefreshGraphVisualState();

            GlobalVariablesText = BuildGlobalVariablesText(context.GlobalVariables, "没有全局变量");

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
                IsExecutionPaused = false;
                PausedNodeName = null;
                _executionGate.Release();
            }
        }
    }

    private void ResetWorkflow()
    {
        if (IsWorkbenchBusy)
        {
            return;
        }

        foreach (var node in _workflow.Nodes)
        {
            node.ClearRuntimeState();

            foreach (var port in node.InputPorts.Concat(node.OutputPorts))
            {
                port.Clear();
            }
        }

        RefreshGraphVisualState();
        GlobalVariablesText = "尚未执行";
        ExecutionLogs.Clear();
        IsExecutionPaused = false;
        PausedNodeName = null;
        StatusMessage = "工作流状态已重置。";
    }

    private void ResumeExecution()
    {
        _debugController.Resume();
        IsExecutionPaused = false;
        StatusMessage = "已继续执行工作流。";
    }

    private void ToggleBreakpoint()
    {
        if (SelectedNode is null)
        {
            return;
        }

        SelectedNode.SetBreakpointEnabled(!SelectedNode.IsBreakpointEnabled);
        SelectedNode.Refresh();
        StatusMessage = SelectedNode.IsBreakpointEnabled
            ? $"已为节点启用断点: {SelectedNode.Title}"
            : $"已移除节点断点: {SelectedNode.Title}";
    }

    private void FitGraphToViewport()
    {
        ApplyViewportToBounds(Nodes.Select(node => node.Location).ToList());
        StatusMessage = "已适配全部节点到当前视口。";
    }

    private void FitSelectionToViewport()
    {
        ApplyViewportToBounds(SelectedNodes.Select(node => node.Location).ToList());
        StatusMessage = "已适配当前选中节点到视口。";
    }

    private void ResetViewport()
    {
        ViewportZoom = 1d;
        ViewportLocation = new Point(0d, 0d);
        StatusMessage = "已重置缩放与视口位置。";
    }

    private void SelectAllNodes()
    {
        SelectedNodes.Clear();

        foreach (var node in Nodes)
        {
            SelectedNodes.Add(node);
        }

        SelectedConnections.Clear();
        StatusMessage = $"已选中全部节点，共 {SelectedNodes.Count} 个。";
    }

    private void ClearExecutionLog()
    {
        ExecutionLogs.Clear();
        ClearExecutionLogCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(ExecutionLogSummary));
        StatusMessage = "已清空执行日志。";
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
        OnPropertyChanged(nameof(ExecutionLogSummary));
        FitGraphCommand.NotifyCanExecuteChanged();
        FitSelectionCommand.NotifyCanExecuteChanged();
        SelectAllNodesCommand.NotifyCanExecuteChanged();
        ClearExecutionLogCommand.NotifyCanExecuteChanged();
        ToggleBreakpointCommand.NotifyCanExecuteChanged();
        ResumeExecutionCommand.NotifyCanExecuteChanged();
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
        ExecuteWorkflowCommand.NotifyCanExecuteChanged();
        SaveWorkflowCommand.NotifyCanExecuteChanged();
        LoadWorkflowCommand.NotifyCanExecuteChanged();
        ResetWorkflowCommand.NotifyCanExecuteChanged();
        DeleteSelectionCommand.NotifyCanExecuteChanged();
        RemoveNodeCommand.NotifyCanExecuteChanged();
        RemoveConnectionCommand.NotifyCanExecuteChanged();
        DisconnectConnectorCommand.NotifyCanExecuteChanged();
        ToggleBreakpointCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsDocumentBusyChanged(bool value)
    {
        ExecuteWorkflowCommand.NotifyCanExecuteChanged();
        SaveWorkflowCommand.NotifyCanExecuteChanged();
        LoadWorkflowCommand.NotifyCanExecuteChanged();
        ResetWorkflowCommand.NotifyCanExecuteChanged();
        DeleteSelectionCommand.NotifyCanExecuteChanged();
        RemoveNodeCommand.NotifyCanExecuteChanged();
        RemoveConnectionCommand.NotifyCanExecuteChanged();
        DisconnectConnectorCommand.NotifyCanExecuteChanged();
        ToggleBreakpointCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedNodeChanged(NodeViewModel? value)
    {
        RemoveNodeCommand.NotifyCanExecuteChanged();
        ToggleBreakpointCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedConnectionChanged(ConnectionViewModel? value)
    {
        RemoveConnectionCommand.NotifyCanExecuteChanged();
    }

    private bool CanConnect(PortViewModel source, PortViewModel target)
    {
        return ValidateConnection(source, target).IsValid;
    }

    private bool TryAddConnection(PortViewModel source, PortViewModel target, bool updateStatus = true, bool updateVisualState = true)
    {
        var validationResult = ValidateConnection(source, target);
        if (!validationResult.IsValid)
        {
            if (updateStatus)
            {
                StatusMessage = validationResult.Message;
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

    private void ApplyViewportToBounds(IReadOnlyList<Point> nodeLocations)
    {
        if (nodeLocations.Count == 0)
        {
            return;
        }

        var left = nodeLocations.Min(location => location.X) - ViewportPadding;
        var top = nodeLocations.Min(location => location.Y) - ViewportPadding;
        var right = nodeLocations.Max(location => location.X + EstimatedNodeWidth) + ViewportPadding;
        var bottom = nodeLocations.Max(location => location.Y + EstimatedNodeHeight) + ViewportPadding;
        var boundsWidth = Math.Max(1d, right - left);
        var boundsHeight = Math.Max(1d, bottom - top);

        if (ViewportSize.Width <= 0 || ViewportSize.Height <= 0)
        {
            ViewportLocation = new Point(left, top);
            return;
        }

        var zoomX = ViewportSize.Width / boundsWidth;
        var zoomY = ViewportSize.Height / boundsHeight;
        var zoom = Math.Clamp(Math.Min(zoomX, zoomY), MinViewportZoom, MaxViewportZoom);
        var worldViewportWidth = ViewportSize.Width / zoom;
        var worldViewportHeight = ViewportSize.Height / zoom;

        ViewportZoom = zoom;
        ViewportLocation = new Point(
            left - ((worldViewportWidth - boundsWidth) / 2d),
            top - ((worldViewportHeight - boundsHeight) / 2d));
    }

    private void ClearNodeRuntimeHighlights()
    {
        foreach (var node in _workflow.Nodes)
        {
            node.SetLastError(null);
            node.SetLastMessage(null);
        }

        RefreshGraphVisualState();
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
        FitSelectionCommand.NotifyCanExecuteChanged();
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

    private void OnLogEmitted(object? sender, WorkflowLogEventArgs e)
    {
        if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            _ = dispatcher.InvokeAsync(() => OnLogEmitted(sender, e));
            return;
        }

        ExecutionLogs.Insert(0, new ExecutionLogItemViewModel(e.Entry));
        while (ExecutionLogs.Count > 200)
        {
            ExecutionLogs.RemoveAt(ExecutionLogs.Count - 1);
        }

        OnPropertyChanged(nameof(ExecutionLogSummary));
        ClearExecutionLogCommand.NotifyCanExecuteChanged();
    }

    private void OnBreakpointHit(object? sender, BreakpointHitEventArgs e)
    {
        if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            _ = dispatcher.InvokeAsync(() => OnBreakpointHit(sender, e));
            return;
        }

        IsExecutionPaused = true;
        PausedNodeName = e.NodeName;
        StatusMessage = $"命中断点：{e.NodeName}";
    }

    private static string BuildGlobalVariablesText(IEnumerable<KeyValuePair<string, object?>> globalVariables, string emptyText)
    {
        var entries = globalVariables.ToList();
        return entries.Count == 0
            ? emptyText
            : string.Join(Environment.NewLine, entries.Select(entry => $"{entry.Key}: {entry.Value}"));
    }

    private ConnectionValidationResult ValidateConnection(PortViewModel source, PortViewModel target)
    {
        return _connectionValidator.ValidateConnection(
            _workflow,
            source.Owner.Model,
            source.Model,
            target.Owner.Model,
            target.Model);
    }

    private bool IsWorkbenchBusy => IsBusy || IsDocumentBusy;

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
