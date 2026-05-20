// GNU Affero General Public License v3.0 - 2026
// Contributors: Claude Sonnet 4.6

using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Core;

namespace WpfHexEditor.Core.Options.Pages;

public sealed partial class HexEditorDisplayPage : UserControl, IOptionsPage
{
    public event EventHandler? Changed;
    private bool _loading;

    public HexEditorDisplayPage() => InitializeComponent();

    // -- IOptionsPage ------------------------------------------------------

    // Entropy window-size options: value = bytes, display = label
    private static readonly (int Value, string Label)[] EntropyWindowSizeOptions =
    [
        (128, "Small (128 bytes)"),
        (256, "Medium (256 bytes)"),
        (512, "Large (512 bytes)"),
    ];

    // Entropy color-theme options: index matches EntropyColorTheme enum
    private static readonly string[] EntropyThemeOptions =
        ["Blue → Red", "Greyscale", "Traffic Light"];

    public void Load(AppSettings s)
    {
        _loading = true;
        try
        {
            // Enum-backed combos
            DataVisualCombo.ItemsSource         = Enum.GetValues<DataVisualType>();
            OffsetVisualCombo.ItemsSource        = Enum.GetValues<DataVisualType>();
            ByteGroupingCombo.ItemsSource        = Enum.GetValues<ByteSpacerGroup>();
            SpacerPositionCombo.ItemsSource      = Enum.GetValues<ByteSpacerPosition>();
            MouseWheelSpeedCombo.ItemsSource     = Enum.GetValues<MouseWheelSpeed>();
            ByteToolTipModeCombo.ItemsSource     = Enum.GetValues<ByteToolTipDisplayMode>();

            // Entropy combos
            EntropyWindowSizeCombo.ItemsSource  = EntropyWindowSizeOptions.Select(x => x.Label).ToArray();
            EntropyColorThemeCombo.ItemsSource  = EntropyThemeOptions;

            // Select current values
            SelectByPerLine(s.HexEditorDefaults.BytePerLine);
            CheckShowOffset.IsChecked          = s.HexEditorDefaults.ShowOffset;
            CheckShowAscii.IsChecked           = s.HexEditorDefaults.ShowAscii;
            MouseWheelSpeedCombo.SelectedItem  = s.HexEditorDefaults.MouseWheelSpeed;
            DataVisualCombo.SelectedItem       = s.HexEditorDefaults.DataStringVisual;
            OffsetVisualCombo.SelectedItem     = s.HexEditorDefaults.OffSetStringVisual;
            ByteGroupingCombo.SelectedItem     = s.HexEditorDefaults.ByteGrouping;
            SpacerPositionCombo.SelectedItem   = s.HexEditorDefaults.ByteSpacerPositioning;
            ByteToolTipModeCombo.SelectedItem  = s.HexEditorDefaults.ByteToolTipDisplayMode;

            // Scroll markers
            CheckShowBookmarkMarkers.IsChecked     = s.HexEditorDefaults.ShowBookmarkMarkers;
            CheckShowModifiedMarkers.IsChecked     = s.HexEditorDefaults.ShowModifiedMarkers;
            CheckShowInsertedMarkers.IsChecked     = s.HexEditorDefaults.ShowInsertedMarkers;
            CheckShowDeletedMarkers.IsChecked      = s.HexEditorDefaults.ShowDeletedMarkers;
            CheckShowSearchResultMarkers.IsChecked = s.HexEditorDefaults.ShowSearchResultMarkers;

            // Column / row highlight
            CheckShowColumnHighlight.IsChecked      = s.HexEditorDefaults.ShowColumnHighlight;
            CheckShowAsciiColumnHighlight.IsChecked = s.HexEditorDefaults.ShowAsciiColumnHighlight;
            CheckShowRowHighlight.IsChecked         = s.HexEditorDefaults.ShowRowHighlight;

            // Split view toggle
            CheckShowSplitToggleButton.IsChecked    = s.HexEditorDefaults.ShowSplitToggleButton;

            // Entropy heatmap
            CheckShowEntropyHeatmap.IsChecked = s.HexEditorDefaults.ShowEntropyHeatmap;
            var wsIdx = Array.FindIndex(EntropyWindowSizeOptions, x => x.Value == s.HexEditorDefaults.EntropyWindowSize);
            EntropyWindowSizeCombo.SelectedIndex = wsIdx >= 0 ? wsIdx : 1;
            EntropyColorThemeCombo.SelectedIndex = Math.Clamp(s.HexEditorDefaults.EntropyColorTheme, 0, 2);
        }
        finally { _loading = false; }
    }

    public void Flush(AppSettings s)
    {
        if (BytePerLineCombo.SelectedItem is ComboBoxItem bplItem &&
            int.TryParse(bplItem.Tag?.ToString(), out int bpl))
            s.HexEditorDefaults.BytePerLine = bpl;

        s.HexEditorDefaults.ShowOffset  = CheckShowOffset.IsChecked == true;
        s.HexEditorDefaults.ShowAscii   = CheckShowAscii.IsChecked  == true;

        if (DataVisualCombo.SelectedItem is DataVisualType dvt)
            s.HexEditorDefaults.DataStringVisual = dvt;
        if (OffsetVisualCombo.SelectedItem is DataVisualType ovt)
            s.HexEditorDefaults.OffSetStringVisual = ovt;
        if (ByteGroupingCombo.SelectedItem is ByteSpacerGroup bsg)
            s.HexEditorDefaults.ByteGrouping = bsg;
        if (SpacerPositionCombo.SelectedItem is ByteSpacerPosition bsp)
            s.HexEditorDefaults.ByteSpacerPositioning = bsp;
        if (MouseWheelSpeedCombo.SelectedItem is MouseWheelSpeed mws)
            s.HexEditorDefaults.MouseWheelSpeed = mws;
        if (ByteToolTipModeCombo.SelectedItem is ByteToolTipDisplayMode ttm)
            s.HexEditorDefaults.ByteToolTipDisplayMode = ttm;

        s.HexEditorDefaults.ShowBookmarkMarkers     = CheckShowBookmarkMarkers.IsChecked == true;
        s.HexEditorDefaults.ShowModifiedMarkers     = CheckShowModifiedMarkers.IsChecked == true;
        s.HexEditorDefaults.ShowInsertedMarkers     = CheckShowInsertedMarkers.IsChecked == true;
        s.HexEditorDefaults.ShowDeletedMarkers      = CheckShowDeletedMarkers.IsChecked  == true;
        s.HexEditorDefaults.ShowSearchResultMarkers = CheckShowSearchResultMarkers.IsChecked == true;

        s.HexEditorDefaults.ShowColumnHighlight      = CheckShowColumnHighlight.IsChecked      == true;
        s.HexEditorDefaults.ShowAsciiColumnHighlight = CheckShowAsciiColumnHighlight.IsChecked == true;
        s.HexEditorDefaults.ShowRowHighlight         = CheckShowRowHighlight.IsChecked         == true;

        s.HexEditorDefaults.ShowSplitToggleButton = CheckShowSplitToggleButton.IsChecked == true;

        // Entropy heatmap
        s.HexEditorDefaults.ShowEntropyHeatmap = CheckShowEntropyHeatmap.IsChecked == true;
        var wsIdx = EntropyWindowSizeCombo.SelectedIndex;
        if (wsIdx >= 0 && wsIdx < EntropyWindowSizeOptions.Length)
            s.HexEditorDefaults.EntropyWindowSize = EntropyWindowSizeOptions[wsIdx].Value;
        s.HexEditorDefaults.EntropyColorTheme = Math.Clamp(EntropyColorThemeCombo.SelectedIndex, 0, 2);
    }

    // -- Control handlers -------------------------------------------------

    private void OnComboChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loading) Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnCheckChanged(object sender, RoutedEventArgs e)
    {
        if (!_loading) Changed?.Invoke(this, EventArgs.Empty);
    }

    // -- Helpers ----------------------------------------------------------

    private void SelectByPerLine(int value)
    {
        foreach (ComboBoxItem item in BytePerLineCombo.Items)
        {
            if (item.Tag?.ToString() == value.ToString())
            {
                BytePerLineCombo.SelectedItem = item;
                return;
            }
        }
        // Fallback: select 16
        if (BytePerLineCombo.Items.Count > 1)
            BytePerLineCombo.SelectedIndex = 1;
    }
}
