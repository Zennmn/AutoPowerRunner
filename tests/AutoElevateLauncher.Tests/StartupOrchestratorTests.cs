using AutoElevateLauncher;

namespace AutoElevateLauncher.Tests;

public sealed class StartupOrchestratorTests
{
    [Fact]
    public async Task RunEnabledItemsAsync_RunsOnlyEnabledItems()
    {
        var launcher = new RecordingLauncher();
        var orchestrator = new StartupOrchestrator(launcher);
        var enabled = new StartupItem { Id = "enabled", Name = "启用", Enabled = true };
        var disabled = new StartupItem { Id = "disabled", Name = "禁用", Enabled = false };
        var config = new StartupConfig { Items = [enabled, disabled] };

        await orchestrator.RunEnabledItemsAsync(config);

        Assert.Equal(["enabled"], launcher.StartedItemIds);
    }

    [Fact]
    public async Task RunEnabledItemsAsync_ContinuesAfterFailure()
    {
        var launcher = new RecordingLauncher { FailItemId = "first" };
        var orchestrator = new StartupOrchestrator(launcher);
        var config = new StartupConfig
        {
            Items =
            [
                new StartupItem { Id = "first", Name = "第一个", Enabled = true },
                new StartupItem { Id = "second", Name = "第二个", Enabled = true }
            ]
        };

        await orchestrator.RunEnabledItemsAsync(config);

        Assert.Equal(["first", "second"], launcher.StartedItemIds);
    }

    [Fact]
    public async Task RunEnabledItemsOnceAsync_RunsOnlyOncePerOrchestratorInstance()
    {
        var launcher = new RecordingLauncher();
        var orchestrator = new StartupOrchestrator(launcher);
        var config = new StartupConfig
        {
            Items = [new StartupItem { Id = "item", Name = "项目", Enabled = true }]
        };

        await orchestrator.RunEnabledItemsOnceAsync(config);
        await orchestrator.RunEnabledItemsOnceAsync(config);

        Assert.Equal(["item"], launcher.StartedItemIds);
    }

    private sealed class RecordingLauncher : IStartupItemLauncher
    {
        public List<string> StartedItemIds { get; } = [];
        public string? FailItemId { get; set; }

        public Task<int> RunAsync(StartupConfig config, StartupItem item, CancellationToken cancellationToken = default)
        {
            StartedItemIds.Add(item.Id);
            if (item.Id == FailItemId)
            {
                throw new InvalidOperationException("boom");
            }

            return Task.FromResult(0);
        }
    }
}
