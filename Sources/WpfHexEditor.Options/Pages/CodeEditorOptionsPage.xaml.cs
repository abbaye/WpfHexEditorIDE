// ==========================================================
// Project: WpfHexEditor.Options
// File: CodeEditorOptionsPage.xaml.cs
// Author: Auto
// Created: 2026-03-06
// Description:
//     Code-behind for the CodeEditor options page.
//     Covers font, indentation, features (IntelliSense, line numbers,
//     zoom), .whchg toggle, and syntax colour overrides.
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

public sealed partial class CodeEditorOptionsPage : UserControl, IOptionsPage
{
    public event EventHandler? Changed;
    private bool _loading;

    public CodeEditorOptionsPage() => InitializeComponent();

    // ── IOptionsPage ──────────────────────────────────────────────────────

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

            SetColorTextAndPreview(TxtBgColor,  BgPreview,  ce.BackgroundColor);
            SetColorTextAndPreview(TxtFgColor,  FgPreview,  ce.ForegroundColor);
            SetColorTextAndPreview(TxtKwColor,  KwPreview,  ce.KeywordColor);
            SetColorTextAndPreview(TxtStrColor, StrPreview, ce.StringColor);
            SetColorTextAndPreview(TxtCmtColor, CmtPreview, ce.CommentColor);
            SetColorTextAndPreview(TxtNumColor, NumPreview, ce.NumberColor);
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
        ce.BackgroundColor   = NormalizeColor(TxtBgColor.Text);
        ce.ForegroundColor   = NormalizeColor(TxtFgColor.Text);
        ce.KeywordColor      = NormalizeColor(TxtKwColor.Text);
        ce.StringColor       = NormalizeColor(TxtStrColor.Text);
        ce.CommentColor      = NormalizeColor(TxtCmtColor.Text);
        ce.NumberColor       = NormalizeColor(TxtNumColor.Text);
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

        // Resolve the named Border via Tag
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
        // Attempt to parse — discard if invalid so we don't store garbage
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
