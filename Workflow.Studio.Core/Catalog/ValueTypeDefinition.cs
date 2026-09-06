using System;

namespace Workflow.Studio.Core.Catalog;

/// <summary>
/// 端口值/流变量共用的类型定义（V2 类型系统）。
/// TypeId 为稳定标识，连接兼容与持久化只使用 TypeId。
/// </summary>
public sealed class ValueTypeDefinition
{
    public required string TypeId { get; init; }

    public required string DisplayName { get; init; }

    public required Type PayloadType { get; init; }

    /// <summary>是否允许作为流变量（大对象类型应关闭）。</summary>
    public bool CanBeFlowVariable { get; init; }

    /// <summary>端口级默认可选性，节点声明端口时可覆盖。</summary>
    public bool IsOptional { get; init; }
}
