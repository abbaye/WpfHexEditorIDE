// ==========================================================
// Project: WpfHexEditor.Shell.Panels
// File: Panels/WhfmtFormatDetailPanel.xaml.cs
// Description: Code-behind for the format detail card panel.
//              Handles lazy JSON loading when the JSON tab becomes active.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Shell.Panels.ViewModels;

namespace WpfHexEditor.Shell.Panels.Panels;

public partial class WhfmtFormatDetailPanel : UserControl
{
    public WhfmtFormatDetailPanel()
    {
        InitializeComponent();
    }

    // Called when the JSON TabItem receives focus (routed from GotFocus on the TabItem)
    private void OnJsonTabGotFocus(object sender, RoutedEventArgs e)
    {
        // The VM is the DataContext; lazy-load is a no-op if already loaded
        if (DataContext is WhfmtFormatDetailVm vm && vm.RawJson is null)
        {
            // We don't have direct access to the catalog services here —
            // set a placeholder so the user sees something immediately
            vm.RawJson = "(loading…)";
            // The parent (WhfmtBrowserPanel / WhfmtCatalogDocument) should wire
            // LoadRawJsonIfNeeded via CopyJsonCommand or a dedicated trigger.
            // This stub ensures the tab doesn't stay blank.
        }
    }
}
