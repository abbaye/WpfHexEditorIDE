// ==========================================================
// Project: WpfHexEditor.ProjectSystem
// File: Documents/NuGet/AsyncRelayCommand.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Minimal ICommand implementation wrapping an async Task delegate.
//     Prevents re-entrancy while the command is executing (busy-guard).
//
// Architecture Notes:
//     Scoped to the NuGet Manager feature; not intended as a global utility.
// ==========================================================

using System.Windows.Input;

namespace WpfHexEditor.Core.ProjectSystem.Documents.NuGet;

/// <summary>
/// Async relay command with built-in busy-guard to prevent concurrent executions.
/// </summary>
internal sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<Task>  _execute;
    private readonly Func<bool>? _canExecute;
    private bool _isExecuting;

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute    = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
        => !_isExecuting && (_canExecute?.Invoke() ?? true);

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;

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

    public void RaiseCanExecuteChanged()
        => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
