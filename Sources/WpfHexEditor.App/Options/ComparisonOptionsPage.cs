// Project      : WpfHexEditorControl
// File         : Options/ComparisonOptionsPage.cs
// Description  : Options page for the Compare Files feature.
//                Exposes view-mode defaults, fold threshold, minimap toggle, and history management.
// Architecture : Code-behind-only UserControl implementing IOptionsPage.
//                No XAML file — keeps the Options project free from WPF XAML dependencies.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfHexEditor.App.Properties;
using WpfHexEditor.Core.Options;

namespace WpfHexEditor.App.Options;

/// <summary>
/// Options page for "Compare Files" settings (view mode, folding, minimap, history).
/// Registered at startup via <c>OptionsPageRegistry.RegisterDynamic</c>.
/// </summary>
public sealed class ComparisonOptionsPage : UserControl, IOptionsPage
{
    private readonly ComparisonSettings _settings;
    private readonly AppSettingsService _settingsService;

    // ── Controls ─────────────────────────────────────────────────────────────
    private readonly ComboBox     _viewModeCombo;
    private readonly CheckBox     _foldCheck;
    private readonly Slider       _foldThresholdSlider;
    private readonly TextBlock    _foldThresholdLabel;
    private readonly CheckBox     _minimapCheck;
    private readonly CheckBox     _charLevelCheck;
    private readonly TextBlock    _historyCountLabel;
    private readonly Button       _clearHistoryBtn;

    // ── IOptionsPage ─────────────────────────────────────────────────────────
    public string PageTitle    => "Compare Files";
    public string CategoryName => "Editor";

    public event EventHandler? Changed;

    public void Load(AppSettings settings)
    {
        var s = settings.Comparison;
        _viewModeCombo.SelectedItem      = s.DefaultViewMode;
        _minimapCheck.IsChecked          = s.ShowMinimap;
        _charLevelCheck.IsChecked        = s.ShowCharLevelDiff;
        _foldCheck.IsChecked             = s.FoldIdenticalRegions;
        _foldThresholdSlider.Value       = s.FoldThreshold;
        _historyCountLabel.Text          = $"{s.RecentComparisons.Count} {AppResources.App_Options_SavedComparisons}";
    }

    public void Flush(AppSettings settings)
    {
        var s = settings.Comparison;
        s.DefaultViewMode      = _viewModeCombo.SelectedItem as string ?? "SideBySide";
        s.ShowMinimap          = _minimapCheck.IsChecked == true;
        s.ShowCharLevelDiff    = _charLevelCheck.IsChecked == true;
        s.FoldIdenticalRegions = _foldCheck.IsChecked == true;
        s.FoldThreshold        = (int)_foldThresholdSlider.Value;
    }

    public ComparisonOptionsPage(ComparisonSettings settings, AppSettingsService settingsService)
    {
        _settings        = settings;
        _settingsService = settingsService;

        var root = new StackPanel { Margin = new Thickness(12) };

        // ── Section: Diff View ────────────────────────────────────────────────
        root.Children.Add(MakeSectionHeader("Diff View"));

        // Default view mode
        root.Children.Add(MakeLabel("Default View Mode"));
        _viewModeCombo = new ComboBox { Margin = new Thickness(0, 2, 0, 8) };
        _viewModeCombo.Items.Add(AppResources.App_Comparison_ModeSideBySide);
        _viewModeCombo.Items.Add(AppResources.App_Comparison_ModeInline);
        _viewModeCombo.Items.Add(AppResources.App_Comparison_ModeHexText);
        _viewModeCombo.SelectedItem = _settings.DefaultViewMode;
        root.Children.Add(_viewModeCombo);

        // Minimap
        _minimapCheck = new CheckBox
        {
            Content    = AppResources.App_Options_ShowMinimap,
            IsChecked  = _settings.ShowMinimap,
            Margin     = new Thickness(0, 0, 0, 6)
        };
        root.Children.Add(_minimapCheck);

        // Char-level diff
        _charLevelCheck = new CheckBox
        {
            Content   = AppResources.App_Options_HighlightCharDiff,
            IsChecked = _settings.ShowCharLevelDiff,
            Margin    = new Thickness(0, 0, 0, 6)
        };
        root.Children.Add(_charLevelCheck);

        // ── Section: Folding ──────────────────────────────────────────────────
        root.Children.Add(MakeSectionHeader("Folding"));

        _foldCheck = new CheckBox
        {
            Content   = AppResources.App_Options_CollapseIdentical,
            IsChecked = _settings.FoldIdenticalRegions,
            Margin    = new Thickness(0, 0, 0, 6)
        };
        root.Children.Add(_foldCheck);

        var threshRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        threshRow.Children.Add(MakeLabel("Minimum identical lines to collapse:"));
        _foldThresholdLabel = new TextBlock
        {
            Text              = _settings.FoldThreshold.ToString(),
            Margin            = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight        = FontWeights.SemiBold,
            MinWidth          = 24
        };
        _foldThresholdSlider = new Slider
        {
            Minimum       = 2,
            Maximum       = 20,
            Value         = _settings.FoldThreshold,
            TickFrequency = 1,
            IsSnapToTickEnabled = true,
            Width         = 150,
            Margin        = new Thickness(8, 0, 4, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        _foldThresholdSlider.ValueChanged += (_, e) =>
            _foldThresholdLabel.Text = ((int)e.NewValue).ToString();

        threshRow.Children.Add(_foldThresholdSlider);
        threshRow.Children.Add(_foldThresholdLabel);
        root.Children.Add(threshRow);

        // ── Section: History ──────────────────────────────────────────────────
        root.Children.Add(MakeSectionHeader("History"));

        var histRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
        _historyCountLabel = new TextBlock
        {
            Text              = $"{_settings.RecentComparisons.Count} {AppResources.App_Options_SavedComparisons}",
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(0, 0, 12, 0)
        };
        _clearHistoryBtn = new Button
        {
            Content = AppResources.App_Options_ClearHistory,
            Padding = new Thickness(8, 3, 8, 3)
        };
        _clearHistoryBtn.Click += OnClearHistory;

        histRow.Children.Add(_historyCountLabel);
        histRow.Children.Add(_clearHistoryBtn);
        root.Children.Add(histRow);

        // ── Save button ───────────────────────────────────────────────────────
        var saveBtn = new Button
        {
            Content             = AppResources.App_Options_Save,
            Padding             = new Thickness(16, 4, 16, 4),
            Margin              = new Thickness(0, 16, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        saveBtn.Click += OnSave;
        root.Children.Add(saveBtn);

        Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void OnClearHistory(object sender, RoutedEventArgs e)
    {
        _settings.RecentComparisons.Clear();
        _settingsService.Save();
        _historyCountLabel.Text = $"0 {AppResources.App_Options_SavedComparisons}";
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        Flush(AppSettingsService.Instance.Current);
        _settingsService.Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // ── Builder helpers ───────────────────────────────────────────────────────

    private static TextBlock MakeSectionHeader(string text) => OptionsPageHelper.SectionHeader(text);

    private static TextBlock MakeLabel(string text) => new()
    {
        Text   = text,
        Margin = new Thickness(0, 0, 0, 2)
    };
}
