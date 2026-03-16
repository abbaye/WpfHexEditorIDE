// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: Views/AssemblySearchPanel.xaml.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// Description:
//     Code-behind for the cross-assembly search panel (Phase 3).
//     Wires keyboard shortcuts, double-click navigation, and
//     ToolbarOverflowManager initialization.
//
// Architecture Notes:
//     Theme: all brushes via DynamicResource (PFP_* tokens).
//     Pattern: MVVM — delegates to AssemblySearchViewModel.
//     Panel is bottom-docked, default auto-hide, PreferredHeight=200.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using WpfHexEditor.Plugins.AssemblyExplorer.ViewModels;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.UI;

namespace WpfHexEditor.Plugins.AssemblyExplorer.Views;

/// <summary>
/// Dockable panel for cross-assembly type and member search.
/// </summary>
public partial class AssemblySearchPanel : UserControl
{
    private ToolbarOverflowManager? _overflowManager;

    // ── Constructor ───────────────────────────────────────────────────────────

    public AssemblySearchPanel(AssemblyExplorerViewModel explorerViewModel)
    {
        InitializeComponent();

        ViewModel   = new AssemblySearchViewModel(explorerViewModel);
        DataContext = ViewModel;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public AssemblySearchViewModel ViewModel { get; }

    /// <summary>Injects the IDE host context for theme registration.</summary>
    public void SetContext(IIDEHostContext context)
    {
        context.Theme.RegisterThemeAwareControl(this);
        Unloaded += (_, _) => context.Theme.UnregisterThemeAwareControl(this);
    }

    /// <summary>Focuses the search box so the user can type immediately.</summary>
    public void FocusSearchBox()
    {
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    // ── Loaded ────────────────────────────────────────────────────────────────

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _overflowManager = new ToolbarOverflowManager(
            toolbarContainer:      ToolbarBorder,
            alwaysVisiblePanel:    ToolbarRightPanel,
            overflowButton:        ToolbarOverflowButton,
            overflowMenu:          OverflowContextMenu,
            groupsInCollapseOrder: [TbgSearch]);

        Dispatcher.InvokeAsync(
            _overflowManager.CaptureNaturalWidths,
            DispatcherPriority.Loaded);
    }

    // ── Toolbar ───────────────────────────────────────────────────────────────

    private void OnOverflowButtonClick(object sender, RoutedEventArgs e)
        => OverflowContextMenu.IsOpen = true;

    private void OnOverflowMenuOpened(object sender, RoutedEventArgs e)
        => _overflowManager?.SyncMenuVisibility();

    // ── Search box Enter key ─────────────────────────────────────────────────

    private void OnSearchBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            ViewModel.SearchCommand.Execute(null);
        }
    }

    // ── DataGrid double-click ────────────────────────────────────────────────

    private void OnResultDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ResultsGrid.SelectedItem is AssemblySearchResultViewModel row)
            row.NavigateCommand.Execute(null);
    }
}
