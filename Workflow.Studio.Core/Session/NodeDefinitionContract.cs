using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Workflow.Studio.Core.Session;

/// <summary>
/// V2 节点类型契约（端口/输出变量/Configure/Execute）。
/// M2 阶段设置以纯数据字典传递；类型化设置 POCO 与 metadata 在节点迁移时叠加。
/// </summary>
public sealed record NodePortDefinition(
    string Id,
    string TypeId,
    bool IsOptional = false,
    string DisplayName = "",
    string GroupName = "");

/// <summary>节点类型静态声明的输出流变量（V2 决策 R3）。</summary>
public sealed record FlowVariableDeclaration(string Name, string TypeId);

public interface INodeDefinition
{
    IReadOnlyList<NodePortDefinition> InputPorts { get; }

    IReadOnlyList<NodePortDefinition> OutputPorts { get; }

    /// <summary>静态声明的输出变量签名（M4 接入变量传播）。</summary>
    IReadOnlyList<FlowVariableDeclaration> OutputVariables { get; }

    NodeConfigureResult Configure(NodeConfigureRequest request);

    Task<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken);
}

public sealed class NodeConfigureRequest
{
    /// <summary>来源文档节点（容器节点在 Configure 时据此读取 InnerWorkflow 与设置）。</summary>
    public required Workflow.Studio.Core.Documents.NodeDocument SourceDocument { get; init; }

    public required string NodePath { get; init; }

    /// <summary>输入端口 Id → 上游输出 Spec（缺失 = 不可用）。Spec 内容由值类型定义。</summary>
    public required IReadOnlyDictionary<string, object?> InputSpecs { get; init; }

    /// <summary>节点设置字段（解析 settingsBindings 前为文档原值；M4 接入绑定解析）。</summary>
    public required IReadOnlyDictionary<string, object?> Settings { get; init; }

    /// <summary>容器声明变量（M4 填充，M2 为空）。</summary>
    public required IReadOnlyDictionary<string, object?> DeclaredVariables { get; init; }
}

public sealed class NodeConfigureResult
{
    /// <summary>全部输出端口须有键（键存在但值为 null 表示空 Spec）。</summary>
    public required IReadOnlyDictionary<string, object?> OutputSpecs { get; init; }

    public string? Error { get; init; }
}

public sealed class NodeExecutionRequest
{
    public required Workflow.Studio.Core.Documents.NodeDocument SourceDocument { get; init; }

    public required string NodePath { get; init; }

    /// <summary>输入端口 Id → 上游输出值（引用组装）。</summary>
    public required IReadOnlyDictionary<string, object?> InputValues { get; init; }

    public required IReadOnlyDictionary<string, object?> Settings { get; init; }

    /// <summary>前驱流变量/容器声明变量（M4 填充）。</summary>
    public required IReadOnlyDictionary<string, object?> Variables { get; init; }

    /// <summary>本容器的声明变量（含入口变量），供容器 in 映射读取。</summary>
    public required IReadOnlyDictionary<string, object?> DeclaredVariables { get; init; }
}

public sealed class NodeExecutionResult
{
    /// <summary>输出端口 Id → 值；键必须覆盖全部输出端口。</summary>
    public required IReadOnlyDictionary<string, object?> OutputValues { get; init; }

    /// <summary>产出的流变量（键必须匹配静态声明，M4 校验）。</summary>
    public IReadOnlyDictionary<string, object?> OutputVariables { get; init; } =
        new Dictionary<string, object?>(System.StringComparer.OrdinalIgnoreCase);

    public string? Message { get; init; }

    public string? Error { get; init; }
}
