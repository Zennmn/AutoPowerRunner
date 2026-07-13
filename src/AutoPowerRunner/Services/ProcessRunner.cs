using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using AutoPowerRunner.Models;

namespace AutoPowerRunner.Services;

public sealed class ProcessRunner : IProcessRunner
{
    private const int MaxRestartAttempts = 3;
    private const int MaxCapturedErrorLength = 8192;
    private static readonly TimeSpan RestartDelay = TimeSpan.FromSeconds(2);

    private readonly ILogService? _log;
    private readonly SynchronizationContext? _updateContext;
    private readonly ConcurrentDictionary<Guid, RunningTask> _runningProcesses = new();

    public ProcessRunner(ILogService? log = null, SynchronizationContext? updateContext = null)
    {
        _log = log;
        _updateContext = updateContext;
    }

    public IReadOnlyCollection<Guid> RunningTaskIds => _runningProcesses.Keys.ToArray();

    public static ProcessStartInfo BuildStartInfo(ManagedTask task)
    {
        var workingDirectory = ResolveWorkingDirectory(task);
        var startInfo = task.Type == ManagedTaskType.PowerShellScript
            ? new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = BuildPowerShellArguments(task)
            }
            : new ProcessStartInfo
            {
                FileName = task.Path,
                Arguments = task.Arguments
            };

        startInfo.WorkingDirectory = workingDirectory;
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.WindowStyle = ProcessWindowStyle.Hidden;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        return startInfo;
    }

    public Process Start(ManagedTask task, Action<ManagedTask>? onUpdated = null)
    {
        try
        {
            ValidateTask(task);
        }
        catch (Exception ex)
        {
            MarkFailedToStart(task, ex.Message, onUpdated);
            _log?.Error($"Task validation failed: {task.Name}", ex);
            throw;
        }

        var runningTask = new RunningTask(task, task.Clone(), onUpdated);
        if (!_runningProcesses.TryAdd(task.Id, runningTask))
        {
            throw new InvalidOperationException($"Task is already running: {task.Name}");
        }

        try
        {
            return StartAttempt(runningTask);
        }
        catch (Exception ex)
        {
            _runningProcesses.TryRemove(task.Id, out _);
            MarkFailedToStart(task, ex.Message, onUpdated);
            _log?.Error($"Task failed to start: {runningTask.Definition.Name}", ex);
            throw;
        }
    }

    public void Stop(Guid taskId)
    {
        if (!_runningProcesses.TryGetValue(taskId, out var runningTask))
        {
            return;
        }

        Interlocked.Exchange(ref runningTask.StopRequested, 1);
        runningTask.RestartCancellation.Cancel();

        var attempt = runningTask.CurrentAttempt;
        if (attempt is null)
        {
            FinalizeWithoutProcess(runningTask, TaskRuntimeStatus.Stopped);
            return;
        }

        try
        {
            if (!SafeHasExited(attempt.Process))
            {
                attempt.Process.CloseMainWindow();
                if (!attempt.Process.WaitForExit(milliseconds: 3000))
                {
                    attempt.Process.Kill(entireProcessTree: true);
                    attempt.Process.WaitForExit(milliseconds: 5000);
                }
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException
                                   or ObjectDisposedException
                                   or System.ComponentModel.Win32Exception
                                   or System.Runtime.InteropServices.COMException)
        {
            _log?.Error($"Could not stop task '{runningTask.Definition.Name}'.", ex);
        }
        finally
        {
            CompleteAttemptAsync(runningTask, attempt).GetAwaiter().GetResult();
        }
    }

    public void StopAll()
    {
        Parallel.ForEach(RunningTaskIds, Stop);
    }

    private Process StartAttempt(RunningTask runningTask)
    {
        if (Volatile.Read(ref runningTask.StopRequested) == 1)
        {
            throw new OperationCanceledException("Task stop was requested.");
        }

        var process = new Process
        {
            StartInfo = BuildStartInfo(runningTask.Definition),
            EnableRaisingEvents = true
        };
        var attempt = new ProcessAttempt(process);
        runningTask.CurrentAttempt = attempt;
        process.Exited += (_, _) => _ = CompleteAttemptAsync(runningTask, attempt);

        try
        {
            process.Start();
        }
        catch
        {
            process.Dispose();
            throw;
        }
        attempt.OutputTask = process.StandardOutput.ReadToEndAsync();
        attempt.ErrorTask = process.StandardError.ReadToEndAsync();

        void MarkRunning()
        {
            var result = runningTask.Task.LastResult;
            result.Status = TaskRuntimeStatus.Running;
            result.ExitCode = null;
            result.Error = null;
            result.StartedAt ??= DateTimeOffset.Now;
            result.ExitedAt = null;
            result.RestartCount = runningTask.RestartCount;
            NotifyUpdated(runningTask.Task, runningTask.OnUpdated);
        }

        if (runningTask.RestartCount == 0) MarkRunning();
        else PostOrRun(MarkRunning);

        _log?.Info($"Task started: {runningTask.Definition.Name}");
        if (SafeHasExited(process))
        {
            _ = CompleteAttemptAsync(runningTask, attempt);
        }

        return process;
    }

    private async Task CompleteAttemptAsync(RunningTask runningTask, ProcessAttempt attempt)
    {
        if (Interlocked.Exchange(ref attempt.Completed, 1) == 1)
        {
            await attempt.Completion.Task.ConfigureAwait(false);
            return;
        }

        try
        {
            if (!SafeHasExited(attempt.Process))
            {
                await attempt.Process.WaitForExitAsync().ConfigureAwait(false);
            }

            var exitCode = SafeExitCode(attempt.Process);
            var error = TrimError(await attempt.ErrorTask.ConfigureAwait(false));
            _ = await attempt.OutputTask.ConfigureAwait(false);
            attempt.Process.Dispose();

            if (ShouldRestart(runningTask, exitCode))
            {
                var restartNumber = Interlocked.Increment(ref runningTask.RestartCount);
                PostOrRun(() =>
                {
                    var result = runningTask.Task.LastResult;
                    result.Status = TaskRuntimeStatus.Restarting;
                    result.ExitCode = exitCode;
                    result.Error = error;
                    result.ExitedAt = DateTimeOffset.Now;
                    result.RestartCount = restartNumber;
                    NotifyUpdated(runningTask.Task, runningTask.OnUpdated);
                });

                try
                {
                    await Task.Delay(RestartDelay, runningTask.RestartCancellation.Token).ConfigureAwait(false);
                    StartAttempt(runningTask);
                    return;
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    _log?.Error($"Task restart failed: {runningTask.Definition.Name}", ex);
                }
            }

            var finalStatus = Volatile.Read(ref runningTask.StopRequested) == 1
                ? TaskRuntimeStatus.Stopped
                : exitCode == 0
                    ? TaskRuntimeStatus.Succeeded
                    : TaskRuntimeStatus.Failed;

            FinalizeTask(runningTask, finalStatus, exitCode, error);
        }
        catch (Exception ex)
        {
            _log?.Error($"Task completion failed: {runningTask.Definition.Name}", ex);
            FinalizeTask(runningTask, TaskRuntimeStatus.Failed, null, ex.Message);
        }
        finally
        {
            attempt.Completion.TrySetResult();
        }
    }

    private static bool ShouldRestart(RunningTask runningTask, int? exitCode)
    {
        return runningTask.Definition.RunMode == ManagedTaskRunMode.LongRunning
            && Volatile.Read(ref runningTask.StopRequested) == 0
            && exitCode != 0
            && runningTask.RestartCount < MaxRestartAttempts;
    }

    private void ApplyFinalResult(RunningTask runningTask, TaskRuntimeStatus status, int? exitCode, string? error)
    {
        var result = runningTask.Task.LastResult;
        result.Status = status;
        result.ExitCode = exitCode;
        result.ExitedAt = DateTimeOffset.Now;
        result.Error = status is TaskRuntimeStatus.Failed or TaskRuntimeStatus.FailedToStart
            ? error ?? (exitCode is null ? "任务运行失败。" : $"进程以退出码 {exitCode} 结束。")
            : null;
        result.RestartCount = runningTask.RestartCount;
        _log?.Info($"Task completed: {runningTask.Definition.Name}, status {status}, code {exitCode}");
        NotifyUpdated(runningTask.Task, runningTask.OnUpdated);
        runningTask.RestartCancellation.Dispose();
    }

    private void FinalizeWithoutProcess(RunningTask runningTask, TaskRuntimeStatus status)
    {
        FinalizeTask(runningTask, status, null, null);
    }

    private void FinalizeTask(RunningTask runningTask, TaskRuntimeStatus status, int? exitCode, string? error)
    {
        if (Interlocked.Exchange(ref runningTask.Finalized, 1) == 1) return;
        _runningProcesses.TryRemove(runningTask.Task.Id, out _);
        PostOrRun(() => ApplyFinalResult(runningTask, status, exitCode, error));
    }

    private static void ValidateTask(ManagedTask task)
    {
        if (string.IsNullOrWhiteSpace(task.Name))
        {
            throw new ArgumentException("Task name is required.", nameof(task));
        }

        if (string.IsNullOrWhiteSpace(task.Path) || !File.Exists(task.Path))
        {
            throw new FileNotFoundException($"Task target file was not found: {task.Path}", task.Path);
        }

        if (!string.IsNullOrWhiteSpace(task.WorkingDirectory) && !Directory.Exists(task.WorkingDirectory))
        {
            throw new DirectoryNotFoundException($"Working directory was not found: {task.WorkingDirectory}");
        }
    }

    private static string ResolveWorkingDirectory(ManagedTask task) =>
        !string.IsNullOrWhiteSpace(task.WorkingDirectory)
            ? task.WorkingDirectory
            : Path.GetDirectoryName(task.Path) ?? Environment.CurrentDirectory;

    private static string BuildPowerShellArguments(ManagedTask task)
    {
        var fixedArguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{task.Path}\"";
        return string.IsNullOrWhiteSpace(task.Arguments) ? fixedArguments : $"{fixedArguments} {task.Arguments}";
    }

    private static int? SafeExitCode(Process process)
    {
        try { return process.ExitCode; }
        catch { return null; }
    }

    private static bool SafeHasExited(Process process)
    {
        try { return process.HasExited; }
        catch { return true; }
    }

    private static string? TrimError(string? error)
    {
        if (string.IsNullOrWhiteSpace(error)) return null;
        var trimmed = error.Trim();
        return trimmed.Length <= MaxCapturedErrorLength ? trimmed : trimmed[^MaxCapturedErrorLength..];
    }

    private void MarkFailedToStart(ManagedTask task, string error, Action<ManagedTask>? onUpdated)
    {
        PostOrRun(() =>
        {
            task.LastResult.Status = TaskRuntimeStatus.FailedToStart;
            task.LastResult.Error = error;
            task.LastResult.ExitCode = null;
            task.LastResult.StartedAt = null;
            task.LastResult.ExitedAt = DateTimeOffset.Now;
            task.LastResult.RestartCount = 0;
            NotifyUpdated(task, onUpdated);
        });
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
                try { action(); }
                catch (Exception ex) { _log?.Error("Task state dispatch failed.", ex); }
            }, null);
        }
        catch (Exception ex)
        {
            _log?.Error("Could not dispatch task state update.", ex);
            action();
        }
    }

    private void NotifyUpdated(ManagedTask task, Action<ManagedTask>? onUpdated)
    {
        if (onUpdated is null) return;
        try { onUpdated(task); }
        catch (Exception ex) { _log?.Error($"Task update callback failed: {task.Name}", ex); }
    }

    private sealed class RunningTask
    {
        public RunningTask(ManagedTask task, ManagedTask definition, Action<ManagedTask>? onUpdated)
        {
            Task = task;
            Definition = definition;
            OnUpdated = onUpdated;
        }

        public ManagedTask Task { get; }
        public ManagedTask Definition { get; }
        public Action<ManagedTask>? OnUpdated { get; }
        public CancellationTokenSource RestartCancellation { get; } = new();
        public ProcessAttempt? CurrentAttempt { get; set; }
        public int StopRequested;
        public int RestartCount;
        public int Finalized;
    }

    private sealed class ProcessAttempt
    {
        public ProcessAttempt(Process process) => Process = process;
        public Process Process { get; }
        public Task<string> OutputTask { get; set; } = Task.FromResult("");
        public Task<string> ErrorTask { get; set; } = Task.FromResult("");
        public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Completed;
    }
}
