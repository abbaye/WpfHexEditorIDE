// ==========================================================
// Project: WpfHexEditor.HexEditor
// File: GoToPositionWindow.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Description:
//     Dialog for navigating to a specific byte position in the hex editor.
//     Accepts hexadecimal (0x prefix optional) or decimal input.
// ==========================================================

using System;
using System.Globalization;
using System.Windows;

namespace WpfHexEditor.HexEditor.Dialog
{
    /// <summary>
    /// Dialog that accepts a byte position (hex or decimal) and returns it via <see cref="Position"/>.
    /// </summary>
    public partial class GoToPositionWindow
    {
        /// <summary>The parsed byte position to navigate to. Valid only when DialogResult is true.</summary>
        public long Position { get; private set; }

        public GoToPositionWindow()
        {
            InitializeComponent();

            Loaded += (_, _) =>
            {
                PositionTextBox.Focus();
                PositionTextBox.SelectAll();
            };
        }

        // ── Event handlers ────────────────────────────────────────────────────

        private void PositionTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
            => OkButton.IsEnabled = TryParsePosition(PositionTextBox.Text, out _);

        private void ModeRadio_Changed(object sender, RoutedEventArgs e)
            => OkButton.IsEnabled = TryParsePosition(PositionTextBox.Text, out _);

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (!TryParsePosition(PositionTextBox.Text, out long pos)) return;
            Position = pos;
            DialogResult = true;
        }

        // ── Parsing ───────────────────────────────────────────────────────────

        private bool TryParsePosition(string input, out long position)
        {
            position = 0;
            if (string.IsNullOrWhiteSpace(input)) return false;

            if (HexRadio.IsChecked == true)
            {
                var raw = input.Trim();
                if (raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    raw = raw.Substring(2);

                return long.TryParse(raw, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out position)
                    && position >= 0;
            }
            else
            {
                return long.TryParse(input.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out position)
                    && position >= 0;
            }
        }
    }
}
