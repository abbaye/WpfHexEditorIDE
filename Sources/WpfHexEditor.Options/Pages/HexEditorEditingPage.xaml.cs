// Apache 2.0 - 2026
// Contributors: Claude Sonnet 4.6

using System;
using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Core.Models;

namespace WpfHexEditor.Options.Pages;

public sealed partial class HexEditorEditingPage : UserControl, IOptionsPage
{
    public event EventHandler? Changed;
    private bool _loading;

    public HexEditorEditingPage() => InitializeComponent();

    // ── IOptionsPage ──────────────────────────────────────────────────────

    public void Load(AppSettings s)
    {
        _loading = true;
        try
        {
            EditModeCombo.ItemsSource   = Enum.GetValues<EditMode>();
            MouseWheelCombo.ItemsSource = Enum.GetValues<WpfHexEditor.Core.MouseWheelSpeed>();

            EditModeCombo.SelectedItem   = s.HexEditorDefaults.DefaultEditMode;
            MouseWheelCombo.SelectedItem = s.HexEditorDefaults.MouseWheelSpeed;
            CheckAllowZoom.IsChecked     = s.HexEditorDefaults.AllowZoom;
            CheckAllowFileDrop.IsChecked = s.HexEditorDefaults.AllowFileDrop;
        }
        finally { _loading = false; }
    }

    public void Flush(AppSettings s)
    {
        if (EditModeCombo.SelectedItem is EditMode em)
            s.HexEditorDefaults.DefaultEditMode = em;

        if (MouseWheelCombo.SelectedItem is WpfHexEditor.Core.MouseWheelSpeed mws)
            s.HexEditorDefaults.MouseWheelSpeed = mws;

        s.HexEditorDefaults.AllowZoom     = CheckAllowZoom.IsChecked == true;
        s.HexEditorDefaults.AllowFileDrop = CheckAllowFileDrop.IsChecked == true;
    }

    // ── Control handlers ─────────────────────────────────────────────────

    private void OnComboChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loading) Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnCheckChanged(object sender, RoutedEventArgs e)
    {
        if (!_loading) Changed?.Invoke(this, EventArgs.Empty);
    }
}
