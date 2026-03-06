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
//     Colour preview: small Border filled via ConvertFromString().
//     Theme: DynamicResource brushes inherited from OptionsEditorControl.
// ==========================================================

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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

            SetColorTextAndPreview(TxtBgColor,  BgPreview,  te.BackgroundColor);
            SetColorTextAndPreview(TxtFgColor,  FgPreview,  te.ForegroundColor);
            SetColorTextAndPreview(TxtKwColor,  KwPreview,  te.KeywordColor);
            SetColorTextAndPreview(TxtStrColor, StrPreview, te.StringColor);
            SetColorTextAndPreview(TxtCmtColor, CmtPreview, te.CommentColor);
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
        te.BackgroundColor = NormalizeColor(TxtBgColor.Text);
        te.ForegroundColor = NormalizeColor(TxtFgColor.Text);
        te.KeywordColor    = NormalizeColor(TxtKwColor.Text);
        te.StringColor     = NormalizeColor(TxtStrColor.Text);
        te.CommentColor    = NormalizeColor(TxtCmtColor.Text);
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

    private void OnColorTextLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox tb) return;

        if (tb.Tag is string previewName && FindName(previewName) is Border preview)
            ApplyColorPreview(preview, tb.Text);

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

    private static string NormalizeColor(string text)
    {
        var trimmed = text.Trim();
        if (string.IsNullOrEmpty(trimmed)) return string.Empty;
        try
        {
            var c = (Color)ColorConverter.ConvertFromString(trimmed);
            return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        }
        catch
        {
            return string.Empty;
        }
    }

    private static void SetColorTextAndPreview(TextBox tb, Border preview, string value)
    {
        tb.Text = value;
        ApplyColorPreview(preview, value);
    }

    private static void ApplyColorPreview(Border preview, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            preview.Background = null;
            return;
        }
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(value.Trim());
            preview.Background = new SolidColorBrush(color);
        }
        catch
        {
            preview.Background = null;
        }
    }
}
