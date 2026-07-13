using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AutoPowerRunner.Models;

public sealed class ManagedTask : INotifyPropertyChanged
{
    private string _name = "";
    private ManagedTaskType _type = ManagedTaskType.PowerShellScript;
    private string _path = "";
    private string _arguments = "";
    private string _workingDirectory = "";
    private ManagedTaskRunMode _runMode = ManagedTaskRunMode.RunOnce;
    private bool _isEnabled = true;
    private TaskRuntimeResult _lastResult = new();

    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public ManagedTaskType Type
    {
        get => _type;
        set => SetProperty(ref _type, value);
    }

    public string Path
    {
        get => _path;
        set => SetProperty(ref _path, value);
    }

    public string Arguments
    {
        get => _arguments;
        set => SetProperty(ref _arguments, value);
    }

    public string WorkingDirectory
    {
        get => _workingDirectory;
        set => SetProperty(ref _workingDirectory, value);
    }

    public ManagedTaskRunMode RunMode
    {
        get => _runMode;
        set => SetProperty(ref _runMode, value);
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public TaskRuntimeResult LastResult
    {
        get => _lastResult;
        set => SetProperty(ref _lastResult, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

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
                Error = LastResult.Error,
                RestartCount = LastResult.RestartCount
            }
        };
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
