// GNU Affero General Public License v3.0 - 2026
// Contributors: Claude Sonnet 4.6

using System;
using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Core;

namespace WpfHexEditor.Options.Pages;

public sealed partial class HexEditorDisplayPage : UserControl, IOptionsPage
{
    public event EventHandler? Changed;
    private bool _loading;

    public HexEditorDisplayPage() => InitializeComponent();

    // -- IOptionsPage ------------------------------------------------------

    public void Load(AppSettings s)
    {
        _loading = true;
        try
        {
            // Enum-backed combos
            DataVisualCombo.ItemsSource      = Enum.GetValues<DataVisualType>();
            OffsetVisualCombo.ItemsSource    = Enum.GetValues<DataVisualType>();
            ByteGroupingCombo.ItemsSource    = Enum.GetValues<ByteSpacerGroup>();
            SpacerPositionCombo.ItemsSource  = Enum.GetValues<ByteSpacerPosition>();
            MouseWheelSpeedCombo.ItemsSource = Enum.GetValues<MouseWheelSpeed>();

            // Select current values
            SelectByPerLine(s.HexEditorDefaults.BytePerLine);
            CheckShowOffset.IsChecked          = s.HexEditorDefaults.ShowOffset;
            CheckShowAscii.IsChecked           = s.HexEditorDefaults.ShowAscii;
            MouseWheelSpeedCombo.SelectedItem  = s.HexEditorDefaults.MouseWheelSpeed;
            DataVisualCombo.SelectedItem       = s.HexEditorDefaults.DataStringVisual;
            OffsetVisualCombo.SelectedItem     = s.HexEditorDefaults.OffSetStringVisual;
            ByteGroupingCombo.SelectedItem     = s.HexEditorDefaults.ByteGrouping;
            SpacerPositionCombo.SelectedItem   = s.HexEditorDefaults.ByteSpacerPositioning;

            // Scroll markers
            CheckShowBookmarkMarkers.IsChecked     = s.HexEditorDefaults.ShowBookmarkMarkers;
            CheckShowModifiedMarkers.IsChecked     = s.HexEditorDefaults.ShowModifiedMarkers;
            CheckShowInsertedMarkers.IsChecked     = s.HexEditorDefaults.ShowInsertedMarkers;
            CheckShowDeletedMarkers.IsChecked      = s.HexEditorDefaults.ShowDeletedMarkers;
            CheckShowSearchResultMarkers.IsChecked = s.HexEditorDefaults.ShowSearchResultMarkers;
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

        s.HexEditorDefaults.ShowBookmarkMarkers     = CheckShowBookmarkMarkers.IsChecked == true;
        s.HexEditorDefaults.ShowModifiedMarkers     = CheckShowModifiedMarkers.IsChecked == true;
        s.HexEditorDefaults.ShowInsertedMarkers     = CheckShowInsertedMarkers.IsChecked == true;
        s.HexEditorDefaults.ShowDeletedMarkers      = CheckShowDeletedMarkers.IsChecked  == true;
        s.HexEditorDefaults.ShowSearchResultMarkers = CheckShowSearchResultMarkers.IsChecked == true;
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
