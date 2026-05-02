// ==========================================================
// Project: WpfHexEditor.Core.Options
// File: Pages/StructureEditorOptionsPage.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Description:
//     Options page for the Format Definition editor (.whfmt).
//     Code-only UserControl implementing IOptionsPage.
//     Registered under "Format Editor" / "General".
// ==========================================================

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using WpfHexEditor.Core.Options.Properties;

namespace WpfHexEditor.Core.Options.Pages;

/// <summary>
/// IDE options page — Format Editor › General.
/// </summary>
public sealed class StructureEditorOptionsPage : UserControl, IOptionsPage
{
    public event EventHandler? Changed;

    // ── Controls ──────────────────────────────────────────────────────────

    private readonly CheckBox _codePreviewVisible;
    private readonly ComboBox _codePreviewDock;
    private readonly Slider   _codePreviewDebounce;
    private readonly Slider   _validationDebounce;
    private readonly CheckBox _autoValidation;
    private readonly CheckBox _autoIncrementVersion;
    private readonly CheckBox _autoFillLastUpdated;
    private readonly ComboBox _defaultEndianness;
    private readonly Slider   _testPanelMaxMb;

    private bool _loading;

    // ── Construction ─────────────────────────────────────────────────────

    public StructureEditorOptionsPage()
    {
        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(20),
        };

        var stack = new StackPanel { Orientation = Orientation.Vertical };

        // Header
        var header = new TextBlock
        {
            Text       = "Format Editor — General",
            FontSize   = 16,
            FontWeight = FontWeights.SemiBold,
            Margin     = new Thickness(0, 0, 0, 4),
        };
        header.SetResourceReference(TextBlock.ForegroundProperty, "DockMenuForegroundBrush");
        stack.Children.Add(header);

        var subtitle = new TextBlock
        {
            Text         = "Settings for the interactive .whfmt format definition editor.",
            TextWrapping = TextWrapping.Wrap,
            FontSize     = 11,
            Opacity      = 0.65,
            Margin       = new Thickness(0, 0, 0, 20),
        };
        subtitle.SetResourceReference(TextBlock.ForegroundProperty, "DockMenuForegroundBrush");
        stack.Children.Add(subtitle);

        // ── CODE PREVIEW ─────────────────────────────────────────────────
        stack.Children.Add(MakeSectionHeader("CODE PREVIEW"));

        _codePreviewVisible = MakeCheckBox("Show live JSON preview pane when a file is opened");
        stack.Children.Add(_codePreviewVisible);

        var dockRow = MakeLabelRow("Default dock position");
        _codePreviewDock = new ComboBox { Width = 120, FontSize = 11 };
        _codePreviewDock.Items.Add(OptionsResources.Opt_Position_Right);
        _codePreviewDock.Items.Add(OptionsResources.Opt_Position_Left);
        _codePreviewDock.Items.Add(OptionsResources.Opt_Position_Bottom);
        _codePreviewDock.Items.Add(OptionsResources.Opt_Position_Top);
        _codePreviewDock.SelectedIndex = 0;
        _codePreviewDock.SetResourceReference(ComboBox.BackgroundProperty, "DockMenuBackgroundBrush");
        _codePreviewDock.SetResourceReference(ComboBox.ForegroundProperty, "DockMenuForegroundBrush");
        _codePreviewDock.SelectionChanged += OnChanged;
        dockRow.Children.Add(_codePreviewDock);
        stack.Children.Add(dockRow);

        var debounceRow = MakeLabelRow("Refresh delay (ms)");
        _codePreviewDebounce = MakeSlider(100, 2000, 400);
        var debounceLabel = new TextBlock { FontSize = 11, Width = 40, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        debounceLabel.SetResourceReference(TextBlock.ForegroundProperty, "DockMenuForegroundBrush");
        _codePreviewDebounce.ValueChanged += (_, _) =>
        {
            debounceLabel.Text = $"{(int)_codePreviewDebounce.Value} ms";
            if (!_loading) Changed?.Invoke(this, EventArgs.Empty);
        };
        debounceRow.Children.Add(_codePreviewDebounce);
        debounceRow.Children.Add(debounceLabel);
        stack.Children.Add(debounceRow);

        // ── VALIDATION ────────────────────────────────────────────────────
        stack.Children.Add(MakeSectionHeader("VALIDATION"));

        _autoValidation = MakeCheckBox("Run validation automatically after content changes");
        stack.Children.Add(_autoValidation);

        var valDebounceRow = MakeLabelRow("Validation delay (ms)");
        _validationDebounce = MakeSlider(100, 2000, 500);
        var valDebounceLabel = new TextBlock { FontSize = 11, Width = 40, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        valDebounceLabel.SetResourceReference(TextBlock.ForegroundProperty, "DockMenuForegroundBrush");
        _validationDebounce.ValueChanged += (_, _) =>
        {
            valDebounceLabel.Text = $"{(int)_validationDebounce.Value} ms";
            if (!_loading) Changed?.Invoke(this, EventArgs.Empty);
        };
        valDebounceRow.Children.Add(_validationDebounce);
        valDebounceRow.Children.Add(valDebounceLabel);
        stack.Children.Add(valDebounceRow);

        // ── AUTHORING ─────────────────────────────────────────────────────
        stack.Children.Add(MakeSectionHeader("AUTHORING"));

        _autoIncrementVersion = MakeCheckBox("Auto-increment patch version on save (e.g. 2.02 → 2.03)");
        stack.Children.Add(_autoIncrementVersion);

        _autoFillLastUpdated = MakeCheckBox("Auto-fill \"Last Updated\" date on save");
        stack.Children.Add(_autoFillLastUpdated);

        var endiRow = MakeLabelRow("Default byte order for new blocks");
        _defaultEndianness = new ComboBox { Width = 100, FontSize = 11 };
        _defaultEndianness.Items.Add(OptionsResources.Opt_Endian_Little);
        _defaultEndianness.Items.Add(OptionsResources.Opt_Endian_Big);
        _defaultEndianness.SelectedIndex = 0;
        _defaultEndianness.SetResourceReference(ComboBox.BackgroundProperty, "DockMenuBackgroundBrush");
        _defaultEndianness.SetResourceReference(ComboBox.ForegroundProperty, "DockMenuForegroundBrush");
        _defaultEndianness.SelectionChanged += OnChanged;
        endiRow.Children.Add(_defaultEndianness);
        stack.Children.Add(endiRow);

        // ── TEST PANEL ────────────────────────────────────────────────────
        stack.Children.Add(MakeSectionHeader("TEST PANEL"));

        var mbRow = MakeLabelRow("Maximum file size to load");
        _testPanelMaxMb = MakeSlider(1, 100, 10);
        _testPanelMaxMb.TickFrequency = 5;
        _testPanelMaxMb.IsSnapToTickEnabled = false;
        var mbLabel = new TextBlock { FontSize = 11, Width = 60, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        mbLabel.SetResourceReference(TextBlock.ForegroundProperty, "DockMenuForegroundBrush");
        _testPanelMaxMb.ValueChanged += (_, _) =>
        {
            mbLabel.Text = $"{(int)_testPanelMaxMb.Value} MB";
            if (!_loading) Changed?.Invoke(this, EventArgs.Empty);
        };
        mbRow.Children.Add(_testPanelMaxMb);
        mbRow.Children.Add(mbLabel);
        stack.Children.Add(mbRow);

        scroll.Content = stack;
        Content = scroll;
    }

    // ── IOptionsPage ─────────────────────────────────────────────────────

    public void Load(AppSettings settings)
    {
        _loading = true;
        try
        {
            var s = settings.StructureEditor;

            _codePreviewVisible.IsChecked = s.CodePreviewVisibleByDefault;
            _codePreviewDock.SelectedItem = s.CodePreviewDock;
            if (_codePreviewDock.SelectedIndex < 0) _codePreviewDock.SelectedIndex = 0;

            _codePreviewDebounce.Value  = Math.Clamp(s.CodePreviewDebounceMs, 100, 2000);
            _autoValidation.IsChecked   = s.AutoValidation;
            _validationDebounce.Value   = Math.Clamp(s.ValidationDebounceMs, 100, 2000);
            _autoIncrementVersion.IsChecked = s.AutoIncrementVersion;
            _autoFillLastUpdated.IsChecked  = s.AutoFillLastUpdated;
            _defaultEndianness.SelectedItem = s.DefaultEndianness;
            if (_defaultEndianness.SelectedIndex < 0) _defaultEndianness.SelectedIndex = 0;

            _testPanelMaxMb.Value = Math.Clamp(s.TestPanelMaxBytes / (1024.0 * 1024.0), 1, 100);
        }
        finally { _loading = false; }
    }

    public void Flush(AppSettings settings)
    {
        var s = settings.StructureEditor;

        s.CodePreviewVisibleByDefault = _codePreviewVisible.IsChecked == true;
        s.CodePreviewDock             = _codePreviewDock.SelectedItem?.ToString() ?? "Right";
        s.CodePreviewDebounceMs       = (int)_codePreviewDebounce.Value;
        s.AutoValidation              = _autoValidation.IsChecked == true;
        s.ValidationDebounceMs        = (int)_validationDebounce.Value;
        s.AutoIncrementVersion        = _autoIncrementVersion.IsChecked == true;
        s.AutoFillLastUpdated         = _autoFillLastUpdated.IsChecked == true;
        s.DefaultEndianness           = _defaultEndianness.SelectedItem?.ToString() ?? "little";
        s.TestPanelMaxBytes           = (long)_testPanelMaxMb.Value * 1024L * 1024L;
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private void OnChanged(object? sender, EventArgs e)
    {
        if (!_loading) Changed?.Invoke(this, EventArgs.Empty);
    }

    private CheckBox MakeCheckBox(string label)
    {
        var cb = new CheckBox { Content = label, FontSize = 11, Margin = new Thickness(0, 4, 0, 4) };
        cb.SetResourceReference(CheckBox.ForegroundProperty, "DockMenuForegroundBrush");
        cb.Checked   += OnChanged;
        cb.Unchecked += OnChanged;
        return cb;
    }

    private static StackPanel MakeLabelRow(string label)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin      = new Thickness(0, 6, 0, 6),
        };
        var lbl = new TextBlock
        {
            Text              = label,
            FontSize          = 11,
            Width             = 240,
            VerticalAlignment = VerticalAlignment.Center,
        };
        lbl.SetResourceReference(TextBlock.ForegroundProperty, "DockMenuForegroundBrush");
        row.Children.Add(lbl);
        return row;
    }

    private static Slider MakeSlider(double min, double max, double value) => new()
    {
        Minimum           = min,
        Maximum           = max,
        Value             = value,
        Width             = 160,
        VerticalAlignment = VerticalAlignment.Center,
        Margin            = new Thickness(0, 0, 8, 0),
    };

    private static TextBlock MakeSectionHeader(string text) => OptionsPageHelper.SectionHeader(text);
}
