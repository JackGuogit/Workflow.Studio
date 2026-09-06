using System;

namespace Workflow.Studio.Core.Catalog;

/// <summary>
/// 标记一个类为工作流节点类型。扫描程序集时据此建立 NodeTypeRegistry 的候选清单。
/// V2 中 TypeId 是持久化与兼容的唯一依据，生命周期内不允许变更。
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class WorkflowNodeTypeAttribute : Attribute
{
    public WorkflowNodeTypeAttribute(string typeId, string displayName, string category = "", string description = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        TypeId = typeId;
        DisplayName = displayName;
        Category = category;
        Description = description;
    }

    public new string TypeId { get; }

    public string DisplayName { get; }

    public string Category { get; }

    public string Description { get; }
}

/// <summary>
/// 标记一个类为能力插件。扫描程序集时据此建立 PluginCatalog 的候选清单。
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class WorkflowPluginAttribute : Attribute
{
    public WorkflowPluginAttribute(string id, string name, string description = "", string[]? capabilities = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Id = id;
        Name = name;
        Description = description;
        Capabilities = capabilities ?? [];
    }

    public string Id { get; }

    public string Name { get; }

    public string Description { get; }

    public IReadOnlyList<string> Capabilities { get; }
}

/// <summary>
/// 标记一个静态类型为自定义值类型提供方（第三方程式中定义 ValueTypeDefinition 用）。
/// Core 内置类型直接以代码注册，不需要该特性。
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ValueTypeProviderAttribute : Attribute
{
    public ValueTypeProviderAttribute(string typeId, string displayName, Type payloadType, bool canBeFlowVariable = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(payloadType);

        TypeId = typeId;
        DisplayName = displayName;
        PayloadType = payloadType;
        CanBeFlowVariable = canBeFlowVariable;
    }

    public new string TypeId { get; }

    public string DisplayName { get; }

    public Type PayloadType { get; }

    public bool CanBeFlowVariable { get; }
}

/// <summary>
/// 节点设置字段元数据（M8/SDK 契约）。标记设置 POCO 的属性，
/// 宿主据此渲染通用属性面板（property-tool 前置）。
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class WorkflowSettingAttribute : Attribute
{
    public WorkflowSettingAttribute(string displayName, string? editorKind = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        DisplayName = displayName;
        EditorKind = editorKind;
    }

    public string DisplayName { get; }

    public string? EditorKind { get; }

    public bool Bindable { get; init; }
}
