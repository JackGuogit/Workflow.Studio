namespace Workflow.Studio.Core.Session;

/// <summary>
/// V2 节点运行时状态（V2 架构文档 5.1 节）。
/// M2 使用子集（NotConfigured/Configured），执行态由 M3 调度器驱动。
/// </summary>
public enum NodeState
{
    /// <summary>未配置/脏：输入、设置或结构变化后需要重新 Configure。</summary>
    NotConfigured,

    /// <summary>已配置（黄）：结构/设置校验通过，可执行。</summary>
    Configured,

    /// <summary>已入队，等待 worker 执行。</summary>
    Queued,

    Running,

    /// <summary>断点暂停。</summary>
    Paused,

    /// <summary>执行成功（绿）：输出可见。</summary>
    Succeeded,

    Failed,

    /// <summary>依赖的必需前驱失败，不调度。</summary>
    Blocked
}
