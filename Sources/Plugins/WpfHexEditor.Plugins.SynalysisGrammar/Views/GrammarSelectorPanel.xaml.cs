// ==========================================================
// Project: WpfHexEditor.Plugins.SynalysisGrammar
// File: Views/GrammarSelectorPanel.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Code-behind for GrammarSelectorPanel.xaml.
//     Wires ViewModel, handles drag-and-drop of .grammar files, the
//     file-open dialog, context menu logic, and mouse double-click apply.
//
// Architecture Notes:
//     Theme: WPF global theme applied via DynamicResource in XAML.
//     Drag-drop: accepts .grammar files dropped onto the panel.
//     Context menu: MenuItem x:Name values inside UserControl.Resources do NOT
//     generate code-behind fields (WPF ResourceDictionary scope limitation).
//     Items are resolved by name from the ContextMenu at runtime instead.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using WpfHexEditor.Plugins.SynalysisGrammar.Properties;
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

    // -- Context menu ------------------------------------------------------

    private void OnContextMenuOpened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu menu) return;

        var entry = ViewModel?.SelectedGrammar;

        // Resolve named items from the ContextMenu at runtime.
        // x:Name inside UserControl.Resources does NOT produce code-behind fields.
        SetMenuItemEnabled(menu, "MenuRemoveGrammar",  entry?.Source == GrammarSource.Disk);
        SetMenuItemEnabled(menu, "MenuCopyName",       entry is not null);
        SetMenuItemEnabled(menu, "MenuCopyExtensions", entry is not null);
    }

    private static void SetMenuItemEnabled(ContextMenu menu, string name, bool enabled)
    {
        var item = menu.FindName(name) as MenuItem;
        if (item is not null) item.IsEnabled = enabled;
    }

    private void OnContextCopyName(object sender, RoutedEventArgs e)
    {
        var name = ViewModel?.SelectedGrammar?.Name;
        if (!string.IsNullOrEmpty(name))
            Clipboard.SetText(name);
    }

    private void OnContextCopyExtensions(object sender, RoutedEventArgs e)
    {
        var ext = ViewModel?.SelectedGrammar?.ExtensionsDisplay;
        if (!string.IsNullOrEmpty(ext))
            Clipboard.SetText(ext);
    }

    private void OnContextRemoveGrammar(object sender, RoutedEventArgs e)
        => ViewModel?.RemoveGrammarCommand.Execute(null);

    // -- Mouse double-click apply ------------------------------------------

    private void OnListMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Only trigger when an item is actually selected (not on empty area click).
        if (ViewModel?.SelectedGrammar is not null)
            ViewModel.ApplyGrammarCommand.Execute(null);
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
            DroppedGrammarFiles?.Invoke(this, file);
        }
    }

    // -- File open dialog --------------------------------------------------

    internal void ShowOpenDialog()
    {
        var dlg = new OpenFileDialog
        {
            Title           = SynalysisGrammarResources.Synalysis_OpenGrammarFile,
            Filter          = "UFWB Grammar Files (*.grammar)|*.grammar|All Files (*.*)|*.*",
            Multiselect     = true,
            CheckFileExists = true,
        };

        if (dlg.ShowDialog() != true) return;

        foreach (var file in dlg.FileNames)
            DroppedGrammarFiles?.Invoke(this, file);
    }

    // -- Events ------------------------------------------------------------

    /// <summary>Raised when the user drops or opens .grammar file(s).</summary>
    public event EventHandler<string>? DroppedGrammarFiles;
}
