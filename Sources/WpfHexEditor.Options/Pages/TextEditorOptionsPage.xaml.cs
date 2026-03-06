// ==========================================================
// Project: WpfHexEditor.Options
// File: TextEditorOptionsPage.xaml.cs
// Author: Auto
// Created: 2026-03-06
// Description:
//     Code-behind for the TextEditor options page.
//     Covers font, indentation, features (line numbers, zoom),
//     .whchg toggle, and syntax colour overrides.
//
// Architecture Notes:
//     Pattern: IOptionsPage (Load / Flush / Changed)
//     Colour override: CheckBox enables the override; ColorPicker holds the value.
//     Empty string stored when CheckBox is unchecked (= use theme default).
//     Theme: DynamicResource brushes inherited from OptionsEditorControl.
//     ColorPicker DynamicResources (BorderBrush, SurfaceElevatedBrush,
//     ForegroundBrush) are resolved from the active application theme.
// ==========================================================

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ColorPickerControl = WpfHexEditor.ColorPicker.Controls.ColorPicker;

namespace WpfHexEditor.Options.Pages;

public sealed partial class TextEditorOptionsPage : UserControl, IOptionsPage
{
    public event EventHandler? Changed;
    private bool _loading;

    public TextEditorOptionsPage() => InitializeComponent();

    // ── IOptionsPage ──────────────────────────────────────────────────────

    public void Load(AppSettings s)
    {
        _loading = true;
        try
        {
            var te = s.TextEditorDefaults;

            SelectComboByTag(FontFamilyCombo, te.FontFamily);
            TxtFontSize.Text   = te.FontSize.ToString("F0");
            TxtIndentSize.Text = te.IndentSize.ToString();
            CheckUseSpaces.IsChecked   = te.UseSpaces;
            CheckLineNumbers.IsChecked = te.ShowLineNumbers;
            TxtZoom.Text = ((int)(te.DefaultZoom * 100)).ToString();
            CheckChangeset.IsChecked = te.ChangesetEnabled;

            LoadColorPicker(ChkBg,  CpBg,  te.BackgroundColor);
            LoadColorPicker(ChkFg,  CpFg,  te.ForegroundColor);
            LoadColorPicker(ChkKw,  CpKw,  te.KeywordColor);
            LoadColorPicker(ChkStr, CpStr, te.StringColor);
            LoadColorPicker(ChkCmt, CpCmt, te.CommentColor);
        }
        finally { _loading = false; }
    }

    public void Flush(AppSettings s)
    {
        var te = s.TextEditorDefaults;

        te.FontFamily      = ReadComboTag(FontFamilyCombo, "Consolas");
        te.FontSize        = ParseDouble(TxtFontSize.Text, 13.0);
        te.IndentSize      = ParseInt(TxtIndentSize.Text, 4);
        te.UseSpaces       = CheckUseSpaces.IsChecked   == true;
        te.ShowLineNumbers = CheckLineNumbers.IsChecked == true;
        te.DefaultZoom     = ParseDouble(TxtZoom.Text, 100.0) / 100.0;
        te.ChangesetEnabled = CheckChangeset.IsChecked == true;
        te.BackgroundColor = FlushColorPicker(ChkBg,  CpBg);
        te.ForegroundColor = FlushColorPicker(ChkFg,  CpFg);
        te.KeywordColor    = FlushColorPicker(ChkKw,  CpKw);
        te.StringColor     = FlushColorPicker(ChkStr, CpStr);
        te.CommentColor    = FlushColorPicker(ChkCmt, CpCmt);
    }

    // ── Control handlers ─────────────────────────────────────────────────

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

    // ── Helpers ──────────────────────────────────────────────────────────

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
