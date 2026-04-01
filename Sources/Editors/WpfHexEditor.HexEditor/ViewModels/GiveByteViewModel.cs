// ==========================================================
// Project: WpfHexEditor.HexEditor
// File: GiveByteViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-06
// Description:
//     ViewModel for the GiveByteWindow dialog. Manages user input for a single byte
//     value (decimal or hex), validates the range, and exposes the confirmed byte
//     result through an OK/Cancel command pattern.
//
// Architecture Notes:
//     MVVM dialog ViewModel — implements INotifyPropertyChanged manually.
//     Uses RelayCommand from Commands/ for OK/Cancel actions.
//
// ==========================================================

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using WpfHexEditor.HexEditor.Commands;

namespace WpfHexEditor.HexEditor.ViewModels
{
    /// <summary>
    /// ViewModel for GiveByteWindow (V2 MVVM architecture).
    /// Manages state and business logic for entering a single byte value.
    /// Testable without UI dependencies.
    /// </summary>
    public class GiveByteViewModel : INotifyPropertyChanged
    {
        #region Fields

        private byte? _byteValue;
        private bool _showConfirmation = true;

        #endregion

        #region Properties

        /// <summary>
        /// Byte value to use (null = invalid/empty)
        /// </summary>
        public byte? ByteValue
        {
            get => _byteValue;
            set
            {
                if (_byteValue != value)
                {
                    _byteValue = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsValid));
                    OnPropertyChanged(nameof(DisplayValue));
                    UpdateCommandStates();
                }
            }
        }

        /// <summary>
        /// Whether the byte value is valid (not null)
        /// </summary>
        public bool IsValid => _byteValue.HasValue;

        /// <summary>
        /// Display value for preview (e.g., "0xFF")
        /// </summary>
        public string DisplayValue
        {
            get
            {
                if (IsValid)
                {
                    return $"0x{_byteValue:X2}";
                }
                return string.Empty;
            }
        }

        /// <summary>
        /// Whether to show confirmation dialog before filling
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

        #endregion

        #region Commands

        /// <summary>
        /// Command to execute the operation (OK button)
        /// </summary>
        public ICommand OkCommand { get; set; }

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new GiveByteViewModel instance
        /// </summary>
        public GiveByteViewModel()
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
