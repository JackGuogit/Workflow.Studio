using System.Reflection;
using System.Windows;

namespace Workflow.Studio.Workbench.ViewModels;

public sealed class NodeParameterEditorViewModel
{
    public NodeParameterEditorViewModel(NodeViewModel node)
    {
        Node = node;
        EditableSettings = CloneSettings(node.Model.Settings);
    }

    public NodeViewModel Node { get; }

    public object? EditableSettings { get; }

    public string Title => $"编辑设置 - {Node.Title}";

    public string Description => Node.Model.Settings?.Description ?? Node.Description;

    public string NodeTypeText => Node.NodeTypeText;

    public bool HasEditableSettings => EditableSettings is not null && Node.Model.SettingsViewType is not null;

    public FrameworkElement? CreateSettingsView(out string? error)
    {
        if (!HasEditableSettings)
        {
            error = null;
            return null;
        }

        if (Activator.CreateInstance(Node.Model.SettingsViewType!) is not FrameworkElement element)
        {
            error = $"无法创建节点设置视图: {Node.Model.SettingsViewType!.FullName}";
            return null;
        }

        element.DataContext = EditableSettings;
        error = null;
        return element;
    }

    public bool TryApplyChanges(out string? error)
    {
        if (EditableSettings is null || Node.Model.Settings is null)
        {
            error = null;
            return true;
        }

        try
        {
            CopyWritableProperties(EditableSettings, Node.Model.Settings);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = $"保存节点设置失败: {ex.Message}";
            return false;
        }
    }

    private static object? CloneSettings(object? source)
    {
        if (source is null)
        {
            return null;
        }

        var settingsType = source.GetType();
        var clone = Activator.CreateInstance(settingsType)
            ?? throw new InvalidOperationException($"无法创建设置类型实例: {settingsType.FullName}");

        CopyWritableProperties(source, clone);
        return clone;
    }

    private static void CopyWritableProperties(object source, object target)
    {
        foreach (var property in source.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!property.CanRead || !property.CanWrite || property.GetIndexParameters().Length != 0)
            {
                continue;
            }

            property.SetValue(target, property.GetValue(source));
        }
    }
}
