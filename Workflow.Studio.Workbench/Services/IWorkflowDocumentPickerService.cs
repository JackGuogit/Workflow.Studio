namespace Workflow.Studio.Workbench.Services;

public interface IWorkflowDocumentPickerService
{
    string? PickOpenFilePath(string? currentFilePath = null);

    string? PickSaveFilePath(string? currentFilePath = null);
}
