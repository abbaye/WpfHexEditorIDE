// ==========================================================
// Project: WpfHexEditor.HexEditor
// File: RelayCommand.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Generic ICommand implementation for the MVVM pattern.
//     Wraps an Action delegate and an optional CanExecute predicate,
//     and uses CommandManager.RequerySuggested for automatic CanExecute refresh.
//
// Architecture Notes:
//     Follows the Command pattern (GoF). Used throughout the HexEditor MVVM
//     layer to bind UI actions to ViewModel logic without coupling views to logic.
//
// ==========================================================

using System;
using System.Windows.Input;

namespace WpfHexEditor.HexEditor.Commands
{
    /// <summary>
    /// Generic command implementation for MVVM pattern.
    /// Uses CommandManager.RequerySuggested for automatic CanExecute refresh.
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute();
        }

        public void Execute(object parameter)
        {
            _execute();
        }

        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>
    /// Generic command with parameter support.
    /// </summary>
    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        private readonly Func<T, bool> _canExecute;

        public RelayCommand(Action<T> execute, Func<T, bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object parameter)
        {
            if (parameter is T typedParam)
                return _canExecute == null || _canExecute(typedParam);
            return parameter == null && default(T) == null;
        }

        public void Execute(object parameter)
        {
            if (parameter is T typedParam)
                _execute(typedParam);
        }

        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
