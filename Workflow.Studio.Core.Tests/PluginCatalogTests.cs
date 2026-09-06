using Workflow.Studio.Core.Plugins;
using Xunit;

namespace Workflow.Studio.Core.Tests;

public sealed class PluginCatalogTests
{
    [Fact]
    public async Task RegisterInitializeAndDispose_Lifecycle()
    {
        var plugin = new TestPlugin();
        var catalog = new PluginCatalog();

        catalog.Register(plugin);
        await catalog.InitializeAsync();

        Assert.True(plugin.Initialized);
        Assert.Single(catalog.Plugins);

        await catalog.DisposeAsync();

        Assert.True(plugin.Disposed);
        Assert.Empty(catalog.Plugins);
    }

    [Fact]
    public void Register_DuplicateIdThrows()
    {
        var catalog = new PluginCatalog();
        catalog.Register(new TestPlugin("p1"));

        Assert.Throws<InvalidOperationException>(() => catalog.Register(new TestPlugin("p1")));
    }

    private sealed class TestPlugin : IWorkflowPlugin
    {
        public TestPlugin(string id = "test.plugin")
        {
            Metadata = new PluginMetadata
            {
                Id = id,
                Name = "Test Plugin",
                Capabilities = ["capability-a"]
            };
        }

        public bool Initialized { get; private set; }

        public bool Disposed { get; private set; }

        public PluginMetadata Metadata { get; }

        public ValueTask InitializeAsync(PluginInitializationContext context, CancellationToken cancellationToken)
        {
            Initialized = true;
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
