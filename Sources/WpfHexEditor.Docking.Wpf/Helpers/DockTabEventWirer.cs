// ==========================================================
// Project: WpfHexEditor.Shell
// File: DockTabEventWirer.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Manages event subscriptions on a DockTabControl with deterministic cleanup.
//     Each instance wires Close, Drag, Float, AutoHide, Hide, DockAsDocument,
//     PinToggle, and Reorder events and stores the named delegates for reliable
//     unsubscription on Dispose.
//
// Architecture Notes:
//     IDisposable pattern for explicit teardown. Named delegate fields prevent
//     anonymous lambda capture bugs. Instantiated by DockControl once per
//     DockTabControl; stored in _tabWirers list for lifetime management.
//
// ==========================================================

using WpfHexEditor.Docking.Core;
using WpfHexEditor.Docking.Core.Nodes;

namespace WpfHexEditor.Shell;

/// <summary>
/// Manages event subscriptions on a <see cref="DockTabControl"/> with deterministic cleanup.
/// Each instance wires Close/Drag/Float/AutoHide events and stores named delegates
/// so they can be reliably unsubscribed in <see cref="Dispose"/>.
/// </summary>
internal sealed class DockTabEventWirer : IDisposable
{
    private readonly DockTabControl _tabControl;
    private readonly Action<DockItem> _closeHandler;
    private readonly Action<DockItem> _dragHandler;
    private readonly Action<DockItem> _floatHandler;
    private readonly Action<DockItem> _autoHideHandler;
    private readonly Action<DockItem> _hideHandler;
    private readonly Action<DockItem> _dockAsDocumentHandler;
    private readonly Action<DockItem> _restoreToToolPanelHandler;
    private readonly Action<DockItem> _pinToggleHandler;
    private readonly Action<DockItem, int> _reorderHandler;
    private readonly Action<IReadOnlyList<DockItem>> _batchCloseHandler;
    private readonly Action<DockItem> _newVerticalGroupHandler;
    private readonly Action<DockItem> _newHorizontalGroupHandler;
    private readonly Action<DockItem> _moveToNextGroupHandler;
    private readonly Action<DockItem> _moveToPreviousGroupHandler;
    private readonly Action<DockItem> _closeGroupHandler;

    public DockTabEventWirer(DockTabControl tabControl, DockControl host)
    {
        _tabControl = tabControl;

        _closeHandler = item => host.RaiseTabCloseRequested(item);

        _dragHandler = item => host.DragManager?.BeginDrag(item);

        _floatHandler = item =>
        {
            if (host.Engine is null) return;
            host.Engine.Float(item);
            host.RebuildVisualTree();
        };

        _autoHideHandler = item =>
        {
            if (host.Engine is null) return;
            // Auto-hide the entire group so all tabs move together.
            // AutoHideGroup assigns a shared AutoHideGroupId so restoring
            // any one item brings back the whole tab group.
            if (item.Owner is { } group)
                host.Engine.AutoHideGroup(group);
            else
                host.Engine.AutoHide(item);
            host.RebuildVisualTree();
        };

        _hideHandler = item =>
        {
            if (host.Engine is null) return;
            host.Engine.Hide(item);
            host.RebuildVisualTree();
        };

        _dockAsDocumentHandler = item =>
        {
            if (host.Engine is null) return;
            host.Engine.DockAsDocument(item);
            host.RebuildVisualTree();
        };

        _restoreToToolPanelHandler = item =>
        {
            if (host.Engine is null) return;
            host.Engine.RestoreToToolPanel(item);
            host.RebuildVisualTree();
        };

        _pinToggleHandler = item =>
        {
            if (item.Owner is not { } group) return;
            item.IsPinned = !item.IsPinned;

            // Reorder: pinned tabs first, preserving relative order within each group
            var current = group.Items.ToList();
            var ordered = current.Where(i => i.IsPinned)
                .Concat(current.Where(i => !i.IsPinned))
                .ToList();

            if (!current.SequenceEqual(ordered))
            {
                var active = group.ActiveItem;
                foreach (var i in current)
                    group.RemoveItem(i);
                foreach (var i in ordered)
                    group.AddItem(i);
                if (active is not null)
                    group.ActiveItem = active;
            }

            host.RebuildVisualTree();
        };

        _reorderHandler = (item, targetIndex) =>
        {
            if (item.Owner is not { } group) return;
            var active = group.ActiveItem;
            group.RemoveItem(item);
            group.InsertItem(Math.Clamp(targetIndex, 0, group.Items.Count), item);
            if (active is not null) group.ActiveItem = active;
            host.RebuildVisualTree();
        };

        // Batch-close: suppress intermediate rebuilds so wirer disposal mid-loop is avoided.
        _batchCloseHandler = items =>
        {
            host.BeginBatchClose();
            foreach (var item in items)
                host.RaiseTabCloseRequested(item);
            host.EndBatchClose();
        };

        _newVerticalGroupHandler = item => host.HandleNewTabGroup(item, DockDirection.Right);
        _newHorizontalGroupHandler = item => host.HandleNewTabGroup(item, DockDirection.Bottom);
        _moveToNextGroupHandler = item => host.HandleMoveToAdjacentGroup(item, forward: true);
        _moveToPreviousGroupHandler = item => host.HandleMoveToAdjacentGroup(item, forward: false);
        _closeGroupHandler = item => host.HandleCloseTabGroup(item);

        _tabControl.TabCloseRequested            += _closeHandler;
        _tabControl.TabBatchCloseRequested       += _batchCloseHandler;
        _tabControl.TabDragStarted               += _dragHandler;
        _tabControl.TabFloatRequested            += _floatHandler;
        _tabControl.TabAutoHideRequested         += _autoHideHandler;
        _tabControl.TabHideRequested             += _hideHandler;
        _tabControl.TabDockAsDocumentRequested       += _dockAsDocumentHandler;
        _tabControl.TabRestoreToToolPanelRequested   += _restoreToToolPanelHandler;
        _tabControl.TabPinToggleRequested            += _pinToggleHandler;
        _tabControl.TabReorderRequested          += _reorderHandler;
        _tabControl.TabNewVerticalGroupRequested     += _newVerticalGroupHandler;
        _tabControl.TabNewHorizontalGroupRequested   += _newHorizontalGroupHandler;
        _tabControl.TabMoveToNextGroupRequested      += _moveToNextGroupHandler;
        _tabControl.TabMoveToPreviousGroupRequested  += _moveToPreviousGroupHandler;
        _tabControl.TabCloseGroupRequested           += _closeGroupHandler;
    }

    public void Dispose()
    {
        _tabControl.TabCloseRequested            -= _closeHandler;
        _tabControl.TabBatchCloseRequested       -= _batchCloseHandler;
        _tabControl.TabDragStarted               -= _dragHandler;
        _tabControl.TabFloatRequested            -= _floatHandler;
        _tabControl.TabAutoHideRequested         -= _autoHideHandler;
        _tabControl.TabHideRequested             -= _hideHandler;
        _tabControl.TabDockAsDocumentRequested       -= _dockAsDocumentHandler;
        _tabControl.TabRestoreToToolPanelRequested   -= _restoreToToolPanelHandler;
        _tabControl.TabPinToggleRequested            -= _pinToggleHandler;
        _tabControl.TabReorderRequested          -= _reorderHandler;
        _tabControl.TabNewVerticalGroupRequested     -= _newVerticalGroupHandler;
        _tabControl.TabNewHorizontalGroupRequested   -= _newHorizontalGroupHandler;
        _tabControl.TabMoveToNextGroupRequested      -= _moveToNextGroupHandler;
        _tabControl.TabMoveToPreviousGroupRequested  -= _moveToPreviousGroupHandler;
        _tabControl.TabCloseGroupRequested           -= _closeGroupHandler;
    }
}
