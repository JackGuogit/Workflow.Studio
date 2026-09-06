using System;
using System.Collections.Generic;

namespace Workflow.Studio.Core.Documents;

public sealed class NodeDocument
{
    /// <summary>容器内唯一。</summary>
    public string NodeId { get; set; } = string.Empty;

    public string NodeTypeId { get; set; } = string.Empty;

    public double X { get; set; }

    public double Y { get; set; }

    public bool IsBreakpointEnabled { get; set; }

    /// <summary>设置字段字典（纯数据，序列化友好；结构校验在接入节点目录后执行）。</summary>
    public Dictionary<string, object?> Settings { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public List<SettingsBinding> SettingsBindings { get; set; } = [];

    /// <summary>仅元节点使用：in/out 变量映射。</summary>
    public List<VariableMapping> VariableMappings { get; set; } = [];

    /// <summary>仅元节点（core.metanode）使用：嵌套容器。</summary>
    public WorkflowDocument? InnerWorkflow { get; set; }

    /// <summary>
    /// 仅边界伪节点（core.boundary-in/out）使用：唯一端口的声明（TypeId/端口名），按实例持久化。
    /// 普通节点的端口表面由节点类型契约派生，不落盘。
    /// </summary>
    public List<PortDocument> Ports { get; set; } = [];
}

public sealed class PortDocument
{
    public string PortId { get; set; } = string.Empty;

    public string TypeId { get; set; } = string.Empty;
}
