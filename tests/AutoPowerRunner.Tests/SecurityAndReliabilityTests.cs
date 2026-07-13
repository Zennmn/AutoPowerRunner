using System.Security;
using AutoPowerRunner.Models;
using AutoPowerRunner.Services;

namespace AutoPowerRunner.Tests;

public sealed class SecurityAndReliabilityTests
{
    [Fact]
    public async Task AuthorizedConfigHash_IgnoresRuntimeResultButRejectsDefinitionChanges()
    {
        using var temp = new TempDirectory();
        var service = new TaskConfigService(temp.Path);
        var task = new ManagedTask { Name = "Task", Path = @"C:\Tools\task.exe", Type = ManagedTaskType.Executable };
        await service.SaveAsync([task]);
        var configFile = Path.Combine(temp.Path, "config.json");
        var authorizedHash = TaskConfigService.ComputeConfigHash(configFile);

        task.LastResult.Status = TaskRuntimeStatus.Failed;
        task.LastResult.ExitCode = 7;
        await service.SaveAsync([task]);
        var authorizedService = new TaskConfigService(temp.Path, authorizedHash);
        Assert.Single(await authorizedService.LoadAsync());

        task.Path = @"C:\Tools\tampered.exe";
        await service.SaveAsync([task]);
        await Assert.ThrowsAsync<SecurityException>(() => authorizedService.LoadAsync());
    }

    [Fact]
    public async Task ConcurrentSaves_PersistLatestRequestedSnapshot()
    {
        using var temp = new TempDirectory();
        var service = new TaskConfigService(temp.Path);
        var saves = Enumerable.Range(0, 50)
            .Select(index => service.SaveAsync([new ManagedTask { Name = $"Task {index}", Path = $@"C:\Tasks\{index}.exe", Type = ManagedTaskType.Executable }]))
            .ToArray();

        await Task.WhenAll(saves);

        var loaded = Assert.Single(await service.LoadAsync());
        Assert.Equal("Task 49", loaded.Name);
    }

    [Fact]
    public async Task ConfigLoad_NormalizesDuplicateAndEmptyTaskIdsDeterministically()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(temp.Path);
        var duplicateId = Guid.NewGuid();
        var json = $$"""
            [
              { "Id": "{{duplicateId}}", "Name": "First", "Type": 1, "Path": "C:\\first.exe" },
              { "Id": "{{duplicateId}}", "Name": "Second", "Type": 1, "Path": "C:\\second.exe" },
              { "Id": "00000000-0000-0000-0000-000000000000", "Name": "Third", "Type": 99, "Path": "C:\\third.ps1" }
            ]
            """;
        await File.WriteAllTextAsync(Path.Combine(temp.Path, "config.json"), json);

        var firstLoad = await new TaskConfigService(temp.Path).LoadAsync();
        var secondLoad = await new TaskConfigService(temp.Path).LoadAsync();

        Assert.Equal(3, firstLoad.Select(task => task.Id).Distinct().Count());
        Assert.DoesNotContain(Guid.Empty, firstLoad.Select(task => task.Id));
        Assert.Equal(firstLoad.Select(task => task.Id), secondLoad.Select(task => task.Id));
        Assert.Equal(ManagedTaskType.PowerShellScript, firstLoad[2].Type);
    }

    [Fact]
    public void LogService_RotatesOversizedLog()
    {
        using var temp = new TempDirectory();
        var paths = new AppPaths(temp.Path, temp.Path);
        var log = new LogService(paths);
        log.Info(new string('x', 5 * 1024 * 1024));

        log.Info("after rotation");

        Assert.True(File.Exists(paths.LogFile + ".1"));
        Assert.Contains("after rotation", File.ReadAllText(paths.LogFile));
    }

    [Fact]
    public void StartupTaskArguments_IncludeAuthorizedConfigHash()
    {
        var arguments = StartupTaskService.BuildCreateArguments(
            "AutoPowerRunner",
            @"C:\Program Files\AutoPowerRunner\AutoPowerRunner.exe",
            @"DESKTOP\User",
            "ABC123");

        Assert.Contains("--authorized-config-hash ABC123", arguments);
    }

    [Fact]
    public void ProtectedInstallPath_RejectsPortableLocation()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        Assert.True(StartupTaskService.IsProtectedInstallPath(Path.Combine(programFiles, "AutoPowerRunner", "AutoPowerRunner.exe")));
        Assert.False(StartupTaskService.IsProtectedInstallPath(Path.Combine(Path.GetTempPath(), "AutoPowerRunner.exe")));
    }

    [Fact]
    public void EnableAdministratorAutostart_FromPortableLocation_IsRejectedBeforeSchtasks()
    {
        using var temp = new TempDirectory();
        var commandWasRun = false;
        var executable = Path.Combine(temp.Path, "AutoPowerRunner.exe");
        var configFile = Path.Combine(temp.Path, "config.json");
        var service = new StartupTaskService(
            executable,
            commandRunner: (_, _) =>
            {
                commandWasRun = true;
                return new StartupTaskService.CommandResult(0, "", "");
            },
            configFile: configFile);

        Assert.Throws<SecurityException>(() => service.Enable());
        Assert.False(commandWasRun);
    }

    [Fact]
    public async Task StartupTaskStatus_RequiresCurrentAuthorizedDefinitionHash()
    {
        using var temp = new TempDirectory();
        var config = new TaskConfigService(temp.Path);
        var task = new ManagedTask { Name = "Task", Path = @"C:\Tools\task.exe", Type = ManagedTaskType.Executable };
        await config.SaveAsync([task]);
        var configFile = Path.Combine(temp.Path, "config.json");
        var executable = @"C:\Program Files\AutoPowerRunner\AutoPowerRunner.exe";
        var authorizedHash = TaskConfigService.ComputeConfigHash(configFile);
        var startup = new StartupTaskService(
            executable,
            commandRunner: (_, _) => new StartupTaskService.CommandResult(0, $"Task To Run: \"{executable}\" --silent-startup --authorized-config-hash {authorizedHash}\nStatus: Ready", ""),
            configFile: configFile);

        Assert.True(startup.IsEnabled());

        task.Arguments = "--tampered";
        await config.SaveAsync([task]);
        Assert.False(startup.IsEnabled());
    }

    [Fact]
    public async Task LongRunningTask_RestartsAfterFailureThenStopsAtLimit()
    {
        using var temp = new TempDirectory();
        var script = temp.WriteScript("restart.ps1", "Write-Error 'restart failure'; exit 2");
        var runner = new ProcessRunner();
        var task = new ManagedTask
        {
            Name = "Restarting task",
            Type = ManagedTaskType.PowerShellScript,
            Path = script,
            WorkingDirectory = temp.Path,
            RunMode = ManagedTaskRunMode.LongRunning
        };

        runner.Start(task);
        Assert.True(await WaitUntilAsync(() => task.LastResult.Status == TaskRuntimeStatus.Failed, 12000));
        Assert.Equal(3, task.LastResult.RestartCount);
        Assert.Contains("restart failure", task.LastResult.Error, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(task.Id, runner.RunningTaskIds);
    }

    [Fact]
    public async Task LongRunningTask_StopDuringRestart_DoesNotRestartOrLeak()
    {
        using var temp = new TempDirectory();
        var script = temp.WriteScript("stop-restart.ps1", "exit 3");
        var runner = new ProcessRunner();
        var task = new ManagedTask
        {
            Name = "Stop restart",
            Type = ManagedTaskType.PowerShellScript,
            Path = script,
            WorkingDirectory = temp.Path,
            RunMode = ManagedTaskRunMode.LongRunning
        };

        runner.Start(task);
        Assert.True(await WaitUntilAsync(() => task.LastResult.Status == TaskRuntimeStatus.Restarting));
        runner.Stop(task.Id);

        Assert.True(await WaitUntilAsync(() => task.LastResult.Status == TaskRuntimeStatus.Stopped));
        Assert.DoesNotContain(task.Id, runner.RunningTaskIds);
    }

    private static async Task<bool> WaitUntilAsync(Func<bool> condition, int timeoutMilliseconds = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);
        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return true;
            await Task.Delay(25);
        }
        return condition();
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AutoPowerRunner.Tests", Guid.NewGuid().ToString("N"));

        public string WriteScript(string name, string content)
        {
            Directory.CreateDirectory(Path);
            var path = System.IO.Path.Combine(Path, name);
            File.WriteAllText(path, content);
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
    }
}
