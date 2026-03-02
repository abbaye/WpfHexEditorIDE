//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6, Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.Docking.Core.Nodes;

namespace WpfHexEditor.Docking.Wpf;

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
    private readonly Action<DockItem> _pinToggleHandler;
    private readonly Action<DockItem, int> _reorderHandler;

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

        _tabControl.TabCloseRequested            += _closeHandler;
        _tabControl.TabDragStarted               += _dragHandler;
        _tabControl.TabFloatRequested            += _floatHandler;
        _tabControl.TabAutoHideRequested         += _autoHideHandler;
        _tabControl.TabHideRequested             += _hideHandler;
        _tabControl.TabDockAsDocumentRequested   += _dockAsDocumentHandler;
        _tabControl.TabPinToggleRequested        += _pinToggleHandler;
        _tabControl.TabReorderRequested          += _reorderHandler;
    }

    public void Dispose()
    {
        _tabControl.TabCloseRequested            -= _closeHandler;
        _tabControl.TabDragStarted               -= _dragHandler;
        _tabControl.TabFloatRequested            -= _floatHandler;
        _tabControl.TabAutoHideRequested         -= _autoHideHandler;
        _tabControl.TabHideRequested             -= _hideHandler;
        _tabControl.TabDockAsDocumentRequested   -= _dockAsDocumentHandler;
        _tabControl.TabPinToggleRequested        -= _pinToggleHandler;
        _tabControl.TabReorderRequested          -= _reorderHandler;
    }
}
