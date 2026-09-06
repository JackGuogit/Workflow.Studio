namespace Workflow.Studio.Core.Documents;

/// <summary>
/// 同一容器内两个节点之间的边（V2 决策 R1：连接统一为节点间边；
/// 元节点边界通过内层伪节点表达，外层连接的目标/源仍是普通节点）。
/// </summary>
public sealed class ConnectionDocument
{
    public string SourceNodeId { get; set; } = string.Empty;

    public string SourcePortId { get; set; } = string.Empty;

    public string TargetNodeId { get; set; } = string.Empty;

    public string TargetPortId { get; set; } = string.Empty;
}
