using System;
using System.Collections.Generic;

namespace Workflow.Studio.Core.Documents;

/// <summary>
/// V2 文档模型：纯结构、可序列化、不含任何运行时状态（V2 决策 D4）。
/// 根容器与元节点的 InnerWorkflow 使用同一类型递归表达。
/// </summary>
public sealed class WorkflowDocument
{
    public const int CurrentSchemaVersion = 2;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>本容器的变量声明（名称/TypeId/默认值）。根容器的声明即入口变量。</summary>
    public List<VariableDeclaration> VariableDeclarations { get; set; } = [];

    public List<NodeDocument> Nodes { get; set; } = [];

    public List<ConnectionDocument> Connections { get; set; } = [];
}

public sealed class VariableDeclaration
{
    public string Name { get; set; } = string.Empty;

    public string TypeId { get; set; } = string.Empty;

    public object? DefaultValue { get; set; }
}

public enum VariableMappingDirection
{
    In,
    Out
}

/// <summary>
/// 元节点的变量映射（V2 决策 H7/R5）：不进入连接图，无变量连线。
/// in：source=父作用域变量名，target=内部声明变量名；
/// out：source=内部流变量名，target=父作用域中产出的变量名。
/// </summary>
public sealed class VariableMapping
{
    public VariableMappingDirection Direction { get; set; }

    public string Source { get; set; } = string.Empty;

    public string Target { get; set; } = string.Empty;
}

/// <summary>
/// 节点设置字段到容器声明变量的绑定（V2 决策 R2）。
/// 引擎在 Configure/Execute 前解析并做类型检查。
/// </summary>
public sealed class SettingsBinding
{
    public string Setting { get; set; } = string.Empty;

    public string Variable { get; set; } = string.Empty;
}
