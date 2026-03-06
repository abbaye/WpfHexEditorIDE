// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;

namespace WpfHexEditor.App.Dialogs;

/// <summary>
/// VS-style "Go To Offset" dialog.
/// The caller reads <see cref="Offset"/> when <see cref="DialogResult"/> is true.
/// </summary>
public partial class GoToOffsetDialog : WpfHexEditor.Editor.Core.Views.ThemedDialog
{
    /// <summary>
    /// Parsed offset value — valid only when DialogResult is true.
    /// </summary>
    public long Offset { get; private set; }

    public GoToOffsetDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => OffsetInput.Focus();
    }

    private void OnGo(object sender, RoutedEventArgs e) => TryAccept();

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnInputKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) TryAccept();
    }

    private void TryAccept()
    {
        var text = OffsetInput.Text.Trim();
        if (string.IsNullOrEmpty(text))
        {
            ShowError("Please enter an offset.");
            return;
        }

        long offset;
        if (HexRadio.IsChecked == true)
        {
            // Strip optional 0x prefix
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                text = text[2..];

            if (!long.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out offset))
            {
                ShowError("Invalid hexadecimal value.");
                return;
            }
        }
        else
        {
            if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out offset))
            {
                ShowError("Invalid decimal value.");
                return;
            }
        }

        if (offset < 0)
        {
            ShowError("Offset must be non-negative.");
            return;
        }

        Offset = offset;
        DialogResult = true;
        Close();
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
        OffsetInput.Focus();
        OffsetInput.SelectAll();
    }
}
