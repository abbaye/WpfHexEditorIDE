// ==========================================================
// Project: WpfHexEditor.Core.Options
// File: Pages/CodeEditorNavigationPage.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-25
// Description:
//     Options page for Code Editor navigation features.
//     Section 1 — Find All References (#157): results mode, grouping, filters, limits.
//     Section 2 — Sticky Scroll (#160): enabled, max lines, syntax highlight, opacity.
//
// Architecture Notes:
//     Code-only UserControl implementing IOptionsPage.
//     Reads/writes AppSettings.CodeEditorDefaults.References and .StickyScroll.
//     Registered statically in OptionsPageRegistry under "Code Editor" / "Navigation".
// ==========================================================

using System;
using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Core.Options;

namespace WpfHexEditor.Core.Options.Pages;

/// <summary>
/// IDE options page — Code Editor › Navigation.
/// Covers Find All References display preferences and Sticky Scroll settings.
/// </summary>
public sealed class CodeEditorNavigationPage : UserControl, IOptionsPage
{
    public event EventHandler? Changed;

    // ── Find All References fields ───────────────────────────────────────────
    private readonly ComboBox  _refsResultsMode;
    private readonly CheckBox  _refsGroupByFile;
    private readonly CheckBox  _refsInComments;
    private readonly CheckBox  _refsInStrings;
    private readonly TextBox   _refsMaxResults;
    private readonly CheckBox  _refsPreviewHover;

    // ── Sticky Scroll fields ─────────────────────────────────────────────────
    private readonly CheckBox  _stickyEnabled;
    private readonly Slider    _stickyMaxLines;
    private readonly TextBlock _stickyMaxLinesLabel;
    private readonly CheckBox  _stickySyntax;
    private readonly CheckBox  _stickyClickNav;
    private readonly Slider    _stickyOpacity;
    private readonly TextBlock _stickyOpacityLabel;
    private readonly Slider    _stickyMinLines;
    private readonly TextBlock _stickyMinLinesLabel;

    public CodeEditorNavigationPage()
    {
        Padding = new Thickness(16);

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };

        var stack = new StackPanel { Orientation = Orientation.Vertical };

        stack.Children.Add(MakePageHeader("Code Editor — Navigation"));

        // ── FIND ALL REFERENCES ──────────────────────────────────────────────
        stack.Children.Add(MakeSectionHeader("FIND ALL REFERENCES (Shift+F12)"));

        var modeRow = MakeLabeledRow("Results display mode");
        _refsResultsMode = new ComboBox { Width = 160, Margin = new Thickness(0, 3, 0, 3) };
        _refsResultsMode.Items.Add(new ComboBoxItem { Content = "Docked Panel (default)", Tag = "DockedPanel"   });
        _refsResultsMode.Items.Add(new ComboBoxItem { Content = "Inline Popup",           Tag = "InlinePopup"  });
        _refsResultsMode.SelectionChanged += OnAnyChanged;
        modeRow.Children.Add(_refsResultsMode);
        stack.Children.Add(modeRow);

        _refsGroupByFile  = MakeCheckBox("Group results by file",                    OnAnyChanged);
        _refsInComments   = MakeCheckBox("Include occurrences in comments",          OnAnyChanged);
        _refsInStrings    = MakeCheckBox("Include occurrences in string literals",   OnAnyChanged);
        _refsPreviewHover = MakeCheckBox("Preview source location on hover",         OnAnyChanged);
        stack.Children.Add(_refsGroupByFile);
        stack.Children.Add(_refsInComments);
        stack.Children.Add(_refsInStrings);
        stack.Children.Add(_refsPreviewHover);

        var refsMaxRow = MakeLabeledRow("Maximum results");
        _refsMaxResults = MakeIntBox(OnAnyChanged);
        refsMaxRow.Children.Add(_refsMaxResults);
        stack.Children.Add(refsMaxRow);

        // ── STICKY SCROLL ────────────────────────────────────────────────────
        stack.Children.Add(MakeSectionHeader("STICKY SCROLL"));

        _stickyEnabled = MakeCheckBox("Enable sticky scroll header", OnAnyChanged);
        stack.Children.Add(_stickyEnabled);

        var maxLinesRow = MakeLabeledRow("Max lines in header (1–10)");
        _stickyMaxLines = MakeSlider(1, 10, 1, OnAnyChanged);
        _stickyMaxLinesLabel = MakeSliderLabel();
        _stickyMaxLines.ValueChanged += (_, e) => _stickyMaxLinesLabel.Text = $"{(int)e.NewValue}";
        maxLinesRow.Children.Add(_stickyMaxLines);
        maxLinesRow.Children.Add(_stickyMaxLinesLabel);
        stack.Children.Add(maxLinesRow);

        _stickySyntax   = MakeCheckBox("Apply syntax highlighting in header",        OnAnyChanged);
        _stickyClickNav = MakeCheckBox("Click header line to navigate to scope start", OnAnyChanged);
        stack.Children.Add(_stickySyntax);
        stack.Children.Add(_stickyClickNav);

        var opacityRow = MakeLabeledRow("Header opacity (50–100 %)");
        _stickyOpacity = MakeSlider(0.5, 1.0, 0.05, OnAnyChanged);
        _stickyOpacityLabel = MakeSliderLabel();
        _stickyOpacity.ValueChanged += (_, e) => _stickyOpacityLabel.Text = $"{(int)(e.NewValue * 100)}%";
        opacityRow.Children.Add(_stickyOpacity);
        opacityRow.Children.Add(_stickyOpacityLabel);
        stack.Children.Add(opacityRow);

        var minLinesRow = MakeLabeledRow("Ignore scopes smaller than (lines)");
        _stickyMinLines = MakeSlider(2, 20, 1, OnAnyChanged);
        _stickyMinLinesLabel = MakeSliderLabel();
        _stickyMinLines.ValueChanged += (_, e) => _stickyMinLinesLabel.Text = $"{(int)e.NewValue}";
        minLinesRow.Children.Add(_stickyMinLines);
        minLinesRow.Children.Add(_stickyMinLinesLabel);
        stack.Children.Add(minLinesRow);

        scroll.Content = stack;
        Content = scroll;
    }

    // ── IOptionsPage.Load ────────────────────────────────────────────────────

    public void Load(AppSettings s)
    {
        var refs = s.CodeEditorDefaults.References;
        SelectComboByTag(_refsResultsMode, refs.ResultsMode.ToString());
        _refsGroupByFile.IsChecked  = refs.GroupByFile;
        _refsInComments.IsChecked   = refs.IncludeInComments;
        _refsInStrings.IsChecked    = refs.IncludeInStrings;
        _refsMaxResults.Text        = refs.MaxResults.ToString();
        _refsPreviewHover.IsChecked = refs.PreviewOnHover;

        var ss = s.CodeEditorDefaults.StickyScroll;
        _stickyEnabled.IsChecked  = ss.Enabled;
        _stickyMaxLines.Value     = Math.Clamp(ss.MaxLines, 1, 10);
        _stickyMaxLinesLabel.Text = $"{ss.MaxLines}";
        _stickySyntax.IsChecked   = ss.SyntaxHighlight;
        _stickyClickNav.IsChecked = ss.ClickToNavigate;
        _stickyOpacity.Value      = Math.Clamp(ss.Opacity, 0.5, 1.0);
        _stickyOpacityLabel.Text  = $"{(int)(ss.Opacity * 100)}%";
        _stickyMinLines.Value     = Math.Clamp(ss.MinScopeLines, 2, 20);
        _stickyMinLinesLabel.Text = $"{ss.MinScopeLines}";
    }

    // ── IOptionsPage.Flush ───────────────────────────────────────────────────

    public void Flush(AppSettings s)
    {
        var refs = s.CodeEditorDefaults.References;
        var modeTag = (_refsResultsMode.SelectedItem as ComboBoxItem)?.Tag as string ?? "DockedPanel";
        refs.ResultsMode = Enum.TryParse<ReferencesResultsMode>(modeTag, out var rm)
            ? rm : ReferencesResultsMode.DockedPanel;
        refs.GroupByFile       = _refsGroupByFile.IsChecked  == true;
        refs.IncludeInComments = _refsInComments.IsChecked   == true;
        refs.IncludeInStrings  = _refsInStrings.IsChecked    == true;
        refs.MaxResults        = ParseInt(_refsMaxResults.Text, 500);
        refs.PreviewOnHover    = _refsPreviewHover.IsChecked == true;

        var ss = s.CodeEditorDefaults.StickyScroll;
        ss.Enabled         = _stickyEnabled.IsChecked  == true;
        ss.MaxLines        = (int)_stickyMaxLines.Value;
        ss.SyntaxHighlight = _stickySyntax.IsChecked   == true;
        ss.ClickToNavigate = _stickyClickNav.IsChecked == true;
        ss.Opacity         = _stickyOpacity.Value;
        ss.MinScopeLines   = (int)_stickyMinLines.Value;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void OnAnyChanged(object? sender, EventArgs e)                                  => Changed?.Invoke(this, EventArgs.Empty);
    private void OnAnyChanged(object? sender, RoutedPropertyChangedEventArgs<double> e)    => Changed?.Invoke(this, EventArgs.Empty);
    private void OnAnyChanged(object? sender, SelectionChangedEventArgs e)                  => Changed?.Invoke(this, EventArgs.Empty);

    private static int ParseInt(string text, int fallback)
        => int.TryParse(text, out var v) ? v : fallback;

    private static void SelectComboByTag(ComboBox combo, string tag)
    {
        foreach (ComboBoxItem item in combo.Items)
        {
            if (item.Tag?.ToString() == tag)
            { combo.SelectedItem = item; return; }
        }
        if (combo.Items.Count > 0) combo.SelectedIndex = 0;
    }

    private static TextBlock MakePageHeader(string text) => new()
    {
        Text       = text,
        FontSize   = 16,
        FontWeight = FontWeights.SemiBold,
        Margin     = new Thickness(0, 0, 0, 12)
    };

    private static TextBlock MakeSectionHeader(string text) => OptionsPageHelper.SectionHeader(text);

    private static CheckBox MakeCheckBox(string label, EventHandler handler)
    {
        var cb = new CheckBox { Content = label, Margin = new Thickness(0, 3, 0, 3) };
        cb.Checked   += (s, e) => handler(s, e);
        cb.Unchecked += (s, e) => handler(s, e);
        return cb;
    }

    private static StackPanel MakeLabeledRow(string label)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
        row.Children.Add(new TextBlock { Text = label, Width = 240, VerticalAlignment = VerticalAlignment.Center });
        return row;
    }

    private static TextBox MakeIntBox(EventHandler handler)
    {
        var tb = new TextBox { Width = 60 };
        tb.TextChanged += (s, e) => handler(s, e);
        return tb;
    }

    private Slider MakeSlider(double min, double max, double tick, EventHandler handler)
    {
        var s = new Slider
        {
            Minimum = min, Maximum = max, TickFrequency = tick,
            IsSnapToTickEnabled = true, Width = 160,
            VerticalAlignment   = VerticalAlignment.Center
        };
        s.ValueChanged += (_, e) => handler(_, e);
        return s;
    }

    private static TextBlock MakeSliderLabel() => new()
    {
        Width             = 40,
        VerticalAlignment = VerticalAlignment.Center,
        Margin            = new Thickness(8, 0, 0, 0)
    };
}
