// ==========================================================
// Project: WpfHexEditor.Shell.Panels
// File: Panels/ViewModels/RelayCommand.cs
// Description: Minimal ICommand implementation for panel ViewModels.
// Architecture: WPF-dependent (CommandManager). Internal to this assembly.
// ==========================================================

using System;
using System.Windows.Input;

namespace WpfHexEditor.Shell.Panels.ViewModels;

internal sealed class RelayCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add    => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;
    public void Execute(object? parameter)    => execute();
}

internal sealed class RelayCommand<T>(Action<T?> execute, Func<T?, bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add    => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => canExecute?.Invoke((T?)parameter) ?? true;
    public void Execute(object? parameter)    => execute((T?)parameter);
}
