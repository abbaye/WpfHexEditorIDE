// ==========================================================
// Project: WpfHexEditor.HexEditor
// File: ReplaceByteWindow.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Code-behind for the ReplaceByteWindow dialog, allowing users to specify
//     a find-byte and replace-byte for a single-byte find-and-replace operation.
//     Binds to ReplaceByteViewModel for validation and result propagation.
//
// Architecture Notes:
//     MVVM dialog pattern — ReplaceByteViewModel owns validation logic.
//     Uses RelayCommand from Commands/ for OK/Cancel actions and HexBox for input.
//
// ==========================================================

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfHexEditor.HexEditor.Commands;
using WpfHexEditor.Core.Properties;
using WpfHexEditor.HexEditor.ViewModels;
using WpfHexEditor.HexBox;

namespace WpfHexEditor.HexEditor.Dialog
{
    /// <summary>
    /// Modern MVVM dialog for single-byte find/replace operations.
    /// Provides validation, preview, and options for scope control.
    /// </summary>
    public partial class ReplaceByteWindow
    {
        /// <summary>
        /// Gets the ViewModel for this dialog
        /// </summary>
        public ReplaceByteViewModel ViewModel { get; private set; }

        /// <summary>
        /// Gets the byte value to find (for backward compatibility)
        /// </summary>
        public byte FindByte => ViewModel.FindByte ?? 0;

        /// <summary>
        /// Gets the byte value to replace with (for backward compatibility)
        /// </summary>
        public byte ReplaceByte => ViewModel.ReplaceByte ?? 0;

        /// <summary>
        /// Gets whether to replace in selection only
        /// </summary>
        public bool ReplaceInSelectionOnly => ViewModel.ReplaceInSelectionOnly;

        public ReplaceByteWindow()
        {
            InitializeComponent();

            // Initialize ViewModel
            ViewModel = new ReplaceByteViewModel();
            DataContext = ViewModel;

            // Clear HexBox text after the window is loaded and bindings are established
            Loaded += (s, e) =>
            {
                // Clear text after all bindings are complete
                ClearHexBoxText(HexTextBox);
                ClearHexBoxText(ReplaceHexTextBox);

                // Force update of validation state
                ViewModel.FindByte = null;
                ViewModel.ReplaceByte = null;

                // Sync HexBox controls with ViewModel using TextChanged event
                // This ensures validation works even when user types "0"
                var findTextBox = FindVisualChild<System.Windows.Controls.TextBox>(HexTextBox);
                if (findTextBox != null)
                {
                    findTextBox.TextChanged += (sender, args) => OnFindByteChanged(sender, EventArgs.Empty);
                }

                var replaceTextBox = FindVisualChild<System.Windows.Controls.TextBox>(ReplaceHexTextBox);
                if (replaceTextBox != null)
                {
                    replaceTextBox.TextChanged += (sender, args) => OnReplaceByteChanged(sender, EventArgs.Empty);
                }
            };

            // Initialize commands in ViewModel
            ViewModel.OkCommand = new RelayCommand(
                execute: () =>
                {
                    if (ViewModel.IsValid)
                    {
                        DialogResult = true;
                    }
                },
                canExecute: () => ViewModel.IsValid
            );

            ViewModel.PreviewCommand = new RelayCommand(
                execute: UpdatePreview,
                canExecute: () => ViewModel.IsValid
            );

            // Keyboard shortcuts
            PreviewKeyDown += OnWindowPreviewKeyDown;
        }

        private void OnFindByteChanged(object sender, EventArgs e)
        {
            // Parse the TextBox text directly (don't wait for binding update)
            var textBox = FindVisualChild<System.Windows.Controls.TextBox>(HexTextBox);
            if (textBox != null && !string.IsNullOrWhiteSpace(textBox.Text))
            {
                if (TryParseHexByte(textBox.Text, out byte value))
                {
                    ViewModel.FindByte = value;
                }
                else
                {
                    ViewModel.FindByte = null;
                }
            }
            else
            {
                ViewModel.FindByte = null;
            }
        }

        private void OnReplaceByteChanged(object sender, EventArgs e)
        {
            // Parse the TextBox text directly (don't wait for binding update)
            var textBox = FindVisualChild<System.Windows.Controls.TextBox>(ReplaceHexTextBox);
            if (textBox != null && !string.IsNullOrWhiteSpace(textBox.Text))
            {
                if (TryParseHexByte(textBox.Text, out byte value))
                {
                    ViewModel.ReplaceByte = value;
                }
                else
                {
                    ViewModel.ReplaceByte = null;
                }
            }
            else
            {
                ViewModel.ReplaceByte = null;
            }
        }

        /// <summary>
        /// Try to parse a hexadecimal string to a byte value
        /// </summary>
        private bool TryParseHexByte(string hexText, out byte value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(hexText))
                return false;

            // Remove any spaces or common prefixes
            hexText = hexText.Trim().Replace("0x", "").Replace("0X", "").Replace(" ", "");

            // Try to parse as hex
            if (byte.TryParse(hexText, System.Globalization.NumberStyles.HexNumber, null, out value))
            {
                return true;
            }

            return false;
        }

        private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
        {
            // F3 for preview
            if (e.Key == Key.F3 && ViewModel.IsValid)
            {
                UpdatePreview();
                e.Handled = true;
            }
        }

        private void UpdatePreview()
        {
            // Update preview message with localized strings
            if (ViewModel.IsValid)
            {
                var scope = ViewModel.ReplaceInSelectionOnly
                    ? GetResourceString("InSelectionString")
                    : GetResourceString("InFileString");
                var preview = GetResourceString("PreviewString");
                ViewModel.StatusMessage = $"{preview} 0x{ViewModel.FindByte:X2} → 0x{ViewModel.ReplaceByte:X2} {scope}";
            }
        }

        private string GetResourceString(string key)
        {
            try
            {
                return Application.Current.TryFindResource(key) as string ?? key;
            }
            catch
            {
                return key;
            }
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ViewModel.IsValid)
                return;

            // Show confirmation dialog if option is enabled
            if (ViewModel.ShowConfirmation)
            {
                var scope = ViewModel.ReplaceInSelectionOnly
                    ? GetResourceString("InSelectionString")
                    : GetResourceString("InFileString");

                var preview = GetResourceString("PreviewString");
                var replaceText = GetResourceString("ReplaceString");
                var title = GetResourceString("ReplaceByByteString");

                var message = $"{preview}\n" +
                             $"0x{ViewModel.FindByte:X2} → 0x{ViewModel.ReplaceByte:X2}\n" +
                             $"{scope}\n\n" +
                             $"{replaceText}?";

                var result = MessageBox.Show(
                    message,
                    title,
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                    return;
            }

            DialogResult = true;
        }

        /// <summary>
        /// Clears the text in a HexBox control by accessing its internal TextBox
        /// </summary>
        private void ClearHexBoxText(WpfHexEditor.HexBox.HexBox hexBox)
        {
            if (hexBox == null) return;

            // Find the TextBox inside the HexBox control
            var textBox = FindVisualChild<System.Windows.Controls.TextBox>(hexBox);
            if (textBox != null)
            {
                textBox.Text = string.Empty;
            }
        }

        /// <summary>
        /// Helper method to find a child control by type in the visual tree
        /// </summary>
        private T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;

            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                {
                    return typedChild;
                }

                var childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                {
                    return childOfChild;
                }
            }

            return null;
        }
    }
}
