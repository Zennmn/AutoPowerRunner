using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AutoPowerRunner.Models;
using AutoPowerRunner.Services;

namespace AutoPowerRunner.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly ITaskConfigService _configService;
    private readonly IProcessRunner _processRunner;
    private readonly IStartupTaskService _startupTaskService;
    private readonly ILogService _logService;
    private readonly SynchronizationContext? _updateContext;
    private readonly bool _isRunningAsAdministrator;
    private ManagedTask? _selectedTask;
    private bool _isAdministratorAutostartEnabled;

    public MainViewModel(
        ITaskConfigService configService,
        IProcessRunner processRunner,
        IStartupTaskService startupTaskService,
        ILogService logService,
        SynchronizationContext? updateContext = null,
        bool isRunningAsAdministrator = true)
    {
        _configService = configService;
        _processRunner = processRunner;
        _startupTaskService = startupTaskService;
        _logService = logService;
        _updateContext = updateContext;
        _isRunningAsAdministrator = isRunningAsAdministrator;

        SaveCommand = new AsyncRelayCommand(
            _ => SaveAsync(),
            _logService,
            "无法保存任务。");
        RunSelectedCommand = new RelayCommand(_ => RunSelected(), _ => SelectedTask is not null);
        StopSelectedCommand = new RelayCommand(_ => StopSelected(), _ => SelectedTask is not null);
        DeleteSelectedCommand = new AsyncRelayCommand(
            _ => DeleteSelectedAsync(),
            _logService,
            "无法删除选中的任务。",
            _ => SelectedTask is not null);
        ToggleSelectedEnabledCommand = new AsyncRelayCommand(
            _ => ToggleSelectedEnabledAsync(),
            _logService,
            "无法更新选中的任务。",
            _ => SelectedTask is not null);
        RunAllEnabledCommand = new RelayCommand(_ => RunAllEnabled());
        StopAllCommand = new RelayCommand(_ => StopAll());
        ToggleAutostartCommand = new AsyncRelayCommand(
            _ => ToggleAutostartAsync(),
            _logService,
            "无法切换管理员自启。");
    }

    public ObservableCollection<ManagedTask> Tasks { get; } = [];

    public IReadOnlyList<ManagedTaskType> TaskTypes { get; } = Enum.GetValues<ManagedTaskType>();

    public IReadOnlyList<ManagedTaskRunMode> RunModes { get; } = Enum.GetValues<ManagedTaskRunMode>();

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
        ? "管理员开机自启已配置"
        : "管理员开机自启未配置";

    public string CurrentPermissionText => _isRunningAsAdministrator ? "管理员" : "普通用户";

    public string ToggleAutostartText => IsAdministratorAutostartEnabled
        ? "关闭管理员自启"
        : "开启管理员自启";

    public string LogFile => _logService.LogFile;

    public ICommand SaveCommand { get; }
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

        SelectedTask = Tasks.FirstOrDefault();
        IsAdministratorAutostartEnabled = _startupTaskService.IsEnabled();
    }

    public async Task SaveAsync()
    {
        await _configService.SaveAsync(Tasks);
    }

    public async Task<ManagedTask> ImportTaskAsync(ManagedTaskType type, string path)
    {
        var task = new ManagedTask
        {
            Name = Path.GetFileNameWithoutExtension(path),
            Type = type,
            Path = path,
            WorkingDirectory = Path.GetDirectoryName(path) ?? "",
            RunMode = ManagedTaskRunMode.RunOnce,
            IsEnabled = true
        };

        await AddOrUpdateTaskAsync(task);
        SelectedTask = task;
        return task;
    }

    public async Task AddOrUpdateTaskAsync(ManagedTask task)
    {
        var existing = Tasks.FirstOrDefault(item => item.Id == task.Id);
        if (existing is null)
        {
            Tasks.Add(task);
            try
            {
                await SaveAsync();
            }
            catch (Exception ex)
            {
                Tasks.Remove(task);
                _logService.Error($"Could not save task '{task.Name}'.", ex);
                throw;
            }

            return;
        }

        var previousSelectedTask = SelectedTask;
        var previousState = existing.Clone();

        UpdateEditableTaskFields(existing, task);
        try
        {
            await SaveAsync();
        }
        catch (Exception ex)
        {
            UpdateEditableTaskFields(existing, previousState);
            SelectedTask = previousSelectedTask;
            _logService.Error($"Could not save task '{task.Name}'.", ex);
            throw;
        }
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

    private async Task DeleteSelectedAsync()
    {
        if (SelectedTask is null)
        {
            return;
        }

        var task = SelectedTask;
        var index = Tasks.IndexOf(task);
        if (index < 0)
        {
            return;
        }

        Tasks.RemoveAt(index);
        SelectedTask = null;
        try
        {
            await SaveAsync();
        }
        catch (Exception ex)
        {
            Tasks.Insert(Math.Min(index, Tasks.Count), task);
            SelectedTask = task;
            _logService.Error($"Could not delete task '{task.Name}'.", ex);
            return;
        }

        _processRunner.Stop(task.Id);
    }

    private async Task ToggleSelectedEnabledAsync()
    {
        if (SelectedTask is null)
        {
            return;
        }

        var task = SelectedTask;
        var previousIsEnabled = task.IsEnabled;
        task.IsEnabled = !task.IsEnabled;
        try
        {
            await SaveAsync();
        }
        catch (Exception ex)
        {
            task.IsEnabled = previousIsEnabled;
            _logService.Error($"Could not update task '{task.Name}'.", ex);
        }
        finally
        {
            OnPropertyChanged(nameof(Tasks));
        }
    }

    private async Task ToggleAutostartAsync()
    {
        await Task.Run(() =>
        {
            if (IsAdministratorAutostartEnabled)
            {
                _startupTaskService.Disable();
            }
            else
            {
                _startupTaskService.Enable();
            }
        });

        IsAdministratorAutostartEnabled = await Task.Run(_startupTaskService.IsEnabled);
    }

    private void RunTask(ManagedTask task)
    {
        try
        {
            _processRunner.Start(task, _ => NotifyTasksChangedFromProcessCallback());
        }
        catch (Exception ex)
        {
            _logService.Error($"Could not run task '{task.Name}'.", ex);
        }
    }

    private static void UpdateEditableTaskFields(ManagedTask target, ManagedTask source)
    {
        target.Name = source.Name;
        target.Type = source.Type;
        target.Path = source.Path;
        target.Arguments = source.Arguments;
        target.WorkingDirectory = source.WorkingDirectory;
        target.RunMode = source.RunMode;
        target.IsEnabled = source.IsEnabled;
    }

    private void NotifyTasksChangedFromProcessCallback()
    {
        _ = SaveTaskResultFromProcessCallbackAsync();

        void Notify()
        {
            try
            {
                OnPropertyChanged(nameof(Tasks));
            }
            catch (Exception ex)
            {
                _logService.Error("Could not notify task update.", ex);
            }
        }

        if (_updateContext is null)
        {
            Notify();
            return;
        }

        try
        {
            _updateContext.Post(_ => Notify(), null);
        }
        catch (Exception ex)
        {
            _logService.Error("Could not dispatch task update notification.", ex);
        }
    }

    private async Task SaveTaskResultFromProcessCallbackAsync()
    {
        try
        {
            await SaveAsync();
        }
        catch (Exception ex)
        {
            _logService.Error("Could not save task result.", ex);
        }
    }

    private void RaiseCommandStates()
    {
        foreach (var command in new[] { RunSelectedCommand, StopSelectedCommand, DeleteSelectedCommand, ToggleSelectedEnabledCommand })
        {
            switch (command)
            {
                case RelayCommand relayCommand:
                    relayCommand.RaiseCanExecuteChanged();
                    break;
                case AsyncRelayCommand asyncRelayCommand:
                    asyncRelayCommand.RaiseCanExecuteChanged();
                    break;
            }
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
