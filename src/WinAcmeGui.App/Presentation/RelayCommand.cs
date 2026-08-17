using System.Windows.Input;

namespace WinAcmeGui.App.Presentation;

/// <summary>Synchronous command with an optional guard.</summary>
public sealed class RelayCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;

    public void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;
        execute();
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

/// <summary>Synchronous command that receives the XAML <c>CommandParameter</c>.</summary>
public sealed class RelayCommand<T>(Action<T?> execute, Func<T?, bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => canExecute?.Invoke(Convert(parameter)) ?? true;

    public void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;
        execute(Convert(parameter));
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

    private static T? Convert(object? parameter) => parameter is T typed ? typed : default;
}

/// <summary>
/// Asynchronous command that reports its own in-flight state, so a double click cannot start a
/// second win-acme process while the first one is still running.
/// </summary>
public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;
    private bool _isExecuting;

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool IsExecuting => _isExecuting;

    public bool CanExecute(object? parameter) => !_isExecuting && (_canExecute?.Invoke() ?? true);

    public async void Execute(object? parameter)
    {
        try
        {
            await ExecuteAsync();
        }
        catch (Exception exception)
        {
            CommandFailed?.Invoke(exception);
        }
    }

    public event Action<Exception>? CommandFailed;

    public async Task ExecuteAsync()
    {
        if (!CanExecute(null)) return;
        _isExecuting = true;
        RaiseCanExecuteChanged();
        try
        {
            await _execute();
        }
        finally
        {
            _isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

/// <summary>Asynchronous command that receives the XAML <c>CommandParameter</c>.</summary>
public sealed class AsyncRelayCommand<T> : ICommand
{
    private readonly Func<T?, Task> _execute;
    private readonly Func<T?, bool>? _canExecute;
    private bool _isExecuting;

    public AsyncRelayCommand(Func<T?, Task> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool IsExecuting => _isExecuting;

    public bool CanExecute(object? parameter) => !_isExecuting && (_canExecute?.Invoke(Convert(parameter)) ?? true);

    public async void Execute(object? parameter)
    {
        try
        {
            await ExecuteAsync(Convert(parameter));
        }
        catch (Exception exception)
        {
            CommandFailed?.Invoke(exception);
        }
    }

    public event Action<Exception>? CommandFailed;

    public async Task ExecuteAsync(T? parameter)
    {
        if (_isExecuting || (_canExecute?.Invoke(parameter) ?? true) == false) return;
        _isExecuting = true;
        RaiseCanExecuteChanged();
        try
        {
            await _execute(parameter);
        }
        finally
        {
            _isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

    private static T? Convert(object? parameter) => parameter is T typed ? typed : default;
}
