namespace Workflow.Studio.Nodify;

/// <summary>
/// Wraps <see cref="global::Nodify.NodifyEditor"/> so consumers depend on this assembly
/// instead of referencing the third-party package directly from XAML.
/// </summary>
public class NodifyEditor : global::Nodify.NodifyEditor
{
    protected override System.Windows.DependencyObject GetContainerForItemOverride()
    {
        return new ItemContainer(this)
        {
            RenderTransform = new System.Windows.Media.TranslateTransform()
        };
    }

    protected override bool IsItemItsOwnContainerOverride(object item)
    {
        return item is global::Nodify.ItemContainer;
    }
}

/// <summary>
/// Wraps <see cref="global::Nodify.ItemContainer"/> for item positioning and selection.
/// </summary>
public class ItemContainer : global::Nodify.ItemContainer
{
    public ItemContainer(global::Nodify.NodifyEditor editor)
        : base(editor)
    {
    }
}

/// <summary>
/// Wraps <see cref="global::Nodify.LineConnection"/> for visual connection rendering.
/// </summary>
public class LineConnection : global::Nodify.LineConnection
{
}

/// <summary>
/// Wraps <see cref="global::Nodify.PendingConnection"/> for interactive connection creation.
/// </summary>
public class PendingConnection : global::Nodify.PendingConnection
{
}

/// <summary>
/// Wraps <see cref="global::Nodify.Node"/> for node content presentation.
/// </summary>
public class Node : global::Nodify.Node
{
}

/// <summary>
/// Wraps <see cref="global::Nodify.NodeInput"/> for input connector presentation.
/// </summary>
public class NodeInput : global::Nodify.NodeInput
{
}

/// <summary>
/// Wraps <see cref="global::Nodify.NodeOutput"/> for output connector presentation.
/// </summary>
public class NodeOutput : global::Nodify.NodeOutput
{
}
