// ==========================================================
// Project: WpfHexEditor.Shell.Panels
// File: Panels/WhfmtFormatDetailPanel.xaml.cs
// Description: Code-behind for the format detail card panel.
//              JSON is populated eagerly by the parent ViewModel on selection.
// ==========================================================

using System.Windows.Controls;

namespace WpfHexEditor.Shell.Panels.Panels;

public partial class WhfmtFormatDetailPanel : UserControl
{
    public WhfmtFormatDetailPanel()
    {
        InitializeComponent();
    }

    // GotFocus handler declared in XAML — no-op since RawJson is set by the VM on selection
    private void OnJsonTabGotFocus(object sender, System.Windows.RoutedEventArgs e) { }
}
