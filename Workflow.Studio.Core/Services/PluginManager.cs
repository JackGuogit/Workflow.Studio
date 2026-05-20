using Workflow.Studio.Core.Plugins;

namespace Workflow.Studio.Core.Services;

public sealed class PluginManager : IDisposable, IAsyncDisposable
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

    public async ValueTask InitializeAsync(CancellationToken cancellationToken)
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

    public async ValueTask DisposeAsync()
    {
        foreach (var plugin in _plugins.Values)
        {
            await plugin.DisposeAsync();
        }

        _plugins.Clear();
        _initialized = false;
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
