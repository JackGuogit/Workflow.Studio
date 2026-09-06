namespace Workflow.Studio.Core.Documents;

/// <summary>
/// Core 内部容器/边界类型的稳定 TypeId（V2 架构文档 4.2/4.3 节）。
/// 这些类型由引擎创建，不面向 SDK 作者。
/// </summary>
public static class ContainerTypeIds
{
    /// <summary>元节点：携带 InnerWorkflow 的容器节点。</summary>
    public const string MetaNode = "core.metanode";

    /// <summary>边界入端口伪节点：一个输出端口；NodeId 即元节点外层输入端口 id。</summary>
    public const string BoundaryIn = "core.boundary-in";

    /// <summary>边界出端口伪节点：一个输入端口；NodeId 即元节点外层输出端口 id。</summary>
    public const string BoundaryOut = "core.boundary-out";
}
