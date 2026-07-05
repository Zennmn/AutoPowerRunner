using System.Text.Json;
using AutoPowerRunner.Models;
using AutoPowerRunner.Services;

namespace AutoPowerRunner.Tests;

public sealed class TaskConfigServiceTests
{
    [Fact]
    public void ManagedTask_SerializesExpectedFields()
    {
        var task = new ManagedTask
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Name = "Daily script",
            Type = ManagedTaskType.PowerShellScript,
            Path = @"C:\Scripts\daily.ps1",
            Arguments = "-Verbose",
            WorkingDirectory = @"C:\Scripts",
            RunMode = ManagedTaskRunMode.RunOnce,
            IsEnabled = true,
            LastResult = new TaskRuntimeResult
            {
                Status = TaskRuntimeStatus.Exited,
                ExitCode = 0,
                StartedAt = new DateTimeOffset(2026, 7, 5, 8, 0, 0, TimeSpan.Zero),
                ExitedAt = new DateTimeOffset(2026, 7, 5, 8, 0, 5, TimeSpan.Zero),
                Error = null
            }
        };

        var json = JsonSerializer.Serialize(task);

        Assert.Contains("\"Name\":\"Daily script\"", json);
        Assert.Contains("\"Type\":0", json);
        Assert.Contains("\"RunMode\":0", json);
        Assert.Contains("\"IsEnabled\":true", json);
        Assert.Contains("\"ExitCode\":0", json);
    }

    [Fact]
    public void ManagedTask_SerializationDoesNotIncludeRuntimeSummary()
    {
        var task = new ManagedTask
        {
            LastResult = new TaskRuntimeResult
            {
                Status = TaskRuntimeStatus.Exited,
                ExitCode = 0
            }
        };

        var json = JsonSerializer.Serialize(task);

        Assert.DoesNotContain("\"Summary\":", json);
    }

    [Fact]
    public void ManagedTask_CloneCopiesLastResultByValue()
    {
        var original = new ManagedTask
        {
            LastResult = new TaskRuntimeResult
            {
                Status = TaskRuntimeStatus.Running,
                ExitCode = null,
                StartedAt = new DateTimeOffset(2026, 7, 5, 8, 0, 0, TimeSpan.Zero),
                ExitedAt = null,
                Error = null
            }
        };

        var clone = original.Clone();

        Assert.NotSame(original.LastResult, clone.LastResult);

        clone.LastResult.Status = TaskRuntimeStatus.Exited;
        clone.LastResult.ExitCode = 42;
        clone.LastResult.ExitedAt = new DateTimeOffset(2026, 7, 5, 8, 0, 5, TimeSpan.Zero);
        clone.LastResult.Error = "changed";

        Assert.Equal(TaskRuntimeStatus.Running, original.LastResult.Status);
        Assert.Null(original.LastResult.ExitCode);
        Assert.Null(original.LastResult.ExitedAt);
        Assert.Null(original.LastResult.Error);
    }

    [Fact]
    public async Task TaskConfigService_SavesAndLoadsMultipleTasks()
    {
        using var temp = new TempDirectory();
        var service = new TaskConfigService(temp.Path);
        var tasks = new List<ManagedTask>
        {
            new() { Name = "Script", Type = ManagedTaskType.PowerShellScript, Path = @"C:\Scripts\a.ps1", IsEnabled = true },
            new() { Name = "Tool", Type = ManagedTaskType.Executable, Path = @"C:\Tools\a.exe", IsEnabled = false }
        };

        await service.SaveAsync(tasks);
        var loaded = await service.LoadAsync();

        Assert.Equal(2, loaded.Count);
        Assert.Equal("Script", loaded[0].Name);
        Assert.Equal(ManagedTaskType.Executable, loaded[1].Type);
        Assert.False(loaded[1].IsEnabled);
    }

    [Fact]
    public async Task TaskConfigService_BacksUpDamagedConfigAndReturnsEmptyList()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(temp.Path);
        await File.WriteAllTextAsync(System.IO.Path.Combine(temp.Path, "config.json"), "{not valid json");
        var service = new TaskConfigService(temp.Path);

        var loaded = await service.LoadAsync();

        Assert.Empty(loaded);
        Assert.Contains(Directory.GetFiles(temp.Path), path => path.Contains("config.corrupt.", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AutoPowerRunner.Tests", Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
