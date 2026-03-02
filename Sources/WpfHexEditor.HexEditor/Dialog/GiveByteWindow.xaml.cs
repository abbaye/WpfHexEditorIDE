//////////////////////////////////////////////
// Apache 2.0 - 2018-2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.HexEditor.Commands;
using WpfHexEditor.HexEditor.ViewModels;
using WpfHexEditor.HexBox;

namespace WpfHexEditor.HexEditor.Dialog
{
    /// <summary>
    /// Modern MVVM dialog for entering a single byte value.
    /// This Window is used to give a hex value for fill the selection with.
    /// </summary>
    public partial class GiveByteWindow
    {
        /// <summary>
        /// Gets the ViewModel for this dialog
        /// </summary>
        public GiveByteViewModel ViewModel { get; private set; }

        /// <summary>
        /// Gets the byte value entered (for backward compatibility)
        /// </summary>
        public byte ByteValue => ViewModel.ByteValue ?? 0;

        public GiveByteWindow()
        {
            InitializeComponent();

            // Initialize ViewModel
            ViewModel = new GiveByteViewModel();
            DataContext = ViewModel;

            // Clear HexBox text after the window is loaded and bindings are established
            Loaded += (s, e) =>
            {
                // Clear text after all bindings are complete
                ClearHexBoxText(HexTextBox);

                // Force update of validation state
                ViewModel.ByteValue = null;

                // Sync HexBox control with ViewModel using TextChanged event
                // This ensures validation works even when user types "0"
                var textBox = FindVisualChild<System.Windows.Controls.TextBox>(HexTextBox);
                if (textBox != null)
                {
                    textBox.TextChanged += (sender, args) => OnByteValueChanged(sender, EventArgs.Empty);
                }
            };

            // Initialize command in ViewModel
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
        }

        private void OnByteValueChanged(object sender, EventArgs e)
        {
            // Parse the TextBox text directly (don't wait for binding update)
            var textBox = FindVisualChild<System.Windows.Controls.TextBox>(HexTextBox);
            if (textBox != null && !string.IsNullOrWhiteSpace(textBox.Text))
            {
                if (TryParseHexByte(textBox.Text, out byte value))
                {
                    ViewModel.ByteValue = value;
                }
                else
                {
                    ViewModel.ByteValue = null;
                }
            }
            else
            {
                ViewModel.ByteValue = null;
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

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ViewModel.IsValid)
                return;

            // Show confirmation dialog if option is enabled
            if (ViewModel.ShowConfirmation)
            {
                var preview = GetResourceString("PreviewString");
                var fillText = GetResourceString("FillSelectionString");
                var title = GetResourceString("EnterHexValueMsgString");

                var message = $"{preview}\n" +
                             $"0x{ViewModel.ByteValue:X2}\n\n" +
                             $"{fillText}?";

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
