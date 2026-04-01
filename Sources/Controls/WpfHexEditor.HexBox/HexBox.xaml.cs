//////////////////////////////////////////////
// Apache 2.0  - 2017-2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
// V2 MVVM Architecture - Minimal code-behind with ViewModel integration
//////////////////////////////////////////////

using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using WpfHexEditor.HexBox.Core;
using WpfHexEditor.HexBox.ViewModels;

namespace WpfHexEditor.HexBox
{
    /// <summary>
    /// HexBox control - V2 MVVM Architecture.
    /// Uses HexBoxViewModel for all business logic.
    /// Maintains V1 DependencyProperty API for backward compatibility.
    /// </summary>
    public partial class HexBox : UserControl
    {
        #region Dependency Properties (V1 Compatibility)

        /// <summary>
        /// LongValue DependencyProperty for backward compatibility.
        /// Syncs with ViewModel.LongValue.
        /// </summary>
        public static readonly DependencyProperty LongValueProperty =
            DependencyProperty.Register(
                nameof(LongValue),
                typeof(long),
                typeof(HexBox),
                new FrameworkPropertyMetadata(
                    0L,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    LongValue_Changed));

        public long LongValue
        {
            get => (long)GetValue(LongValueProperty);
            set => SetValue(LongValueProperty, value);
        }

        private static void LongValue_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not HexBox control || control.ViewModel == null) return;
            if (e.NewValue == e.OldValue) return;

            // Sync DependencyProperty → ViewModel (avoid circular updates)
            if (control._isSyncingFromViewModel) return;

            control._isSyncingFromDP = true;
            try
            {
                control.ViewModel.LongValue = (long)e.NewValue;
            }
            finally
            {
                control._isSyncingFromDP = false;
            }
        }

        /// <summary>
        /// MaximumValue DependencyProperty for backward compatibility.
        /// Syncs with ViewModel.MaximumValue.
        /// </summary>
        public static readonly DependencyProperty MaximumValueProperty =
            DependencyProperty.Register(
                nameof(MaximumValue),
                typeof(long),
                typeof(HexBox),
                new FrameworkPropertyMetadata(long.MaxValue, MaximumValue_Changed));

        public long MaximumValue
        {
            get => (long)GetValue(MaximumValueProperty);
            set => SetValue(MaximumValueProperty, value);
        }

        private static void MaximumValue_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not HexBox control || control.ViewModel == null) return;
            if (e.NewValue == e.OldValue) return;

            // Sync DependencyProperty → ViewModel
            if (control._isSyncingFromViewModel) return;

            control._isSyncingFromDP = true;
            try
            {
                control.ViewModel.MaximumValue = (long)e.NewValue;
            }
            finally
            {
                control._isSyncingFromDP = false;
            }
        }

        #endregion

        #region Fields

        private bool _isSyncingFromViewModel; // Prevent circular updates
        private bool _isSyncingFromDP; // Prevent circular updates

        #endregion

        #region Properties

        /// <summary>
        /// ViewModel instance (V2 architecture)
        /// </summary>
        public HexBoxViewModel ViewModel { get; private set; }

        /// <summary>
        /// V1 Compatibility: Get hexadecimal value
        /// </summary>
        public string HexValue => ViewModel?.HexValue ?? "0";

        #endregion

        #region Events (V1 Compatibility)

        /// <summary>
        /// Raised when value changes (V1 compatibility)
        /// </summary>
        public event EventHandler? ValueChanged;

        #endregion

        #region Constructor

        public HexBox()
        {
            // Create ViewModel BEFORE InitializeComponent
            ViewModel = new HexBoxViewModel();
            DataContext = ViewModel;

            InitializeComponent();

            // Subscribe to ViewModel events AFTER InitializeComponent
            ViewModel.ValueChanged += OnViewModelValueChanged;
            ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handle ViewModel.ValueChanged event
        /// </summary>
        private void OnViewModelValueChanged(object? sender, EventArgs e)
        {
            // Sync ViewModel → DependencyProperty (for V1 compatibility)
            if (!_isSyncingFromDP && LongValue != ViewModel.LongValue)
            {
                _isSyncingFromViewModel = true;
                try
                {
                    SetValue(LongValueProperty, ViewModel.LongValue);
                }
                finally
                {
                    _isSyncingFromViewModel = false;
                }
            }

            // Raise V1 event for backward compatibility
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Handle ViewModel property changes
        /// </summary>
        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Sync ViewModel → DependencyProperties
            if (_isSyncingFromDP) return;

            _isSyncingFromViewModel = true;
            try
            {
                switch (e.PropertyName)
                {
                    case nameof(HexBoxViewModel.LongValue):
                        if (LongValue != ViewModel.LongValue)
                            SetValue(LongValueProperty, ViewModel.LongValue);
                        break;

                    case nameof(HexBoxViewModel.MaximumValue):
                        if (MaximumValue != ViewModel.MaximumValue)
                            SetValue(MaximumValueProperty, ViewModel.MaximumValue);
                        break;
                }
            }
            finally
            {
                _isSyncingFromViewModel = false;
            }
        }

        /// <summary>
        /// Validate keyboard input (V1 logic preserved).
        /// Only allow hex keys, backspace, delete, arrows, tab, enter.
        /// This is a UI concern, so it stays in code-behind.
        /// </summary>
        private void HexTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Allow all navigation/editing keys
            if (HexKeyValidator.IsHexKey(e.Key) ||
                HexKeyValidator.IsBackspaceKey(e.Key) ||
                HexKeyValidator.IsDeleteKey(e.Key) ||
                HexKeyValidator.IsArrowKey(e.Key) ||
                HexKeyValidator.IsTabKey(e.Key) ||
                HexKeyValidator.IsEnterKey(e.Key))
            {
                e.Handled = false;
                return;
            }

            // Block all other keys
            e.Handled = true;
        }

        /// <summary>
        /// Handle arrow keys for increment/decrement (V1 compatibility)
        /// </summary>
        private void HexTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Up && ViewModel.IncrementCommand.CanExecute(null))
            {
                // Force binding update before increment
                var binding = HexTextBox.GetBindingExpression(TextBox.TextProperty);
                binding?.UpdateSource();

                ViewModel.IncrementCommand.Execute(null);
                HexTextBox.Focus();
            }
            else if (e.Key == Key.Down && ViewModel.DecrementCommand.CanExecute(null))
            {
                // Force binding update before decrement
                var binding = HexTextBox.GetBindingExpression(TextBox.TextProperty);
                binding?.UpdateSource();

                ViewModel.DecrementCommand.Execute(null);
                HexTextBox.Focus();
            }
            else if (e.Key == Key.Enter)
            {
                // Force binding update and move focus
                var binding = HexTextBox.GetBindingExpression(TextBox.TextProperty);
                binding?.UpdateSource();
                HexTextBox.SelectAll();
                e.Handled = true;
            }
        }

        #endregion

        #region Public Methods (V1 Compatibility)

        /// <summary>
        /// Increment value by 1 (V1 compatibility)
        /// </summary>
        public void AddOne()
        {
            if (ViewModel.IncrementCommand.CanExecute(null))
            {
                ViewModel.IncrementCommand.Execute(null);
            }
        }

        /// <summary>
        /// Decrement value by 1 (V1 compatibility)
        /// </summary>
        public void SubstractOne()
        {
            if (ViewModel.DecrementCommand.CanExecute(null))
            {
                ViewModel.DecrementCommand.Execute(null);
            }
        }

        #endregion
    }
}
