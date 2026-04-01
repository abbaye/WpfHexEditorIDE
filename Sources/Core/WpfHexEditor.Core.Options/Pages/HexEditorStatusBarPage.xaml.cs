//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Windows;
using System.Windows.Controls;

namespace WpfHexEditor.Core.Options.Pages;

public sealed partial class HexEditorStatusBarPage : UserControl, IOptionsPage
{
    public event EventHandler? Changed;
    private bool _loading;

    public HexEditorStatusBarPage() => InitializeComponent();

    // -- IOptionsPage ------------------------------------------------------

    public void Load(AppSettings s)
    {
        _loading = true;
        try
        {
            CheckShowStatusMessage.IsChecked = s.HexEditorDefaults.ShowStatusMessage;
            CheckShowFileSize.IsChecked      = s.HexEditorDefaults.ShowFileSizeInStatusBar;
            CheckShowSelection.IsChecked     = s.HexEditorDefaults.ShowSelectionInStatusBar;
            CheckShowPosition.IsChecked      = s.HexEditorDefaults.ShowPositionInStatusBar;
            CheckShowEditMode.IsChecked      = s.HexEditorDefaults.ShowEditModeInStatusBar;
            CheckShowBytesPerLine.IsChecked  = s.HexEditorDefaults.ShowBytesPerLineInStatusBar;
            CheckShowRefreshRate.IsChecked   = s.HexEditorDefaults.ShowRefreshRateInStatusBar;
        }
        finally { _loading = false; }
    }

    public void Flush(AppSettings s)
    {
        s.HexEditorDefaults.ShowStatusMessage        = CheckShowStatusMessage.IsChecked == true;
        s.HexEditorDefaults.ShowFileSizeInStatusBar  = CheckShowFileSize.IsChecked      == true;
        s.HexEditorDefaults.ShowSelectionInStatusBar = CheckShowSelection.IsChecked     == true;
        s.HexEditorDefaults.ShowPositionInStatusBar  = CheckShowPosition.IsChecked      == true;
        s.HexEditorDefaults.ShowEditModeInStatusBar  = CheckShowEditMode.IsChecked      == true;
        s.HexEditorDefaults.ShowBytesPerLineInStatusBar = CheckShowBytesPerLine.IsChecked == true;
        s.HexEditorDefaults.ShowRefreshRateInStatusBar  = CheckShowRefreshRate.IsChecked  == true;
    }

    // -- Control handlers -------------------------------------------------

    private void OnCheckChanged(object sender, RoutedEventArgs e)
    {
        if (!_loading) Changed?.Invoke(this, EventArgs.Empty);
    }
}
