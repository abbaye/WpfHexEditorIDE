//////////////////////////////////////////////
// Project      : WpfHexEditor.App
// File         : MainWindow.TabGroups.cs
// Description  : Tab group management handlers — new vertical/horizontal group,
//                move between groups, close group(s), focus group by index.
//                Bridges the _Window > Tab Groups menu to DockCommands routed commands.
// Architecture : Partial class of MainWindow. All operations route through DockHost
//                (the DockControl) so CommandBindings in DockControl handle the logic.
//////////////////////////////////////////////

using System.Windows;
using WpfHexEditor.Shell.Commands;

namespace WpfHexEditor.App;

public partial class MainWindow
{
    // ── Click handlers (wired in MainWindow.xaml) ───────────────────────────

    private void OnTabGroupNewVertical(object sender, RoutedEventArgs e)
        => DockCommands.NewVerticalTabGroup.Execute(null, DockHost);

    private void OnTabGroupNewHorizontal(object sender, RoutedEventArgs e)
        => DockCommands.NewHorizontalTabGroup.Execute(null, DockHost);

    private void OnTabGroupMoveNext(object sender, RoutedEventArgs e)
        => DockCommands.MoveToNextTabGroup.Execute(null, DockHost);

    private void OnTabGroupMovePrevious(object sender, RoutedEventArgs e)
        => DockCommands.MoveToPreviousTabGroup.Execute(null, DockHost);

    private void OnTabGroupCloseCurrentGroup(object sender, RoutedEventArgs e)
    {
        var activeItem = DockHost.GetActiveDocumentItem();
        if (activeItem is not null)
            DockHost.HandleCloseTabGroup(activeItem);
    }

    private void OnTabGroupCloseAll(object sender, RoutedEventArgs e)
        => DockHost.HandleCloseAllTabGroups();

    // ── Focus group by index (0-based) ──────────────────────────────────────

    internal void FocusDocumentGroup(int index)
    {
        var hosts = _layout?.GetAllDocumentHosts().ToList();
        if (hosts is null || index >= hosts.Count) return;

        var target = hosts[index].ActiveItem ?? hosts[index].Items.FirstOrDefault();
        if (target is null) return;

        // DockHost will focus the tab control for this host via visual tree lookup
        DockHost.FocusDocumentHost(hosts[index]);
    }

    // ── Window menu state sync ───────────────────────────────────────────────

    /// <summary>
    /// Updates the enabled state of the Close/Close-All menu items based on whether
    /// multiple document tab groups are open. Wired to DockHost.LayoutChanged.
    /// </summary>
    internal void UpdateWindowTabGroupMenu()
    {
        bool hasMultipleGroups = _layout?.GetAllDocumentHosts().Skip(1).Any() ?? false;

        if (MenuItemCloseTabGroup    is not null) MenuItemCloseTabGroup.IsEnabled    = hasMultipleGroups;
        if (MenuItemCloseAllTabGroups is not null) MenuItemCloseAllTabGroups.IsEnabled = hasMultipleGroups;
        if (MenuItemMoveToNextTabGroup     is not null) MenuItemMoveToNextTabGroup.IsEnabled     = hasMultipleGroups;
        if (MenuItemMoveToPreviousTabGroup is not null) MenuItemMoveToPreviousTabGroup.IsEnabled = hasMultipleGroups;
    }
}
