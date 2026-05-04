// ==========================================================
// Project: WpfHexEditor.Core.Options
// File: Pages/DocumentEditorOptionsPage.xaml.cs
// Description:
//     Options page for the Document Editor.
//     IOptionsPage implementation: Load / Flush / Changed.
//     Sections: Appearance, Sync, Hover, Auto-save, Editing,
//               Hex Highlight Colors.
// ==========================================================

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfHexEditor.Core;
using WpfHexEditor.Editor.DocumentEditor.Core.Options;
using ColorPickerControl = WpfHexEditor.ColorPicker.Controls.ColorPicker;

namespace WpfHexEditor.Core.Options.Pages;

public sealed partial class DocumentEditorOptionsPage : UserControl, IOptionsPage
{
    public event EventHandler? Changed;
    private bool _loading;

    public DocumentEditorOptionsPage()
    {
        InitializeComponent();

        // Populate view mode combo
        ViewModeCombo.Items.Add(new ComboBoxItem { Content = LoadStr("DocOpt_ViewMode_TextOnly"), Tag = DocumentViewMode.TextOnly });
        ViewModeCombo.Items.Add(new ComboBoxItem { Content = LoadStr("DocOpt_ViewMode_Structure"), Tag = DocumentViewMode.Structure });
        ViewModeCombo.Items.Add(new ComboBoxItem { Content = LoadStr("DocOpt_ViewMode_Focus"),    Tag = DocumentViewMode.Focus });

        // Populate render mode combo
        RenderModeCombo.Items.Add(new ComboBoxItem { Content = LoadStr("DocOpt_RenderMode_Page"),    Tag = DocumentRenderMode.Page });
        RenderModeCombo.Items.Add(new ComboBoxItem { Content = LoadStr("DocOpt_RenderMode_Draft"),   Tag = DocumentRenderMode.Draft });
        RenderModeCombo.Items.Add(new ComboBoxItem { Content = LoadStr("DocOpt_RenderMode_Outline"), Tag = DocumentRenderMode.Outline });

        WireAutoCheck(ChkBlockHighlight,  CpBlockHighlight);
        WireAutoCheck(ChkSelectedBlock,   CpSelectedBlock);
        WireAutoCheck(ChkForensicAlert,   CpForensicAlert);
    }

    private static string LoadStr(string key)
        => Application.Current?.TryFindResource(key) as string ?? key;

    private void WireAutoCheck(CheckBox chk, ColorPickerControl cp)
    {
        cp.ColorChanged += (_, _) =>
        {
            if (!_loading) chk.IsChecked = true;
        };
    }

    // ── IOptionsPage ──────────────────────────────────────────────────────────

    public void Load(AppSettings s)
    {
        _loading = true;
        try
        {
            var de = s.DocumentEditor;

            TxtFontSize.Text        = de.DefaultTextFontSize.ToString("F0");
            SelectComboByTag(ViewModeCombo,   de.DefaultViewMode);
            SelectComboByTag(RenderModeCombo, de.DefaultRenderMode);
            CheckScrollMarkers.IsChecked  = de.ShowScrollMarkers;
            CheckMiniMap.IsChecked        = de.ShowMiniMap;
            CheckForensicGutter.IsChecked = de.ShowForensicGutter;

            TxtSyncThrottle.Text    = de.SyncThrottleMs.ToString();
            CheckSyncTextToHex.IsChecked = de.SyncTextToHex;
            CheckSyncHexToText.IsChecked = de.SyncHexToText;

            CheckHoverTooltip.IsChecked = de.ShowBlockHoverTooltip;
            TxtHoverDelay.Text          = de.HoverDelayMs.ToString();

            CheckAutoSave.IsChecked    = de.AutoSaveEnabled;
            TxtAutoSaveInterval.Text   = de.AutoSaveIntervalSeconds.ToString();

            TxtIndentWidth.Text = de.DefaultIndentWidth.ToString();

            LoadColorPicker(ChkBlockHighlight, CpBlockHighlight, de.BlockHighlightColor);
            LoadColorPicker(ChkSelectedBlock,  CpSelectedBlock,  de.SelectedBlockColor);
            LoadColorPicker(ChkForensicAlert,  CpForensicAlert,  de.ForensicAlertColor);
        }
        finally { _loading = false; }
    }

    public void Flush(AppSettings s)
    {
        var de = s.DocumentEditor;

        de.DefaultTextFontSize   = ParseDouble(TxtFontSize.Text, 14.0);
        de.DefaultViewMode       = ReadComboTag(ViewModeCombo,   DocumentViewMode.TextOnly);
        de.DefaultRenderMode     = ReadComboTag(RenderModeCombo, DocumentRenderMode.Page);
        de.ShowScrollMarkers     = CheckScrollMarkers.IsChecked  == true;
        de.ShowMiniMap           = CheckMiniMap.IsChecked        == true;
        de.ShowForensicGutter    = CheckForensicGutter.IsChecked == true;

        de.SyncThrottleMs  = ParseInt(TxtSyncThrottle.Text, 150);
        de.SyncTextToHex   = CheckSyncTextToHex.IsChecked == true;
        de.SyncHexToText   = CheckSyncHexToText.IsChecked == true;

        de.ShowBlockHoverTooltip = CheckHoverTooltip.IsChecked == true;
        de.HoverDelayMs          = ParseInt(TxtHoverDelay.Text, 400);

        de.AutoSaveEnabled        = CheckAutoSave.IsChecked == true;
        de.AutoSaveIntervalSeconds = Math.Max(30, ParseInt(TxtAutoSaveInterval.Text, 60));

        de.DefaultIndentWidth = ParseInt(TxtIndentWidth.Text, 4);

        de.BlockHighlightColor = FlushColorPicker(ChkBlockHighlight, CpBlockHighlight);
        de.SelectedBlockColor  = FlushColorPicker(ChkSelectedBlock,  CpSelectedBlock);
        de.ForensicAlertColor  = FlushColorPicker(ChkForensicAlert,  CpForensicAlert);
    }

    // ── Control handlers ─────────────────────────────────────────────────────

    private void RaiseChanged()
    {
        if (!_loading) Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnCheckChanged(object sender, RoutedEventArgs e)          => RaiseChanged();
    private void OnComboChanged(object sender, SelectionChangedEventArgs e) => RaiseChanged();
    private void OnTextLostFocus(object sender, RoutedEventArgs e)          => RaiseChanged();
    private void OnColorCheckChanged(object sender, RoutedEventArgs e)      => RaiseChanged();
    private void OnColorPickerChanged(object sender, Color e)               => RaiseChanged();

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static void SelectComboByTag<T>(ComboBox combo, T value)
    {
        foreach (ComboBoxItem item in combo.Items)
        {
            if (item.Tag is T t && EqualityComparer<T>.Default.Equals(t, value))
            {
                combo.SelectedItem = item;
                return;
            }
        }
        if (combo.Items.Count > 0)
            combo.SelectedIndex = 0;
    }

    private static T ReadComboTag<T>(ComboBox combo, T fallback)
        => combo.SelectedItem is ComboBoxItem { Tag: T t } ? t : fallback;

    private static double ParseDouble(string text, double fallback)
        => double.TryParse(text, out double v) && v > 0 ? v : fallback;

    private static int ParseInt(string text, int fallback)
        => int.TryParse(text, out int v) && v > 0 ? v : fallback;

    private static void LoadColorPicker(CheckBox chk, ColorPickerControl cp, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            chk.IsChecked = false;
            cp.SelectedColor = Colors.Transparent;
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
            cp.SelectedColor = Colors.Transparent;
        }
    }

    private static string FlushColorPicker(CheckBox chk, ColorPickerControl cp)
    {
        if (chk.IsChecked != true) return string.Empty;
        var c = cp.SelectedColor;
        return $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
    }
}
