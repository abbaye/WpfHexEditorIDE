// ==========================================================
// Project: WpfHexEditor.Plugins.SynalysisGrammar
// File: Views/GrammarSelectorPanel.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Code-behind for GrammarSelectorPanel.xaml.
//     Wires ViewModel, handles drag-and-drop of .grammar files,
//     and manages the file-open dialog for manual grammar loading.
//
// Architecture Notes:
//     Theme: WPF global theme applied via DynamicResource in XAML.
//     Drag-drop: accepts .grammar files dropped onto the list or the panel.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using WpfHexEditor.Plugins.SynalysisGrammar.ViewModels;

namespace WpfHexEditor.Plugins.SynalysisGrammar.Views;

/// <summary>
/// VS-Like dockable panel for browsing and applying UFWB grammars.
/// </summary>
public partial class GrammarSelectorPanel : UserControl
{
    public GrammarSelectorPanel()
    {
        InitializeComponent();
        AllowDrop = true;
        DragOver += OnDragOver;
        Drop     += OnDrop;
    }

    // -- ViewModel wiring --------------------------------------------------

    internal GrammarSelectorViewModel? ViewModel
    {
        get => DataContext as GrammarSelectorViewModel;
        set => DataContext = value;
    }

    // -- Drag & drop -------------------------------------------------------

    private static void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        foreach (var file in files.Where(f =>
            f.EndsWith(".grammar", StringComparison.OrdinalIgnoreCase)))
        {
            RegisterDroppedFile(file);
        }
    }

    private void RegisterDroppedFile(string path)
    {
        if (ViewModel is null) return;

        // Notify service layer via callback (wired in SynalysisGrammarPlugin).
        DroppedGrammarFiles?.Invoke(this, path);
    }

    // -- File open dialog --------------------------------------------------

    internal void ShowOpenDialog()
    {
        var dlg = new OpenFileDialog
        {
            Title            = "Open Grammar File",
            Filter           = "UFWB Grammar Files (*.grammar)|*.grammar|All Files (*.*)|*.*",
            Multiselect      = true,
            CheckFileExists  = true,
        };

        if (dlg.ShowDialog() != true) return;

        foreach (var file in dlg.FileNames)
            DroppedGrammarFiles?.Invoke(this, file);
    }

    // -- Events ------------------------------------------------------------

    /// <summary>Raised when the user drops or opens .grammar file(s).</summary>
    public event EventHandler<string>? DroppedGrammarFiles;
}
