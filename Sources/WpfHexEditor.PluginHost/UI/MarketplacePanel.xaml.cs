//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

// ==========================================================
// Project: WpfHexEditor.PluginHost
// File: UI/MarketplacePanel.xaml.cs
// Created: 2026-03-15
// Description:
//     Code-behind for the Marketplace dockable panel.
//     Sets the DataContext to MarketplacePanelViewModel and wires drag-drop
//     for local .whxplugin package installation.
// Theme: DynamicResource — conforms to global WPF theme (PanelCommon.xaml + Colors.xaml).
// ==========================================================

using System.Windows;
using System.Windows.Controls;

namespace WpfHexEditor.PluginHost.UI;

/// <summary>
/// Marketplace plugin browser panel — code-behind for MarketplacePanel.xaml.
/// </summary>
public sealed partial class MarketplacePanel : UserControl
{
    public MarketplacePanel()
    {
        InitializeComponent();
        AllowDrop = true;
    }

    /// <summary>Initializes the panel with its view-model after construction.</summary>
    public void Initialize(MarketplacePanelViewModel viewModel)
    {
        DataContext = viewModel;
    }

    // ── Drag-and-drop for local .whxplugin files ─────────────────────────────

    protected override void OnDragOver(DragEventArgs e)
    {
        base.OnDragOver(e);
        e.Effects = IsWhxPluginDrop(e) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    protected override void OnDrop(DragEventArgs e)
    {
        base.OnDrop(e);
        if (!IsWhxPluginDrop(e)) return;

        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        if (files is null || files.Length == 0) return;

        if (DataContext is MarketplacePanelViewModel vm)
            vm.InstallFromDroppedFiles(files);
    }

    private static bool IsWhxPluginDrop(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return false;
        var files = e.Data.GetData(DataFormats.FileDrop) as string[];
        return files?.Any(f => f.EndsWith(".whxplugin", StringComparison.OrdinalIgnoreCase)) ?? false;
    }
}
