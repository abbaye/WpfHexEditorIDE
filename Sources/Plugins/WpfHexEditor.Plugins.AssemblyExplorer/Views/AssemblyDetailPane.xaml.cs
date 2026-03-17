// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: Views/AssemblyDetailPane.xaml.cs
// Author: Derek Tremblay
// Created: 2026-03-08
// Updated: 2026-03-16 — Phase 4: wire CfgCanvas.BlockClicked → IL tab scroll.
// Description:
//     Code-behind for the detail pane. Minimal — all state is in ViewModel.
//     OnExtractButtonClick forwards the extract request to the hosting panel.
//     CfgCanvas.BlockClicked: switches to the IL tab so the user can see the
//     matching IL_XXXX offset (CfgViewModel.RaiseBlockOffsetSelected handled by VM).
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Plugins.AssemblyExplorer.ViewModels;

namespace WpfHexEditor.Plugins.AssemblyExplorer.Views;

/// <summary>
/// Detail pane showing stub decompiled text and metadata for the selected tree node.
/// </summary>
public partial class AssemblyDetailPane : UserControl
{
    /// <summary>
    /// Raised when the user clicks the "Extract" button in the detail pane header.
    /// The hosting panel (<see cref="AssemblyExplorerPanel"/>) handles the actual
    /// extract workflow so it can access <see cref="AssemblyExplorerPanel._solutionManager"/>.
    /// </summary>
    public event EventHandler<AssemblyNodeViewModel>? ExtractRequested;

    public AssemblyDetailPane()
    {
        InitializeComponent();
        CfgCanvasControl.BlockClicked += OnCfgBlockClicked;
    }

    // The Extract button in the header bar raises this event so the panel can
    // delegate to ExecuteExtractToProjectAsync with the currently displayed node.
    private void OnExtractButtonClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is AssemblyDetailViewModel vm && vm.IsExtractAvailable)
            ExtractRequested?.Invoke(this, vm.CurrentNode!);
    }

    /// <summary>
    /// When a CFG block is clicked: notify the VM (which switches to IL tab)
    /// and forward the offset so callers can scroll the IL TextBox if needed.
    /// </summary>
    private void OnCfgBlockClicked(int offset)
    {
        if (DataContext is AssemblyDetailViewModel vm)
            vm.CfgViewModel.RaiseBlockOffsetSelected(offset);
    }
}
