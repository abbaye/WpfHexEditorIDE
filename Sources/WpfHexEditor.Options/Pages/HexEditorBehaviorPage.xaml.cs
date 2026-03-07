//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Core;

namespace WpfHexEditor.Options.Pages;

public sealed partial class HexEditorBehaviorPage : UserControl, IOptionsPage
{
    public event EventHandler? Changed;
    private bool _loading;

    public HexEditorBehaviorPage() => InitializeComponent();

    // -- IOptionsPage ------------------------------------------------------

    public void Load(AppSettings s)
    {
        _loading = true;
        try
        {
            PreloadModeCombo.ItemsSource = Enum.GetValues<PreloadByteInEditor>();

            CheckAllowAutoHighLight.IsChecked          = s.HexEditorDefaults.AllowAutoHighLightSelectionByte;
            CheckAllowAutoSelectDoubleClick.IsChecked  = s.HexEditorDefaults.AllowAutoSelectSameByteAtDoubleClick;
            CheckAllowContextMenu.IsChecked            = s.HexEditorDefaults.AllowContextMenu;
            CheckAllowDeleteByte.IsChecked             = s.HexEditorDefaults.AllowDeleteByte;
            CheckAllowExtend.IsChecked                 = s.HexEditorDefaults.AllowExtend;
            CheckFileDroppingConfirmation.IsChecked    = s.HexEditorDefaults.FileDroppingConfirmation;
            PreloadModeCombo.SelectedItem              = s.HexEditorDefaults.PreloadByteInEditorMode;
        }
        finally { _loading = false; }
    }

    public void Flush(AppSettings s)
    {
        s.HexEditorDefaults.AllowAutoHighLightSelectionByte       = CheckAllowAutoHighLight.IsChecked         == true;
        s.HexEditorDefaults.AllowAutoSelectSameByteAtDoubleClick  = CheckAllowAutoSelectDoubleClick.IsChecked == true;
        s.HexEditorDefaults.AllowContextMenu                      = CheckAllowContextMenu.IsChecked           == true;
        s.HexEditorDefaults.AllowDeleteByte                       = CheckAllowDeleteByte.IsChecked            == true;
        s.HexEditorDefaults.AllowExtend                           = CheckAllowExtend.IsChecked                == true;
        s.HexEditorDefaults.FileDroppingConfirmation              = CheckFileDroppingConfirmation.IsChecked   == true;

        if (PreloadModeCombo.SelectedItem is PreloadByteInEditor pbm)
            s.HexEditorDefaults.PreloadByteInEditorMode = pbm;
    }

    // -- Control handlers -------------------------------------------------

    private void OnCheckChanged(object sender, RoutedEventArgs e)
    {
        if (!_loading) Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnComboChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loading) Changed?.Invoke(this, EventArgs.Empty);
    }
}
