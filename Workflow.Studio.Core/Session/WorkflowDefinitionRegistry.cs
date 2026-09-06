using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Workflow.Studio.Core.Session;

/// <summary>
/// V2 节点定义注册中心：按稳定 TypeId 提供 INodeDefinition，
/// 供桌面宿主与无头 CLI 复用的统一组装入口。
/// </summary>
public sealed class WorkflowDefinitionRegistry
{
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);

    public void Register(string typeId, INodeDefinition definition)
    {
        Register(typeId, definition, new NodeTypeDescriptor(typeId, typeId));
    }

    public void Register(string typeId, INodeDefinition definition, NodeTypeDescriptor descriptor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeId);
        ArgumentNullException.ThrowIfNull(definition);

        if (descriptor is null || !string.Equals(descriptor.TypeId, typeId, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Descriptor TypeId must match the registration type id.", nameof(descriptor));
        }

        if (!_entries.TryAdd(typeId, new Entry(definition, descriptor)))
        {
            throw new InvalidOperationException($"Node definition '{typeId}' is already registered.");
        }
    }

    public bool Remove(string typeId)
    {
        return _entries.Remove(typeId);
    }

    public INodeDefinition? TryResolve(string typeId)
    {
        return _entries.TryGetValue(typeId, out var entry) ? entry.Definition : null;
    }

    public IReadOnlyCollection<NodeTypeDescriptor> Descriptors =>
        _entries.Values.Select(entry => entry.Descriptor).OrderBy(descriptor => descriptor.Category, StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>
    /// 从带 [WorkflowSetting] 特性的设置 POCO 反射出 SettingsFields，
    /// 供 SDK 节点注册时使用（无 POCO 的内置节点仍可直接提供字段）。
    /// </summary>
    public static IReadOnlyList<NodeSettingField> ExtractSettingsFields(Type settingsType)
    {
        ArgumentNullException.ThrowIfNull(settingsType);

        return settingsType
            .GetProperties()
            .Select(property => (Property: property, Attribute: property.GetCustomAttribute<Workflow.Studio.Core.Catalog.WorkflowSettingAttribute>()))
            .Where(item => item.Attribute is not null)
            .Select(item => new NodeSettingField(
                item.Property.Name,
                item.Attribute!.DisplayName,
                item.Attribute.EditorKind,
                item.Attribute.EditorKind == "enum"
                    && item.Attribute.EditorOptionsType is { IsEnum: true } enumType
                    ? Enum.GetNames(enumType)
                    : null))
            .ToList();
    }

    public IReadOnlyCollection<string> TypeIds => _entries.Keys;

    /// <summary>会话构造使用的解析器：未注册类型返回 null，由 WorkflowSession 报错。</summary>
    public Func<string, INodeDefinition> CreateResolver()
    {
        return typeId => TryResolve(typeId) ?? null!;
    }

    private sealed record Entry(INodeDefinition Definition, NodeTypeDescriptor Descriptor);
}

public sealed record NodeTypeDescriptor(
    string TypeId,
    string DisplayName,
    string Category = "",
    string Description = "",
    string? SettingsViewTypeName = null,
    IReadOnlyList<NodeSettingField>? SettingsFields = null,
    bool IsExternal = false,
    IReadOnlyList<string>? RequiredCapabilities = null)
{
    public IReadOnlyList<NodeSettingField> SettingsFields { get; init; } = SettingsFields ?? [];

    public IReadOnlyList<string> RequiredCapabilities { get; init; } = RequiredCapabilities ?? [];
}

/// <summary>节点设置字段元数据（M8 property-tool 的前置契约）。</summary>
public sealed record NodeSettingField(
    string Key,
    string DisplayName,
    string? EditorKind = null,
    IReadOnlyList<string>? Options = null);
