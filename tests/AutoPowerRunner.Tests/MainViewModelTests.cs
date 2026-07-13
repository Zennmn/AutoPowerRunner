using System.Diagnostics;
using System.Windows.Input;
using AutoPowerRunner.Models;
using AutoPowerRunner.Services;
using AutoPowerRunner.ViewModels;

namespace AutoPowerRunner.Tests;

public sealed class MainViewModelTests
{
    [Fact]
    public async Task MainViewModel_AddOrUpdateTaskAsync_AddsAndSaves()
    {
        var config = new FakeTaskConfigService();
        var viewModel = CreateViewModel(config);
        var task = new ManagedTask { Name = "Task" };

        await viewModel.AddOrUpdateTaskAsync(task);

        Assert.Same(task, Assert.Single(viewModel.Tasks));
        Assert.Equal(1, config.SaveCount);
        Assert.Same(task, Assert.Single(config.LastSavedTasks));
    }

    [Fact]
    public async Task MainViewModel_ImportTaskAsync_CreatesSelectsAndSavesScriptTaskFromPath()
    {
        var config = new FakeTaskConfigService();
        var viewModel = CreateViewModel(config);
        var path = "C:\\Scripts\\check.ps1";

        var task = await viewModel.ImportTaskAsync(ManagedTaskType.PowerShellScript, path);

        Assert.Same(task, Assert.Single(viewModel.Tasks));
        Assert.Same(task, viewModel.SelectedTask);
        Assert.Equal("check", task.Name);
        Assert.Equal(ManagedTaskType.PowerShellScript, task.Type);
        Assert.Equal(path, task.Path);
        Assert.Equal("C:\\Scripts", task.WorkingDirectory);
        Assert.Equal(ManagedTaskRunMode.RunOnce, task.RunMode);
        Assert.True(task.IsEnabled);
        Assert.Equal(1, config.SaveCount);
        Assert.Same(task, Assert.Single(config.LastSavedTasks));
    }

    [Fact]
    public async Task MainViewModel_ImportTaskAsync_CreatesSelectsAndSavesExecutableTaskFromPath()
    {
        var config = new FakeTaskConfigService();
        var viewModel = CreateViewModel(config);
        var path = "C:\\Program Files\\Sample App\\Sample App.exe";

        var task = await viewModel.ImportTaskAsync(ManagedTaskType.Executable, path);

        Assert.Same(task, Assert.Single(viewModel.Tasks));
        Assert.Same(task, viewModel.SelectedTask);
        Assert.Equal("Sample App", task.Name);
        Assert.Equal(ManagedTaskType.Executable, task.Type);
        Assert.Equal(path, task.Path);
        Assert.Equal("C:\\Program Files\\Sample App", task.WorkingDirectory);
        Assert.Equal(ManagedTaskRunMode.RunOnce, task.RunMode);
        Assert.True(task.IsEnabled);
        Assert.Equal(1, config.SaveCount);
        Assert.Same(task, Assert.Single(config.LastSavedTasks));
    }

    [Fact]
    public async Task MainViewModel_SaveCommand_SavesInlineEditedTasks()
    {
        var config = new FakeTaskConfigService();
        var viewModel = CreateViewModel(config);
        var task = new ManagedTask { Name = "旧名称", Type = ManagedTaskType.PowerShellScript };
        viewModel.Tasks.Add(task);
        viewModel.SelectedTask = task;
        task.Name = "Sample App";
        task.Type = ManagedTaskType.Executable;

        viewModel.SaveCommand.Execute(null);
        await AwaitCommandAsync(viewModel.SaveCommand);

        var saved = Assert.Single(config.LastSavedTasks);
        Assert.Same(task, saved);
        Assert.Equal("Sample App", saved.Name);
        Assert.Equal(ManagedTaskType.Executable, saved.Type);
    }

    [Fact]
    public async Task MainViewModel_LoadAsync_SelectsFirstLoadedTask()
    {
        var first = new ManagedTask { Name = "Sample App" };
        var second = new ManagedTask { Name = "Health Check" };
        var config = new FakeTaskConfigService();
        config.TasksToLoad.Add(first);
        config.TasksToLoad.Add(second);
        var viewModel = CreateViewModel(config);

        await viewModel.LoadAsync();

        Assert.Same(first, viewModel.SelectedTask);
    }

    [Fact]
    public async Task MainViewModel_LoadAsync_UsesChineseAdministratorAutostartText()
    {
        var startup = new FakeStartupTaskService { Enabled = true };
        var viewModel = CreateViewModel(startupTaskService: startup, isRunningAsAdministrator: true);

        await viewModel.LoadAsync();

        Assert.Equal("管理员", viewModel.CurrentPermissionText);
        Assert.Equal("管理员开机自启已配置", viewModel.AutostartStatusText);
        Assert.Equal("关闭管理员自启", viewModel.ToggleAutostartText);
    }

    [Fact]
    public void MainViewModel_CurrentPermissionText_WhenNotAdministrator_ReturnsStandardUser()
    {
        var viewModel = CreateViewModel(isRunningAsAdministrator: false);

        Assert.Equal("普通用户", viewModel.CurrentPermissionText);
    }

    [Fact]
    public async Task MainViewModel_AddOrUpdateTaskAsync_WhenNewTaskSaveFails_RollsBackAndLogs()
    {
        var config = new FakeTaskConfigService { SaveException = new IOException("save failed") };
        var log = new FakeLogService();
        var viewModel = CreateViewModel(config, logService: log);
        var task = new ManagedTask { Name = "New" };

        await Assert.ThrowsAsync<IOException>(() => viewModel.AddOrUpdateTaskAsync(task));

        Assert.Empty(viewModel.Tasks);
        Assert.Contains(log.Errors, entry =>
            entry.Message.Contains("Could not save task 'New'.") &&
            entry.Exception is IOException);
    }

    [Fact]
    public async Task MainViewModel_AddOrUpdateTaskAsync_WhenExistingTaskSaveFails_RollsBackAndLogs()
    {
        var config = new FakeTaskConfigService { SaveException = new IOException("save failed") };
        var log = new FakeLogService();
        var existing = new ManagedTask
        {
            Name = "Existing",
            Type = ManagedTaskType.PowerShellScript,
            Path = "old.ps1",
            Arguments = "-Old",
            WorkingDirectory = "C:\\Old",
            RunMode = ManagedTaskRunMode.RunOnce,
            IsEnabled = true
        };
        var currentResult = existing.LastResult;
        var updated = new ManagedTask
        {
            Id = existing.Id,
            Name = "Updated",
            Type = ManagedTaskType.Executable,
            Path = "new.exe",
            Arguments = "-New",
            WorkingDirectory = "C:\\New",
            RunMode = ManagedTaskRunMode.LongRunning,
            IsEnabled = false
        };
        var viewModel = CreateViewModel(config, logService: log);
        viewModel.Tasks.Add(existing);
        viewModel.SelectedTask = existing;

        await Assert.ThrowsAsync<IOException>(() => viewModel.AddOrUpdateTaskAsync(updated));

        Assert.Same(existing, Assert.Single(viewModel.Tasks));
        Assert.Same(existing, viewModel.SelectedTask);
        Assert.Equal("Existing", existing.Name);
        Assert.Equal(ManagedTaskType.PowerShellScript, existing.Type);
        Assert.Equal("old.ps1", existing.Path);
        Assert.Equal("-Old", existing.Arguments);
        Assert.Equal("C:\\Old", existing.WorkingDirectory);
        Assert.Equal(ManagedTaskRunMode.RunOnce, existing.RunMode);
        Assert.True(existing.IsEnabled);
        Assert.Same(currentResult, existing.LastResult);
        Assert.Contains(log.Errors, entry =>
            entry.Message.Contains("Could not save task 'Updated'.") &&
            entry.Exception is IOException);
    }

    [Fact]
    public async Task MainViewModel_AddOrUpdateTaskAsync_UpdateExistingTaskKeepsReferenceAndUpdatesEditableFields()
    {
        var config = new FakeTaskConfigService();
        var existing = new ManagedTask
        {
            Name = "Existing",
            Type = ManagedTaskType.PowerShellScript,
            Path = "old.ps1",
            Arguments = "-Old",
            WorkingDirectory = "C:\\Old",
            RunMode = ManagedTaskRunMode.RunOnce,
            IsEnabled = true
        };
        var currentResult = existing.LastResult;
        currentResult.Status = TaskRuntimeStatus.Running;
        var updated = new ManagedTask
        {
            Id = existing.Id,
            Name = "Updated",
            Type = ManagedTaskType.Executable,
            Path = "new.exe",
            Arguments = "-New",
            WorkingDirectory = "C:\\New",
            RunMode = ManagedTaskRunMode.LongRunning,
            IsEnabled = false,
            LastResult = new TaskRuntimeResult { Status = TaskRuntimeStatus.Exited, ExitCode = 1 }
        };
        var viewModel = CreateViewModel(config);
        viewModel.Tasks.Add(existing);
        viewModel.SelectedTask = existing;

        await viewModel.AddOrUpdateTaskAsync(updated);

        Assert.Same(existing, Assert.Single(viewModel.Tasks));
        Assert.Same(existing, viewModel.SelectedTask);
        Assert.Equal("Updated", existing.Name);
        Assert.Equal(ManagedTaskType.Executable, existing.Type);
        Assert.Equal("new.exe", existing.Path);
        Assert.Equal("-New", existing.Arguments);
        Assert.Equal("C:\\New", existing.WorkingDirectory);
        Assert.Equal(ManagedTaskRunMode.LongRunning, existing.RunMode);
        Assert.False(existing.IsEnabled);
        Assert.Same(currentResult, existing.LastResult);
        Assert.Equal(TaskRuntimeStatus.Running, existing.LastResult.Status);
        Assert.Same(existing, Assert.Single(config.LastSavedTasks));
    }

    [Fact]
    public void MainViewModel_RunAllEnabled_RunsOnlyEnabledTasks()
    {
        var processRunner = new FakeProcessRunner();
        var viewModel = CreateViewModel(processRunner: processRunner);
        var enabled = new ManagedTask { Name = "Enabled", IsEnabled = true };
        var disabled = new ManagedTask { Name = "Disabled", IsEnabled = false };
        viewModel.Tasks.Add(enabled);
        viewModel.Tasks.Add(disabled);

        viewModel.RunAllEnabled();

        Assert.Equal([enabled], processRunner.StartedTasks);
    }

    [Fact]
    public async Task MainViewModel_DeleteSelected_WhenSaveFails_RollsBackAndLogs()
    {
        var config = new FakeTaskConfigService { SaveException = new IOException("save failed") };
        var processRunner = new FakeProcessRunner();
        var log = new FakeLogService();
        var viewModel = CreateViewModel(config, processRunner, logService: log);
        var first = new ManagedTask { Name = "First" };
        var selected = new ManagedTask { Name = "Selected" };
        var last = new ManagedTask { Name = "Last" };
        viewModel.Tasks.Add(first);
        viewModel.Tasks.Add(selected);
        viewModel.Tasks.Add(last);
        viewModel.SelectedTask = selected;

        viewModel.DeleteSelectedCommand.Execute(null);
        await AwaitCommandAsync(viewModel.DeleteSelectedCommand);

        Assert.Equal([first, selected, last], viewModel.Tasks);
        Assert.Same(selected, viewModel.SelectedTask);
        Assert.DoesNotContain(selected.Id, processRunner.StoppedTaskIds);
        Assert.Contains(log.Errors, entry => entry.Message.Contains("Could not delete task 'Selected'."));
    }

    [Fact]
    public async Task MainViewModel_DeleteSelected_WhenSaveSucceeds_StopsAfterSave()
    {
        var events = new List<string>();
        var config = new FakeTaskConfigService { Events = events };
        var processRunner = new FakeProcessRunner { Events = events };
        var viewModel = CreateViewModel(config, processRunner);
        var task = new ManagedTask { Name = "Selected" };
        viewModel.Tasks.Add(task);
        viewModel.SelectedTask = task;

        viewModel.DeleteSelectedCommand.Execute(null);
        await AwaitCommandAsync(viewModel.DeleteSelectedCommand);

        Assert.Empty(viewModel.Tasks);
        Assert.Null(viewModel.SelectedTask);
        Assert.Equal(["save", "stop"], events);
        Assert.Contains(task.Id, processRunner.StoppedTaskIds);
    }

    [Fact]
    public async Task MainViewModel_ToggleSelectedEnabled_WhenSaveFails_RollsBackAndLogs()
    {
        var config = new FakeTaskConfigService { SaveException = new IOException("save failed") };
        var log = new FakeLogService();
        var task = new ManagedTask { Name = "Toggle", IsEnabled = true };
        var viewModel = CreateViewModel(config, logService: log);
        viewModel.Tasks.Add(task);
        viewModel.SelectedTask = task;

        viewModel.ToggleSelectedEnabledCommand.Execute(null);
        await AwaitCommandAsync(viewModel.ToggleSelectedEnabledCommand);

        Assert.True(task.IsEnabled);
        Assert.Contains(log.Errors, entry => entry.Message.Contains("Could not update task 'Toggle'."));
    }

    [Fact]
    public void MainViewModel_SelectedTask_RaisesCanExecuteChanged()
    {
        var viewModel = CreateViewModel();
        var raisedCount = 0;
        viewModel.RunSelectedCommand.CanExecuteChanged += (_, _) => raisedCount++;

        viewModel.SelectedTask = new ManagedTask();

        Assert.True(raisedCount > 0);
        Assert.True(viewModel.RunSelectedCommand.CanExecute(null));
    }

    [Fact]
    public void MainViewModel_ProcessCallback_UsesSynchronizationContextWhenProvided()
    {
        var processRunner = new FakeProcessRunner();
        var updateContext = new CapturingSynchronizationContext();
        var viewModel = CreateViewModel(processRunner: processRunner, updateContext: updateContext);
        var task = new ManagedTask { IsEnabled = true };
        var changedProperties = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);
        viewModel.Tasks.Add(task);

        viewModel.RunAllEnabled();
        processRunner.LastUpdateCallback?.Invoke(task);

        Assert.Empty(changedProperties);
        var postedCallback = Assert.Single(updateContext.PostedCallbacks);

        postedCallback();

        Assert.Contains(nameof(MainViewModel.Tasks), changedProperties);
    }

    [Fact]
    public async Task MainViewModel_ProcessCallback_PersistsLastResult()
    {
        var config = new FakeTaskConfigService();
        var processRunner = new FakeProcessRunner();
        var viewModel = CreateViewModel(config, processRunner);
        var task = new ManagedTask { IsEnabled = true };
        viewModel.Tasks.Add(task);

        viewModel.RunAllEnabled();
        task.LastResult.Status = TaskRuntimeStatus.Exited;
        task.LastResult.ExitCode = 0;
        processRunner.LastUpdateCallback?.Invoke(task);

        var saved = await WaitUntilAsync(() => config.SaveCount == 1);

        Assert.True(saved);
        Assert.Same(task, Assert.Single(config.LastSavedTasks));
    }

    [Fact]
    public async Task MainViewModel_ProcessCallback_WhenSaveFails_LogsAndDoesNotThrow()
    {
        var config = new FakeTaskConfigService { SaveException = new IOException("save failed") };
        var log = new FakeLogService();
        var processRunner = new FakeProcessRunner();
        var viewModel = CreateViewModel(config, processRunner, logService: log);
        var task = new ManagedTask { IsEnabled = true };
        viewModel.Tasks.Add(task);

        viewModel.RunAllEnabled();
        var exception = Record.Exception(() => processRunner.LastUpdateCallback?.Invoke(task));
        var logged = await WaitUntilAsync(() => log.Errors.Any(entry =>
            entry.Message.Contains("Could not save task result.") &&
            entry.Exception is IOException));

        Assert.Null(exception);
        Assert.True(logged);
    }

    [Fact]
    public async Task AsyncRelayCommand_WhenExecuteFails_LogsAndDoesNotThrow()
    {
        var log = new FakeLogService();
        var command = new AsyncRelayCommand(
            _ => throw new InvalidOperationException("boom"),
            log,
            "Async command failed.");

        var exception = Record.Exception(() => command.Execute(null));
        await command.ExecutionTask!;

        Assert.Null(exception);
        Assert.Contains(log.Errors, entry =>
            entry.Message == "Async command failed." &&
            entry.Exception is InvalidOperationException);
    }

    private static MainViewModel CreateViewModel(
        ITaskConfigService? configService = null,
        IProcessRunner? processRunner = null,
        IStartupTaskService? startupTaskService = null,
        ILogService? logService = null,
        SynchronizationContext? updateContext = null,
        bool isRunningAsAdministrator = true)
    {
        return new MainViewModel(
            configService ?? new FakeTaskConfigService(),
            processRunner ?? new FakeProcessRunner(),
            startupTaskService ?? new FakeStartupTaskService(),
            logService ?? new FakeLogService(),
            updateContext,
            isRunningAsAdministrator);
    }

    private static async Task AwaitCommandAsync(ICommand command)
    {
        var asyncCommand = Assert.IsType<AsyncRelayCommand>(command);
        await asyncCommand.ExecutionTask!;
    }

    private static async Task<bool> WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (!timeout.IsCancellationRequested)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(25, timeout.Token).ContinueWith(_ => { });
        }

        return condition();
    }

    private sealed class FakeTaskConfigService : ITaskConfigService
    {
        public List<ManagedTask> TasksToLoad { get; } = [];
        public IReadOnlyCollection<ManagedTask> LastSavedTasks { get; private set; } = [];
        public int SaveCount { get; private set; }
        public Exception? SaveException { get; set; }
        public List<string>? Events { get; set; }

        public Task<List<ManagedTask>> LoadAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(TasksToLoad.ToList());
        }

        public Task SaveAsync(IReadOnlyCollection<ManagedTask> tasks, CancellationToken cancellationToken = default)
        {
            SaveCount++;
            Events?.Add("save");
            if (SaveException is not null)
            {
                throw SaveException;
            }

            LastSavedTasks = tasks.ToList();
            return Task.CompletedTask;
        }
    }

    private sealed class FakeProcessRunner : IProcessRunner
    {
        public IReadOnlyCollection<Guid> RunningTaskIds => [];
        public List<ManagedTask> StartedTasks { get; } = [];
        public List<Guid> StoppedTaskIds { get; } = [];
        public List<string>? Events { get; set; }
        public Action<ManagedTask>? LastUpdateCallback { get; private set; }

        public Process Start(ManagedTask task, Action<ManagedTask>? onUpdated = null)
        {
            StartedTasks.Add(task);
            LastUpdateCallback = onUpdated;
            return new Process();
        }

        public void Stop(Guid taskId)
        {
            Events?.Add("stop");
            StoppedTaskIds.Add(taskId);
        }

        public void StopAll()
        {
        }
    }

    private sealed class FakeStartupTaskService : IStartupTaskService
    {
        public bool Enabled { get; set; }

        public bool IsEnabled()
        {
            return Enabled;
        }

        public void Enable()
        {
            Enabled = true;
        }

        public void Disable()
        {
            Enabled = false;
        }
    }

    private sealed class FakeLogService : ILogService
    {
        public string LogFile => "test.log";
        public List<string> InfoMessages { get; } = [];
        public List<(string Message, Exception? Exception)> Errors { get; } = [];

        public void Info(string message)
        {
            InfoMessages.Add(message);
        }

        public void Error(string message, Exception? exception = null)
        {
            Errors.Add((message, exception));
        }
    }

    private sealed class CapturingSynchronizationContext : SynchronizationContext
    {
        public List<Action> PostedCallbacks { get; } = [];

        public override void Post(SendOrPostCallback d, object? state)
        {
            PostedCallbacks.Add(() => d(state));
        }
    }
}
