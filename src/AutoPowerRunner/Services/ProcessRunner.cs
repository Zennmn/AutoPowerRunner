using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using AutoPowerRunner.Models;

namespace AutoPowerRunner.Services;

public sealed class ProcessRunner : IProcessRunner
{
    private readonly LogService? _log;
    private readonly SynchronizationContext? _updateContext;
    private readonly ConcurrentDictionary<Guid, RunningTask> _runningProcesses = new();

    public ProcessRunner(LogService? log = null, SynchronizationContext? updateContext = null)
    {
        _log = log;
        _updateContext = updateContext;
    }

    public IReadOnlyCollection<Guid> RunningTaskIds => _runningProcesses.Keys.ToArray();

    public static ProcessStartInfo BuildStartInfo(ManagedTask task)
    {
        var workingDirectory = ResolveWorkingDirectory(task);

        if (task.Type == ManagedTaskType.PowerShellScript)
        {
            return new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = BuildPowerShellArguments(task),
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
        }

        return new ProcessStartInfo
        {
            FileName = task.Path,
            Arguments = task.Arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
    }

    public Process Start(ManagedTask task, Action<ManagedTask>? onUpdated = null)
    {
        if (_runningProcesses.ContainsKey(task.Id))
        {
            throw new InvalidOperationException($"Task is already running: {task.Name}");
        }

        if (!File.Exists(task.Path))
        {
            var error = $"File not found: {task.Path}";
            MarkFailed(task, error, onUpdated);
            _log?.Error(error);
            throw new FileNotFoundException("Task target file was not found.", task.Path);
        }

        var startInfo = BuildStartInfo(task);
        var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };
        var runningTask = new RunningTask(task, process, onUpdated);

        process.Exited += (_, _) => Complete(runningTask);

        task.LastResult.Status = TaskRuntimeStatus.Running;
        task.LastResult.ExitCode = null;
        task.LastResult.Error = null;
        task.LastResult.StartedAt = DateTimeOffset.Now;
        task.LastResult.ExitedAt = null;

        if (!_runningProcesses.TryAdd(task.Id, runningTask))
        {
            process.Dispose();
            throw new InvalidOperationException($"Task is already running: {task.Name}");
        }

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            _runningProcesses.TryRemove(task.Id, out _);
            process.Dispose();
            MarkFailed(task, ex.Message, onUpdated);
            _log?.Error($"Task failed to start: {task.Name}", ex);
            throw;
        }

        _log?.Info($"Task started: {task.Name}");
        NotifyUpdated(task, onUpdated);
        if (SafeHasExited(process))
        {
            Complete(runningTask);
        }

        return process;
    }

    public void Stop(Guid taskId)
    {
        if (!_runningProcesses.TryGetValue(taskId, out var runningTask))
        {
            return;
        }

        var process = runningTask.Process;
        var shouldComplete = false;
        try
        {
            if (SafeHasExited(process))
            {
                shouldComplete = true;
            }
            else
            {
                process.CloseMainWindow();
                if (!process.WaitForExit(milliseconds: 3000))
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit();
                }

                shouldComplete = true;
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        {
            shouldComplete = true;
        }
        finally
        {
            if (shouldComplete)
            {
                Complete(runningTask);
            }
        }
    }

    public void StopAll()
    {
        foreach (var taskId in RunningTaskIds)
        {
            Stop(taskId);
        }
    }

    private static string ResolveWorkingDirectory(ManagedTask task)
    {
        if (!string.IsNullOrWhiteSpace(task.WorkingDirectory))
        {
            return task.WorkingDirectory;
        }

        return Path.GetDirectoryName(task.Path) ?? Environment.CurrentDirectory;
    }

    private static string BuildPowerShellArguments(ManagedTask task)
    {
        var fixedArguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{task.Path}\"";

        // ManagedTask.Arguments is a user-provided raw command-line fragment.
        return string.IsNullOrWhiteSpace(task.Arguments)
            ? fixedArguments
            : $"{fixedArguments} {task.Arguments}";
    }

    private static int? SafeExitCode(Process process)
    {
        try
        {
            return process.ExitCode;
        }
        catch
        {
            return null;
        }
    }

    private static bool SafeHasExited(Process process)
    {
        try
        {
            return process.HasExited;
        }
        catch
        {
            return true;
        }
    }

    private void Complete(RunningTask runningTask)
    {
        if (Interlocked.Exchange(ref runningTask.Completed, 1) == 1)
        {
            return;
        }

        var task = runningTask.Task;
        _runningProcesses.TryRemove(task.Id, out _);
        PostOrRun(() => ApplyCompletion(runningTask));
    }

    private void ApplyCompletion(RunningTask runningTask)
    {
        var task = runningTask.Task;
        var process = runningTask.Process;
        task.LastResult.Status = TaskRuntimeStatus.Exited;
        task.LastResult.ExitCode = SafeExitCode(process);
        task.LastResult.ExitedAt = DateTimeOffset.Now;
        _log?.Info($"Task exited: {task.Name}, code {task.LastResult.ExitCode}");
        NotifyUpdated(task, runningTask.OnUpdated);
        process.Dispose();
    }

    private void PostOrRun(Action action)
    {
        if (_updateContext is null)
        {
            action();
            return;
        }

        try
        {
            _updateContext.Post(_ =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    _log?.Error("Task completion dispatch failed.", ex);
                }
            }, null);
        }
        catch (Exception ex)
        {
            _log?.Error("Could not dispatch task completion.", ex);
            action();
        }
    }

    private void MarkFailed(ManagedTask task, string error, Action<ManagedTask>? onUpdated)
    {
        task.LastResult.Status = TaskRuntimeStatus.FailedToStart;
        task.LastResult.Error = error;
        task.LastResult.ExitCode = null;
        task.LastResult.StartedAt = null;
        task.LastResult.ExitedAt = DateTimeOffset.Now;
        NotifyUpdated(task, onUpdated);
    }

    private void NotifyUpdated(ManagedTask task, Action<ManagedTask>? onUpdated)
    {
        if (onUpdated is null)
        {
            return;
        }

        void InvokeCallback()
        {
            try
            {
                onUpdated(task);
            }
            catch (Exception ex)
            {
                _log?.Error($"Task update callback failed: {task.Name}", ex);
            }
        }

        if (_updateContext is null)
        {
            InvokeCallback();
            return;
        }

        try
        {
            _updateContext.Post(_ => InvokeCallback(), null);
        }
        catch (Exception ex)
        {
            _log?.Error($"Task update callback dispatch failed: {task.Name}", ex);
        }
    }

    private sealed class RunningTask
    {
        public RunningTask(ManagedTask task, Process process, Action<ManagedTask>? onUpdated)
        {
            Task = task;
            Process = process;
            OnUpdated = onUpdated;
        }

        public ManagedTask Task { get; }
        public Process Process { get; }
        public Action<ManagedTask>? OnUpdated { get; }
        public int Completed;
    }
}
