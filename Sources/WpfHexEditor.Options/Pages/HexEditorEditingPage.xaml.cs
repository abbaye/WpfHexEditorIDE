// Apache 2.0 - 2026
// Contributors: Claude Sonnet 4.6

using System;
using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Core;
using WpfHexEditor.Core.Models;

namespace WpfHexEditor.Options.Pages;

public sealed partial class HexEditorEditingPage : UserControl, IOptionsPage
{
    public event EventHandler? Changed;
    private bool _loading;

    public HexEditorEditingPage() => InitializeComponent();

    // -- IOptionsPage ------------------------------------------------------

    public void Load(AppSettings s)
    {
        _loading = true;
        try
        {
            EditModeCombo.ItemsSource    = Enum.GetValues<EditMode>();
            MouseWheelCombo.ItemsSource  = Enum.GetValues<MouseWheelSpeed>();
            ByteSizeCombo.ItemsSource    = Enum.GetValues<ByteSizeType>();
            ByteOrderCombo.ItemsSource   = Enum.GetValues<ByteOrderType>();
            CopyModeCombo.ItemsSource    = Enum.GetValues<CopyPasteMode>();
            SpacerVisualCombo.ItemsSource = Enum.GetValues<ByteSpacerVisual>();

            EditModeCombo.SelectedItem    = s.HexEditorDefaults.DefaultEditMode;
            MouseWheelCombo.SelectedItem  = s.HexEditorDefaults.MouseWheelSpeed;
            CheckAllowZoom.IsChecked      = s.HexEditorDefaults.AllowZoom;
            CheckAllowFileDrop.IsChecked  = s.HexEditorDefaults.AllowFileDrop;
            ByteSizeCombo.SelectedItem    = s.HexEditorDefaults.ByteSize;
            ByteOrderCombo.SelectedItem   = s.HexEditorDefaults.ByteOrder;
            CopyModeCombo.SelectedItem    = s.HexEditorDefaults.DefaultCopyToClipboardMode;
            SpacerVisualCombo.SelectedItem = s.HexEditorDefaults.ByteSpacerVisualStyle;
        }
        finally { _loading = false; }
    }

    public void Flush(AppSettings s)
    {
        if (EditModeCombo.SelectedItem is EditMode em)
            s.HexEditorDefaults.DefaultEditMode = em;
        if (MouseWheelCombo.SelectedItem is MouseWheelSpeed mws)
            s.HexEditorDefaults.MouseWheelSpeed = mws;
        if (ByteSizeCombo.SelectedItem is ByteSizeType bst)
            s.HexEditorDefaults.ByteSize = bst;
        if (ByteOrderCombo.SelectedItem is ByteOrderType bot)
            s.HexEditorDefaults.ByteOrder = bot;
        if (CopyModeCombo.SelectedItem is CopyPasteMode cpm)
            s.HexEditorDefaults.DefaultCopyToClipboardMode = cpm;
        if (SpacerVisualCombo.SelectedItem is ByteSpacerVisual bsv)
            s.HexEditorDefaults.ByteSpacerVisualStyle = bsv;

        s.HexEditorDefaults.AllowZoom     = CheckAllowZoom.IsChecked == true;
        s.HexEditorDefaults.AllowFileDrop = CheckAllowFileDrop.IsChecked == true;
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
}
