//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using WpfHexEditor.HexEditor.Commands;

namespace WpfHexEditor.HexEditor.ViewModels
{
    /// <summary>
    /// ViewModel for ReplaceByteWindow (V2 MVVM architecture).
    /// Manages state and business logic for single-byte find/replace operations.
    /// Testable without UI dependencies.
    /// </summary>
    public class ReplaceByteViewModel : INotifyPropertyChanged
    {
        #region Fields

        private byte? _findByte;
        private byte? _replaceByte;
        private bool _replaceInSelectionOnly;
        private bool _showConfirmation = true;
        private string _statusMessage = string.Empty;
        private int _occurrencesFound;

        #endregion

        #region Properties

        /// <summary>
        /// Byte value to find (null = invalid/empty)
        /// </summary>
        public byte? FindByte
        {
            get => _findByte;
            set
            {
                if (_findByte != value)
                {
                    _findByte = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsValid));
                    OnPropertyChanged(nameof(PreviewText));
                    OnPropertyChanged(nameof(FindByteValid));
                    UpdateCommandStates();
                }
            }
        }

        /// <summary>
        /// Byte value to replace with (null = invalid/empty)
        /// </summary>
        public byte? ReplaceByte
        {
            get => _replaceByte;
            set
            {
                if (_replaceByte != value)
                {
                    _replaceByte = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsValid));
                    OnPropertyChanged(nameof(PreviewText));
                    OnPropertyChanged(nameof(ReplaceByteValid));
                    UpdateCommandStates();
                }
            }
        }

        /// <summary>
        /// Whether to replace only within the current selection
        /// </summary>
        public bool ReplaceInSelectionOnly
        {
            get => _replaceInSelectionOnly;
            set
            {
                if (_replaceInSelectionOnly != value)
                {
                    _replaceInSelectionOnly = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Whether to show confirmation dialog before replacing
        /// </summary>
        public bool ShowConfirmation
        {
            get => _showConfirmation;
            set
            {
                if (_showConfirmation != value)
                {
                    _showConfirmation = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Status message displayed in the UI
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                if (_statusMessage != value)
                {
                    _statusMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Number of occurrences found (for preview)
        /// </summary>
        public int OccurrencesFound
        {
            get => _occurrencesFound;
            set
            {
                if (_occurrencesFound != value)
                {
                    _occurrencesFound = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Whether both byte values are valid (not null)
        /// </summary>
        public bool IsValid => _findByte.HasValue && _replaceByte.HasValue;

        /// <summary>
        /// Whether the FindByte is valid
        /// </summary>
        public bool FindByteValid => _findByte.HasValue;

        /// <summary>
        /// Whether the ReplaceByte is valid
        /// </summary>
        public bool ReplaceByteValid => _replaceByte.HasValue;

        /// <summary>
        /// Preview text showing the replacement (e.g., "0xFF → 0x42")
        /// </summary>
        public string PreviewText
        {
            get
            {
                if (IsValid)
                {
                    return $"0x{_findByte:X2} → 0x{_replaceByte:X2}";
                }
                return string.Empty;
            }
        }

        #endregion

        #region Commands

        /// <summary>
        /// Command to execute the replacement (OK button)
        /// </summary>
        public ICommand OkCommand { get; set; }

        /// <summary>
        /// Command to preview the replacement (count occurrences)
        /// </summary>
        public ICommand PreviewCommand { get; set; }

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new ReplaceByteViewModel instance
        /// </summary>
        public ReplaceByteViewModel()
        {
            // Commands will be initialized in the code-behind
            // to avoid circular dependencies with DialogResult
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Updates the CanExecute state of all commands
        /// </summary>
        private void UpdateCommandStates()
        {
            (OkCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (PreviewCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
