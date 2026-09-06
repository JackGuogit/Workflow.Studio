using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Workflow.Studio.Core.Plugins;

/// <summary>
/// 插件 = 能力提供者（与节点是不同概念）。插件不进入画布，
/// 由宿主目录管理，供节点（能力接口）或其他宿主逻辑消费。
/// </summary>
public sealed class PluginMetadata
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public string Description { get; init; } = string.Empty;

    public string Version { get; init; } = "1.0.0";

    public string Publisher { get; init; } = string.Empty;

    public IReadOnlyList<string> Capabilities { get; init; } = [];
}

public sealed class PluginInitializationContext
{
}

public interface IWorkflowPlugin : IAsyncDisposable
{
    PluginMetadata Metadata { get; }

    ValueTask InitializeAsync(
        PluginInitializationContext context,
        CancellationToken cancellationToken);
}

/// <summary>
/// 进程内插件目录：注册/初始化/释放；重复 Id 拒绝注册。
/// </summary>
public sealed class PluginCatalog
{
    private readonly Dictionary<string, IWorkflowPlugin> _plugins = new(StringComparer.OrdinalIgnoreCase);
    private bool _initialized;

    public IReadOnlyCollection<IWorkflowPlugin> Plugins => _plugins.Values;

    public void Register(IWorkflowPlugin plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);

        if (_plugins.ContainsKey(plugin.Metadata.Id))
        {
            throw new InvalidOperationException($"Plugin '{plugin.Metadata.Id}' is already registered.");
        }

        _plugins.Add(plugin.Metadata.Id, plugin);
    }

    public async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        var context = new PluginInitializationContext();
        foreach (var plugin in _plugins.Values)
        {
            await plugin.InitializeAsync(context, cancellationToken);
        }

        _initialized = true;
    }

    public bool Remove(string pluginId)
    {
        return _plugins.Remove(pluginId);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var plugin in _plugins.Values)
        {
            await plugin.DisposeAsync();
        }

        _plugins.Clear();
        _initialized = false;
    }
}
