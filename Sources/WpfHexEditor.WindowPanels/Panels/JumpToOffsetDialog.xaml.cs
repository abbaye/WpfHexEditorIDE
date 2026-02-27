//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Windows;

namespace WpfHexEditor.WindowPanels.Panels
{
    /// <summary>
    /// Dialog for jumping to a specific offset
    /// </summary>
    public partial class JumpToOffsetDialog : Window
    {
        public long Offset { get; private set; }

        public JumpToOffsetDialog()
        {
            InitializeComponent();
            OffsetTextBox.Focus();
        }

        private void OffsetTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            // Visual feedback for valid/invalid input could be added here
        }

        private void JumpButton_Click(object sender, RoutedEventArgs e)
        {
            var input = OffsetTextBox.Text.Trim();

            if (string.IsNullOrEmpty(input))
            {
                MessageBox.Show("Please enter an offset.", "Invalid Input",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Try to parse as hex (with 0x prefix) or decimal
                if (input.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    Offset = Convert.ToInt64(input.Substring(2), 16);
                }
                else if (input.Contains("x", StringComparison.OrdinalIgnoreCase))
                {
                    // Handle formats like "1A4x" or "x1A4"
                    var hexPart = input.Replace("x", "").Replace("X", "");
                    Offset = Convert.ToInt64(hexPart, 16);
                }
                else
                {
                    // Try decimal first
                    if (long.TryParse(input, out var decValue))
                    {
                        Offset = decValue;
                    }
                    else
                    {
                        // Try hex without prefix
                        Offset = Convert.ToInt64(input, 16);
                    }
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Invalid offset format: {ex.Message}\n\nPlease enter a valid hexadecimal (0x100) or decimal (256) value.",
                    "Invalid Offset", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
