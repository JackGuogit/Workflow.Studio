using System.IO;
using Microsoft.Win32;
using Workflow.Studio.Workbench.Services;

namespace Workflow.Studio.Desktop.Services;

public sealed class WorkflowDocumentPickerService : IWorkflowDocumentPickerService
{
    private const string WorkflowFileFilter = "Workflow Studio 工作流 (*.workflow.json)|*.workflow.json|JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*";

    public string? PickOpenFilePath(string? currentFilePath = null)
    {
        var dialog = new OpenFileDialog
        {
            Filter = WorkflowFileFilter,
            Title = "打开工作流",
            DefaultExt = ".workflow.json",
            CheckFileExists = true,
            CheckPathExists = true
        };

        ApplyInitialLocation(dialog, currentFilePath);
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickSaveFilePath(string? currentFilePath = null)
    {
        var dialog = new SaveFileDialog
        {
            Filter = WorkflowFileFilter,
            Title = "保存工作流",
            DefaultExt = ".workflow.json",
            AddExtension = true,
            OverwritePrompt = true
        };

        ApplyInitialLocation(dialog, currentFilePath);

        if (string.IsNullOrWhiteSpace(dialog.FileName))
        {
            dialog.FileName = "workflow.workflow.json";
        }

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private static void ApplyInitialLocation(FileDialog dialog, string? currentFilePath)
    {
        if (string.IsNullOrWhiteSpace(currentFilePath))
        {
            return;
        }

        var fullPath = Path.GetFullPath(currentFilePath);
        var directoryPath = Path.GetDirectoryName(fullPath);
        var fileName = Path.GetFileName(fullPath);

        if (!string.IsNullOrWhiteSpace(directoryPath) && Directory.Exists(directoryPath))
        {
            dialog.InitialDirectory = directoryPath;
        }

        if (!string.IsNullOrWhiteSpace(fileName))
        {
            dialog.FileName = fileName;
        }
    }
}
