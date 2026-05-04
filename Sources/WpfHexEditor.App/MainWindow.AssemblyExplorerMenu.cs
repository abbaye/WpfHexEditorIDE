// ==========================================================
// Project      : WpfHexEditor.App
// File         : MainWindow.AssemblyExplorerMenu.cs
// Description  : Assembly Explorer menu wiring — View > Assembly Explorer
//                + Tools > Analyze Assembly + Tools > Search in Assemblies
//                + Edit > Go to Metadata Token (handlers invoked from
//                MainWindow.xaml MenuItems and from the View menu organizer).
//                Replaces the IUIRegistry.RegisterMenuItem block that the
//                AssemblyExplorer plugin previously used (ADR-011 follow-up).
// Architecture : Partial of MainWindow. The View entry is registered in
//                MainWindow.ViewMenu.cs as a built-in ViewMenuEntry. Tools/
//                Edit entries are wired through MainWindow.xaml MenuItem.Click
//                handlers defined below.
// ==========================================================

using System.Windows;
using WpfHexEditor.App.AssemblyExplorer;
using WpfHexEditor.App.AssemblyExplorer.Properties;
using WpfHexEditor.Docking.Core;
using WpfHexEditor.Editor.Core.Dialogs;

namespace WpfHexEditor.App;

public partial class MainWindow
{
    /// <summary>Invoked by View > Assembly Explorer (registered in MainWindow.ViewMenu.cs).</summary>
    private void OnShowAssemblyExplorer()
        => ShowOrCreatePanel("Assembly Explorer", AssemblyExplorerModule.ContentIdMain, DockDirection.Left);

    /// <summary>Invoked by Tools > Analyze Assembly. Loads the active hex-editor
    /// file into the Assembly Explorer panel and brings it to the front.</summary>
    private void OnAnalyzeAssembly()
    {
        if (_assemblyExplorerModule is null || _hexEditorService is null) return;

        // Surface the panel first so EnsureActivated runs and ViewModel is ready.
        ShowOrCreatePanel("Assembly Explorer", AssemblyExplorerModule.ContentIdMain, DockDirection.Left);

        var path = _hexEditorService.CurrentFilePath;
        if (string.IsNullOrEmpty(path)) return;

        // Get the panel (which guarantees the ViewModel is built) and load.
        var panel = _assemblyExplorerModule.GetPanel(AssemblyExplorerModule.ContentIdMain)
                    as AssemblyExplorer.Views.AssemblyExplorerPanel;
        _ = panel?.ViewModel.LoadAssemblyAsync(path);
    }

    /// <summary>Invoked by Tools > Search in Assemblies.</summary>
    private void OnSearchInAssemblies()
        => ShowOrCreatePanel("Search Assemblies", AssemblyExplorerModule.ContentIdSearch, DockDirection.Bottom);

    /// <summary>Invoked by Edit > Go to Metadata Token….</summary>
    private void OnGoToMetadataToken()
    {
        IdeMessageBox.Show(
            "Go to Metadata Token — Coming in a future release.",
            AssemblyExplorerResources.AsmExplorer_PluginName,
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}
