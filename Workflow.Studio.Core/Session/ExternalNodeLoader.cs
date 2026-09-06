using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Workflow.Studio.Core.Catalog;
using Workflow.Studio.Core.Plugins;

namespace Workflow.Studio.Core.Session;

/// <summary>
/// 外部节点扩展目录加载（M8/SDK）：用可回收 AssemblyLoadContext 从指定目录加载 DLL，
/// 通过 [WorkflowNodeType] 发现并注册 INodeDefinition。
/// 卸载能力：定义实例被注册表引用，当前支持"加载"；完整 ALC 卸载需宿主不再引用后调用。
/// </summary>
public static class ExternalNodeLoader
{
    public static IReadOnlyList<string> LoadPluginsFromDirectory(
        PluginCatalog catalog,
        string directoryPath)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        if (!Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException($"插件目录不存在: {directoryPath}");
        }

        var registered = new List<string>();
        foreach (var assemblyPath in Directory.EnumerateFiles(directoryPath, "*.dll"))
        {
            var assembly = LoadAssembly(assemblyPath);
            if (assembly is null)
            {
                continue;
            }

            foreach (var type in assembly.GetTypes().Where(type =>
                         type is { IsClass: true, IsAbstract: false }
                         && typeof(IWorkflowPlugin).IsAssignableFrom(type)))
            {
                var attribute = type.GetCustomAttribute<WorkflowPluginAttribute>();
                if (attribute is null || Activator.CreateInstance(type) is not IWorkflowPlugin plugin)
                {
                    continue;
                }

                if (!catalog.Plugins.Any(existing =>
                        string.Equals(existing.Metadata.Id, attribute.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    catalog.Register(plugin);
                    registered.Add(attribute.Id);
                }
            }
        }

        return registered;
    }

    public static IReadOnlyList<string> LoadFromDirectory(
        WorkflowDefinitionRegistry registry,
        string directoryPath)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        if (!Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException($"节点扩展目录不存在: {directoryPath}");
        }

        var registered = new List<string>();

        foreach (var assemblyPath in Directory.EnumerateFiles(directoryPath, "*.dll"))
        {
            var assembly = LoadAssembly(assemblyPath);
            if (assembly is null)
            {
                continue;
            }

            foreach (var type in assembly.GetTypes().Where(type =>
                         type is { IsClass: true, IsAbstract: false }
                         && typeof(INodeDefinition).IsAssignableFrom(type)))
            {
                var attribute = type.GetCustomAttribute<WorkflowNodeTypeAttribute>();
                if (attribute is null)
                {
                    continue;
                }

                if (Activator.CreateInstance(type) is not INodeDefinition definition)
                {
                    continue;
                }

                if (registry.TryResolve(attribute.TypeId) is null)
                {
                    registry.Register(
                        attribute.TypeId,
                        definition,
                        new NodeTypeDescriptor(
                            attribute.TypeId,
                            attribute.DisplayName,
                            attribute.Category,
                            attribute.Description,
                            IsExternal: true));
                    registered.Add(attribute.TypeId);
                }
            }
        }

        return registered;
    }

    private static Assembly? LoadAssembly(string assemblyPath)
    {
        try
        {
            var context = new AssemblyLoadContext(
                $"external-{Path.GetFileNameWithoutExtension(assemblyPath)}-{Guid.NewGuid():N}",
                isCollectible: true);
            context.Resolving += (_, name) =>
            {
                var candidate = Path.Combine(Path.GetDirectoryName(assemblyPath) ?? string.Empty, $"{name.Name}.dll");
                return File.Exists(candidate)
                    ? context.LoadFromAssemblyPath(candidate)
                    : null;
            };

            return context.LoadFromAssemblyPath(assemblyPath);
        }
        catch
        {
            return null;
        }
    }
}
