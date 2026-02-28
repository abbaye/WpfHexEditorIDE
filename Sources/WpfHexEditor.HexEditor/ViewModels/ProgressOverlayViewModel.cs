//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.ComponentModel;
using System.Windows.Input;

namespace WpfHexEditor.HexEditor.ViewModels
{
    /// <summary>
    /// ViewModel for the progress overlay control
    /// </summary>
    public class ProgressOverlayViewModel : INotifyPropertyChanged
    {
        private string _operationTitle;
        private string _statusMessage;
        private int _progressPercentage;
        private string _progressText;
        private bool _isIndeterminate;
        private bool _canCancel;

        public event PropertyChangedEventHandler PropertyChanged;

        public ProgressOverlayViewModel()
        {
            CancelCommand = new RelayCommand(OnCancel, () => CanCancel);
        }

        /// <summary>
        /// Title of the operation being performed
        /// </summary>
        public string OperationTitle
        {
            get => _operationTitle;
            set
            {
                if (_operationTitle != value)
                {
                    _operationTitle = value;
                    OnPropertyChanged(nameof(OperationTitle));
                }
            }
        }

        /// <summary>
        /// Current status message
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                if (_statusMessage != value)
                {
                    _statusMessage = value;
                    OnPropertyChanged(nameof(StatusMessage));
                }
            }
        }

        /// <summary>
        /// Progress percentage (0-100)
        /// </summary>
        public int ProgressPercentage
        {
            get => _progressPercentage;
            set
            {
                if (_progressPercentage != value)
                {
                    _progressPercentage = value;
                    OnPropertyChanged(nameof(ProgressPercentage));
                    ProgressText = $"{value}%";
                }
            }
        }

        /// <summary>
        /// Progress text displayed (e.g., "45%")
        /// </summary>
        public string ProgressText
        {
            get => _progressText;
            set
            {
                if (_progressText != value)
                {
                    _progressText = value;
                    OnPropertyChanged(nameof(ProgressText));
                }
            }
        }

        /// <summary>
        /// Whether the progress bar is indeterminate (no specific percentage)
        /// </summary>
        public bool IsIndeterminate
        {
            get => _isIndeterminate;
            set
            {
                if (_isIndeterminate != value)
                {
                    _isIndeterminate = value;
                    OnPropertyChanged(nameof(IsIndeterminate));
                }
            }
        }

        /// <summary>
        /// Whether the operation can be cancelled
        /// </summary>
        public bool CanCancel
        {
            get => _canCancel;
            set
            {
                if (_canCancel != value)
                {
                    _canCancel = value;
                    OnPropertyChanged(nameof(CanCancel));
                    (CancelCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// Command to cancel the operation
        /// </summary>
        public ICommand CancelCommand { get; }

        /// <summary>
        /// Event raised when the user clicks Cancel
        /// </summary>
        public event EventHandler CancelRequested;

        private void OnCancel()
        {
            CancelRequested?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Simple relay command implementation
        /// </summary>
        private class RelayCommand : ICommand
        {
            private readonly Action _execute;
            private readonly Func<bool> _canExecute;

            public event EventHandler CanExecuteChanged;

            public RelayCommand(Action execute, Func<bool> canExecute = null)
            {
                _execute = execute ?? throw new ArgumentNullException(nameof(execute));
                _canExecute = canExecute;
            }

            public bool CanExecute(object parameter) => _canExecute?.Invoke() ?? true;

            public void Execute(object parameter) => _execute();

            public void RaiseCanExecuteChanged()
            {
                CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
