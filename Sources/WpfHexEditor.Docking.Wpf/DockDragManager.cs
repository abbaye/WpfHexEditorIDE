//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Docking.Core;
using WpfHexEditor.Docking.Core.Nodes;

namespace WpfHexEditor.Docking.Wpf;

/// <summary>
/// Manages drag and drop operations for dock tabs.
/// Coordinates between the <see cref="DockOverlayWindow"/> and the <see cref="DockEngine"/>.
/// </summary>
public class DockDragManager
{
    private readonly DockControl _dockControl;
    private DockOverlayWindow? _overlay;
    private DockItem? _draggedItem;
    private DockDirection? _lastDirection;
    private DockTabControl? _currentTarget;

    public DockDragManager(DockControl dockControl)
    {
        _dockControl = dockControl;
    }

    public bool IsDragging => _draggedItem is not null;

    /// <summary>
    /// Starts a drag operation for the given item.
    /// </summary>
    public void BeginDrag(DockItem item)
    {
        if (IsDragging) return;

        _draggedItem = item;
        _lastDirection = null;
        _currentTarget = null;

        var data = new DataObject("DockItem", item);
        DragDrop.DoDragDrop(_dockControl, data, DragDropEffects.Move);

        // DoDragDrop is synchronous - clean up after it returns
        EndDrag();
    }

    /// <summary>
    /// Called when drag enters a tab control (DragEnter handler).
    /// </summary>
    public void OnDragEnter(DockTabControl target, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent("DockItem")) return;

        _currentTarget = target;
        e.Effects = DragDropEffects.Move;
        e.Handled = true;

        _overlay ??= new DockOverlayWindow();
        _overlay.ShowOverTarget(target);
    }

    /// <summary>
    /// Called during drag over a tab control (DragOver handler).
    /// </summary>
    public void OnDragOver(DockTabControl target, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent("DockItem")) return;

        e.Effects = DragDropEffects.Move;
        e.Handled = true;

        if (_overlay is null) return;

        var screenPoint = target.PointToScreen(e.GetPosition(target));
        _lastDirection = _overlay.HitTest(screenPoint);
        _overlay.HighlightedDirection = _lastDirection;
    }

    /// <summary>
    /// Called when drag leaves a tab control (DragLeave handler).
    /// </summary>
    public void OnDragLeave(DockTabControl target, DragEventArgs e)
    {
        _lastDirection = null;
        _currentTarget = null;
        _overlay?.Hide();
    }

    /// <summary>
    /// Called on drop (Drop handler).
    /// </summary>
    public void OnDrop(DockTabControl target, DragEventArgs e)
    {
        if (_draggedItem is null || _dockControl.Engine is null) return;
        if (!e.Data.GetDataPresent("DockItem")) return;

        e.Handled = true;

        // Use the last computed direction, default to Center
        var direction = _lastDirection ?? DockDirection.Center;
        var targetGroup = target.Node;

        if (targetGroup is null) return;

        // Remove from current location and dock at new location
        _dockControl.Engine.Dock(_draggedItem, targetGroup, direction);
        _dockControl.RebuildVisualTree();
    }

    /// <summary>
    /// Ends the drag operation and cleans up.
    /// </summary>
    public void EndDrag()
    {
        _draggedItem = null;
        _lastDirection = null;
        _currentTarget = null;
        _overlay?.Hide();
    }
}
