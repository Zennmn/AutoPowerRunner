namespace AutoElevateLauncher;

public sealed class StartupOrchestrator
{
    private readonly IStartupItemLauncher _launcher;
    private bool _hasRunAutomaticStartup;

    public StartupOrchestrator(IStartupItemLauncher launcher)
    {
        _launcher = launcher;
    }

    public async Task RunEnabledItemsOnceAsync(StartupConfig config, CancellationToken cancellationToken = default)
    {
        if (_hasRunAutomaticStartup)
        {
            return;
        }

        _hasRunAutomaticStartup = true;
        await RunEnabledItemsAsync(config, cancellationToken);
    }

    public async Task RunEnabledItemsAsync(StartupConfig config, CancellationToken cancellationToken = default)
    {
        foreach (var item in config.Items.Where(item => item.Enabled).ToList())
        {
            try
            {
                await _launcher.RunAsync(config, item, cancellationToken);
            }
            catch
            {
                item.LastRunFinishedAt = DateTimeOffset.Now;
                item.LastExitCode = -1;
                item.LastStatus = StartupItemStatus.Failed;
            }
        }
    }
}
