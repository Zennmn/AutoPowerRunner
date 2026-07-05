using System.Diagnostics;
using System.Security.Principal;

namespace AutoPowerRunner.Services;

public sealed class StartupTaskService
{
    public const string DefaultTaskName = "AutoPowerRunner";

    private readonly string _taskName;
    private readonly string _executablePath;
    private readonly LogService? _log;

    public StartupTaskService(string executablePath, LogService? log = null, string taskName = DefaultTaskName)
    {
        _executablePath = executablePath;
        _log = log;
        _taskName = taskName;
    }

    public static string BuildCreateArguments(string taskName, string executablePath, string userName)
    {
        var quotedTarget = $"\\\"{executablePath}\\\"";
        return $"/Create /TN \"{taskName}\" /SC ONLOGON /RL HIGHEST /RU \"{userName}\" /TR \"{quotedTarget}\" /F";
    }

    public static string BuildDeleteArguments(string taskName)
    {
        return $"/Delete /TN \"{taskName}\" /F";
    }

    public static string BuildQueryArguments(string taskName)
    {
        return $"/Query /TN \"{taskName}\"";
    }

    public bool IsEnabled()
    {
        var result = RunSchtasks(BuildQueryArguments(_taskName), runAsAdmin: false);
        return result.ExitCode == 0;
    }

    public void Enable()
    {
        var userName = WindowsIdentity.GetCurrent().Name;
        var args = BuildCreateArguments(_taskName, _executablePath, userName);
        var result = RunSchtasks(args, runAsAdmin: true);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"Failed to create startup task: {result.Error}{result.Output}");
        }

        _log?.Info("Administrator autostart enabled.");
    }

    public void Disable()
    {
        var result = RunSchtasks(BuildDeleteArguments(_taskName), runAsAdmin: true);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"Failed to delete startup task: {result.Error}{result.Output}");
        }

        _log?.Info("Administrator autostart disabled.");
    }

    private static CommandResult RunSchtasks(string arguments, bool runAsAdmin)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "schtasks.exe",
            Arguments = arguments,
            UseShellExecute = runAsAdmin,
            Verb = runAsAdmin ? "runas" : "",
            CreateNoWindow = !runAsAdmin,
            RedirectStandardOutput = !runAsAdmin,
            RedirectStandardError = !runAsAdmin
        };

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start schtasks.exe.");
        process.WaitForExit();

        var output = startInfo.RedirectStandardOutput ? process.StandardOutput.ReadToEnd() : "";
        var error = startInfo.RedirectStandardError ? process.StandardError.ReadToEnd() : "";
        return new CommandResult(process.ExitCode, output, error);
    }

    private sealed record CommandResult(int ExitCode, string Output, string Error);
}
