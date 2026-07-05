# Auto Power Runner Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a lightweight .NET 8 WPF tray app that manages multiple PowerShell script and EXE startup tasks, including elevated login autostart through Windows Task Scheduler.

**Architecture:** The solution has a WPF app for UI/tray behavior and a test project for domain/service coverage. Core logic is separated into focused models and services so process launching, JSON configuration, logging, and scheduled-task command generation can be tested without opening the UI.

**Tech Stack:** C# 12, .NET 8, WPF, xUnit, System.Text.Json, System.Diagnostics.Process, Windows Forms NotifyIcon, Windows `schtasks.exe`.

---

## File Structure

- Create: `AutoPowerRunner.sln` - solution containing the app and tests.
- Create: `src/AutoPowerRunner/AutoPowerRunner.csproj` - .NET 8 WPF project.
- Create: `src/AutoPowerRunner/App.xaml` - WPF app declaration.
- Create: `src/AutoPowerRunner/App.xaml.cs` - composition root, single-instance guard, tray lifecycle, startup task run, and shutdown.
- Create: `src/AutoPowerRunner/MainWindow.xaml` - basic management UI.
- Create: `src/AutoPowerRunner/MainWindow.xaml.cs` - window event bindings and dialog flow.
- Create: `src/AutoPowerRunner/TaskEditorWindow.xaml` - add/edit task dialog.
- Create: `src/AutoPowerRunner/TaskEditorWindow.xaml.cs` - task editor validation and result creation.
- Create: `src/AutoPowerRunner/Models/ManagedTask.cs` - task configuration model.
- Create: `src/AutoPowerRunner/Models/TaskEnums.cs` - task type, run mode, and runtime status enums.
- Create: `src/AutoPowerRunner/Models/TaskRuntimeResult.cs` - last-run status data.
- Create: `src/AutoPowerRunner/Services/AppPaths.cs` - app data path calculation.
- Create: `src/AutoPowerRunner/Services/TaskConfigService.cs` - JSON load/save and damaged-config backup.
- Create: `src/AutoPowerRunner/Services/LogService.cs` - append-only log writer.
- Create: `src/AutoPowerRunner/Services/ProcessRunner.cs` - start, monitor, and stop child processes.
- Create: `src/AutoPowerRunner/Services/StartupTaskService.cs` - scheduled-task status, command construction, enable, and disable.
- Create: `src/AutoPowerRunner/ViewModels/MainViewModel.cs` - observable task list, commands, and status updates.
- Create: `src/AutoPowerRunner/ViewModels/RelayCommand.cs` - simple WPF command helper.
- Create: `tests/AutoPowerRunner.Tests/AutoPowerRunner.Tests.csproj` - xUnit test project.
- Create: `tests/AutoPowerRunner.Tests/TaskConfigServiceTests.cs` - configuration behavior tests.
- Create: `tests/AutoPowerRunner.Tests/ProcessRunnerTests.cs` - command construction and process status tests.
- Create: `tests/AutoPowerRunner.Tests/StartupTaskServiceTests.cs` - scheduled-task command tests.

## Task 1: Scaffold Solution And Projects

**Files:**
- Create: `AutoPowerRunner.sln`
- Create: `src/AutoPowerRunner/AutoPowerRunner.csproj`
- Create: `src/AutoPowerRunner/App.xaml`
- Create: `src/AutoPowerRunner/App.xaml.cs`
- Create: `src/AutoPowerRunner/MainWindow.xaml`
- Create: `src/AutoPowerRunner/MainWindow.xaml.cs`
- Create: `tests/AutoPowerRunner.Tests/AutoPowerRunner.Tests.csproj`

- [ ] **Step 1: Create the solution and projects**

Run:

```powershell
dotnet new sln -n AutoPowerRunner
dotnet new wpf -n AutoPowerRunner -o src/AutoPowerRunner -f net8.0-windows
dotnet new xunit -n AutoPowerRunner.Tests -o tests/AutoPowerRunner.Tests -f net8.0
dotnet sln AutoPowerRunner.sln add src/AutoPowerRunner/AutoPowerRunner.csproj
dotnet sln AutoPowerRunner.sln add tests/AutoPowerRunner.Tests/AutoPowerRunner.Tests.csproj
dotnet add tests/AutoPowerRunner.Tests/AutoPowerRunner.Tests.csproj reference src/AutoPowerRunner/AutoPowerRunner.csproj
```

Expected: solution and two projects are created, and `dotnet sln list` shows both projects.

- [ ] **Step 2: Update the app project for Windows Forms tray support**

Replace `src/AutoPowerRunner/AutoPowerRunner.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UseWPF>true</UseWPF>
    <UseWindowsForms>true</UseWindowsForms>
    <ApplicationIcon />
  </PropertyGroup>
</Project>
```

- [ ] **Step 3: Update the test project for the Windows target framework**

Replace `tests/AutoPowerRunner.Tests/AutoPowerRunner.Tests.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.2">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\AutoPowerRunner\AutoPowerRunner.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4: Remove the default test file**

Delete:

```text
tests/AutoPowerRunner.Tests/UnitTest1.cs
```

- [ ] **Step 5: Build to verify the scaffold**

Run:

```powershell
dotnet build AutoPowerRunner.sln
```

Expected: build succeeds with 0 errors.

- [ ] **Step 6: Commit the scaffold**

Run:

```powershell
git add AutoPowerRunner.sln src/AutoPowerRunner tests/AutoPowerRunner.Tests
git commit -m "chore: scaffold Auto Power Runner solution"
```

Expected: commit succeeds. If Git author identity is unset, use per-command environment variables rather than changing global config:

```powershell
$env:GIT_AUTHOR_NAME='Codex'; $env:GIT_AUTHOR_EMAIL='codex@local'; $env:GIT_COMMITTER_NAME='Codex'; $env:GIT_COMMITTER_EMAIL='codex@local'; git commit -m "chore: scaffold Auto Power Runner solution"
```

## Task 2: Add Domain Models

**Files:**
- Create: `src/AutoPowerRunner/Models/TaskEnums.cs`
- Create: `src/AutoPowerRunner/Models/TaskRuntimeResult.cs`
- Create: `src/AutoPowerRunner/Models/ManagedTask.cs`
- Test: `tests/AutoPowerRunner.Tests/TaskConfigServiceTests.cs`

- [ ] **Step 1: Write the failing model serialization test**

Create `tests/AutoPowerRunner.Tests/TaskConfigServiceTests.cs`:

```csharp
using System.Text.Json;
using AutoPowerRunner.Models;

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
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run:

```powershell
dotnet test AutoPowerRunner.sln --filter ManagedTask_SerializesExpectedFields
```

Expected: FAIL because `AutoPowerRunner.Models` types do not exist.

- [ ] **Step 3: Add enum definitions**

Create `src/AutoPowerRunner/Models/TaskEnums.cs`:

```csharp
namespace AutoPowerRunner.Models;

public enum ManagedTaskType
{
    PowerShellScript = 0,
    Executable = 1
}

public enum ManagedTaskRunMode
{
    RunOnce = 0,
    LongRunning = 1
}

public enum TaskRuntimeStatus
{
    NotRunning = 0,
    Running = 1,
    Exited = 2,
    FailedToStart = 3
}
```

- [ ] **Step 4: Add runtime result model**

Create `src/AutoPowerRunner/Models/TaskRuntimeResult.cs`:

```csharp
namespace AutoPowerRunner.Models;

public sealed class TaskRuntimeResult
{
    public TaskRuntimeStatus Status { get; set; } = TaskRuntimeStatus.NotRunning;
    public int? ExitCode { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? ExitedAt { get; set; }
    public string? Error { get; set; }

    public string Summary
    {
        get
        {
            return Status switch
            {
                TaskRuntimeStatus.NotRunning => "Not running",
                TaskRuntimeStatus.Running => StartedAt is null ? "Running" : $"Running since {StartedAt:yyyy-MM-dd HH:mm:ss}",
                TaskRuntimeStatus.Exited => ExitCode is null ? "Exited" : $"Exited with code {ExitCode}",
                TaskRuntimeStatus.FailedToStart => string.IsNullOrWhiteSpace(Error) ? "Failed to start" : $"Failed: {Error}",
                _ => Status.ToString()
            };
        }
    }
}
```

- [ ] **Step 5: Add managed task model**

Create `src/AutoPowerRunner/Models/ManagedTask.cs`:

```csharp
namespace AutoPowerRunner.Models;

public sealed class ManagedTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public ManagedTaskType Type { get; set; } = ManagedTaskType.PowerShellScript;
    public string Path { get; set; } = "";
    public string Arguments { get; set; } = "";
    public string WorkingDirectory { get; set; } = "";
    public ManagedTaskRunMode RunMode { get; set; } = ManagedTaskRunMode.RunOnce;
    public bool IsEnabled { get; set; } = true;
    public TaskRuntimeResult LastResult { get; set; } = new();

    public ManagedTask Clone()
    {
        return new ManagedTask
        {
            Id = Id,
            Name = Name,
            Type = Type,
            Path = Path,
            Arguments = Arguments,
            WorkingDirectory = WorkingDirectory,
            RunMode = RunMode,
            IsEnabled = IsEnabled,
            LastResult = new TaskRuntimeResult
            {
                Status = LastResult.Status,
                ExitCode = LastResult.ExitCode,
                StartedAt = LastResult.StartedAt,
                ExitedAt = LastResult.ExitedAt,
                Error = LastResult.Error
            }
        };
    }
}
```

- [ ] **Step 6: Run the model test**

Run:

```powershell
dotnet test AutoPowerRunner.sln --filter ManagedTask_SerializesExpectedFields
```

Expected: PASS.

- [ ] **Step 7: Commit domain models**

Run:

```powershell
git add src/AutoPowerRunner/Models tests/AutoPowerRunner.Tests/TaskConfigServiceTests.cs
git commit -m "feat: add managed task models"
```

## Task 3: Add Configuration And Logging Services

**Files:**
- Create: `src/AutoPowerRunner/Services/AppPaths.cs`
- Create: `src/AutoPowerRunner/Services/TaskConfigService.cs`
- Create: `src/AutoPowerRunner/Services/LogService.cs`
- Modify: `tests/AutoPowerRunner.Tests/TaskConfigServiceTests.cs`

- [ ] **Step 1: Add failing config service tests**

Append these tests inside `TaskConfigServiceTests`:

```csharp
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
```

Add these `using` statements at the top:

```csharp
using AutoPowerRunner.Services;
```

- [ ] **Step 2: Run config tests to verify they fail**

Run:

```powershell
dotnet test AutoPowerRunner.sln --filter TaskConfigService
```

Expected: FAIL because `TaskConfigService` does not exist.

- [ ] **Step 3: Add app paths helper**

Create `src/AutoPowerRunner/Services/AppPaths.cs`:

```csharp
namespace AutoPowerRunner.Services;

public sealed class AppPaths
{
    public AppPaths(string appDataRoot, string localAppDataRoot)
    {
        ConfigDirectory = Path.Combine(appDataRoot, "AutoPowerRunner");
        ConfigFile = Path.Combine(ConfigDirectory, "config.json");
        LogDirectory = Path.Combine(localAppDataRoot, "AutoPowerRunner", "logs");
        LogFile = Path.Combine(LogDirectory, "app.log");
    }

    public string ConfigDirectory { get; }
    public string ConfigFile { get; }
    public string LogDirectory { get; }
    public string LogFile { get; }

    public static AppPaths ForCurrentUser()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return new AppPaths(appData, localAppData);
    }
}
```

- [ ] **Step 4: Add config service**

Create `src/AutoPowerRunner/Services/TaskConfigService.cs`:

```csharp
using System.Text.Json;
using AutoPowerRunner.Models;

namespace AutoPowerRunner.Services;

public sealed class TaskConfigService
{
    private readonly string _configDirectory;
    private readonly string _configFile;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    public TaskConfigService(string configDirectory)
    {
        _configDirectory = configDirectory;
        _configFile = Path.Combine(configDirectory, "config.json");
    }

    public TaskConfigService(AppPaths paths) : this(paths.ConfigDirectory)
    {
    }

    public async Task<List<ManagedTask>> LoadAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_configDirectory);

        if (!File.Exists(_configFile))
        {
            return [];
        }

        try
        {
            await using var stream = File.OpenRead(_configFile);
            var tasks = await JsonSerializer.DeserializeAsync<List<ManagedTask>>(stream, _jsonOptions, cancellationToken);
            return tasks ?? [];
        }
        catch (JsonException)
        {
            BackupCorruptConfig();
            return [];
        }
    }

    public async Task SaveAsync(IReadOnlyCollection<ManagedTask> tasks, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_configDirectory);
        var tempFile = _configFile + ".tmp";
        await using (var stream = File.Create(tempFile))
        {
            await JsonSerializer.SerializeAsync(stream, tasks, _jsonOptions, cancellationToken);
        }

        if (File.Exists(_configFile))
        {
            File.Delete(_configFile);
        }

        File.Move(tempFile, _configFile);
    }

    private void BackupCorruptConfig()
    {
        var stamp = DateTimeOffset.Now.ToString("yyyyMMddHHmmss");
        var backupPath = Path.Combine(_configDirectory, $"config.corrupt.{stamp}.json");
        File.Move(_configFile, backupPath, overwrite: true);
    }
}
```

- [ ] **Step 5: Add log service**

Create `src/AutoPowerRunner/Services/LogService.cs`:

```csharp
namespace AutoPowerRunner.Services;

public sealed class LogService
{
    private readonly string _logFile;
    private readonly object _gate = new();

    public LogService(AppPaths paths)
    {
        _logFile = paths.LogFile;
    }

    public string LogFile => _logFile;

    public void Info(string message)
    {
        Write("INFO", message);
    }

    public void Error(string message, Exception? exception = null)
    {
        var detail = exception is null ? message : $"{message} {exception.GetType().Name}: {exception.Message}";
        Write("ERROR", detail);
    }

    private void Write(string level, string message)
    {
        var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz} [{level}] {message}{Environment.NewLine}";
        lock (_gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_logFile)!);
            File.AppendAllText(_logFile, line);
        }
    }
}
```

- [ ] **Step 6: Run config tests**

Run:

```powershell
dotnet test AutoPowerRunner.sln --filter TaskConfigService
```

Expected: PASS.

- [ ] **Step 7: Commit configuration and logging services**

Run:

```powershell
git add src/AutoPowerRunner/Services tests/AutoPowerRunner.Tests/TaskConfigServiceTests.cs
git commit -m "feat: add task configuration storage"
```

## Task 4: Add Process Runner

**Files:**
- Create: `src/AutoPowerRunner/Services/ProcessRunner.cs`
- Create: `tests/AutoPowerRunner.Tests/ProcessRunnerTests.cs`

- [ ] **Step 1: Write failing process runner tests**

Create `tests/AutoPowerRunner.Tests/ProcessRunnerTests.cs`:

```csharp
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
    }
}
```

- [ ] **Step 2: Run process tests to verify they fail**

Run:

```powershell
dotnet test AutoPowerRunner.sln --filter ProcessRunnerTests
```

Expected: FAIL because `ProcessRunner` does not exist.

- [ ] **Step 3: Add process runner**

Create `src/AutoPowerRunner/Services/ProcessRunner.cs`:

```csharp
using System.Collections.Concurrent;
using System.Diagnostics;
using AutoPowerRunner.Models;

namespace AutoPowerRunner.Services;

public sealed class ProcessRunner
{
    private readonly LogService? _log;
    private readonly ConcurrentDictionary<Guid, Process> _runningProcesses = new();

    public ProcessRunner(LogService? log = null)
    {
        _log = log;
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
                Arguments = $"-ExecutionPolicy Bypass -File \"{task.Path}\" {task.Arguments}".TrimEnd(),
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = false
            };
        }

        return new ProcessStartInfo
        {
            FileName = task.Path,
            Arguments = task.Arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = false
        };
    }

    public Process Start(ManagedTask task, Action<ManagedTask>? onUpdated = null)
    {
        if (!File.Exists(task.Path))
        {
            MarkFailed(task, $"File not found: {task.Path}", onUpdated);
            throw new FileNotFoundException("Task target file was not found.", task.Path);
        }

        var startInfo = BuildStartInfo(task);
        var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        process.Exited += (_, _) =>
        {
            task.LastResult.Status = TaskRuntimeStatus.Exited;
            task.LastResult.ExitCode = SafeExitCode(process);
            task.LastResult.ExitedAt = DateTimeOffset.Now;
            _runningProcesses.TryRemove(task.Id, out _);
            _log?.Info($"Task exited: {task.Name}, code {task.LastResult.ExitCode}");
            onUpdated?.Invoke(task);
            process.Dispose();
        };

        try
        {
            process.Start();
            task.LastResult.Status = TaskRuntimeStatus.Running;
            task.LastResult.ExitCode = null;
            task.LastResult.Error = null;
            task.LastResult.StartedAt = DateTimeOffset.Now;
            task.LastResult.ExitedAt = null;
            _runningProcesses[task.Id] = process;
            _log?.Info($"Task started: {task.Name}");
            onUpdated?.Invoke(task);
            return process;
        }
        catch (Exception ex)
        {
            process.Dispose();
            MarkFailed(task, ex.Message, onUpdated);
            _log?.Error($"Task failed to start: {task.Name}", ex);
            throw;
        }
    }

    public void Stop(Guid taskId)
    {
        if (!_runningProcesses.TryRemove(taskId, out var process))
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.CloseMainWindow();
                if (!process.WaitForExit(milliseconds: 3000))
                {
                    process.Kill(entireProcessTree: true);
                }
            }
        }
        finally
        {
            process.Dispose();
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

    private static void MarkFailed(ManagedTask task, string error, Action<ManagedTask>? onUpdated)
    {
        task.LastResult.Status = TaskRuntimeStatus.FailedToStart;
        task.LastResult.Error = error;
        task.LastResult.ExitCode = null;
        task.LastResult.StartedAt = null;
        task.LastResult.ExitedAt = DateTimeOffset.Now;
        onUpdated?.Invoke(task);
    }
}
```

- [ ] **Step 4: Run process tests**

Run:

```powershell
dotnet test AutoPowerRunner.sln --filter ProcessRunnerTests
```

Expected: PASS.

- [ ] **Step 5: Run all tests**

Run:

```powershell
dotnet test AutoPowerRunner.sln
```

Expected: PASS.

- [ ] **Step 6: Commit process runner**

Run:

```powershell
git add src/AutoPowerRunner/Services/ProcessRunner.cs tests/AutoPowerRunner.Tests/ProcessRunnerTests.cs
git commit -m "feat: add process runner"
```

## Task 5: Add Scheduled Task Service

**Files:**
- Create: `src/AutoPowerRunner/Services/StartupTaskService.cs`
- Create: `tests/AutoPowerRunner.Tests/StartupTaskServiceTests.cs`

- [ ] **Step 1: Write failing scheduled task tests**

Create `tests/AutoPowerRunner.Tests/StartupTaskServiceTests.cs`:

```csharp
using AutoPowerRunner.Services;

namespace AutoPowerRunner.Tests;

public sealed class StartupTaskServiceTests
{
    [Fact]
    public void BuildCreateArguments_UsesCurrentUserLogonAndHighestPrivilege()
    {
        var args = StartupTaskService.BuildCreateArguments(
            taskName: "AutoPowerRunner",
            executablePath: @"C:\Program Files\AutoPowerRunner\AutoPowerRunner.exe",
            userName: @"DESKTOP\User");

        Assert.Contains("/Create", args);
        Assert.Contains("/TN \"AutoPowerRunner\"", args);
        Assert.Contains("/SC ONLOGON", args);
        Assert.Contains("/RL HIGHEST", args);
        Assert.Contains("/RU \"DESKTOP\\User\"", args);
        Assert.Contains("/TR \"\\\"C:\\Program Files\\AutoPowerRunner\\AutoPowerRunner.exe\\\"\"", args);
        Assert.Contains("/F", args);
    }

    [Fact]
    public void BuildDeleteArguments_ForcesDeletion()
    {
        var args = StartupTaskService.BuildDeleteArguments("AutoPowerRunner");

        Assert.Equal("/Delete /TN \"AutoPowerRunner\" /F", args);
    }
}
```

- [ ] **Step 2: Run scheduled task tests to verify they fail**

Run:

```powershell
dotnet test AutoPowerRunner.sln --filter StartupTaskServiceTests
```

Expected: FAIL because `StartupTaskService` does not exist.

- [ ] **Step 3: Add scheduled task service**

Create `src/AutoPowerRunner/Services/StartupTaskService.cs`:

```csharp
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
```

- [ ] **Step 4: Run scheduled task tests**

Run:

```powershell
dotnet test AutoPowerRunner.sln --filter StartupTaskServiceTests
```

Expected: PASS.

- [ ] **Step 5: Commit scheduled task service**

Run:

```powershell
git add src/AutoPowerRunner/Services/StartupTaskService.cs tests/AutoPowerRunner.Tests/StartupTaskServiceTests.cs
git commit -m "feat: add elevated startup task service"
```

## Task 6: Add View Model And Commands

**Files:**
- Create: `src/AutoPowerRunner/ViewModels/RelayCommand.cs`
- Create: `src/AutoPowerRunner/ViewModels/MainViewModel.cs`

- [ ] **Step 1: Add relay command**

Create `src/AutoPowerRunner/ViewModels/RelayCommand.cs`:

```csharp
using System.Windows.Input;

namespace AutoPowerRunner.ViewModels;

public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Predicate<object?>? _canExecute;

    public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        return _canExecute?.Invoke(parameter) ?? true;
    }

    public void Execute(object? parameter)
    {
        _execute(parameter);
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
```

- [ ] **Step 2: Add main view model**

Create `src/AutoPowerRunner/ViewModels/MainViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AutoPowerRunner.Models;
using AutoPowerRunner.Services;

namespace AutoPowerRunner.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly TaskConfigService _configService;
    private readonly ProcessRunner _processRunner;
    private readonly StartupTaskService _startupTaskService;
    private readonly LogService _logService;
    private ManagedTask? _selectedTask;
    private bool _isAdministratorAutostartEnabled;

    public MainViewModel(
        TaskConfigService configService,
        ProcessRunner processRunner,
        StartupTaskService startupTaskService,
        LogService logService)
    {
        _configService = configService;
        _processRunner = processRunner;
        _startupTaskService = startupTaskService;
        _logService = logService;

        RunSelectedCommand = new RelayCommand(_ => RunSelected(), _ => SelectedTask is not null);
        StopSelectedCommand = new RelayCommand(_ => StopSelected(), _ => SelectedTask is not null);
        DeleteSelectedCommand = new RelayCommand(_ => DeleteSelected(), _ => SelectedTask is not null);
        ToggleSelectedEnabledCommand = new RelayCommand(_ => ToggleSelectedEnabled(), _ => SelectedTask is not null);
        RunAllEnabledCommand = new RelayCommand(_ => RunAllEnabled());
        StopAllCommand = new RelayCommand(_ => StopAll());
        ToggleAutostartCommand = new RelayCommand(_ => ToggleAutostart());
    }

    public ObservableCollection<ManagedTask> Tasks { get; } = [];

    public ManagedTask? SelectedTask
    {
        get => _selectedTask;
        set
        {
            _selectedTask = value;
            OnPropertyChanged();
            RaiseCommandStates();
        }
    }

    public bool IsAdministratorAutostartEnabled
    {
        get => _isAdministratorAutostartEnabled;
        private set
        {
            _isAdministratorAutostartEnabled = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(AutostartStatusText));
            OnPropertyChanged(nameof(ToggleAutostartText));
        }
    }

    public string AutostartStatusText => IsAdministratorAutostartEnabled
        ? "Administrator autostart is enabled"
        : "Administrator autostart is disabled";

    public string ToggleAutostartText => IsAdministratorAutostartEnabled
        ? "Disable administrator autostart"
        : "Enable administrator autostart";

    public string LogFile => _logService.LogFile;

    public ICommand RunSelectedCommand { get; }
    public ICommand StopSelectedCommand { get; }
    public ICommand DeleteSelectedCommand { get; }
    public ICommand ToggleSelectedEnabledCommand { get; }
    public ICommand RunAllEnabledCommand { get; }
    public ICommand StopAllCommand { get; }
    public ICommand ToggleAutostartCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task LoadAsync()
    {
        Tasks.Clear();
        foreach (var task in await _configService.LoadAsync())
        {
            Tasks.Add(task);
        }

        IsAdministratorAutostartEnabled = _startupTaskService.IsEnabled();
    }

    public async Task SaveAsync()
    {
        await _configService.SaveAsync(Tasks);
    }

    public async Task AddOrUpdateTaskAsync(ManagedTask task)
    {
        var existing = Tasks.FirstOrDefault(item => item.Id == task.Id);
        if (existing is null)
        {
            Tasks.Add(task);
        }
        else
        {
            var index = Tasks.IndexOf(existing);
            Tasks[index] = task;
        }

        await SaveAsync();
    }

    public void RunAllEnabled()
    {
        foreach (var task in Tasks.Where(task => task.IsEnabled))
        {
            RunTask(task);
        }
    }

    public void StopAll()
    {
        _processRunner.StopAll();
    }

    private void RunSelected()
    {
        if (SelectedTask is not null)
        {
            RunTask(SelectedTask);
        }
    }

    private void StopSelected()
    {
        if (SelectedTask is not null)
        {
            _processRunner.Stop(SelectedTask.Id);
        }
    }

    private async void DeleteSelected()
    {
        if (SelectedTask is null)
        {
            return;
        }

        _processRunner.Stop(SelectedTask.Id);
        Tasks.Remove(SelectedTask);
        SelectedTask = null;
        await SaveAsync();
    }

    private async void ToggleSelectedEnabled()
    {
        if (SelectedTask is null)
        {
            return;
        }

        SelectedTask.IsEnabled = !SelectedTask.IsEnabled;
        await SaveAsync();
        OnPropertyChanged(nameof(Tasks));
    }

    private void ToggleAutostart()
    {
        if (IsAdministratorAutostartEnabled)
        {
            _startupTaskService.Disable();
        }
        else
        {
            _startupTaskService.Enable();
        }

        IsAdministratorAutostartEnabled = _startupTaskService.IsEnabled();
    }

    private void RunTask(ManagedTask task)
    {
        try
        {
            _processRunner.Start(task, _ => OnPropertyChanged(nameof(Tasks)));
        }
        catch (Exception ex)
        {
            _logService.Error($"Could not run task '{task.Name}'.", ex);
        }
    }

    private void RaiseCommandStates()
    {
        foreach (var command in new[] { RunSelectedCommand, StopSelectedCommand, DeleteSelectedCommand, ToggleSelectedEnabledCommand })
        {
            if (command is RelayCommand relayCommand)
            {
                relayCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
```

- [ ] **Step 3: Build view model code**

Run:

```powershell
dotnet build AutoPowerRunner.sln
```

Expected: PASS.

- [ ] **Step 4: Commit view model**

Run:

```powershell
git add src/AutoPowerRunner/ViewModels
git commit -m "feat: add main view model"
```

## Task 7: Add Window UI And Task Editor

**Files:**
- Modify: `src/AutoPowerRunner/MainWindow.xaml`
- Modify: `src/AutoPowerRunner/MainWindow.xaml.cs`
- Create: `src/AutoPowerRunner/TaskEditorWindow.xaml`
- Create: `src/AutoPowerRunner/TaskEditorWindow.xaml.cs`

- [ ] **Step 1: Replace main window XAML**

Replace `src/AutoPowerRunner/MainWindow.xaml` with:

```xml
<Window x:Class="AutoPowerRunner.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:models="clr-namespace:AutoPowerRunner.Models"
        Title="Auto Power Runner"
        Height="560"
        Width="920"
        MinHeight="460"
        MinWidth="760">
    <DockPanel Margin="12">
        <Border DockPanel.Dock="Top" Padding="10" BorderBrush="#D0D7DE" BorderThickness="1" CornerRadius="4" Margin="0,0,0,10">
            <DockPanel>
                <TextBlock Text="{Binding AutostartStatusText}" VerticalAlignment="Center" FontWeight="SemiBold" />
                <Button DockPanel.Dock="Right" Content="{Binding ToggleAutostartText}" Command="{Binding ToggleAutostartCommand}" Padding="10,4" />
            </DockPanel>
        </Border>

        <StackPanel DockPanel.Dock="Bottom" Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,10,0,0">
            <Button Content="Add" Width="88" Margin="4" Click="AddTask_Click" />
            <Button Content="Edit" Width="88" Margin="4" Click="EditTask_Click" />
            <Button Content="Delete" Width="88" Margin="4" Command="{Binding DeleteSelectedCommand}" />
            <Button Content="Enable/Disable" Width="120" Margin="4" Command="{Binding ToggleSelectedEnabledCommand}" />
            <Button Content="Run" Width="88" Margin="4" Command="{Binding RunSelectedCommand}" />
            <Button Content="Stop" Width="88" Margin="4" Command="{Binding StopSelectedCommand}" />
            <Button Content="Open Log" Width="96" Margin="4" Click="OpenLog_Click" />
        </StackPanel>

        <DataGrid ItemsSource="{Binding Tasks}"
                  SelectedItem="{Binding SelectedTask}"
                  AutoGenerateColumns="False"
                  CanUserAddRows="False"
                  IsReadOnly="True"
                  SelectionMode="Single">
            <DataGrid.Columns>
                <DataGridTextColumn Header="Name" Binding="{Binding Name}" Width="*" />
                <DataGridTextColumn Header="Type" Binding="{Binding Type}" Width="140" />
                <DataGridTextColumn Header="Mode" Binding="{Binding RunMode}" Width="130" />
                <DataGridCheckBoxColumn Header="Enabled" Binding="{Binding IsEnabled}" Width="90" />
                <DataGridTextColumn Header="Status" Binding="{Binding LastResult.Status}" Width="120" />
                <DataGridTextColumn Header="Recent Result" Binding="{Binding LastResult.Summary}" Width="240" />
            </DataGrid.Columns>
        </DataGrid>
    </DockPanel>
</Window>
```

- [ ] **Step 2: Add task editor XAML**

Create `src/AutoPowerRunner/TaskEditorWindow.xaml`:

```xml
<Window x:Class="AutoPowerRunner.TaskEditorWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Task"
        Height="390"
        Width="620"
        ResizeMode="NoResize"
        WindowStartupLocation="CenterOwner">
    <Grid Margin="14">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="130"/>
            <ColumnDefinition Width="*"/>
            <ColumnDefinition Width="Auto"/>
        </Grid.ColumnDefinitions>

        <TextBlock Grid.Row="0" Grid.Column="0" Text="Name" VerticalAlignment="Center" Margin="0,0,10,8"/>
        <TextBox Grid.Row="0" Grid.Column="1" Grid.ColumnSpan="2" x:Name="NameBox" Margin="0,0,0,8"/>

        <TextBlock Grid.Row="1" Grid.Column="0" Text="Type" VerticalAlignment="Center" Margin="0,0,10,8"/>
        <ComboBox Grid.Row="1" Grid.Column="1" Grid.ColumnSpan="2" x:Name="TypeBox" Margin="0,0,0,8">
            <ComboBoxItem Content="PowerShellScript"/>
            <ComboBoxItem Content="Executable"/>
        </ComboBox>

        <TextBlock Grid.Row="2" Grid.Column="0" Text="Path" VerticalAlignment="Center" Margin="0,0,10,8"/>
        <TextBox Grid.Row="2" Grid.Column="1" x:Name="PathBox" Margin="0,0,8,8"/>
        <Button Grid.Row="2" Grid.Column="2" Content="Browse" Width="80" Margin="0,0,0,8" Click="Browse_Click"/>

        <TextBlock Grid.Row="3" Grid.Column="0" Text="Arguments" VerticalAlignment="Center" Margin="0,0,10,8"/>
        <TextBox Grid.Row="3" Grid.Column="1" Grid.ColumnSpan="2" x:Name="ArgumentsBox" Margin="0,0,0,8"/>

        <TextBlock Grid.Row="4" Grid.Column="0" Text="Working Directory" VerticalAlignment="Center" Margin="0,0,10,8"/>
        <TextBox Grid.Row="4" Grid.Column="1" Grid.ColumnSpan="2" x:Name="WorkingDirectoryBox" Margin="0,0,0,8"/>

        <TextBlock Grid.Row="5" Grid.Column="0" Text="Run Mode" VerticalAlignment="Center" Margin="0,0,10,8"/>
        <ComboBox Grid.Row="5" Grid.Column="1" Grid.ColumnSpan="2" x:Name="RunModeBox" Margin="0,0,0,8">
            <ComboBoxItem Content="RunOnce"/>
            <ComboBoxItem Content="LongRunning"/>
        </ComboBox>

        <CheckBox Grid.Row="6" Grid.Column="1" Grid.ColumnSpan="2" x:Name="EnabledBox" Content="Enabled at app startup" VerticalAlignment="Top"/>

        <StackPanel Grid.Row="7" Grid.Column="0" Grid.ColumnSpan="3" Orientation="Horizontal" HorizontalAlignment="Right">
            <Button Content="Cancel" Width="88" Margin="4" IsCancel="True"/>
            <Button Content="Save" Width="88" Margin="4" IsDefault="True" Click="Save_Click"/>
        </StackPanel>
    </Grid>
</Window>
```

- [ ] **Step 3: Add task editor code-behind**

Create `src/AutoPowerRunner/TaskEditorWindow.xaml.cs`:

```csharp
using System.Windows;
using Microsoft.Win32;
using AutoPowerRunner.Models;

namespace AutoPowerRunner;

public partial class TaskEditorWindow : Window
{
    private readonly ManagedTask _original;

    public TaskEditorWindow(ManagedTask? task = null)
    {
        InitializeComponent();
        _original = task?.Clone() ?? new ManagedTask();
        LoadTask(_original);
    }

    public ManagedTask Result { get; private set; } = new();

    private void LoadTask(ManagedTask task)
    {
        NameBox.Text = task.Name;
        TypeBox.SelectedIndex = task.Type == ManagedTaskType.PowerShellScript ? 0 : 1;
        PathBox.Text = task.Path;
        ArgumentsBox.Text = task.Arguments;
        WorkingDirectoryBox.Text = task.WorkingDirectory;
        RunModeBox.SelectedIndex = task.RunMode == ManagedTaskRunMode.RunOnce ? 0 : 1;
        EnabledBox.IsChecked = task.IsEnabled;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = TypeBox.SelectedIndex == 0
                ? "PowerShell scripts (*.ps1)|*.ps1|All files (*.*)|*.*"
                : "Executable files (*.exe)|*.exe|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) == true)
        {
            PathBox.Text = dialog.FileName;
            if (string.IsNullOrWhiteSpace(WorkingDirectoryBox.Text))
            {
                WorkingDirectoryBox.Text = System.IO.Path.GetDirectoryName(dialog.FileName) ?? "";
            }
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            MessageBox.Show(this, "Name is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(PathBox.Text))
        {
            MessageBox.Show(this, "Path is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Result = new ManagedTask
        {
            Id = _original.Id,
            Name = NameBox.Text.Trim(),
            Type = TypeBox.SelectedIndex == 0 ? ManagedTaskType.PowerShellScript : ManagedTaskType.Executable,
            Path = PathBox.Text.Trim(),
            Arguments = ArgumentsBox.Text.Trim(),
            WorkingDirectory = WorkingDirectoryBox.Text.Trim(),
            RunMode = RunModeBox.SelectedIndex == 0 ? ManagedTaskRunMode.RunOnce : ManagedTaskRunMode.LongRunning,
            IsEnabled = EnabledBox.IsChecked == true,
            LastResult = _original.LastResult
        };

        DialogResult = true;
    }
}
```

- [ ] **Step 4: Replace main window code-behind**

Replace `src/AutoPowerRunner/MainWindow.xaml.cs` with:

```csharp
using System.Diagnostics;
using System.Windows;
using AutoPowerRunner.Models;
using AutoPowerRunner.ViewModels;

namespace AutoPowerRunner;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _allowClose;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    public async Task InitializeAsync()
    {
        await _viewModel.LoadAsync();
    }

    public void AllowClose()
    {
        _allowClose = true;
    }

    private async void AddTask_Click(object sender, RoutedEventArgs e)
    {
        var editor = new TaskEditorWindow { Owner = this };
        if (editor.ShowDialog() == true)
        {
            await _viewModel.AddOrUpdateTaskAsync(editor.Result);
        }
    }

    private async void EditTask_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedTask is not ManagedTask selected)
        {
            return;
        }

        var editor = new TaskEditorWindow(selected) { Owner = this };
        if (editor.ShowDialog() == true)
        {
            await _viewModel.AddOrUpdateTaskAsync(editor.Result);
        }
    }

    private void OpenLog_Click(object sender, RoutedEventArgs e)
    {
        var path = _viewModel.LogFile;
        if (!System.IO.File.Exists(path))
        {
            MessageBox.Show(this, "No log file exists yet.", "Log", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }
}
```

- [ ] **Step 5: Build UI**

Run:

```powershell
dotnet build AutoPowerRunner.sln
```

Expected: PASS.

- [ ] **Step 6: Commit window UI**

Run:

```powershell
git add src/AutoPowerRunner/MainWindow.xaml src/AutoPowerRunner/MainWindow.xaml.cs src/AutoPowerRunner/TaskEditorWindow.xaml src/AutoPowerRunner/TaskEditorWindow.xaml.cs
git commit -m "feat: add task management window"
```

## Task 8: Add App Composition, Tray Icon, And Startup Run

**Files:**
- Modify: `src/AutoPowerRunner/App.xaml`
- Modify: `src/AutoPowerRunner/App.xaml.cs`

- [ ] **Step 1: Replace app XAML**

Replace `src/AutoPowerRunner/App.xaml` with:

```xml
<Application x:Class="AutoPowerRunner.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             ShutdownMode="OnExplicitShutdown">
    <Application.Resources>
    </Application.Resources>
</Application>
```

- [ ] **Step 2: Replace app startup code**

Replace `src/AutoPowerRunner/App.xaml.cs` with:

```csharp
using System.Reflection;
using System.Threading;
using System.Windows;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;
using AutoPowerRunner.Services;
using AutoPowerRunner.ViewModels;

namespace AutoPowerRunner;

public partial class App : Application
{
    private Mutex? _singleInstanceMutex;
    private Forms.NotifyIcon? _notifyIcon;
    private MainWindow? _mainWindow;
    private MainViewModel? _viewModel;
    private ProcessRunner? _processRunner;
    private LogService? _logService;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(initiallyOwned: true, "AutoPowerRunner.SingleInstance", out var createdNew);
        if (!createdNew)
        {
            Shutdown();
            return;
        }

        var paths = AppPaths.ForCurrentUser();
        _logService = new LogService(paths);
        var configService = new TaskConfigService(paths);
        _processRunner = new ProcessRunner(_logService);
        var executablePath = Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location;
        var startupTaskService = new StartupTaskService(executablePath, _logService);
        _viewModel = new MainViewModel(configService, _processRunner, startupTaskService, _logService);

        _mainWindow = new MainWindow(_viewModel);
        await _mainWindow.InitializeAsync();
        CreateTrayIcon();

        _viewModel.RunAllEnabled();
        _mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _notifyIcon?.Dispose();
        _processRunner?.StopAll();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }

    private void CreateTrayIcon()
    {
        _notifyIcon = new Forms.NotifyIcon
        {
            Text = "Auto Power Runner",
            Icon = Drawing.SystemIcons.Application,
            Visible = true,
            ContextMenuStrip = BuildTrayMenu()
        };

        _notifyIcon.DoubleClick += (_, _) => ShowMainWindow();
    }

    private Forms.ContextMenuStrip BuildTrayMenu()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open window", null, (_, _) => ShowMainWindow());
        menu.Items.Add("Run all enabled tasks", null, (_, _) => _viewModel?.RunAllEnabled());
        menu.Items.Add("Stop all running tasks", null, (_, _) => _viewModel?.StopAll());
        menu.Items.Add("Toggle administrator autostart", null, (_, _) => _viewModel?.ToggleAutostartCommand.Execute(null));
        menu.Items.Add("Exit", null, (_, _) => ExitApplication());
        return menu;
    }

    private void ShowMainWindow()
    {
        if (_mainWindow is null)
        {
            return;
        }

        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    private void ExitApplication()
    {
        _mainWindow?.AllowClose();
        _mainWindow?.Close();
        Shutdown();
    }
}
```

- [ ] **Step 3: Build app composition**

Run:

```powershell
dotnet build AutoPowerRunner.sln
```

Expected: PASS.

- [ ] **Step 4: Run tests**

Run:

```powershell
dotnet test AutoPowerRunner.sln
```

Expected: PASS.

- [ ] **Step 5: Commit tray and startup behavior**

Run:

```powershell
git add src/AutoPowerRunner/App.xaml src/AutoPowerRunner/App.xaml.cs
git commit -m "feat: add tray startup lifecycle"
```

## Task 9: Manual Verification And Publish

**Files:**
- No required source edits unless verification reveals defects.

- [ ] **Step 1: Publish a local Windows build**

Run:

```powershell
dotnet publish src/AutoPowerRunner/AutoPowerRunner.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

Expected: publish succeeds and creates:

```text
src\AutoPowerRunner\bin\Release\net8.0-windows\win-x64\publish\AutoPowerRunner.exe
```

- [ ] **Step 2: Launch the app**

Run:

```powershell
& 'src\AutoPowerRunner\bin\Release\net8.0-windows\win-x64\publish\AutoPowerRunner.exe'
```

Expected: app window opens and tray icon appears.

- [ ] **Step 3: Verify run-once PowerShell task**

Create a temporary script:

```powershell
New-Item -ItemType Directory -Force -Path "$env:TEMP\AutoPowerRunnerManual" | Out-Null
Set-Content -LiteralPath "$env:TEMP\AutoPowerRunnerManual\once.ps1" -Value 'exit 7'
```

In the app:

```text
Add -> Type PowerShellScript -> Path %TEMP%\AutoPowerRunnerManual\once.ps1 -> Run mode RunOnce -> Enabled checked -> Save -> Run
```

Expected: status becomes `Exited` and recent result shows exit code `7`.

- [ ] **Step 4: Verify long-running PowerShell stop behavior**

Create a temporary script:

```powershell
Set-Content -LiteralPath "$env:TEMP\AutoPowerRunnerManual\long.ps1" -Value 'while ($true) { Start-Sleep -Seconds 1 }'
```

In the app:

```text
Add -> Type PowerShellScript -> Path %TEMP%\AutoPowerRunnerManual\long.ps1 -> Run mode LongRunning -> Save -> Run -> Stop
```

Expected: status changes from `Running` to stopped/exited after Stop. It does not restart automatically.

- [ ] **Step 5: Verify EXE launch**

In the app:

```text
Add -> Type Executable -> Path C:\Windows\System32\notepad.exe -> Run mode RunOnce -> Save -> Run
```

Expected: Notepad starts. Closing Notepad records an exited result.

- [ ] **Step 6: Verify window hide and tray restore**

In the app:

```text
Click X on the window -> double-click tray icon -> open tray menu -> Open window
```

Expected: X hides the window, tray actions restore it.

- [ ] **Step 7: Verify elevated autostart registration**

In the app:

```text
Click Enable administrator autostart -> approve one UAC prompt if shown
```

Then run:

```powershell
schtasks /Query /TN "AutoPowerRunner"
```

Expected: query succeeds. In Task Scheduler, the task is configured for current-user logon and highest privileges.

- [ ] **Step 8: Verify no login-time manual UAC**

Sign out and sign back in.

Expected:

```text
Auto Power Runner starts automatically.
No manual UAC approval is needed at login.
Configured enabled tasks start.
```

- [ ] **Step 9: Run final automated checks**

Run:

```powershell
dotnet test AutoPowerRunner.sln
dotnet build AutoPowerRunner.sln -c Release
```

Expected: both commands pass.

- [ ] **Step 10: Commit any verification fixes**

If manual verification required source fixes, commit them:

```powershell
git add src tests
git commit -m "fix: address manual verification issues"
```

If no source fixes were needed, do not create an empty commit.

## Self-Review

- Spec coverage: covered multiple PowerShell/EXE tasks, tray and window UI, enabled-task startup, run-once and long-running modes, no auto-restart, elevated Task Scheduler autostart, local config/log paths, damaged config handling, and manual UAC-free login verification.
- Placeholder scan: no placeholders are intentionally left in implementation steps.
- Type consistency: model names are `ManagedTask`, `ManagedTaskType`, `ManagedTaskRunMode`, `TaskRuntimeStatus`, and `TaskRuntimeResult` across tests and app code.
