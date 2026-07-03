using System.Diagnostics;

namespace AutoElevateLauncher;

public sealed class ItemRunner
{
    private readonly ConfigStore _configStore;

    public ItemRunner(ConfigStore configStore)
    {
        _configStore = configStore;
    }

    public async Task<int> RunAsync(string itemId, CancellationToken cancellationToken = default)
    {
        var config = _configStore.Load();
        var item = config.Items.FirstOrDefault(candidate => candidate.Id == itemId);
        if (item is null)
        {
            return 2;
        }

        Directory.CreateDirectory(AppPaths.GetItemLogDirectory(item.Id));
        var logPath = Path.Combine(AppPaths.GetItemLogDirectory(item.Id), DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss-fffffff") + ".log");

        item.LastRunStartedAt = DateTimeOffset.Now;
        item.LastRunFinishedAt = null;
        item.LastExitCode = null;
        item.LastStatus = StartupItemStatus.Running;
        _configStore.Save(config);

        try
        {
            var exitCode = item.Type == StartupItemType.PowerShellScript
                ? await RunPowerShellAsync(item, logPath, cancellationToken)
                : await RunExecutableAsync(item, logPath, cancellationToken);

            item.LastRunFinishedAt = DateTimeOffset.Now;
            item.LastExitCode = exitCode;
            item.LastStatus = exitCode == 0 ? StartupItemStatus.Succeeded : StartupItemStatus.Failed;
            _configStore.Save(config);
            return exitCode;
        }
        catch (Exception ex)
        {
            await File.AppendAllTextAsync(logPath, Environment.NewLine + ex, cancellationToken);
            item.LastRunFinishedAt = DateTimeOffset.Now;
            item.LastExitCode = -1;
            item.LastStatus = StartupItemStatus.Failed;
            _configStore.Save(config);
            return -1;
        }
    }

    public static string BuildPowerShellArguments(StartupItem item)
    {
        var args = $"-NoProfile -ExecutionPolicy Bypass -File \"{item.Path}\"";
        return string.IsNullOrWhiteSpace(item.Arguments) ? args : args + " " + item.Arguments;
    }

    public static ProcessStartInfo BuildExecutableStartInfo(StartupItem item)
    {
        return new ProcessStartInfo
        {
            FileName = item.Path,
            Arguments = item.Arguments,
            WorkingDirectory = string.IsNullOrWhiteSpace(item.WorkingDirectory) ? Path.GetDirectoryName(item.Path) ?? Environment.CurrentDirectory : item.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            CreateNoWindow = true
        };
    }

    private static async Task<int> RunPowerShellAsync(StartupItem item, string logPath, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = BuildPowerShellArguments(item),
            WorkingDirectory = string.IsNullOrWhiteSpace(item.WorkingDirectory) ? Path.GetDirectoryName(item.Path) ?? Environment.CurrentDirectory : item.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        await File.WriteAllTextAsync(logPath, BuildHeader(item, startInfo), cancellationToken);
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start powershell.exe.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        await File.AppendAllTextAsync(logPath,
            $"{Environment.NewLine}--- stdout ---{Environment.NewLine}{await stdoutTask}{Environment.NewLine}--- stderr ---{Environment.NewLine}{await stderrTask}{Environment.NewLine}ExitCode: {process.ExitCode}{Environment.NewLine}",
            cancellationToken);

        return process.ExitCode;
    }

    private static async Task<int> RunExecutableAsync(StartupItem item, string logPath, CancellationToken cancellationToken)
    {
        var startInfo = BuildExecutableStartInfo(item);
        await File.WriteAllTextAsync(logPath, BuildHeader(item, startInfo), cancellationToken);
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Failed to start {item.Path}.");
        await File.AppendAllTextAsync(logPath, $"ProcessId: {process.Id}{Environment.NewLine}", cancellationToken);
        return 0;
    }

    private static string BuildHeader(StartupItem item, ProcessStartInfo startInfo)
    {
        return $"StartTime: {DateTimeOffset.Now:O}{Environment.NewLine}Type: {item.Type}{Environment.NewLine}Target: {startInfo.FileName}{Environment.NewLine}WorkingDirectory: {startInfo.WorkingDirectory}{Environment.NewLine}Arguments: {startInfo.Arguments}{Environment.NewLine}";
    }
}