//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Windows.Input;

namespace WpfHexEditor.SDK.Commands;

/// <summary>
/// Minimal ICommand implementation for use in ViewModels and plugin descriptors.
/// Supports both parameterized (Action&lt;object?&gt;) and parameter-less (Action) delegates.
/// </summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    /// <summary>Parameterized constructor — execute receives the WPF command parameter.</summary>
    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute    = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <summary>Parameter-less constructor — execute ignores the WPF command parameter.</summary>
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
        : this(
            _ => (execute ?? throw new ArgumentNullException(nameof(execute)))(),
            canExecute is null ? null : _ => canExecute())
    {
    }

    public event EventHandler? CanExecuteChanged
    {
        add    => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => _canExecute is null || _canExecute(parameter);

    public void Execute(object? parameter) => _execute(parameter);

    /// <summary>Forces WPF to re-evaluate CanExecute on all command bindings.</summary>
    public void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
}

/// <summary>
/// Typed ICommand implementation — execute and canExecute receive a strongly-typed parameter T.
/// </summary>
public sealed class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;
    private readonly Func<T?, bool>? _canExecute;

    public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
    {
        _execute    = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add    => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter)
    {
        var typed = parameter is T t ? t : default;
        return _canExecute is null || _canExecute(typed);
    }

    public void Execute(object? parameter)
    {
        var typed = parameter is T t ? t : default;
        _execute(typed);
    }

    /// <summary>Forces WPF to re-evaluate CanExecute on all command bindings.</summary>
    public void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
}
