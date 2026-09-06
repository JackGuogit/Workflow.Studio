using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Workflow.Studio.Core.Catalog;

public sealed record NodeTypeRegistration(Type ImplementationType, WorkflowNodeTypeAttribute Attribute);

public sealed record PluginTypeRegistration(Type ImplementationType, WorkflowPluginAttribute Attribute);

public sealed record ValueTypeRegistration(Type ProviderType, ValueTypeProviderAttribute Attribute);

/// <summary>
/// 程序集内自动发现（V2 决策 D6/R 系列）。
/// 只做"扫描 + 元数据提取"，不负责实例化（实例化由宿主 DI 完成）。
/// </summary>
public static class AssemblyCatalog
{
    public static IReadOnlyList<NodeTypeRegistration> DiscoverNodeTypes(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        return assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Select(type => (Type: type, Attribute: type.GetCustomAttribute<WorkflowNodeTypeAttribute>()))
            .Where(item => item.Attribute is not null)
            .Select(item => new NodeTypeRegistration(item.Type, item.Attribute!))
            .OrderBy(item => item.Attribute.TypeId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<PluginTypeRegistration> DiscoverPlugins(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        return assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Select(type => (Type: type, Attribute: type.GetCustomAttribute<WorkflowPluginAttribute>()))
            .Where(item => item.Attribute is not null)
            .Select(item => new PluginTypeRegistration(item.Type, item.Attribute!))
            .OrderBy(item => item.Attribute.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<ValueTypeRegistration> DiscoverValueTypes(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        return assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Select(type => (Type: type, Attribute: type.GetCustomAttribute<ValueTypeProviderAttribute>()))
            .Where(item => item.Attribute is not null)
            .Select(item => new ValueTypeRegistration(item.Type, item.Attribute!))
            .OrderBy(item => item.Attribute.TypeId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
