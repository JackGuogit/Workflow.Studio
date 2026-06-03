using System.Windows.Media;
using Workflow.Studio.Core.Models;

namespace Workflow.Studio.Workbench.ViewModels;

public sealed class ExecutionLogItemViewModel
{
    public ExecutionLogItemViewModel(WorkflowLogEntry entry)
    {
        Entry = entry;
    }

    public WorkflowLogEntry Entry { get; }

    public string TimestampText => Entry.Timestamp.ToString("HH:mm:ss.fff");

    public string LevelText => Entry.Level.ToString().ToUpperInvariant();

    public string NodeText => string.IsNullOrWhiteSpace(Entry.NodeName) ? "系统" : Entry.NodeName!;

    public string Message => Entry.Message;

    public Brush AccentBrush => Entry.Level switch
    {
        WorkflowLogLevel.Debug => CreateBrush(0x60, 0xA5, 0xFA),
        WorkflowLogLevel.Info => CreateBrush(0x10, 0xB9, 0x81),
        WorkflowLogLevel.Warning => CreateBrush(0xF5, 0x9E, 0x0B),
        WorkflowLogLevel.Error => CreateBrush(0xEF, 0x44, 0x44),
        _ => CreateBrush(0x94, 0xA3, 0xB8)
    };

    private static Brush CreateBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
