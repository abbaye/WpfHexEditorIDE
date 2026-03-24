// ==========================================================
// Project: WpfHexEditor.App
// File: Options/CommandPaletteOptionsPage.cs
// Description:
//     Options page for configuring the Command Palette (Ctrl+Shift+P) behaviour.
//     Provides 5 sections: Appearance, Description, Search, Recents, Modes/Prefixes.
//
// Architecture Notes:
//     Code-behind-only UserControl (no XAML) implementing IOptionsPage.
//     No constructor arguments — registered as a factory via OptionsPageRegistry.
//     Load/Flush read and write directly to AppSettings.CommandPalette.
// ==========================================================

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using WpfHexEditor.Options;

namespace WpfHexEditor.App.Options;

/// <summary>
/// IDE options page — Command Palette › Général.
/// Lets the user configure all fuzzy-bar behaviours.
/// </summary>
public sealed class CommandPaletteOptionsPage : UserControl, IOptionsPage
{
    // ── IOptionsPage ────────────────────────────────────────────────────────

    public event EventHandler? Changed;

    // ── UI fields ───────────────────────────────────────────────────────────

    private readonly Slider       _widthSlider;
    private readonly TextBlock    _widthLabel;
    private readonly CheckBox     _showIcons;
    private readonly CheckBox     _showGestures;
    private readonly CheckBox     _showHeaders;

    private readonly RadioButton  _descNone;
    private readonly RadioButton  _descTooltip;
    private readonly RadioButton  _descPanel;

    private readonly CheckBox     _highlight;
    private readonly CheckBox     _contextBoost;
    private readonly TextBox      _maxResults;
    private readonly TextBox      _debounce;
    private readonly TextBox      _maxGrepResults;
    private readonly TextBox      _maxGrepFileMb;

    private readonly CheckBox     _showRecents;
    private readonly CheckBox     _freqBoost;
    private readonly TextBox      _recentCount;

    private readonly ComboBox     _defaultMode;

    // ── Construction ────────────────────────────────────────────────────────

    public CommandPaletteOptionsPage()
    {
        Padding = new Thickness(16);

        // Merge DialogStyles locally so implicit TextBox/Button/CheckBox/RadioButton styles
        // survive ApplyTheme() clearing Application.Resources.
        Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri(
                "pack://application:,,,/WpfHexEditor.App;component/Themes/DialogStyles.xaml")
        });

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };

        var stack = new StackPanel { Orientation = Orientation.Vertical };

        // ── Header ──────────────────────────────────────────────────────────
        stack.Children.Add(MakePageHeader("Command Palette"));

        // ── Section: APPEARANCE ─────────────────────────────────────────────
        stack.Children.Add(MakeSectionHeader("APPEARANCE"));

        var widthRow = MakeLabeledRow("Window width (px)");
        _widthSlider = new Slider
        {
            Minimum = 400, Maximum = 1200, TickFrequency = 40,
            IsSnapToTickEnabled = true, Width = 200,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _widthSlider.ValueChanged += OnAnyChanged;
        _widthLabel = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), Width = 36 };
        _widthSlider.ValueChanged += (_, e) => _widthLabel.Text = $"{(int)e.NewValue}";
        widthRow.Children.Add(_widthSlider);
        widthRow.Children.Add(_widthLabel);
        stack.Children.Add(widthRow);

        _showIcons    = MakeCheckBox("Show command icons",                OnAnyChanged);
        _showGestures = MakeCheckBox("Show keyboard shortcuts (gestures)", OnAnyChanged);
        _showHeaders  = MakeCheckBox("Show category group headers",        OnAnyChanged);
        stack.Children.Add(_showIcons);
        stack.Children.Add(_showGestures);
        stack.Children.Add(_showHeaders);

        // ── Section: DESCRIPTION ────────────────────────────────────────────
        stack.Children.Add(MakeSectionHeader("COMMAND DESCRIPTION"));

        _descNone    = MakeRadio("None",                  "descMode", OnAnyChanged);
        _descTooltip = MakeRadio("Tooltip (default)",     "descMode", OnAnyChanged);
        _descPanel   = MakeRadio("Bottom panel (48 px)",  "descMode", OnAnyChanged);
        stack.Children.Add(_descNone);
        stack.Children.Add(_descTooltip);
        stack.Children.Add(_descPanel);

        // ── Section: SEARCH ─────────────────────────────────────────────────
        stack.Children.Add(MakeSectionHeader("SEARCH"));

        _highlight    = MakeCheckBox("Highlight matched characters",          OnAnyChanged);
        _contextBoost = MakeCheckBox("Context boost (active editor category)", OnAnyChanged);
        stack.Children.Add(_highlight);
        stack.Children.Add(_contextBoost);

        var maxRow = MakeLabeledRow("Max results");
        _maxResults = MakeIntBox(OnAnyChanged);
        maxRow.Children.Add(_maxResults);
        stack.Children.Add(maxRow);

        var debounceRow = MakeLabeledRow("Search delay (ms)");
        _debounce = MakeIntBox(OnAnyChanged);
        debounceRow.Children.Add(_debounce);
        stack.Children.Add(debounceRow);

        var grepResultsRow = MakeLabeledRow("Max grep results (% mode)");
        _maxGrepResults = MakeIntBox(OnAnyChanged);
        grepResultsRow.Children.Add(_maxGrepResults);
        stack.Children.Add(grepResultsRow);

        var grepFileSizeRow = MakeLabeledRow("Max file size for grep (MB)");
        _maxGrepFileMb = MakeIntBox(OnAnyChanged);
        grepFileSizeRow.Children.Add(_maxGrepFileMb);
        stack.Children.Add(grepFileSizeRow);

        // ── Section: RECENT COMMANDS ────────────────────────────────────────
        stack.Children.Add(MakeSectionHeader("RECENT COMMANDS"));

        _showRecents = MakeCheckBox("Show recent commands when query is empty", OnAnyChanged);
        _freqBoost   = MakeCheckBox("Frequency boost (recently used commands)",  OnAnyChanged);
        stack.Children.Add(_showRecents);
        stack.Children.Add(_freqBoost);

        var recentRow = MakeLabeledRow("Number of recents shown");
        _recentCount = MakeIntBox(OnAnyChanged);
        recentRow.Children.Add(_recentCount);
        stack.Children.Add(recentRow);

        // ── Section: MODES / PREFIXES ───────────────────────────────────────
        stack.Children.Add(MakeSectionHeader("MODES / PREFIXES"));

        var modeRow = MakeLabeledRow("Default mode on open");
        _defaultMode = new ComboBox { Width = 160, Margin = new Thickness(0, 3, 0, 3) };
        _defaultMode.Items.Add(new ComboBoxItem { Content = "Commands (default)", Tag = "" });
        _defaultMode.Items.Add(new ComboBoxItem { Content = "> Commands",          Tag = ">" });
        _defaultMode.Items.Add(new ComboBoxItem { Content = "@ Symbols (LSP)",    Tag = "@" });
        _defaultMode.Items.Add(new ComboBoxItem { Content = ": Go to line",        Tag = ":" });
        _defaultMode.Items.Add(new ComboBoxItem { Content = "# Files (solution)",  Tag = "#" });
        _defaultMode.Items.Add(new ComboBoxItem { Content = "% Grep (content)",   Tag = "%" });
        _defaultMode.SelectionChanged += OnAnyChanged;
        modeRow.Children.Add(_defaultMode);
        stack.Children.Add(modeRow);

        stack.Children.Add(MakeInfoText("Tip  @=Symbols   :=Line   #=Files   %=Grep   ?=Help   Tab=Cycle"));

        // ── Reset button ────────────────────────────────────────────────────
        stack.Children.Add(new Separator { Margin = new Thickness(0, 12, 0, 8) });

        var resetBtn = new Button
        {
            Content = "Reset to defaults",
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(12, 4, 12, 4),
        };
        resetBtn.Click += OnResetClick;
        stack.Children.Add(resetBtn);

        scroll.Content = stack;
        Content = scroll;
    }

    // ── IOptionsPage.Load ───────────────────────────────────────────────────

    public void Load(AppSettings settings)
    {
        var cp = settings.CommandPalette;

        _widthSlider.Value      = cp.WindowWidth;
        _widthLabel.Text        = $"{cp.WindowWidth}";
        _showIcons.IsChecked    = cp.ShowIconGlyphs;
        _showGestures.IsChecked = cp.ShowGestureHints;
        _showHeaders.IsChecked  = cp.ShowCategoryHeaders;

        _descNone.IsChecked    = cp.DescriptionMode == CpDescriptionMode.None;
        _descTooltip.IsChecked = cp.DescriptionMode == CpDescriptionMode.Tooltip;
        _descPanel.IsChecked   = cp.DescriptionMode == CpDescriptionMode.BottomPanel;

        _highlight.IsChecked    = cp.HighlightMatchChars;
        _contextBoost.IsChecked = cp.ContextBoostEnabled;
        _maxResults.Text        = cp.MaxResults.ToString();
        _debounce.Text          = cp.SearchDebounceMs.ToString();
        _maxGrepResults.Text    = cp.MaxGrepResults.ToString();
        _maxGrepFileMb.Text     = (cp.MaxGrepFileSizeBytes / 1_000_000).ToString();

        _showRecents.IsChecked = cp.ShowRecentCommands;
        _freqBoost.IsChecked   = cp.FrequencyBoostEnabled;
        _recentCount.Text      = cp.RecentCommandsCount.ToString();

        SelectModeCombo(cp.DefaultMode);
    }

    // ── IOptionsPage.Flush ──────────────────────────────────────────────────

    public void Flush(AppSettings settings)
    {
        var cp = settings.CommandPalette;

        cp.WindowWidth        = (int)_widthSlider.Value;
        cp.ShowIconGlyphs     = _showIcons.IsChecked   == true;
        cp.ShowGestureHints   = _showGestures.IsChecked == true;
        cp.ShowCategoryHeaders= _showHeaders.IsChecked  == true;

        cp.DescriptionMode = _descPanel.IsChecked   == true ? CpDescriptionMode.BottomPanel
                           : _descNone.IsChecked    == true ? CpDescriptionMode.None
                           : CpDescriptionMode.Tooltip;

        cp.HighlightMatchChars   = _highlight.IsChecked    == true;
        cp.ContextBoostEnabled   = _contextBoost.IsChecked == true;
        cp.MaxResults            = ParseInt(_maxResults.Text, 50);
        cp.SearchDebounceMs      = ParseInt(_debounce.Text,    0);
        cp.MaxGrepResults        = ParseInt(_maxGrepResults.Text, 100);
        cp.MaxGrepFileSizeBytes  = (long)ParseInt(_maxGrepFileMb.Text, 2) * 1_000_000L;

        cp.ShowRecentCommands   = _showRecents.IsChecked == true;
        cp.FrequencyBoostEnabled= _freqBoost.IsChecked   == true;
        cp.RecentCommandsCount  = ParseInt(_recentCount.Text, 5);

        cp.DefaultMode = (_defaultMode.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private void OnAnyChanged(object? sender, EventArgs e) => Changed?.Invoke(this, EventArgs.Empty);
    private void OnAnyChanged(object? sender, RoutedPropertyChangedEventArgs<double> e) => Changed?.Invoke(this, EventArgs.Empty);
    private void OnAnyChanged(object? sender, SelectionChangedEventArgs e)               => Changed?.Invoke(this, EventArgs.Empty);

    private void OnResetClick(object sender, RoutedEventArgs e)
    {
        // Reload from a fresh defaults instance
        var defaults = new CommandPaletteSettings();
        var tmp = new AppSettings { CommandPalette = defaults };
        Load(tmp);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void SelectModeCombo(string mode)
    {
        foreach (ComboBoxItem item in _defaultMode.Items)
        {
            if (item.Tag as string == mode)
            { _defaultMode.SelectedItem = item; return; }
        }
        _defaultMode.SelectedIndex = 0;
    }

    private static int ParseInt(string text, int fallback)
        => int.TryParse(text, out var v) ? v : fallback;

    // ── UI factory helpers ───────────────────────────────────────────────────

    private static TextBlock MakePageHeader(string text) => new()
    {
        Text       = text,
        FontSize   = 16,
        FontWeight = FontWeights.SemiBold,
        Margin     = new Thickness(0, 0, 0, 12),
    };

    private TextBlock MakeSectionHeader(string text)
    {
        var tb = new TextBlock
        {
            Text       = text,
            FontSize   = 10,
            FontWeight = FontWeights.Bold,
            Margin     = new Thickness(0, 14, 0, 4),
        };
        tb.SetResourceReference(TextBlock.ForegroundProperty, "CP_SecondaryTextBrush");
        return tb;
    }

    private static CheckBox MakeCheckBox(string label, EventHandler handler)
    {
        var cb = new CheckBox { Content = label, Margin = new Thickness(0, 3, 0, 3) };
        cb.Checked   += (s, e) => handler(s, e);
        cb.Unchecked += (s, e) => handler(s, e);
        return cb;
    }

    private static RadioButton MakeRadio(string label, string group, EventHandler handler)
    {
        var rb = new RadioButton { Content = label, GroupName = group, Margin = new Thickness(0, 3, 0, 3) };
        rb.Checked += (s, e) => handler(s, e);
        return rb;
    }

    private static StackPanel MakeLabeledRow(string label)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
        row.Children.Add(new TextBlock
        {
            Text  = label,
            Width = 220,
            VerticalAlignment = VerticalAlignment.Center,
        });
        return row;
    }

    private static TextBox MakeIntBox(EventHandler handler)
    {
        var tb = new TextBox { Width = 60, Margin = new Thickness(0, 0, 0, 0) };
        tb.TextChanged += (s, e) => handler(s, e);
        return tb;
    }

    private TextBlock MakeInfoText(string text)
    {
        var tb = new TextBlock
        {
            Text      = text,
            FontStyle = FontStyles.Italic,
            Margin    = new Thickness(0, 4, 0, 0),
            FontSize  = 11,
        };
        tb.SetResourceReference(TextBlock.ForegroundProperty, "CP_SecondaryTextBrush");
        return tb;
    }
}
