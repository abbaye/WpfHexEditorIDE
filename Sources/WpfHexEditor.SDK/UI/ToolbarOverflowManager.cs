// ==========================================================
// Project: WpfHexEditor.SDK
// File: ToolbarOverflowManager.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-08
// Description:
//     Shared helper that manages toolbar overflow for all dockable panels.
//     When a panel is too narrow to display all toolbar items, the manager
//     automatically collapses groups and shows a [...] overflow button.
//     Clicking the button opens a ContextMenu listing the hidden actions.
//
// Architecture Notes:
//     Pattern: Strategy + Observer
//     - Groups are collapsed in priority order (index 0 = first to collapse).
//     - Natural widths are captured once after the initial layout, then
//       reused for all subsequent resize calculations to avoid re-layout loops.
//     - ContextMenu items are shown/hidden by matching their Tag property
//       to the name of the collapsed group FrameworkElement.
//     - Caller is responsible for syncing IsChecked states on ContextMenu.Opened.
//     - leftFixedElements: optional array of always-visible LEFT-side elements
//       (e.g. a Scope ComboBox) whose width must be subtracted from available
//       space when the collapsible groups are also left-docked.
//
//     Usage (in panel code-behind):
//       _overflowManager = new ToolbarOverflowManager(
//           ToolbarBorder, ToolbarRightPanel, ToolbarOverflowButton,
//           OverflowContextMenu,
//           new FrameworkElement[] { TbgLowPriority, TbgHighPriority },
//           leftFixedElements: new FrameworkElement[] { TbgScope }); // optional
//
//       // After Loaded:
//       Dispatcher.InvokeAsync(_overflowManager.CaptureNaturalWidths, DispatcherPriority.Loaded);
//
//       // In SizeChanged: if (e.WidthChanged) _overflowManager.Update();
//       // In RefreshTheme: _overflowManager.InvalidateWidths();
// ==========================================================

using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace WpfHexEditor.SDK.UI;

/// <summary>
/// Manages toolbar overflow for dockable panels.
/// Automatically collapses toolbar groups when the panel is too narrow and
/// shows a [...] button to access the hidden actions.
/// </summary>
public sealed class ToolbarOverflowManager
{
    // ── Fields ────────────────────────────────────────────────────────────────

    private readonly FrameworkElement   _toolbarContainer;
    private readonly FrameworkElement   _alwaysVisiblePanel;
    private readonly ButtonBase         _overflowButton;
    private readonly ContextMenu        _overflowMenu;
    private readonly FrameworkElement[] _groups;

    /// <summary>
    /// Always-visible left-side elements (e.g. Scope ComboBox) whose widths reduce
    /// the space available for left-docked collapsible groups.
    /// </summary>
    private readonly FrameworkElement[] _leftFixedElements;

    private double[]? _naturalWidths;

    // ── Constructor ───────────────────────────────────────────────────────────

    /// <summary>
    /// Initializes the manager.
    /// </summary>
    /// <param name="toolbarContainer">The toolbar border/panel whose ActualWidth is the budget.</param>
    /// <param name="alwaysVisiblePanel">The right-side panel that is never collapsed (Category, etc.).</param>
    /// <param name="overflowButton">The [...] button — shown only when overflow is active.</param>
    /// <param name="overflowMenu">The ContextMenu attached to the overflow button.</param>
    /// <param name="groupsInCollapseOrder">
    ///   Collapsible groups, ordered from <b>lowest priority (index 0, first to collapse)</b>
    ///   to highest priority (last to collapse).
    /// </param>
    /// <param name="leftFixedElements">
    ///   Optional. Always-visible elements on the LEFT side of the toolbar (e.g. a scope
    ///   ComboBox) that also consume horizontal space. Required only when the collapsible
    ///   groups are left-docked and a separate always-visible left element is present.
    /// </param>
    public ToolbarOverflowManager(
        FrameworkElement    toolbarContainer,
        FrameworkElement    alwaysVisiblePanel,
        ButtonBase          overflowButton,
        ContextMenu         overflowMenu,
        FrameworkElement[]  groupsInCollapseOrder,
        FrameworkElement[]? leftFixedElements = null)
    {
        _toolbarContainer   = toolbarContainer;
        _alwaysVisiblePanel = alwaysVisiblePanel;
        _overflowButton     = overflowButton;
        _overflowMenu       = overflowMenu;
        _groups             = groupsInCollapseOrder;
        _leftFixedElements  = leftFixedElements ?? [];
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Captures the natural (uncollapsed) width of each group.
    /// Must be called after the initial layout pass, e.g., via
    /// <c>Dispatcher.InvokeAsync(CaptureNaturalWidths, DispatcherPriority.Loaded)</c>.
    /// Skips capture if any group is already collapsed or not yet measured (ActualWidth == 0).
    /// </summary>
    public void CaptureNaturalWidths()
    {
        // Guard: groups must be visible and already laid out (ActualWidth > 0).
        // ActualWidth stays 0 when the panel is in a hidden/inactive tab — defer
        // until the panel becomes visible and SizeChanged fires.
        if (_groups.Any(g => g.Visibility != Visibility.Visible)) return;
        if (_groups.Any(g => g.ActualWidth <= 0)) return;

        _naturalWidths = _groups.Select(g => g.ActualWidth).ToArray();

        // Immediately evaluate overflow so the button appears at startup
        // without requiring the user to trigger a first SizeChanged.
        Update();
    }

    /// <summary>
    /// Invalidates the cached widths so the next <see cref="Update"/> call
    /// will re-capture them. Call this after theme or font changes.
    /// </summary>
    public void InvalidateWidths() => _naturalWidths = null;

    /// <summary>
    /// Evaluates whether the toolbar overflows and collapses/restores groups accordingly.
    /// Call this from the toolbar container's <c>SizeChanged</c> event handler
    /// (only when <c>e.WidthChanged</c> is true).
    /// </summary>
    public void Update()
    {
        // If widths were invalidated, try to recapture with all groups visible.
        // SizeChanged fires after a layout pass, so ActualWidths should be valid here
        // as long as the panel is visible (i.e., not a hidden docking tab).
        if (_naturalWidths is null)
        {
            foreach (var g in _groups) g.Visibility = Visibility.Visible;
            _overflowButton.Visibility = Visibility.Collapsed;
            CaptureNaturalWidths();
            if (_naturalWidths is null) return; // not ready yet — wait for next SizeChanged
        }

        // Budget: total toolbar width minus always-visible right panel, left-fixed
        // elements, and a small padding margin.
        const double margin = 8.0;
        double leftFixed  = _leftFixedElements.Sum(e => e.ActualWidth);
        double available  = _toolbarContainer.ActualWidth
                          - _alwaysVisiblePanel.ActualWidth
                          - leftFixed
                          - margin;

        double totalNeeded = _naturalWidths.Sum();

        if (totalNeeded <= available)
        {
            // All fit — restore all groups and hide overflow button
            foreach (var g in _groups) g.Visibility = Visibility.Visible;
            _overflowButton.Visibility = Visibility.Collapsed;
            return;
        }

        // Overflow active: show the overflow button and subtract its cost
        _overflowButton.Visibility = Visibility.Visible;
        double overflowCost = _overflowButton.ActualWidth > 0 ? _overflowButton.ActualWidth : 26;
        available -= overflowCost;

        // Collapse groups from lowest to highest priority until items fit
        for (int i = 0; i < _groups.Length; i++)
        {
            if (totalNeeded <= available)
            {
                _groups[i].Visibility = Visibility.Visible;
            }
            else
            {
                _groups[i].Visibility = Visibility.Collapsed;
                totalNeeded -= _naturalWidths[i];
            }
        }

        SyncMenuVisibility();
    }

    /// <summary>
    /// Shows overflow menu items whose owning group is currently collapsed,
    /// and hides items whose group is visible.
    /// Items in the overflow <see cref="ContextMenu"/> must have their
    /// <c>Tag</c> property set to the <c>Name</c> of their owning group.
    /// Call this from <c>ContextMenu.Opened</c> after syncing IsChecked states.
    /// </summary>
    public void SyncMenuVisibility()
    {
        // Build a lookup: group name → is collapsed
        var collapsed = new System.Collections.Generic.Dictionary<string, bool>(_groups.Length);
        foreach (var g in _groups)
        {
            if (g.Name is { Length: > 0 } name)
                collapsed[name] = g.Visibility == Visibility.Collapsed;
        }

        foreach (var item in _overflowMenu.Items.OfType<FrameworkElement>())
        {
            if (item.Tag is string tag && collapsed.TryGetValue(tag, out bool isCollapsed))
                item.Visibility = isCollapsed ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
