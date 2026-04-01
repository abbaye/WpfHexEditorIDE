//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using WpfHexEditor.HexBox.Commands;
using WpfHexEditor.HexBox.Core;
using WpfHexEditor.HexBox.Models;

namespace WpfHexEditor.HexBox.ViewModels
{
    /// <summary>
    /// ViewModel for HexBox control (V2 MVVM architecture).
    /// Manages state and business logic for hex value input/display.
    /// Testable without UI dependencies.
    /// </summary>
    public class HexBoxViewModel : INotifyPropertyChanged
    {
        #region Fields

        private long _longValue;
        private long _maximumValue = long.MaxValue;
        private bool _isReadOnly;

        #endregion

        #region Properties

        /// <summary>
        /// Current decimal value (0 to MaximumValue)
        /// </summary>
        public long LongValue
        {
            get => _longValue;
            set
            {
                if (_longValue != value)
                {
                    // Coerce value to valid range
                    var coercedValue = CoerceValue(value);

                    if (_longValue != coercedValue)
                    {
                        _longValue = coercedValue;
                        OnPropertyChanged();
                        OnPropertyChanged(nameof(HexValue));
                        OnPropertyChanged(nameof(DisplayValue));
                        ValueChanged?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
        }

        /// <summary>
        /// Current hexadecimal value (read/write).
        /// Automatically converts between hex string and decimal value.
        /// </summary>
        public string HexValue
        {
            get
            {
                var hex = HexConversion.LongToHex(_longValue);
                var trimmed = hex.TrimStart('0');
                return string.IsNullOrEmpty(trimmed) ? "0" : trimmed;
            }
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    // Empty input defaults to 0
                    LongValue = 0;
                }
                else
                {
                    var (success, val) = HexConversion.HexLiteralToLong(value);
                    if (success)
                    {
                        LongValue = val;
                    }
                    // If parsing fails, keep current value (no update)
                }
            }
        }

        /// <summary>
        /// Display value for TextBox (formatted hex without leading zeros, uppercase).
        /// Used for two-way binding with TextBox.Text.
        /// </summary>
        public string DisplayValue
        {
            get
            {
                var hex = HexConversion.LongToHex(_longValue);
                var trimmed = hex.TrimStart('0');
                return string.IsNullOrEmpty(trimmed) ? "0" : trimmed.ToUpperInvariant();
            }
            set
            {
                HexValue = value;
            }
        }

        /// <summary>
        /// Maximum allowed value (inclusive)
        /// </summary>
        public long MaximumValue
        {
            get => _maximumValue;
            set
            {
                if (_maximumValue != value)
                {
                    _maximumValue = value;
                    OnPropertyChanged();

                    // Revalidate current value against new maximum
                    if (_longValue > _maximumValue)
                    {
                        LongValue = _maximumValue;
                    }
                }
            }
        }

        /// <summary>
        /// Read-only mode (disables editing)
        /// </summary>
        public bool IsReadOnly
        {
            get => _isReadOnly;
            set
            {
                if (_isReadOnly != value)
                {
                    _isReadOnly = value;
                    OnPropertyChanged();

                    // Update command states when read-only changes
                    (IncrementCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (DecrementCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// Current state as immutable value object
        /// </summary>
        public HexBoxState State => new HexBoxState(_longValue, _maximumValue, _isReadOnly);

        #endregion

        #region Commands

        /// <summary>
        /// Command to increment value by 1
        /// </summary>
        public ICommand IncrementCommand { get; }

        /// <summary>
        /// Command to decrement value by 1
        /// </summary>
        public ICommand DecrementCommand { get; }

        /// <summary>
        /// Command to copy value as hexadecimal string (with 0x prefix)
        /// </summary>
        public ICommand CopyHexCommand { get; }

        /// <summary>
        /// Command to copy value as decimal string
        /// </summary>
        public ICommand CopyDecimalCommand { get; }

        #endregion

        #region Events

        /// <summary>
        /// Raised when LongValue changes (for V1 compatibility)
        /// </summary>
        public event EventHandler? ValueChanged;

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new HexBoxViewModel instance
        /// </summary>
        public HexBoxViewModel()
        {
            // Initialize commands
            IncrementCommand = new RelayCommand(Increment, CanIncrement);
            DecrementCommand = new RelayCommand(Decrement, CanDecrement);
            CopyHexCommand = new RelayCommand(CopyHex);
            CopyDecimalCommand = new RelayCommand(CopyDecimal);
        }

        #endregion

        #region Command Methods

        private void Increment()
        {
            if (_longValue < _maximumValue)
            {
                LongValue++;
            }
        }

        private bool CanIncrement() => !_isReadOnly && _longValue < _maximumValue;

        private void Decrement()
        {
            if (_longValue > 0)
            {
                LongValue--;
            }
        }

        private bool CanDecrement() => !_isReadOnly && _longValue > 0;

        private void CopyHex()
        {
            try
            {
                System.Windows.Clipboard.SetText($"0x{HexValue}");
            }
            catch
            {
                // Clipboard access can fail in certain contexts
            }
        }

        private void CopyDecimal()
        {
            try
            {
                System.Windows.Clipboard.SetText(_longValue.ToString());
            }
            catch
            {
                // Clipboard access can fail in certain contexts
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Coerce value to valid range [0, MaximumValue]
        /// </summary>
        private long CoerceValue(long value)
        {
            if (value < 0) return 0;
            if (value > _maximumValue) return _maximumValue;
            return value;
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
