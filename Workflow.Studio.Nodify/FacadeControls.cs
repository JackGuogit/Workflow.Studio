namespace Workflow.Studio.Nodify;

/// <summary>
/// Nodify 手势配置的外观入口：业务层只依赖本类型，
/// 需要定制时在这里统一修改默认手势。
/// </summary>
public static class NodifyGestures
{
    public static object CreateDefault()
    {
        var gestures = new global::Nodify.Interactivity.EditorGestures();
        gestures.Editor.PanWithMouseWheel = true;
        gestures.Editor.ZoomModifierKey = System.Windows.Input.ModifierKeys.Control;
        return gestures;
    }
}

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

/// <summary>
/// Wraps <see cref="global::Nodify.Minimap"/> for viewport overview rendering.
/// </summary>
public class Minimap : global::Nodify.Minimap
{
    protected override System.Windows.DependencyObject GetContainerForItemOverride()
    {
        return new MinimapItem();
    }

    protected override bool IsItemItsOwnContainerOverride(object item)
    {
        return item is global::Nodify.MinimapItem;
    }
}

/// <summary>
/// Wraps <see cref="global::Nodify.MinimapItem"/> for minimap item positioning.
/// </summary>
public class MinimapItem : global::Nodify.MinimapItem
{
}
