using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Threading.Tasks;

namespace AutoPowerRunner.Services;

public sealed class StartupTaskService : IStartupTaskService
{
    public const string DefaultTaskName = "AutoPowerRunner";

    private readonly string _taskName;
    private readonly string _executablePath;
    private readonly LogService? _log;
    private readonly Func<string, bool, CommandResult>? _commandRunner;

    public StartupTaskService(string executablePath, LogService? log = null, string taskName = DefaultTaskName)
    {
        _executablePath = executablePath;
        _log = log;
        _taskName = taskName;
    }

    public StartupTaskService(
        string executablePath,
        Func<string, bool, CommandResult> commandRunner,
        LogService? log = null,
        string taskName = DefaultTaskName)
    {
        _executablePath = executablePath;
        _commandRunner = commandRunner;
        _log = log;
        _taskName = taskName;
    }

    public readonly record struct CommandResult(int ExitCode, string Output, string Error);

    public static string BuildCreateArguments(string taskName, string executablePath, string userName)
    {
        EnsureNoDoubleQuote(taskName, nameof(taskName));
        EnsureNoDoubleQuote(executablePath, nameof(executablePath));
        EnsureNoDoubleQuote(userName, nameof(userName));

        var quotedTarget = $"\\\"{executablePath}\\\"";
        return $"/Create /TN \"{taskName}\" /SC ONLOGON /RL HIGHEST /RU \"{userName}\" /TR \"{quotedTarget}\" /F";
    }

    public static string BuildDeleteArguments(string taskName)
    {
        EnsureNoDoubleQuote(taskName, nameof(taskName));

        return $"/Delete /TN \"{taskName}\" /F";
    }

    public static string BuildQueryArguments(string taskName)
    {
        EnsureNoDoubleQuote(taskName, nameof(taskName));

        return $"/Query /TN \"{taskName}\"";
    }

    public static string BuildQueryVerboseArguments(string taskName)
    {
        EnsureNoDoubleQuote(taskName, nameof(taskName));

        return $"/Query /TN \"{taskName}\" /V /FO LIST";
    }

    public static string GetSchtasksPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "System32",
            "schtasks.exe");
    }

    public bool IsEnabled()
    {
        var result = RunCommand(BuildQueryVerboseArguments(_taskName), runAsAdmin: false, operation: "query");
        return result.ExitCode == 0 && QueryOutputMatchesCurrentEnabledTask(result.Output, _executablePath);
    }

    public void Enable()
    {
        var userName = WindowsIdentity.GetCurrent().Name;
        var args = BuildCreateArguments(_taskName, _executablePath, userName);
        var result = RunCommand(args, runAsAdmin: true, operation: "create");
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(BuildExitFailureMessage("create", _taskName, result));
        }

        _log?.Info("Administrator autostart enabled.");
    }

    public void Disable()
    {
        var result = RunCommand(BuildDeleteArguments(_taskName), runAsAdmin: true, operation: "delete");
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(BuildExitFailureMessage("delete", _taskName, result));
        }

        _log?.Info("Administrator autostart disabled.");
    }

    private CommandResult RunCommand(string arguments, bool runAsAdmin, string operation)
    {
        if (_commandRunner is not null)
        {
            return _commandRunner(arguments, runAsAdmin);
        }

        return RunSchtasks(arguments, runAsAdmin, operation, _taskName);
    }

    private static CommandResult RunSchtasks(string arguments, bool runAsAdmin, string operation, string taskName)
    {
        return RunSchtasksAsync(arguments, runAsAdmin, operation, taskName).GetAwaiter().GetResult();
    }

    private static async Task<CommandResult> RunSchtasksAsync(string arguments, bool runAsAdmin, string operation, string taskName)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = GetSchtasksPath(),
            Arguments = arguments,
            UseShellExecute = runAsAdmin,
            Verb = runAsAdmin ? "runas" : "",
            CreateNoWindow = !runAsAdmin,
            RedirectStandardOutput = !runAsAdmin,
            RedirectStandardError = !runAsAdmin
        };

        using var process = StartProcess(startInfo, operation, taskName);

        var outputTask = startInfo.RedirectStandardOutput ? process.StandardOutput.ReadToEndAsync() : Task.FromResult("");
        var errorTask = startInfo.RedirectStandardError ? process.StandardError.ReadToEndAsync() : Task.FromResult("");

        await process.WaitForExitAsync();

        var output = await outputTask;
        var error = await errorTask;
        return new CommandResult(process.ExitCode, output, error);
    }

    private static Process StartProcess(ProcessStartInfo startInfo, string operation, string taskName)
    {
        try
        {
            return Process.Start(startInfo)
                ?? throw new InvalidOperationException(BuildStartFailureMessage(operation, taskName));
        }
        catch (Win32Exception exception)
        {
            throw new InvalidOperationException(BuildStartFailureMessage(operation, taskName), exception);
        }
    }

    private static void EnsureNoDoubleQuote(string value, string parameterName)
    {
        if (value.Contains('"'))
        {
            throw new ArgumentException("Value cannot contain double quotes.", parameterName);
        }
    }

    private static string BuildStartFailureMessage(string operation, string taskName)
    {
        return $"Failed to {operation} startup task '{taskName}': administrator authorization is required or schtasks.exe could not be started.";
    }

    private static string BuildExitFailureMessage(string operation, string taskName, CommandResult result)
    {
        return $"Failed to {operation} startup task '{taskName}'. Exit code: {result.ExitCode}. Output: {result.Output} Error: {result.Error}";
    }

    private static bool QueryOutputMatchesCurrentEnabledTask(string output, string executablePath)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return false;
        }

        var taskToRun = GetListField(output, "Task To Run");
        var searchableOutput = taskToRun ?? output;
        if (!searchableOutput.Contains(executablePath, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var status = GetListField(output, "Status");
        return status is null || !status.Contains("Disabled", StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetListField(string output, string fieldName)
    {
        using var reader = new StringReader(output);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var trimmed = line.TrimStart();
            if (!trimmed.StartsWith(fieldName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var separatorIndex = trimmed.IndexOf(':');
            if (separatorIndex >= 0)
            {
                return trimmed[(separatorIndex + 1)..].Trim();
            }
        }

        return null;
    }
}
