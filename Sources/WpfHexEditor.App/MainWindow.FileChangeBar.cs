// ==========================================================
// Project: WpfHexEditor.App
// File: MainWindow.FileChangeBar.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-16
// Description:
//     Partial class that manages the FileChangeInfoBar — the IDE-level orange
//     banner shown when the active hex file is modified by an external process.
//
// Architecture Notes:
//     Pattern: Observer — subscribes to HexEditor.FileExternallyChanged when an
//     editor becomes active (wired in MainWindow.xaml.cs ActiveHexEditor setter).
//     User choices: [Reload] discards in-memory edits via ReloadFromDisk();
//                   [Keep my edits] dismisses without action;
//                   [×] same as Keep.
//     The bar is always hidden on active-editor change so stale messages never
//     persist across document tabs.
// ==========================================================

using System.IO;
using System.Windows;
using WpfHexEditor.HexEditor;

namespace WpfHexEditor.App;

public partial class MainWindow
{
    // --- Event handler wired by ActiveHexEditor setter -------------------

    private void OnActiveFileExternallyChanged(object? sender, ExternalFileChangedEventArgs e)
    {
        // Already dispatched to UI thread by HexEditor.FileOperations.cs.
        ShowFileChangeInfoBar(Path.GetFileName(e.FilePath), e.HasUnsavedChanges);
    }

    // --- InfoBar show/hide -----------------------------------------------

    private void ShowFileChangeInfoBar(string fileName, bool hasUnsavedChanges)
    {
        FileChangeInfoBarMessage.Text = hasUnsavedChanges
            ? $"'{fileName}' was modified externally — you have unsaved edits."
            : $"'{fileName}' was modified externally.";

        // "Keep my edits" only makes sense when there is something to keep.
        FileChangeInfoBarKeep.Visibility = hasUnsavedChanges
            ? Visibility.Visible
            : Visibility.Collapsed;

        FileChangeInfoBar.Visibility = Visibility.Visible;
    }

    private void HideFileChangeInfoBar()
        => FileChangeInfoBar.Visibility = Visibility.Collapsed;

    // --- Button handlers --------------------------------------------------

    private void OnFileChangeInfoBarReload(object sender, RoutedEventArgs e)
    {
        ActiveHexEditor?.ReloadFromDisk();
        HideFileChangeInfoBar();
    }

    private void OnFileChangeInfoBarKeep(object sender, RoutedEventArgs e)
        => HideFileChangeInfoBar(); // user keeps in-memory edits, no reload

    private void OnFileChangeInfoBarClose(object sender, RoutedEventArgs e)
        => HideFileChangeInfoBar();
}
