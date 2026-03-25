//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Windows;
using System.Windows.Controls;

namespace WpfHexEditor.Core.Options.Pages;

/// <summary>
/// Options page for Output panel log-level colours.
/// Accessible via Options → Environment → Output.
///
/// Architecture Notes:
///   Pattern: IOptionsPage (Load / Flush / Changed)
///   Colours are persisted as hex strings inside AppSettings.OutputLogger
///   and applied live to OutputLogger via OutputLoggerSettings.ColorsChanged.
/// </summary>
public sealed partial class OutputOptionsPage : UserControl, IOptionsPage
{
    public event EventHandler? Changed;
    private bool _loading;

    public OutputOptionsPage() => InitializeComponent();

    // -- IOptionsPage ---------------------------------------------------------

    public void Load(AppSettings s)
    {
        _loading = true;
        try
        {
            if (FindName("PickerWarn") is ColorPicker.Controls.ColorPicker pickerWarn)
                pickerWarn.SelectedColor = ParseHexColor(s.OutputLogger.WarnColor);

            if (FindName("PickerError") is ColorPicker.Controls.ColorPicker pickerError)
                pickerError.SelectedColor = ParseHexColor(s.OutputLogger.ErrorColor);

            if (FindName("PickerDebug") is ColorPicker.Controls.ColorPicker pickerDebug)
                pickerDebug.SelectedColor = ParseHexColor(s.OutputLogger.DebugColor);

            if (FindName("PickerSuccess") is ColorPicker.Controls.ColorPicker pickerSuccess)
                pickerSuccess.SelectedColor = ParseHexColor(s.OutputLogger.SuccessColor);
        }
        finally { _loading = false; }
    }

    public void Flush(AppSettings s)
    {
        if (FindName("PickerWarn") is ColorPicker.Controls.ColorPicker pickerWarn)
            s.OutputLogger.WarnColor = ColorToHex(pickerWarn.SelectedColor);

        if (FindName("PickerError") is ColorPicker.Controls.ColorPicker pickerError)
            s.OutputLogger.ErrorColor = ColorToHex(pickerError.SelectedColor);

        if (FindName("PickerDebug") is ColorPicker.Controls.ColorPicker pickerDebug)
            s.OutputLogger.DebugColor = ColorToHex(pickerDebug.SelectedColor);

        if (FindName("PickerSuccess") is ColorPicker.Controls.ColorPicker pickerSuccess)
            s.OutputLogger.SuccessColor = ColorToHex(pickerSuccess.SelectedColor);

        // Notify OutputLogger to rebuild its brushes immediately.
        OutputLoggerSettings.NotifyChanged();
    }

    // -- Control handlers -----------------------------------------------------

    private void OnColorPickerChanged(object sender, System.Windows.Media.Color e)
    {
        if (_loading) return;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnResetColours(object sender, RoutedEventArgs e)
    {
        _loading = true;
        try
        {
            if (FindName("PickerWarn") is ColorPicker.Controls.ColorPicker pickerWarn)
                pickerWarn.SelectedColor = ParseHexColor("#DCB432");

            if (FindName("PickerError") is ColorPicker.Controls.ColorPicker pickerError)
                pickerError.SelectedColor = ParseHexColor("#F0503C");

            if (FindName("PickerDebug") is ColorPicker.Controls.ColorPicker pickerDebug)
                pickerDebug.SelectedColor = ParseHexColor("#828282");

            if (FindName("PickerSuccess") is ColorPicker.Controls.ColorPicker pickerSuccess)
                pickerSuccess.SelectedColor = ParseHexColor("#4EC9B0");
        }
        finally { _loading = false; }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    // -- Helpers --------------------------------------------------------------

    private static System.Windows.Media.Color ParseHexColor(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 6)
            hex = "FF" + hex;

        return System.Windows.Media.Color.FromArgb(
            Convert.ToByte(hex[..2], 16),
            Convert.ToByte(hex[2..4], 16),
            Convert.ToByte(hex[4..6], 16),
            Convert.ToByte(hex[6..8], 16));
    }

    private static string ColorToHex(System.Windows.Media.Color color)
        => $"#{color.R:X2}{color.G:X2}{color.B:X2}";
}
