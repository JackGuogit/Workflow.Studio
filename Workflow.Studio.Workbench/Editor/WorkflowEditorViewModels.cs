using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Workflow.Studio.Core.Catalog;
using Workflow.Studio.Core.Documents;
using Workflow.Studio.Core.Session;
using Workflow.Studio.Nodify;

namespace Workflow.Studio.Workbench.Editor;

/// <summary>
/// 端口编辑视图模型：渲染端口表面与当前值预览。
/// </summary>
public sealed class PortViewModel : ObservableObject
{
    private Point _anchor;
    private bool _isConnected;

    public Point Anchor
    {
        get => _anchor;
        set => SetProperty(ref _anchor, value);
    }

    public bool IsConnected
    {
        get => _isConnected;
        private set => SetProperty(ref _isConnected, value);
    }

    public PortViewModel(string nodeId, PortSlot slot, bool isInput)
    {
        NodeId = nodeId;
        PortId = slot.PortId;
        TypeId = slot.TypeId;
        DisplayName = string.IsNullOrWhiteSpace(slot.DisplayName) ? slot.PortId : slot.DisplayName;
        IsInput = isInput;
        Slot = slot;
    }

    public string NodeId { get; }

    public string PortId { get; }

    public string TypeId { get; }

    public string DisplayName { get; }

    public string Title => DisplayName;

    public string TypeDisplayText => TypeId;

    public string SemanticBadgeText => TypeId;

    public bool IsInput { get; }

    public PortSlot Slot { get; }

    public string ValuePreview
    {
        get
        {
            if (!Slot.HasValue)
            {
                return string.Empty;
            }

            var text = Slot.DisplayValue is null ? "(null)" : Convert.ToString(Slot.DisplayValue) ?? string.Empty;
            return text.Length > 80 ? $"{text[..80]}…" : text;
        }
    }

    public void RefreshValue()
    {
        OnPropertyChanged(nameof(ValuePreview));
    }

    internal void SetIsConnected(bool isConnected)
    {
        IsConnected = isConnected;
    }
}

/// <summary>
/// 节点编辑视图模型：文档结构 + Session 状态投影。
/// </summary>
public sealed class NodeViewModel : ObservableObject
{
    private string _stateText = NodeState.NotConfigured.ToString();
    private string? _errorText;
    private bool _isSelected;
    private Size _nodeSize = new(200, 150);

    public NodeViewModel(NodeDocument document, NodeRuntime runtime, NodeTypeDescriptor descriptor)
    {
        Model = document;
        Runtime = runtime;
        Descriptor = descriptor;

        Inputs = new ObservableCollection<PortViewModel>(
            runtime.InputPorts.Select(port => new PortViewModel(document.NodeId, port, isInput: true)));
        Outputs = new ObservableCollection<PortViewModel>(
            runtime.OutputPorts.Select(port => new PortViewModel(document.NodeId, port, isInput: false)));

        Refresh();
    }

    public NodeDocument Model { get; }

    public NodeRuntime Runtime { get; }

    public NodeTypeDescriptor Descriptor { get; }

    public ObservableCollection<PortViewModel> Inputs { get; }

    public ObservableCollection<PortViewModel> Outputs { get; }

    public string NodeId => Model.NodeId;

    public string Title => Descriptor.DisplayName;

    public string Description => Descriptor.Description;

    public string NodeTypeText => $"节点类型: {Model.NodeTypeId}";

    public string Category => Descriptor.Category;

    public bool IsMetaNode => Model.InnerWorkflow is not null;

    public string PortSummary => $"{Inputs.Count} in / {Outputs.Count} out";

    public string PreviewText
    {
        get
        {
            var active = Outputs.FirstOrDefault(port => port.Slot.HasValue);
            return active is null ? "暂无数据" : $"{active.DisplayName}: {active.ValuePreview}";
        }
    }

    public double X
    {
        get => Model.X;
        set
        {
            if (!EqualityComparer<double>.Default.Equals(Model.X, value))
            {
                Model.X = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Location));
            }
        }
    }

    public double Y
    {
        get => Model.Y;
        set
        {
            if (!EqualityComparer<double>.Default.Equals(Model.Y, value))
            {
                Model.Y = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Location));
            }
        }
    }

    public Point Location
    {
        get => new(X, Y);
        set
        {
            X = value.X;
            Y = value.Y;
            OnPropertyChanged();
        }
    }

    public string StateText
    {
        get => _stateText;
        private set
        {
            if (SetProperty(ref _stateText, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public string? ErrorText
    {
        get => _errorText;
        private set => SetProperty(ref _errorText, value);
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);

    public bool IsBreakpointEnabled => Model.IsBreakpointEnabled;

    public string BreakpointText => IsBreakpointEnabled ? "● 断点" : string.Empty;

    public NodeState State => Runtime.State;

    public Size Size
    {
        get => _nodeSize;
        set => SetProperty(ref _nodeSize, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                SelectionChanged?.Invoke(this, value);
            }
        }
    }

    public event Action<NodeViewModel, bool>? SelectionChanged;

    public void Refresh()
    {
        StateText = Runtime.State.ToString();
        ErrorText = Runtime.LastError;
        OnPropertyChanged(nameof(IsBreakpointEnabled));
        OnPropertyChanged(nameof(BreakpointText));
        OnPropertyChanged(nameof(State));

        foreach (var port in Inputs.Concat(Outputs))
        {
            OnPortChanged(port);
        }
    }

    private void OnPortChanged(PortViewModel port)
    {
        port.RefreshValue();
    }
}

public sealed class ConnectionViewModel
{
    public ConnectionViewModel(ConnectionDocument model, NodeViewModel sourceNode, NodeViewModel targetNode)
    {
        Model = model;
        SourceNode = sourceNode;
        TargetNode = targetNode;
        SourcePort = sourceNode.Outputs.FirstOrDefault(port =>
            string.Equals(port.PortId, model.SourcePortId, StringComparison.OrdinalIgnoreCase))!;
        TargetPort = targetNode.Inputs.FirstOrDefault(port =>
            string.Equals(port.PortId, model.TargetPortId, StringComparison.OrdinalIgnoreCase))!;
    }

    public ConnectionDocument Model { get; }

    public NodeViewModel SourceNode { get; }

    public NodeViewModel TargetNode { get; }

    public PortViewModel SourcePort { get; }

    public PortViewModel TargetPort { get; }

    public string DisplayName => $"{SourceNode.Title}.{Model.SourcePortId} -> {TargetNode.Title}.{Model.TargetPortId}";
}

public sealed class PendingConnectionViewModel
{
    private PortViewModel? _source;

    public PendingConnectionViewModel(WorkflowEditorViewModel editor)
    {
        StartCommand = new RelayCommand<PortViewModel?>(source => _source = source);
        FinishCommand = new RelayCommand<PortViewModel?>(target =>
        {
            if (_source is null || target is null)
            {
                return;
            }

            editor.TryConnect(_source, target, out _);
            _source = null;
        });
    }

    public IRelayCommand<PortViewModel?> StartCommand { get; }

    public IRelayCommand<PortViewModel?> FinishCommand { get; }
}

public sealed class NodeLibraryItemViewModel
{
    public NodeLibraryItemViewModel(NodeTypeDescriptor descriptor)
    {
        Descriptor = descriptor;
        IsExternal = descriptor.IsExternal;
    }

    public NodeTypeDescriptor Descriptor { get; }

    public string TypeId => Descriptor.TypeId;

    public string DisplayName => Descriptor.DisplayName;

    public string Category => Descriptor.Category;

    public bool IsExternal { get; }
}

public sealed class EntryVariableViewModel : ObservableObject
{
    private string _value = string.Empty;

    public EntryVariableViewModel(VariableDeclaration declaration, object? currentValue)
    {
        Declaration = declaration;
        _value = Convert.ToString(currentValue) ?? string.Empty;
    }

    public VariableDeclaration Declaration { get; }

    public string Name => Declaration.Name;

    public string TypeId => Declaration.TypeId;

    public string Value
    {
        get => _value;
        set
        {
            if (SetProperty(ref _value, value))
            {
                ValueChanged?.Invoke(this, value);
            }
        }
    }

    public event Action<EntryVariableViewModel, string>? ValueChanged;

    public void Refresh(object? currentValue)
    {
        _value = Convert.ToString(currentValue) ?? string.Empty;
        OnPropertyChanged(nameof(Value));
    }
}

/// <summary>
/// 单文档编辑器 VM：结构编辑（注册表校验）+ Session 投影 + 执行命令。
/// 线程封送由 UI 宿主负责，本类不引用 Dispatcher。
/// </summary>
public sealed partial class WorkflowEditorViewModel : ObservableObject
{
    private readonly WorkflowDefinitionRegistry _registry;
    private readonly int? _maxConcurrency;
    private WorkflowDocumentEditor _editor;
    private WorkflowSession _session = null!;
    private string _statusMessage = "就绪";
    private string _title = "未命名工作流";
    private string? _documentFilePath;
    private readonly Stack<ContainerNavigationEntry> _navigationStack = new();
    private readonly HashSet<string> _selectedNodeIds = new(StringComparer.OrdinalIgnoreCase);
    private bool _suppressSelectionSync;
    private ObservableCollection<NodeViewModel> _selectedNodes = [];
    private readonly SemaphoreSlim _executionGate = new(1, 1);
    private bool _isBusy;
    private readonly Stack<WorkflowDocument> _undoStack = new();
    private readonly Stack<WorkflowDocument> _redoStack = new();
    private TaskCompletionSource<bool>? _pauseSignal;
    private bool _isPaused;
    private string? _pausedNodeName;
    private Point _viewportLocation;
    private double _viewportZoom = 1d;
    private Size _viewportSize = new(1200, 800);
    private readonly System.Threading.SynchronizationContext? _synchronizationContext = System.Threading.SynchronizationContext.Current;

    public WorkflowEditorViewModel(
        WorkflowDocument document,
        WorkflowDefinitionRegistry registry,
        int? maxConcurrency = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(registry);

        Document = document;
        _registry = registry;
        _maxConcurrency = maxConcurrency;
        _editor = new WorkflowDocumentEditor(document, registry);

        AvailableNodes = new ObservableCollection<NodeLibraryItemViewModel>(
            registry.Descriptors.Select(descriptor => new NodeLibraryItemViewModel(descriptor)));

        Nodes = [];
        Connections = [];
        EntryVariables = [];
        ExecutionLogs = [];
        PendingConnection = new PendingConnectionViewModel(this);
        _selectedNodes.CollectionChanged += OnSelectedNodesCollectionChanged;

        ExecuteAllCommand = new AsyncRelayCommand(ExecuteAllAsync, () => !IsBusy);
        AddNodeCommand = new RelayCommand<string>(typeId =>
            AddNode(typeId!, GetDefaultX(), GetDefaultY()));
        ResetCommand = new RelayCommand(Reset);
        NavigateBackCommand = new RelayCommand(NavigateBack, () => CanNavigateBack);
        PackSelectedCommand = new RelayCommand(PackSelected, () => _selectedNodeIds.Count >= 2);
        UnpackSelectedCommand = new RelayCommand(UnpackSelected, CanUnpackSelected);
        UndoCommand = new RelayCommand(Undo, () => CanUndo);
        RedoCommand = new RelayCommand(Redo, () => CanRedo);
        ResumeCommand = new RelayCommand(Resume, () => IsPaused);
        ToggleBreakpointCommand = new RelayCommand(ToggleSelectedBreakpoint, () => _selectedNodeIds.Count == 1);
        DeleteSelectionCommand = new RelayCommand(DeleteSelection);
        SelectAllNodesCommand = new RelayCommand(SelectAllNodes);
        FitGraphCommand = new RelayCommand(FitGraph);
        ResetViewportCommand = new RelayCommand(ResetViewport);

        RebuildSession();
    }

    public WorkflowDocument Document { get; private set; }

    public WorkflowSession Session => _session;

    public object EditorGestures { get; } = NodifyGestures.CreateDefault();

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                ExecuteAllCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string? DocumentFilePath
    {
        get => _documentFilePath;
        private set => SetProperty(ref _documentFilePath, value);
    }

    public bool CanNavigateBack => _navigationStack.Count > 0;

    public bool IsInsideContainer => _navigationStack.Count > 0;

    public IRelayCommand NavigateBackCommand { get; }

    public IRelayCommand PackSelectedCommand { get; }

    public IRelayCommand UnpackSelectedCommand { get; }

    public IRelayCommand UndoCommand { get; }

    public IRelayCommand RedoCommand { get; }

    public bool CanUndo => !IsInsideContainer && _undoStack.Count > 0;

    public bool CanRedo => !IsInsideContainer && _redoStack.Count > 0;

    public bool IsPaused
    {
        get => _isPaused;
        private set
        {
            if (SetProperty(ref _isPaused, value))
            {
                ResumeCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string? PausedNodeName
    {
        get => _pausedNodeName;
        private set => SetProperty(ref _pausedNodeName, value);
    }

    public IRelayCommand ResumeCommand { get; }

    public IRelayCommand ToggleBreakpointCommand { get; }

    public IRelayCommand DeleteSelectionCommand { get; }

    public IRelayCommand SelectAllNodesCommand { get; }

    public IRelayCommand FitGraphCommand { get; }

    public IRelayCommand ResetViewportCommand { get; }

    public Point ViewportLocation
    {
        get => _viewportLocation;
        set => SetProperty(ref _viewportLocation, value);
    }

    public double ViewportZoom
    {
        get => _viewportZoom;
        set => SetProperty(ref _viewportZoom, value);
    }

    public Size ViewportSize
    {
        get => _viewportSize;
        set => SetProperty(ref _viewportSize, value);
    }

    public double MinViewportZoom => 0.2d;

    public double MaxViewportZoom => 2.5d;

    public IReadOnlyCollection<string> SelectedNodeIds => _selectedNodeIds.ToList();

    public IReadOnlyList<VariableMapping> SelectedMetaMappings
    {
        get
        {
            if (_selectedNodeIds.Count != 1)
            {
                return [];
            }

            var node = Document.Nodes.FirstOrDefault(candidate =>
                string.Equals(candidate.NodeId, _selectedNodeIds.First(), StringComparison.OrdinalIgnoreCase));

            return node?.VariableMappings ?? [];
        }
    }

    public IReadOnlyList<NodeSettingField> GetSettingsSchema(string nodeTypeId)
    {
        var descriptor = _registry.Descriptors.FirstOrDefault(candidate =>
            string.Equals(candidate.TypeId, nodeTypeId, StringComparison.OrdinalIgnoreCase));

        return descriptor?.SettingsFields ?? [];
    }

    public void NotifyNodeSettingsSaved(string nodeId)
    {
        StatusMessage = $"已更新节点 '{nodeId}' 的设置（下次执行时生效）。";
    }

    public async Task SaveAsync(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (_navigationStack.Count > 0)
        {
            throw new InvalidOperationException("请先返回工作流顶层再保存。");
        }

        await WorkflowDocumentSerializer.SaveAsync(Document, filePath);
        DocumentFilePath = Path.GetFullPath(filePath);
        Title = Path.GetFileName(filePath);
    }

    public bool TryNavigateInto(string nodeId)
    {
        var containerNode = Document.Nodes.FirstOrDefault(node =>
            string.Equals(node.NodeId, nodeId, StringComparison.OrdinalIgnoreCase));

        if (containerNode?.InnerWorkflow is not { } innerWorkflow)
        {
            return false;
        }

        var boundaryNodeIds = innerWorkflow.Nodes
            .Where(node => string.Equals(node.NodeTypeId, ContainerTypeIds.BoundaryIn, StringComparison.OrdinalIgnoreCase)
                || string.Equals(node.NodeTypeId, ContainerTypeIds.BoundaryOut, StringComparison.OrdinalIgnoreCase))
            .Select(node => node.NodeId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var projection = new WorkflowDocument
        {
            SchemaVersion = WorkflowDocument.CurrentSchemaVersion,
            VariableDeclarations = innerWorkflow.VariableDeclarations.ToList(),
            Nodes = innerWorkflow.Nodes.Where(node => !boundaryNodeIds.Contains(node.NodeId)).ToList(),
            Connections = []
        };

        var boundaryConnections = new List<ConnectionDocument>();
        foreach (var connection in innerWorkflow.Connections)
        {
            if (boundaryNodeIds.Contains(connection.SourceNodeId) || boundaryNodeIds.Contains(connection.TargetNodeId))
            {
                boundaryConnections.Add(CloneConnection(connection));
            }
            else
            {
                projection.Connections.Add(CloneConnection(connection));
            }
        }

        var boundaryNodes = innerWorkflow.Nodes
            .Where(node => boundaryNodeIds.Contains(node.NodeId))
            .ToList();

        _navigationStack.Push(new ContainerNavigationEntry(
            Document,
            projection,
            innerWorkflow,
            boundaryNodes,
            boundaryConnections));

        Document = projection;
        RebuildSession();
        NavigateBackCommand.NotifyCanExecuteChanged();
        StatusMessage = $"已进入元节点 '{containerNode.NodeId}'（可返回上级）。";
        return true;
    }

    public void NavigateBack()
    {
        if (_navigationStack.Count == 0)
        {
            return;
        }

        var entry = _navigationStack.Pop();
        SyncContainer(entry);
        Document = entry.ParentDocument;
        RebuildSession();
        NavigateBackCommand.NotifyCanExecuteChanged();
        StatusMessage = "已返回上级工作流。";
    }

    private static void SyncContainer(ContainerNavigationEntry entry)
    {
        entry.InnerWorkflow.Nodes.Clear();
        foreach (var boundaryNode in entry.BoundaryNodes)
        {
            entry.InnerWorkflow.Nodes.Add(boundaryNode);
        }

        foreach (var node in entry.ProjectedDocument.Nodes)
        {
            entry.InnerWorkflow.Nodes.Add(node);
        }

        entry.InnerWorkflow.Connections.Clear();
        foreach (var connection in entry.BoundaryConnections)
        {
            entry.InnerWorkflow.Connections.Add(CloneConnection(connection));
        }

        foreach (var connection in entry.ProjectedDocument.Connections)
        {
            entry.InnerWorkflow.Connections.Add(CloneConnection(connection));
        }
    }

    private static ConnectionDocument CloneConnection(ConnectionDocument connection)
    {
        return new ConnectionDocument
        {
            SourceNodeId = connection.SourceNodeId,
            SourcePortId = connection.SourcePortId,
            TargetNodeId = connection.TargetNodeId,
            TargetPortId = connection.TargetPortId
        };
    }

    public ObservableCollection<NodeLibraryItemViewModel> AvailableNodes { get; }

    public ObservableCollection<NodeViewModel> Nodes { get; }

    public ObservableCollection<ConnectionViewModel> Connections { get; }

    public ObservableCollection<NodeViewModel> SelectedNodes
    {
        get => _selectedNodes;
        set
        {
            if (ReferenceEquals(_selectedNodes, value))
            {
                return;
            }

            _selectedNodes.CollectionChanged -= OnSelectedNodesCollectionChanged;
            _selectedNodes = value ?? [];
            _selectedNodes.CollectionChanged += OnSelectedNodesCollectionChanged;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<EntryVariableViewModel> EntryVariables { get; }

    public ObservableCollection<string> ExecutionLogs { get; }

    public PendingConnectionViewModel PendingConnection { get; }

    public NodeViewModel? SelectedNode
    {
        get
        {
            if (_selectedNodeIds.Count != 1)
            {
                return null;
            }

            var nodeId = _selectedNodeIds.First();
            return Nodes.FirstOrDefault(node => string.Equals(node.NodeId, nodeId, StringComparison.OrdinalIgnoreCase));
        }
    }

    public IAsyncRelayCommand ExecuteAllCommand { get; }

    public IRelayCommand<string> AddNodeCommand { get; }

    public IRelayCommand ResetCommand { get; }

    public NodeViewModel AddNode(string typeId, double x = 0, double y = 0)
    {
        PushUndoSnapshot();

        var descriptor = _registry.Descriptors.FirstOrDefault(candidate =>
            string.Equals(candidate.TypeId, typeId, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException($"Node type '{typeId}' is not registered.");

        var node = _editor.AddNode(typeId, x, y);
        RebuildSession();
        return Nodes.First(candidate => candidate.NodeId == node.NodeId);
    }

    public bool RemoveNode(string nodeId)
    {
        PushUndoSnapshot();
        var removed = _editor.RemoveNode(nodeId);
        if (removed)
        {
            RebuildSession();
        }

        return removed;
    }

    public bool TryConnect(PortViewModel source, PortViewModel target, out string? error)
    {
        PushUndoSnapshot();

        if (source.IsInput || !target.IsInput)
        {
            error = "仅支持输出端口连接输入端口。";
            return false;
        }

        var connected = _editor.TryConnect(source.NodeId, source.PortId, target.NodeId, target.PortId, out error);
        if (connected)
        {
            StatusMessage = "已连接。";
            RebuildSession();
        }
        else
        {
            StatusMessage = error ?? "连接失败。";
        }

        return connected;
    }

    public bool RemoveConnection(ConnectionViewModel connection)
    {
        PushUndoSnapshot();
        var model = connection.Model;
        var removed = _editor.RemoveConnection(model.SourceNodeId, model.SourcePortId, model.TargetNodeId, model.TargetPortId);
        if (removed)
        {
            RebuildSession();
        }

        return removed;
    }

    public bool TryAddVariableMapping(
        string metaNodeId,
        VariableMappingDirection direction,
        string source,
        string target,
        out string? error)
    {
        error = null;

        var meta = Document.Nodes.FirstOrDefault(node =>
            string.Equals(node.NodeId, metaNodeId, StringComparison.OrdinalIgnoreCase)
            && node.InnerWorkflow is not null);

        if (meta is null)
        {
            error = $"找不到元节点 '{metaNodeId}'。";
            return false;
        }

        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target))
        {
            error = "映射源与目标不能为空。";
            return false;
        }

        var duplicates = meta.VariableMappings.Any(mapping =>
            mapping.Direction == direction
            && string.Equals(mapping.Source, source, StringComparison.OrdinalIgnoreCase)
            && string.Equals(mapping.Target, target, StringComparison.OrdinalIgnoreCase));
        if (duplicates)
        {
            error = "该变量映射已存在。";
            return false;
        }

        if (direction == VariableMappingDirection.In)
        {
            var targetDeclared = meta.InnerWorkflow!.VariableDeclarations.Any(declaration =>
                string.Equals(declaration.Name, target, StringComparison.OrdinalIgnoreCase));

            if (!targetDeclared)
            {
                error = $"in 映射目标 '{target}' 未在子工作流中声明。";
                return false;
            }
        }
        else
        {
            var producedByInner = meta.InnerWorkflow!.Nodes.Any(node =>
            {
                var definition = _registry.TryResolve(node.NodeTypeId);
                return definition is not null
                    && definition.OutputVariables.Any(declaration =>
                        string.Equals(declaration.Name, source, StringComparison.OrdinalIgnoreCase));
            });

            if (!producedByInner)
            {
                error = $"out 映射源 '{source}' 未在子工作流中找到产出节点。";
                return false;
            }
        }

        PushUndoSnapshot();
        meta.VariableMappings.Add(new VariableMapping
        {
            Direction = direction,
            Source = source,
            Target = target
        });

        RebuildSession();
        StatusMessage = $"已添加变量映射: {direction} {source} -> {target}。";
        return true;
    }

    public bool RemoveVariableMapping(string metaNodeId, int index)
    {
        var meta = Document.Nodes.FirstOrDefault(node =>
            string.Equals(node.NodeId, metaNodeId, StringComparison.OrdinalIgnoreCase)
            && node.InnerWorkflow is not null);

        if (meta is null || index < 0 || index >= meta.VariableMappings.Count)
        {
            return false;
        }

        var mapping = meta.VariableMappings[index];
        PushUndoSnapshot();
        meta.VariableMappings.RemoveAt(index);
        RebuildSession();
        StatusMessage = $"已移除变量映射: {mapping.Direction} {mapping.Source} -> {mapping.Target}。";
        return true;
    }

    private void PushUndoSnapshot()
    {
        if (IsInsideContainer)
        {
            return;
        }

        _undoStack.Push(CaptureSnapshot());

        if (_undoStack.Count > 50)
        {
            var items = _undoStack.ToArray();
            _undoStack.Clear();
            for (var index = items.Length - 1; index >= 1; index--)
            {
                _undoStack.Push(items[index]);
            }
        }

        _redoStack.Clear();
        NotifyHistoryCommands();
    }

    private void Undo()
    {
        if (!CanUndo)
        {
            return;
        }

        _redoStack.Push(CaptureSnapshot());
        Document = _undoStack.Pop();
        RestoreDocument();
    }

    private void Redo()
    {
        if (!CanRedo)
        {
            return;
        }

        _undoStack.Push(CaptureSnapshot());
        Document = _redoStack.Pop();
        RestoreDocument();
    }

    private void RestoreDocument()
    {
        ClearSelection();
        RebuildSession();
        NotifyHistoryCommands();
        StatusMessage = "已恢复历史版本。";
    }

    private WorkflowDocument CaptureSnapshot()
    {
        var json = WorkflowDocumentSerializer.Serialize(Document);
        return WorkflowDocumentSerializer.Deserialize(json);
    }

    private void NotifyHistoryCommands()
    {
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    public void SetEntryVariable(string name, string value)
    {
        if (!TryConvertForEntryVariable(name, value, out var converted, out var error))
        {
            throw new InvalidOperationException(error);
        }

        var declaration = Document.VariableDeclarations.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));
        if (declaration is not null)
        {
            declaration.DefaultValue = converted;
        }

        _session.SetEntryVariable(name, converted);
        RefreshEntryVariables();
        RefreshNodeStates();
    }

    public async Task<WorkflowExecutionResult> ExecuteAllAsync()
    {
        if (!await _executionGate.WaitAsync(0))
        {
            StatusMessage = "已有执行在进行中。";
            return new WorkflowExecutionResult(false, [], [], [], []);
        }

        IsBusy = true;
        var executor = new WorkflowExecutor(_session, _maxConcurrency, BreakpointGateAsync);

        try
        {
            var result = await executor.ExecuteAllAsync();
            RefreshNodeStates();
            RefreshEntryVariables();
            AppendLog($"执行完成: 成功 {result.ExecutedNodeIds.Count}，失败 {result.FailedNodeIds.Count}，阻塞 {result.BlockedNodeIds.Count}。");
            return result;
        }
        finally
        {
            IsPaused = false;
            PausedNodeName = null;
            _pauseSignal = null;
            IsBusy = false;
            _executionGate.Release();
        }
    }

    private async Task BreakpointGateAsync(NodeRuntime node, CancellationToken cancellationToken)
    {
        var signal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pauseSignal = signal;

        void PauseUi()
        {
            PausedNodeName = node.NodeId;
            IsPaused = true;
            StatusMessage = $"断点暂停于 '{node.NodeId}'，点击继续执行恢复。";
            CenterViewportOnNode(node.NodeId);
        }

        if (_synchronizationContext is null)
        {
            PauseUi();
        }
        else
        {
            _synchronizationContext.Post(_ => PauseUi(), null);
        }

        using var registration = cancellationToken.Register(() => signal.TrySetCanceled(cancellationToken));
        await signal.Task;
    }

    private void CenterViewportOnNode(string nodeId)
    {
        var node = Nodes.FirstOrDefault(candidate => string.Equals(candidate.NodeId, nodeId, StringComparison.OrdinalIgnoreCase));
        if (node is null)
        {
            return;
        }

        var zoom = Math.Max(ViewportZoom, 0.2);
        ViewportLocation = new Point(
            Math.Max(0, node.X + 100 - ViewportSize.Width / (2 * zoom)),
            Math.Max(0, node.Y + 75 - ViewportSize.Height / (2 * zoom)));
    }

    private void Resume()
    {
        _pauseSignal?.TrySetResult(true);
    }

    private void ToggleSelectedBreakpoint()
    {
        if (_selectedNodeIds.Count != 1)
        {
            return;
        }

        var nodeId = _selectedNodeIds.First();
        var model = Document.Nodes.First(node => string.Equals(node.NodeId, nodeId, StringComparison.OrdinalIgnoreCase));
        model.IsBreakpointEnabled = !model.IsBreakpointEnabled;

        var nodeViewModel = Nodes.First(node => string.Equals(node.NodeId, nodeId, StringComparison.OrdinalIgnoreCase));
        nodeViewModel.Refresh();
        StatusMessage = model.IsBreakpointEnabled ? $"已在 '{nodeId}' 设置断点。" : $"已移除 '{nodeId}' 的断点。";
    }

    private void DeleteSelection()
    {
        var nodeIds = _selectedNodeIds.ToList();
        ClearSelection();

        foreach (var nodeId in nodeIds)
        {
            RemoveNode(nodeId);
        }

        StatusMessage = $"已删除 {nodeIds.Count} 个节点。";
    }

    private void SelectAllNodes()
    {
        _suppressSelectionSync = true;
        try
        {
            SelectedNodes.Clear();
            foreach (var node in Nodes)
            {
                SelectedNodes.Add(node);
                _selectedNodeIds.Add(node.NodeId);
            }
        }
        finally
        {
            _suppressSelectionSync = false;
        }

        PackSelectedCommand.NotifyCanExecuteChanged();
        UnpackSelectedCommand.NotifyCanExecuteChanged();
        ToggleBreakpointCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(SelectedMetaMappings));
        OnPropertyChanged(nameof(SelectedNode));
    }

    private void FitGraph()
    {
        if (Nodes.Count == 0)
        {
            ResetViewport();
            return;
        }

        const double nodeWidth = 200;
        const double nodeHeight = 150;
        const double padding = 60;

        var minX = Nodes.Min(node => node.X);
        var minY = Nodes.Min(node => node.Y);
        var maxX = Nodes.Max(node => node.X + nodeWidth);
        var maxY = Nodes.Max(node => node.Y + nodeHeight);

        var boundsWidth = Math.Max(1, maxX - minX + padding * 2);
        var boundsHeight = Math.Max(1, maxY - minY + padding * 2);
        var zoom = Math.Clamp(
            Math.Min(ViewportSize.Width / boundsWidth, ViewportSize.Height / boundsHeight),
            MinViewportZoom,
            MaxViewportZoom);

        ViewportZoom = zoom;
        ViewportLocation = new Point(
            minX - padding + (ViewportSize.Width / zoom - boundsWidth) / 2,
            minY - padding + (ViewportSize.Height / zoom - boundsHeight) / 2);
    }

    private void ResetViewport()
    {
        ViewportZoom = 1d;
        ViewportLocation = new Point(0, 0);
    }

    public void Reset()
    {
        RebuildSession();
    }

    private void RebuildSession()
    {
        if (_session is not null)
        {
            _session.NodeStateChanged -= OnSessionNodeStateChanged;
        }

        _session = new WorkflowSession(Document, _registry.CreateResolver());
        _session.NodeStateChanged += OnSessionNodeStateChanged;
        _session.ConfigureAll();
        _editor = new WorkflowDocumentEditor(Document, _registry);

        var runtimesById = _session.Nodes.ToDictionary(node => node.NodeId, StringComparer.OrdinalIgnoreCase);
        var descriptorByType = _registry.Descriptors.ToDictionary(
            descriptor => descriptor.TypeId,
            StringComparer.OrdinalIgnoreCase);
        var nodeViewModels = Document.Nodes
            .Select(nodeDocument =>
            {
                var runtime = runtimesById[nodeDocument.NodeId];
                var descriptor = descriptorByType.TryGetValue(nodeDocument.NodeTypeId, out var resolved)
                    ? resolved
                    : new NodeTypeDescriptor(nodeDocument.NodeTypeId, nodeDocument.NodeTypeId);
                return new NodeViewModel(nodeDocument, runtime, descriptor);
            })
            .ToList();

        Nodes.Clear();
        foreach (var node in nodeViewModels)
        {
            node.SelectionChanged += OnNodeSelectionChanged;
            Nodes.Add(node);
        }

        var nodeById = nodeViewModels.ToDictionary(node => node.NodeId, StringComparer.OrdinalIgnoreCase);

        foreach (var node in nodeViewModels)
        {
            foreach (var port in node.Inputs.Concat(node.Outputs))
            {
                port.SetIsConnected(false);
            }
        }

        foreach (var connection in Document.Connections)
        {
            var sourceNode = nodeById[connection.SourceNodeId];
            sourceNode.Outputs.First(port => string.Equals(port.PortId, connection.SourcePortId, StringComparison.OrdinalIgnoreCase))
                .SetIsConnected(true);
            var targetNode = nodeById[connection.TargetNodeId];
            targetNode.Inputs.First(port => string.Equals(port.PortId, connection.TargetPortId, StringComparison.OrdinalIgnoreCase))
                .SetIsConnected(true);
        }

        var connectionViewModels = Document.Connections
            .Select(connection => new ConnectionViewModel(
                connection,
                nodeById[connection.SourceNodeId],
                nodeById[connection.TargetNodeId]))
            .ToList();

        Connections.Clear();
        foreach (var connection in connectionViewModels)
        {
            Connections.Add(connection);
        }

        RefreshEntryVariables();
        OnPropertyChanged(nameof(SelectedMetaMappings));
        OnPropertyChanged(nameof(SelectedNode));
    }

    private void OnSessionNodeStateChanged(object? sender, NodeStateChangedEventArgs e)
    {
        if (e.NewState is NodeState.Running or NodeState.Succeeded or NodeState.Failed or NodeState.Blocked or NodeState.Paused)
        {
            AppendLog($"{e.NewState}: {e.NodePath}");
        }
    }

    private void AppendLog(string message)
    {
        void Add()
        {
            ExecutionLogs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {message}");
            while (ExecutionLogs.Count > 200)
            {
                ExecutionLogs.RemoveAt(ExecutionLogs.Count - 1);
            }
        }

        if (_synchronizationContext is null)
        {
            Add();
        }
        else
        {
            _synchronizationContext.Post(_ => Add(), null);
        }
    }

    private void OnNodeSelectionChanged(NodeViewModel node, bool isSelected)
    {
        if (_suppressSelectionSync)
        {
            return;
        }

        if (isSelected)
        {
            _selectedNodeIds.Add(node.NodeId);
            if (!SelectedNodes.Contains(node))
            {
                SelectedNodes.Add(node);
            }
        }
        else
        {
            _selectedNodeIds.Remove(node.NodeId);
            SelectedNodes.Remove(node);
        }

        PackSelectedCommand.NotifyCanExecuteChanged();
        UnpackSelectedCommand.NotifyCanExecuteChanged();
        ToggleBreakpointCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(SelectedMetaMappings));
        OnPropertyChanged(nameof(SelectedNode));
    }

    private void OnSelectedNodesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_suppressSelectionSync)
        {
            return;
        }

        _suppressSelectionSync = true;
        try
        {
            _selectedNodeIds.Clear();
            foreach (var node in SelectedNodes)
            {
                _selectedNodeIds.Add(node.NodeId);
            }

            PackSelectedCommand.NotifyCanExecuteChanged();
            UnpackSelectedCommand.NotifyCanExecuteChanged();
            ToggleBreakpointCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(SelectedMetaMappings));
            OnPropertyChanged(nameof(SelectedNode));
        }
        finally
        {
            _suppressSelectionSync = false;
        }
    }

    private bool CanUnpackSelected()
    {
        if (_selectedNodeIds.Count != 1)
        {
            return false;
        }

        var node = Document.Nodes.FirstOrDefault(candidate =>
            string.Equals(candidate.NodeId, _selectedNodeIds.First(), StringComparison.OrdinalIgnoreCase));

        return node?.InnerWorkflow is not null;
    }

    private void UnpackSelected()
    {
        if (!CanUnpackSelected())
        {
            return;
        }

        var metaNodeId = _selectedNodeIds.First();
        PushUndoSnapshot();
        WorkflowMetanodeUnpacker.Unpack(Document, metaNodeId);
        ClearSelection();
        RebuildSession();
        StatusMessage = $"已拆开元节点 '{metaNodeId}'。";
    }

    private void PackSelected()
    {
        if (_selectedNodeIds.Count < 2)
        {
            return;
        }

        PushUndoSnapshot();
        var meta = WorkflowMetanodePacker.Pack(Document, _registry, _selectedNodeIds.ToList());
        ClearSelection();
        RebuildSession();
        StatusMessage = $"已打包为元节点 '{meta.NodeId}'（双击节点可进入编辑）。";
    }

    public void ClearSelection()
    {
        _suppressSelectionSync = true;
        try
        {
            foreach (var node in Nodes)
            {
                node.IsSelected = false;
            }

            SelectedNodes.Clear();
            _selectedNodeIds.Clear();
            PackSelectedCommand.NotifyCanExecuteChanged();
            UnpackSelectedCommand.NotifyCanExecuteChanged();
            ToggleBreakpointCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(SelectedMetaMappings));
            OnPropertyChanged(nameof(SelectedNode));
        }
        finally
        {
            _suppressSelectionSync = false;
        }
    }

    private double GetDefaultX()
    {
        return 60 + (Nodes.Count % 3) * 300;
    }

    private double GetDefaultY()
    {
        return 60 + (Nodes.Count / 3) * 180;
    }

    private void RefreshEntryVariables()
    {
        EntryVariables.Clear();

        foreach (var declaration in Document.VariableDeclarations)
        {
            var value = _session.DeclaredVariableValues.TryGetValue(declaration.Name, out var current)
                ? current
                : declaration.DefaultValue;
            var entry = new EntryVariableViewModel(declaration, value);
            entry.ValueChanged += (_, newValue) =>
            {
                try
                {
                    SetEntryVariable(entry.Name, newValue);
                }
                catch (Exception ex)
                {
                    StatusMessage = $"更新入口变量失败: {ex.Message}";
                    RefreshEntryVariables();
                }
            };
            EntryVariables.Add(entry);
        }
    }

    private void RefreshNodeStates()
    {
        foreach (var node in Nodes)
        {
            node.Refresh();
        }
    }

    private bool TryConvertForEntryVariable(string name, string value, out object? converted, out string? error)
    {
        var declaration = Document.VariableDeclarations.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));

        if (declaration is null)
        {
            converted = null;
            error = $"入口变量 '{name}' 不存在。";
            return false;
        }

        var valueTypes = ValueTypeRegistry.CreateDefault();
        var payloadType = valueTypes.Get(declaration.TypeId).PayloadType;

        if (payloadType == typeof(string))
        {
            converted = value;
        }
        else if (payloadType == typeof(long))
        {
            converted = long.Parse(value);
        }
        else if (payloadType == typeof(double))
        {
            converted = double.Parse(value);
        }
        else if (payloadType == typeof(bool))
        {
            converted = bool.Parse(value);
        }
        else
        {
            converted = null;
            error = $"暂不支持编辑类型 '{payloadType.Name}' 的入口变量。";
            return false;
        }

        error = null;
        return true;
    }

    private sealed record ContainerNavigationEntry(
        WorkflowDocument ParentDocument,
        WorkflowDocument ProjectedDocument,
        WorkflowDocument InnerWorkflow,
        List<NodeDocument> BoundaryNodes,
        List<ConnectionDocument> BoundaryConnections);
}
