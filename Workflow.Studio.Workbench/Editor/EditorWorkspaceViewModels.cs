using System.IO;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Workflow.Studio.Core.Documents;
using Workflow.Studio.Core.Plugins;
using Workflow.Studio.Core.Session;
using Workflow.Studio.Workbench.Services;

namespace Workflow.Studio.Workbench.Editor;

/// <summary>
/// 多文档工作区 VM：维护一组打开的编辑器标签，并提供新建/关闭文档。
/// </summary>
public sealed class EditorWorkspaceViewModel : ObservableObject
{
    private readonly WorkflowDefinitionRegistry _registry;
    private readonly IWorkflowDocumentPickerService? _picker;
    private readonly PluginCatalog? _pluginCatalog;
    private readonly IReadOnlyList<string> _externalPluginDirectories;
    private WorkflowEditorViewModel? _activeDocument;
    private int _documentCounter;
    private const int MaxRecentFiles = 10;

    public EditorWorkspaceViewModel(
        WorkflowDefinitionRegistry registry,
        IWorkflowDocumentPickerService? picker = null,
        PluginCatalog? pluginCatalog = null,
        IReadOnlyList<string>? externalPluginDirectories = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
        _picker = picker;
        _pluginCatalog = pluginCatalog;
        _externalPluginDirectories = externalPluginDirectories ?? [];
        Documents = [];
        RecentFiles = [];
        PluginSummaries = [];

        RefreshPluginSummaries();
        NewDocumentCommand = new RelayCommand(CreateNewDocument);
        CloseActiveDocumentCommand = new RelayCommand(CloseActiveDocument, () => ActiveDocument is not null);
        SaveActiveDocumentCommand = new AsyncRelayCommand(SaveActiveDocumentAsync);
        OpenDocumentCommand = new AsyncRelayCommand(OpenDocumentAsync);
        OpenRecentFileCommand = new AsyncRelayCommand<string>(OpenRecentFileAsync);
        UnloadSelectedPluginCommand = new RelayCommand(UnloadSelectedPlugin, () => SelectedPlugin is not null);
        ReloadSelectedPluginCommand = new RelayCommand(ReloadSelectedPlugin, () => SelectedPlugin is not null);
    }

    public ObservableCollection<WorkflowEditorViewModel> Documents { get; }

    public ObservableCollection<RecentFileItem> RecentFiles { get; }

    public ObservableCollection<PluginSummaryItem> PluginSummaries { get; }

    public bool HasPlugins => PluginSummaries.Count > 0;

    public PluginSummaryItem? SelectedPlugin
    {
        get => _selectedPlugin;
        set
        {
            if (SetProperty(ref _selectedPlugin, value))
            {
                UnloadSelectedPluginCommand.NotifyCanExecuteChanged();
                ReloadSelectedPluginCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public IRelayCommand UnloadSelectedPluginCommand { get; }

    public IRelayCommand ReloadSelectedPluginCommand { get; }

    public WorkflowEditorViewModel? ActiveDocument
    {
        get => _activeDocument;
        set
        {
            if (SetProperty(ref _activeDocument, value))
            {
                OnPropertyChanged(nameof(HasActiveDocument));
                CloseActiveDocumentCommand?.NotifyCanExecuteChanged();
            }
        }
    }

    public bool HasActiveDocument => ActiveDocument is not null;

    public IRelayCommand NewDocumentCommand { get; }

    public IRelayCommand CloseActiveDocumentCommand { get; }

    public IAsyncRelayCommand SaveActiveDocumentCommand { get; }

    public IAsyncRelayCommand OpenDocumentCommand { get; }

    public IAsyncRelayCommand<string> OpenRecentFileCommand { get; }

    public WorkflowEditorViewModel AddDocument(WorkflowDocument document, string title, int? maxConcurrency = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        var editor = new WorkflowEditorViewModel(document, _registry, maxConcurrency)
        {
            Title = title
        };

        Documents.Add(editor);
        ActiveDocument = editor;
        CloseActiveDocumentCommand.NotifyCanExecuteChanged();
        return editor;
    }

    public bool CloseDocument(WorkflowEditorViewModel editor)
    {
        if (!Documents.Remove(editor))
        {
            return false;
        }

        if (ReferenceEquals(ActiveDocument, editor))
        {
            ActiveDocument = Documents.LastOrDefault();
        }

        CloseActiveDocumentCommand.NotifyCanExecuteChanged();
        return true;
    }

    private void CreateNewDocument()
    {
        _documentCounter++;
        AddDocument(new WorkflowDocument(), $"未命名工作流 {_documentCounter}");
    }

    private void CloseActiveDocument()
    {
        if (ActiveDocument is not null)
        {
            CloseDocument(ActiveDocument);
        }
    }

    public async Task<bool> SaveActiveDocumentAsync(string filePath)
    {
        if (ActiveDocument is null)
        {
            return false;
        }

        await ActiveDocument.SaveAsync(filePath);
        AddRecent(filePath);
        return true;
    }

    public async Task<WorkflowEditorViewModel> OpenDocumentAsync(string filePath)
    {
        var document = await WorkflowDocumentSerializer.LoadAsync(filePath);
        var editor = AddDocument(document, Path.GetFileName(filePath));
        AddRecent(filePath);
        return editor;
    }

    public async Task OpenRecentFileAsync(string? filePath)
    {
        if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
        {
            await OpenDocumentAsync(filePath);
        }
    }

    private void AddRecent(string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);
        var existing = RecentFiles.FirstOrDefault(item => string.Equals(item.Path, fullPath, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            RecentFiles.Remove(existing);
        }

        RecentFiles.Insert(0, new RecentFileItem(fullPath));
        while (RecentFiles.Count > MaxRecentFiles)
        {
            RecentFiles.RemoveAt(RecentFiles.Count - 1);
        }
    }

    private PluginSummaryItem? _selectedPlugin;

    private void RefreshPluginSummaries()
    {
        PluginSummaries.Clear();

        if (_pluginCatalog is null)
        {
            return;
        }

        foreach (var plugin in _pluginCatalog.Plugins)
        {
            var related = _registry.Descriptors
                .Where(descriptor => descriptor.RequiredCapabilities.Any(capability =>
                    plugin.Metadata.Capabilities.Contains(capability, StringComparer.OrdinalIgnoreCase)))
                .Select(descriptor => descriptor.DisplayName)
                .ToList();

            PluginSummaries.Add(new PluginSummaryItem(
                plugin.Metadata.Id,
                plugin.Metadata.Name,
                plugin.Metadata.Description,
                string.Join(", ", plugin.Metadata.Capabilities),
                plugin.Metadata.Version,
                plugin.Metadata.Publisher,
                string.Join("、", related)));
        }
    }

    private void UnloadSelectedPlugin()
    {
        if (SelectedPlugin is null || _pluginCatalog is null)
        {
            return;
        }

        _pluginCatalog.Remove(SelectedPlugin.Id);
        SelectedPlugin = null;
        RefreshPluginSummaries();
    }

    private void ReloadSelectedPlugin()
    {
        if (SelectedPlugin is null || _pluginCatalog is null)
        {
            return;
        }

        var id = SelectedPlugin.Id;
        _pluginCatalog.Remove(id);
        foreach (var directory in _externalPluginDirectories)
        {
            try
            {
                ExternalNodeLoader.LoadPluginsFromDirectory(_pluginCatalog, directory);
            }
            catch
            {
                // 单个目录失败不影响其余。
            }
        }

        SelectedPlugin = null;
        RefreshPluginSummaries();
    }

    private async Task SaveActiveDocumentAsync()
    {
        if (_picker is null || ActiveDocument is null)
        {
            return;
        }

        var filePath = _picker.PickSaveFilePath(ActiveDocument.DocumentFilePath);
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            await SaveActiveDocumentAsync(filePath);
        }
    }

    private async Task OpenDocumentAsync()
    {
        if (_picker is null)
        {
            return;
        }

        var filePath = _picker.PickOpenFilePath();
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            await OpenDocumentAsync(filePath);
        }
    }
}

public sealed record RecentFileItem(string Path)
{
    public string Name => System.IO.Path.GetFileName(Path);
}

public sealed record PluginSummaryItem(
    string Id,
    string Name,
    string Description,
    string Capabilities,
    string Version,
    string Publisher,
    string RelatedNodes);
