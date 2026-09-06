namespace Workflow.Studio.Core.Session;

/// <summary>
/// 端口运行时槽：Spec（配置期）+ Value（执行期）。
/// 值写入与读取由 NodeRuntime 按状态门控，端口本身不感知节点状态。
/// </summary>
public sealed class PortSlot
{
    private object? _value;
    private bool _hasValue;

    internal PortSlot(NodePortDefinition definition)
    {
        PortId = definition.Id;
        TypeId = definition.TypeId;
        IsOptional = definition.IsOptional;
        DisplayName = definition.DisplayName;
        GroupName = definition.GroupName;
    }

    public string PortId { get; }

    public string TypeId { get; }

    public bool IsOptional { get; }

    public string DisplayName { get; }

    public string GroupName { get; }

    public bool IsSpecComputed { get; private set; }

    public object? Spec { get; private set; }

    public bool HasValue => _hasValue;

    internal void SetSpec(object? spec)
    {
        Spec = spec;
        IsSpecComputed = true;
    }

    internal void ClearSpec()
    {
        Spec = null;
        IsSpecComputed = false;
    }

    internal void SetValue(object? value)
    {
        _value = value;
        _hasValue = true;
    }

    internal void ClearValue()
    {
        _value = null;
        _hasValue = false;
    }

    internal object? GetValue()
    {
        return _value;
    }
}
