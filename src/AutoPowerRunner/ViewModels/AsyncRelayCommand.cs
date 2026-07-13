using System.Windows.Input;
using AutoPowerRunner.Services;

namespace AutoPowerRunner.ViewModels;

public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<object?, Task> _execute;
    private readonly Predicate<object?>? _canExecute;
    private readonly ILogService? _logService;
    private readonly string _errorMessage;
    private readonly Action<string>? _errorSink;
    private bool _isExecuting;

    public AsyncRelayCommand(
        Func<object?, Task> execute,
        ILogService? logService = null,
        string errorMessage = "Command failed.",
        Predicate<object?>? canExecute = null,
        Action<string>? errorSink = null)
    {
        _execute = execute;
        _logService = logService;
        _errorMessage = errorMessage;
        _canExecute = canExecute;
        _errorSink = errorSink;
    }

    public event EventHandler? CanExecuteChanged;

    public Task? ExecutionTask { get; private set; }

    public bool CanExecute(object? parameter)
    {
        return !_isExecuting && (_canExecute?.Invoke(parameter) ?? true);
    }

    public void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        ExecutionTask = ExecuteAsync(parameter);
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task ExecuteAsync(object? parameter)
    {
        _isExecuting = true;
        RaiseCanExecuteChanged();

        try
        {
            await _execute(parameter);
        }
        catch (Exception ex)
        {
            _logService?.Error(_errorMessage, ex);
            _errorSink?.Invoke($"{_errorMessage} {ex.Message}");
        }
        finally
        {
            _isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }
}
