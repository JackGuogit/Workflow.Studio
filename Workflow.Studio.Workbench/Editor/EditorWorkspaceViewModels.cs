using System.IO;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Workflow.Studio.Core.Documents;
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
    private WorkflowEditorViewModel? _activeDocument;
    private int _documentCounter;

    public EditorWorkspaceViewModel(
        WorkflowDefinitionRegistry registry,
        IWorkflowDocumentPickerService? picker = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
        _picker = picker;
        Documents = [];
        NewDocumentCommand = new RelayCommand(CreateNewDocument);
        CloseActiveDocumentCommand = new RelayCommand(CloseActiveDocument, () => ActiveDocument is not null);
        SaveActiveDocumentCommand = new AsyncRelayCommand(SaveActiveDocumentAsync);
        OpenDocumentCommand = new AsyncRelayCommand(OpenDocumentAsync);
    }

    public ObservableCollection<WorkflowEditorViewModel> Documents { get; }

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
        return true;
    }

    public async Task<WorkflowEditorViewModel> OpenDocumentAsync(string filePath)
    {
        var document = await WorkflowDocumentSerializer.LoadAsync(filePath);
        return AddDocument(document, Path.GetFileName(filePath));
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
