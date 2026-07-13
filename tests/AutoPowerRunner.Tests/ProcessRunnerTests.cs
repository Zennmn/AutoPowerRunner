using System.Diagnostics;
using AutoPowerRunner.Models;
using AutoPowerRunner.Services;

namespace AutoPowerRunner.Tests;

public sealed class ProcessRunnerTests
{
    [Fact]
    public void BuildStartInfo_ForPowerShellScript_UsesExecutionPolicyBypassAndFile()
    {
        var task = new ManagedTask
        {
            Type = ManagedTaskType.PowerShellScript,
            Path = @"C:\Scripts\boot.ps1",
            Arguments = "-Mode Fast",
            WorkingDirectory = @"C:\Scripts"
        };

        var startInfo = ProcessRunner.BuildStartInfo(task);

        Assert.Equal("powershell.exe", startInfo.FileName);
        Assert.Contains("-ExecutionPolicy Bypass", startInfo.Arguments);
        Assert.Contains("-File \"C:\\Scripts\\boot.ps1\"", startInfo.Arguments);
        Assert.EndsWith("-Mode Fast", startInfo.Arguments);
        Assert.Equal(@"C:\Scripts", startInfo.WorkingDirectory);
        Assert.False(startInfo.UseShellExecute);
        Assert.True(startInfo.CreateNoWindow);
        Assert.Equal(ProcessWindowStyle.Hidden, startInfo.WindowStyle);
        Assert.Contains("-NoProfile", startInfo.Arguments);
        Assert.Contains("-WindowStyle Hidden", startInfo.Arguments);
    }

    [Fact]
    public void BuildStartInfo_ForPowerShellScript_QuotesScriptPathWithSpacesAndAppendsRawArguments()
    {
        const string rawArguments = "-Mode Fast -Name \"Raw Value\"";
        var task = new ManagedTask
        {
            Type = ManagedTaskType.PowerShellScript,
            Path = @"C:\Scripts With Spaces\boot script.ps1",
            Arguments = rawArguments,
            WorkingDirectory = @"C:\Scripts With Spaces"
        };

        var startInfo = ProcessRunner.BuildStartInfo(task);

        Assert.Equal("powershell.exe", startInfo.FileName);
        Assert.Contains("-ExecutionPolicy Bypass", startInfo.Arguments);
        Assert.Contains("-File \"C:\\Scripts With Spaces\\boot script.ps1\"", startInfo.Arguments);
        Assert.EndsWith(rawArguments, startInfo.Arguments);
        Assert.Equal(@"C:\Scripts With Spaces", startInfo.WorkingDirectory);
        Assert.False(startInfo.UseShellExecute);
        Assert.True(startInfo.CreateNoWindow);
        Assert.Equal(ProcessWindowStyle.Hidden, startInfo.WindowStyle);
    }

    [Fact]
    public void BuildStartInfo_ForExe_UsesExecutablePathAndArguments()
    {
        var task = new ManagedTask
        {
            Type = ManagedTaskType.Executable,
            Path = @"C:\Tools\agent.exe",
            Arguments = "--quiet",
            WorkingDirectory = ""
        };

        var startInfo = ProcessRunner.BuildStartInfo(task);

        Assert.Equal(@"C:\Tools\agent.exe", startInfo.FileName);
        Assert.Equal("--quiet", startInfo.Arguments);
        Assert.Equal(@"C:\Tools", startInfo.WorkingDirectory);
        Assert.False(startInfo.UseShellExecute);
        Assert.True(startInfo.CreateNoWindow);
        Assert.Equal(ProcessWindowStyle.Hidden, startInfo.WindowStyle);
    }

    [Fact]
    public void Start_ForMissingPath_MarksFailedAndLogsError()
    {
        using var temp = new TempDirectory();
        var log = new LogService(new AppPaths(temp.Path, temp.Path));
        var runner = new ProcessRunner(log);
        var task = new ManagedTask
        {
            Name = "Missing script",
            Type = ManagedTaskType.PowerShellScript,
            Path = System.IO.Path.Combine(temp.Path, "missing.ps1")
        };

        var exception = Assert.Throws<FileNotFoundException>(() => runner.Start(task));

        Assert.Equal(task.Path, exception.FileName);
        Assert.Equal(TaskRuntimeStatus.FailedToStart, task.LastResult.Status);
        Assert.Contains(task.Path, task.LastResult.Error);
        Assert.Null(task.LastResult.ExitCode);
        Assert.Null(task.LastResult.StartedAt);
        Assert.NotNull(task.LastResult.ExitedAt);
        Assert.True(File.Exists(log.LogFile));
        var logText = File.ReadAllText(log.LogFile);
        Assert.Contains("[ERROR]", logText);
        Assert.Contains(task.Path, logText);
    }

    [Fact]
    public async Task Start_ForQuickExit_ProcessEndsExitedAndDoesNotRemainRunning()
    {
        using var temp = new TempDirectory();
        var script = temp.WritePowerShellScript("quick-exit.ps1", "exit 7");
        var runner = new ProcessRunner();
        var task = new ManagedTask
        {
            Name = "Quick exit",
            Type = ManagedTaskType.PowerShellScript,
            Path = script,
            WorkingDirectory = temp.Path
        };

        using var process = runner.Start(task);

        var exited = await WaitUntilAsync(() => task.LastResult.Status == TaskRuntimeStatus.Exited);

        Assert.True(exited);
        Assert.Equal(7, task.LastResult.ExitCode);
        Assert.NotNull(task.LastResult.StartedAt);
        Assert.NotNull(task.LastResult.ExitedAt);
        Assert.DoesNotContain(task.Id, runner.RunningTaskIds);
    }

    [Fact]
    public async Task Start_WhenCallbackThrows_DoesNotMarkTaskFailedOrLeakRunningProcess()
    {
        using var temp = new TempDirectory();
        var script = temp.WritePowerShellScript("callback-throws.ps1", "exit 0");
        var runner = new ProcessRunner();
        var task = new ManagedTask
        {
            Name = "Callback throws",
            Type = ManagedTaskType.PowerShellScript,
            Path = script,
            WorkingDirectory = temp.Path
        };

        var exception = Record.Exception(() => runner.Start(task, _ => throw new InvalidOperationException("callback boom")));

        Assert.Null(exception);
        var exited = await WaitUntilAsync(() => task.LastResult.Status == TaskRuntimeStatus.Exited);
        Assert.True(exited);
        Assert.Equal(0, task.LastResult.ExitCode);
        Assert.DoesNotContain(task.Id, runner.RunningTaskIds);
    }

    [Fact]
    public async Task Start_WithSynchronizationContext_DispatchesCompletionResultChanges()
    {
        using var temp = new TempDirectory();
        var script = temp.WritePowerShellScript("context-completion.ps1", "Start-Sleep -Milliseconds 200; exit 3");
        var updateContext = new CapturingSynchronizationContext();
        var runner = new ProcessRunner(updateContext: updateContext);
        var task = new ManagedTask
        {
            Name = "Context completion",
            Type = ManagedTaskType.PowerShellScript,
            Path = script,
            WorkingDirectory = temp.Path
        };
        var directChanges = new List<string?>();
        var dispatchedChanges = new List<string?>();
        task.LastResult.PropertyChanged += (_, args) =>
        {
            if (updateContext.IsDraining)
            {
                dispatchedChanges.Add(args.PropertyName);
            }
            else
            {
                directChanges.Add(args.PropertyName);
            }
        };

        using var process = runner.Start(task);
        directChanges.Clear();
        dispatchedChanges.Clear();

        var completionPosted = await WaitUntilAsync(() => updateContext.PostedCount > 0);

        Assert.True(completionPosted);
        Assert.Empty(directChanges);
        Assert.Equal(TaskRuntimeStatus.Running, task.LastResult.Status);

        updateContext.Drain();

        Assert.Equal(TaskRuntimeStatus.Exited, task.LastResult.Status);
        Assert.Equal(3, task.LastResult.ExitCode);
        Assert.Contains(nameof(TaskRuntimeResult.Status), dispatchedChanges);
        Assert.Contains(nameof(TaskRuntimeResult.ExitCode), dispatchedChanges);
        Assert.DoesNotContain(task.Id, runner.RunningTaskIds);
    }

    [Fact]
    public void Start_WhenTaskAlreadyRunning_ThrowsInvalidOperationException()
    {
        using var temp = new TempDirectory();
        var script = temp.WritePowerShellScript("long-running.ps1", "Start-Sleep -Seconds 30");
        var runner = new ProcessRunner();
        var task = new ManagedTask
        {
            Name = "Already running",
            Type = ManagedTaskType.PowerShellScript,
            Path = script,
            WorkingDirectory = temp.Path
        };
        Process? firstProcess = null;
        Process? secondProcess = null;

        try
        {
            firstProcess = runner.Start(task);

            var exception = Record.Exception(() => secondProcess = runner.Start(task));

            Assert.IsType<InvalidOperationException>(exception);
        }
        finally
        {
            runner.Stop(task.Id);
            KillIfRunning(secondProcess);
            KillIfRunning(firstProcess);
        }
    }

    [Fact]
    public async Task Stop_ForLongRunningProcess_RemovesProcessFromRunningTasks()
    {
        using var temp = new TempDirectory();
        var script = temp.WritePowerShellScript("stop-running.ps1", "Start-Sleep -Seconds 30");
        var runner = new ProcessRunner();
        var task = new ManagedTask
        {
            Name = "Stop me",
            Type = ManagedTaskType.PowerShellScript,
            Path = script,
            WorkingDirectory = temp.Path
        };

        runner.Start(task);
        var started = await WaitUntilAsync(() => runner.RunningTaskIds.Contains(task.Id));

        Assert.True(started);

        runner.Stop(task.Id);

        Assert.DoesNotContain(task.Id, runner.RunningTaskIds);
        Assert.Equal(TaskRuntimeStatus.Exited, task.LastResult.Status);
        Assert.NotNull(task.LastResult.ExitedAt);
    }

    private static async Task<bool> WaitUntilAsync(Func<bool> condition, int timeoutMilliseconds = 5000)
    {
        var deadline = DateTimeOffset.Now.AddMilliseconds(timeoutMilliseconds);
        while (DateTimeOffset.Now < deadline)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(50);
        }

        return condition();
    }

    private static void KillIfRunning(Process? process)
    {
        if (process is null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(milliseconds: 5000);
            }
        }
        catch
        {
        }
        finally
        {
            process.Dispose();
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AutoPowerRunner.Tests", Guid.NewGuid().ToString("N"));

        public string WritePowerShellScript(string name, string content)
        {
            Directory.CreateDirectory(Path);
            var scriptPath = System.IO.Path.Combine(Path, name);
            File.WriteAllText(scriptPath, content);
            return scriptPath;
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }

    private sealed class CapturingSynchronizationContext : SynchronizationContext
    {
        private readonly Queue<Action> _postedCallbacks = [];
        private readonly object _gate = new();

        public int PostedCount
        {
            get
            {
                lock (_gate)
                {
                    return _postedCallbacks.Count;
                }
            }
        }

        public bool IsDraining { get; private set; }

        public override void Post(SendOrPostCallback d, object? state)
        {
            lock (_gate)
            {
                _postedCallbacks.Enqueue(() => d(state));
            }
        }

        public void Drain()
        {
            IsDraining = true;
            try
            {
                while (TryDequeue(out var callback))
                {
                    callback();
                }
            }
            finally
            {
                IsDraining = false;
            }
        }

        private bool TryDequeue(out Action callback)
        {
            lock (_gate)
            {
                if (_postedCallbacks.Count == 0)
                {
                    callback = () => { };
                    return false;
                }

                callback = _postedCallbacks.Dequeue();
                return true;
            }
        }
    }
}
