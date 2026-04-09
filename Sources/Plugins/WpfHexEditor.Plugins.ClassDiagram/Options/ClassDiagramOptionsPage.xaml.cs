// ==========================================================
// Project: WpfHexEditor.Plugins.ClassDiagram
// File: Options/ClassDiagramOptionsPage.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     Code-behind for the Class Diagram plugin options page.
//     Loads current option values into controls on Load(),
//     reads them back into the options model on Save().
//
// Architecture Notes:
//     Pattern: View (MVVM-lite). The options model is passed in via
//     the constructor — no static singleton to keep the page
//     independently testable.
//     Load/Save are called by ClassDiagramPlugin via IPluginWithOptions.
// ==========================================================

using System.Windows.Controls;

namespace WpfHexEditor.Plugins.ClassDiagram.Options;

/// <summary>
/// Interaction logic for ClassDiagramOptionsPage.xaml.
/// </summary>
public partial class ClassDiagramOptionsPage : UserControl
{
    private readonly ClassDiagramOptions _options;

    /// <summary>
    /// Initialises the options page bound to <paramref name="options"/>.
    /// </summary>
    public ClassDiagramOptionsPage(ClassDiagramOptions options)
    {
        _options = options;
        InitializeComponent();
    }

    // ── Load ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Populates all UI controls from the current <see cref="ClassDiagramOptions"/> state.
    /// Call this each time the options page is displayed.
    /// </summary>
    public void Load()
    {
        ChkAutoLayout.IsChecked     = _options.AutoLayout;
        ChkGroupByNamespace.IsChecked = _options.GroupByNamespace;

        TxtNodeWidth.Text  = _options.DefaultNodeWidth.ToString("F0");
        TxtNodeHeight.Text = _options.DefaultNodeHeight.ToString("F0");

        ChkShowGrid.IsChecked    = _options.ShowGridByDefault;
        ChkSnapEnabled.IsChecked = _options.SnapEnabledByDefault;

        SnapSizeSlider.Value = _options.SnapSize;
        UpdateSnapSizeLabel(_options.SnapSize);

        ChkIncludePrivate.IsChecked   = _options.IncludePrivateMembers;
        ChkIncludeInherited.IsChecked = _options.IncludeInheritedMembers;

        ChkShowMinimapByDefault.IsChecked      = _options.ShowMinimapByDefault;
        ChkOutlineShowMembers.IsChecked        = _options.OutlinePanelShowMembers;
        ChkOutlineColorByVisibility.IsChecked  = _options.OutlinePanelColorByVisibility;

        ChkShowHoverTooltips.IsChecked         = _options.ShowHoverTooltips;
        TooltipDelaySlider.Value               = _options.TooltipDelayMs;
        UpdateTooltipDelayLabel(_options.TooltipDelayMs);

        ChkRestoreLastState.IsChecked          = _options.RestoreLastState;

        // Solution generation
        ChkSolutionShowSwimLanes.IsChecked      = _options.SolutionShowSwimLanesByDefault;
        ChkSolutionIncludePrivate.IsChecked     = _options.SolutionIncludePrivateMembers;
        ChkSolutionIncludeInternal.IsChecked    = _options.SolutionIncludeInternalTypes;
        ChkSolutionExcludeTestProjects.IsChecked= _options.SolutionExcludeTestProjects;
        TxtSolutionMaxFiles.Text                = _options.SolutionMaxFilesPromptThreshold.ToString();
    }

    // ── Save ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Reads all UI control values back into <see cref="ClassDiagramOptions"/>.
    /// Called when the user clicks Apply or OK in the IDE options dialog.
    /// </summary>
    public void Save()
    {
        _options.AutoLayout       = ChkAutoLayout.IsChecked == true;
        _options.GroupByNamespace = ChkGroupByNamespace.IsChecked == true;

        if (double.TryParse(TxtNodeWidth.Text, out double w) && w is >= 60 and <= 800)
            _options.DefaultNodeWidth = w;

        if (double.TryParse(TxtNodeHeight.Text, out double h) && h is >= 40 and <= 400)
            _options.DefaultNodeHeight = h;

        _options.ShowGridByDefault    = ChkShowGrid.IsChecked == true;
        _options.SnapEnabledByDefault = ChkSnapEnabled.IsChecked == true;
        _options.SnapSize             = SnapSizeSlider.Value;

        _options.IncludePrivateMembers   = ChkIncludePrivate.IsChecked == true;
        _options.IncludeInheritedMembers = ChkIncludeInherited.IsChecked == true;

        _options.ShowMinimapByDefault     = ChkShowMinimapByDefault.IsChecked == true;
        _options.OutlinePanelShowMembers  = ChkOutlineShowMembers.IsChecked == true;
        _options.OutlinePanelColorByVisibility = ChkOutlineColorByVisibility.IsChecked == true;

        _options.ShowHoverTooltips        = ChkShowHoverTooltips.IsChecked == true;
        _options.TooltipDelayMs           = (int)TooltipDelaySlider.Value;

        _options.RestoreLastState         = ChkRestoreLastState.IsChecked == true;

        // Solution generation
        _options.SolutionShowSwimLanesByDefault    = ChkSolutionShowSwimLanes.IsChecked == true;
        _options.SolutionIncludePrivateMembers     = ChkSolutionIncludePrivate.IsChecked == true;
        _options.SolutionIncludeInternalTypes      = ChkSolutionIncludeInternal.IsChecked == true;
        _options.SolutionExcludeTestProjects       = ChkSolutionExcludeTestProjects.IsChecked == true;
        if (int.TryParse(TxtSolutionMaxFiles.Text, out int maxFiles) && maxFiles is >= 0 and <= 10000)
            _options.SolutionMaxFilesPromptThreshold = maxFiles;
    }

    // ── Event handlers ───────────────────────────────────────────────────────

    private void OnSnapSizeChanged(
        object sender,
        System.Windows.RoutedPropertyChangedEventArgs<double> e)
    {
        if (SnapSizeLabel is not null)
            UpdateSnapSizeLabel(e.NewValue);
    }

    // ── Event handlers ───────────────────────────────────────────────────────

    private void OnTooltipDelayChanged(
        object sender,
        System.Windows.RoutedPropertyChangedEventArgs<double> e)
    {
        if (TooltipDelayLabel is not null)
            UpdateTooltipDelayLabel((int)e.NewValue);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void UpdateSnapSizeLabel(double value) =>
        SnapSizeLabel.Text = $"{(int)value} px";

    private void UpdateTooltipDelayLabel(int ms) =>
        TooltipDelayLabel.Text = $"{ms} ms";
}
