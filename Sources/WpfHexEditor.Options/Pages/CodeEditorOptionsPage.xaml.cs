//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ColorPickerControl = WpfHexEditor.ColorPicker.Controls.ColorPicker;

namespace WpfHexEditor.Options.Pages;

public sealed partial class CodeEditorOptionsPage : UserControl, IOptionsPage
{
    public event EventHandler? Changed;
    private bool _loading;

    public CodeEditorOptionsPage() => InitializeComponent();

    // -- IOptionsPage ------------------------------------------------------

    public void Load(AppSettings s)
    {
        _loading = true;
        try
        {
            var ce = s.CodeEditorDefaults;

            SelectComboByTag(FontFamilyCombo, ce.FontFamily);
            TxtFontSize.Text   = ce.FontSize.ToString("F0");
            TxtIndentSize.Text = ce.IndentSize.ToString();
            CheckUseSpaces.IsChecked     = ce.UseSpaces;
            CheckIntelliSense.IsChecked  = ce.ShowIntelliSense;
            CheckLineNumbers.IsChecked   = ce.ShowLineNumbers;
            CheckHighlightLine.IsChecked = ce.HighlightCurrentLine;
            TxtZoom.Text      = ((int)(ce.DefaultZoom * 100)).ToString();
            CheckChangeset.IsChecked = ce.ChangesetEnabled;

            LoadColorPicker(ChkBg,  CpBg,  ce.BackgroundColor);
            LoadColorPicker(ChkFg,  CpFg,  ce.ForegroundColor);
            LoadColorPicker(ChkKw,  CpKw,  ce.KeywordColor);
            LoadColorPicker(ChkStr, CpStr, ce.StringColor);
            LoadColorPicker(ChkCmt, CpCmt, ce.CommentColor);
            LoadColorPicker(ChkNum, CpNum, ce.NumberColor);
        }
        finally { _loading = false; }
    }

    public void Flush(AppSettings s)
    {
        var ce = s.CodeEditorDefaults;

        ce.FontFamily        = ReadComboTag(FontFamilyCombo, "Consolas");
        ce.FontSize          = ParseDouble(TxtFontSize.Text, 13.0);
        ce.IndentSize        = ParseInt(TxtIndentSize.Text, 4);
        ce.UseSpaces         = CheckUseSpaces.IsChecked    == true;
        ce.ShowIntelliSense  = CheckIntelliSense.IsChecked == true;
        ce.ShowLineNumbers   = CheckLineNumbers.IsChecked  == true;
        ce.HighlightCurrentLine = CheckHighlightLine.IsChecked == true;
        ce.DefaultZoom       = ParseDouble(TxtZoom.Text, 100.0) / 100.0;
        ce.ChangesetEnabled  = CheckChangeset.IsChecked == true;
        ce.BackgroundColor   = FlushColorPicker(ChkBg,  CpBg);
        ce.ForegroundColor   = FlushColorPicker(ChkFg,  CpFg);
        ce.KeywordColor      = FlushColorPicker(ChkKw,  CpKw);
        ce.StringColor       = FlushColorPicker(ChkStr, CpStr);
        ce.CommentColor      = FlushColorPicker(ChkCmt, CpCmt);
        ce.NumberColor       = FlushColorPicker(ChkNum, CpNum);
    }

    // -- Control handlers -------------------------------------------------

    private void OnCheckChanged(object sender, RoutedEventArgs e)
    {
        if (!_loading) Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnComboChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loading) Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnTextLostFocus(object sender, RoutedEventArgs e)
    {
        if (!_loading) Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnColorCheckChanged(object sender, RoutedEventArgs e)
    {
        if (!_loading) Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnColorPickerChanged(object sender, Color e)
    {
        if (!_loading) Changed?.Invoke(this, EventArgs.Empty);
    }

    // -- Helpers ----------------------------------------------------------

    private static void SelectComboByTag(ComboBox combo, string tag)
    {
        foreach (ComboBoxItem item in combo.Items)
        {
            if (item.Tag?.ToString() == tag)
            {
                combo.SelectedItem = item;
                return;
            }
        }
        if (combo.Items.Count > 0)
            combo.SelectedIndex = 0;
    }

    private static string ReadComboTag(ComboBox combo, string fallback)
        => combo.SelectedItem is ComboBoxItem item
            ? item.Tag?.ToString() ?? fallback
            : fallback;

    private static double ParseDouble(string text, double fallback)
        => double.TryParse(text, out double v) && v > 0 ? v : fallback;

    private static int ParseInt(string text, int fallback)
        => int.TryParse(text, out int v) && v > 0 ? v : fallback;

    // Restores CheckBox + ColorPicker from a stored hex string (empty = no override).
    private static void LoadColorPicker(CheckBox chk, ColorPickerControl cp, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            chk.IsChecked = false;
            return;
        }
        try
        {
            cp.SelectedColor = (Color)ColorConverter.ConvertFromString(value.Trim());
            chk.IsChecked = true;
        }
        catch
        {
            chk.IsChecked = false;
        }
    }

    // Returns "#RRGGBB" when the override is active, empty string otherwise.
    private static string FlushColorPicker(CheckBox chk, ColorPickerControl cp)
    {
        if (chk.IsChecked != true) return string.Empty;
        var c = cp.SelectedColor;
        return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
    }
}
