// ==========================================================
// Project: WpfHexEditor.App
// File: MainWindow.WorkspaceSymbols.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     Handles Ctrl+T — Go to Symbol in Workspace.
//     Opens WorkspaceSymbolsPopup; resolves the active ILspClient from the
//     bridge service. Navigates to symbol location on commit.
// ==========================================================

using WpfHexEditor.App.Dialogs;
using WpfHexEditor.Editor.Core.LSP;

namespace WpfHexEditor.App;

public partial class MainWindow
{
    /// <summary>
    /// Shows the "Go to Symbol in Workspace" popup (Ctrl+T).
    /// Resolves the active ILspClient from the bridge service.
    /// The popup degrades gracefully when no LSP client is active.
    /// </summary>
    private void ShowWorkspaceSymbolsPopup()
    {
        ILspClient? client = _lspBridgeService?.ActiveClient;

        var popup = new WorkspaceSymbolsPopup(client, NavigateToSymbolLocation);
        popup.Owner = this;
        popup.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner;
        popup.Show();
        popup.Activate();
    }

    /// <summary>Opens a file URI and navigates to the given 0-based line.</summary>
    private void NavigateToSymbolLocation(string uri, int line, int column)
    {
        var path = uri.StartsWith("file:///", StringComparison.OrdinalIgnoreCase)
            ? new Uri(uri).LocalPath
            : uri;

        // Open the file (or bring existing tab to front).
        OpenFileDirectly(path);

        // Navigate to the target line in the now-active editor.
        if (_activeDocumentEditor is WpfHexEditor.Editor.Core.INavigableDocument nav)
            nav.NavigateTo(line + 1, column + 1);  // NavigateTo is 1-based
    }
}
