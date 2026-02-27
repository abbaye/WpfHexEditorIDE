//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using WpfHexEditor.Docking.Core.Nodes;

namespace WpfHexEditor.Docking.Core;

/// <summary>
/// Engine that manipulates the <see cref="DockLayoutRoot"/> tree.
/// Provides docking, undocking, floating, closing, splitting and normalization.
/// </summary>
public class DockEngine
{
    private int _transactionDepth;

    public DockEngine(DockLayoutRoot layout)
    {
        Layout = layout ?? throw new ArgumentNullException(nameof(layout));
    }

    public DockLayoutRoot Layout { get; }

    /// <summary>True when inside a transaction (NormalizeTree deferred).</summary>
    public bool IsInTransaction => _transactionDepth > 0;

    // Events
    public event Action<DockItem>? ItemDocked;
    public event Action<DockItem>? ItemUndocked;
    public event Action<DockItem>? ItemClosed;
    public event Action<DockItem>? ItemFloated;
    public event Action<DockGroupNode>? GroupFloated;
    public event Action? LayoutChanged;

    /// <summary>
    /// Begins a transaction. NormalizeTree() calls are deferred until CommitTransaction().
    /// </summary>
    public void BeginTransaction() => _transactionDepth++;

    /// <summary>
    /// Commits the transaction and normalizes the tree.
    /// </summary>
    public void CommitTransaction()
    {
        if (_transactionDepth <= 0)
            throw new InvalidOperationException("No active transaction.");

        _transactionDepth--;
        if (_transactionDepth == 0)
        {
            NormalizeTree();
            LayoutChanged?.Invoke();
        }
    }

    /// <summary>
    /// Docks an item relative to a target group in the given direction.
    /// Direction.Center adds the item to the target group.
    /// Other directions create a new split around the target.
    /// </summary>
    public void Dock(DockItem item, DockGroupNode target, DockDirection direction)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(target);

        if (target.LockMode.HasFlag(DockLockMode.PreventSplitting) && direction != DockDirection.Center)
            throw new InvalidOperationException("Splitting is locked on the target.");

        // Remove from previous owner if any
        item.Owner?.RemoveItem(item);
        Layout.FloatingItems.Remove(item);
        Layout.AutoHideItems.Remove(item);

        if (direction == DockDirection.Center)
        {
            target.AddItem(item);
            target.ActiveItem = item;
        }
        else
        {
            // Create a new group for the item
            var newGroup = new DockGroupNode();
            newGroup.AddItem(item);

            WrapWithSplit(target, newGroup, direction);
        }

        item.State = DockItemState.Docked;

        // Track the dock side for auto-hide bar placement
        if (direction != DockDirection.Center)
        {
            item.LastDockSide = direction switch
            {
                DockDirection.Left => DockSide.Left,
                DockDirection.Right => DockSide.Right,
                DockDirection.Top => DockSide.Top,
                DockDirection.Bottom => DockSide.Bottom,
                _ => item.LastDockSide
            };
        }

        AutoNormalize();
        ItemDocked?.Invoke(item);
        if (!IsInTransaction) LayoutChanged?.Invoke();
    }

    /// <summary>
    /// Removes an item from its current group (undock). The item becomes floating.
    /// </summary>
    public void Undock(DockItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (item.Owner?.LockMode.HasFlag(DockLockMode.PreventUndocking) == true)
            throw new InvalidOperationException("Undocking is locked on the owner.");

        var previousOwner = item.Owner;
        previousOwner?.RemoveItem(item);

        item.State = DockItemState.Float;
        Layout.FloatingItems.Add(item);

        AutoNormalize();
        ItemUndocked?.Invoke(item);
        if (!IsInTransaction) LayoutChanged?.Invoke();
    }

    /// <summary>
    /// Floats an item (moves it to the floating list).
    /// </summary>
    public void Float(DockItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (item.State == DockItemState.Float)
            return;

        item.Owner?.RemoveItem(item);
        Layout.AutoHideItems.Remove(item);

        item.State = DockItemState.Float;
        if (!Layout.FloatingItems.Contains(item))
            Layout.FloatingItems.Add(item);

        AutoNormalize();
        ItemFloated?.Invoke(item);
        if (!IsInTransaction) LayoutChanged?.Invoke();
    }

    /// <summary>
    /// Closes an item, removing it from the layout entirely.
    /// </summary>
    public void Close(DockItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (item.Owner?.LockMode.HasFlag(DockLockMode.PreventClosing) == true)
            throw new InvalidOperationException("Closing is locked on the owner.");

        if (!item.CanClose)
            throw new InvalidOperationException("This item cannot be closed.");

        item.Owner?.RemoveItem(item);
        Layout.FloatingItems.Remove(item);
        Layout.AutoHideItems.Remove(item);

        item.State = DockItemState.Hidden;

        AutoNormalize();
        ItemClosed?.Invoke(item);
        if (!IsInTransaction) LayoutChanged?.Invoke();
    }

    /// <summary>
    /// Moves an item to auto-hide state.
    /// </summary>
    public void AutoHide(DockItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        item.Owner?.RemoveItem(item);
        Layout.FloatingItems.Remove(item);

        item.State = DockItemState.AutoHide;
        if (!Layout.AutoHideItems.Contains(item))
            Layout.AutoHideItems.Add(item);

        AutoNormalize();
        if (!IsInTransaction) LayoutChanged?.Invoke();
    }

    /// <summary>
    /// Restores an item from auto-hide state, docking it to the given target.
    /// If no target is specified, docks to the MainDocumentHost.
    /// </summary>
    public void RestoreFromAutoHide(DockItem item, DockGroupNode? target = null, DockDirection direction = DockDirection.Center)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (!Layout.AutoHideItems.Remove(item))
            return;

        target ??= Layout.MainDocumentHost;
        Dock(item, target, direction);
    }

    /// <summary>
    /// Moves an item from one group to another.
    /// </summary>
    public void MoveItem(DockItem item, DockGroupNode targetGroup)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(targetGroup);

        if (item.Owner == targetGroup) return;

        item.Owner?.RemoveItem(item);
        Layout.FloatingItems.Remove(item);
        Layout.AutoHideItems.Remove(item);

        targetGroup.AddItem(item);
        targetGroup.ActiveItem = item;
        item.State = DockItemState.Docked;

        AutoNormalize();
        if (!IsInTransaction) LayoutChanged?.Invoke();
    }

    /// <summary>
    /// Normalizes the dock tree:
    /// - Removes empty groups (except MainDocumentHost)
    /// - Collapses splits with a single child
    /// - Normalizes ratios
    /// </summary>
    public void NormalizeTree()
    {
        Layout.RootNode = NormalizeNode(Layout.RootNode);
        // Ensure the root has a parent of null
        Layout.RootNode.Parent = null;
    }

    private DockNode NormalizeNode(DockNode node)
    {
        if (node is DockSplitNode split)
        {
            // Recursively normalize children first
            for (var i = split.Children.Count - 1; i >= 0; i--)
            {
                var child = split.Children[i];
                var normalized = NormalizeNode(child);

                if (normalized != child)
                    split.ReplaceChild(child, normalized);
            }

            // Remove empty groups (but never the MainDocumentHost)
            for (var i = split.Children.Count - 1; i >= 0; i--)
            {
                if (split.Children[i] is DockGroupNode group && group.IsEmpty &&
                    group is not DocumentHostNode { IsMain: true })
                {
                    split.RemoveChild(group);
                }
            }

            // If split has 0 children, return an empty group placeholder
            if (split.Children.Count == 0)
                return new DockGroupNode();

            // If split has only 1 child, collapse it
            if (split.Children.Count == 1)
                return split.Children[0];

            split.NormalizeRatios();
            return split;
        }

        return node;
    }

    /// <summary>
    /// Wraps a target node with a split, placing a new node beside it.
    /// </summary>
    private void WrapWithSplit(DockNode target, DockNode newNode, DockDirection direction)
    {
        var orientation = direction is DockDirection.Left or DockDirection.Right
            ? SplitOrientation.Horizontal
            : SplitOrientation.Vertical;

        var split = new DockSplitNode { Orientation = orientation };

        var parent = target.Parent as DockSplitNode;

        if (direction is DockDirection.Left or DockDirection.Top)
        {
            split.AddChild(newNode, 0.25);
            split.AddChild(target, 0.75);
        }
        else
        {
            split.AddChild(target, 0.75);
            split.AddChild(newNode, 0.25);
        }

        if (parent is not null)
        {
            parent.ReplaceChild(target, split);
            // ReplaceChild sets target.Parent = null, but target is now inside the
            // new split node. Restore the correct parent reference.
            target.Parent = split;
        }
        else
        {
            // Target was root
            Layout.RootNode = split;
            split.Parent = null;
        }
    }

    /// <summary>
    /// Floats an entire group as a single floating window.
    /// All items are removed from the group and a new floating DockGroupNode is created.
    /// </summary>
    public void FloatGroup(DockGroupNode group)
    {
        ArgumentNullException.ThrowIfNull(group);

        var items = group.Items.ToList();
        if (items.Count == 0) return;

        var originalActive = group.ActiveItem;

        foreach (var item in items)
        {
            group.RemoveItem(item);
            Layout.AutoHideItems.Remove(item);
            item.State = DockItemState.Float;
            if (!Layout.FloatingItems.Contains(item))
                Layout.FloatingItems.Add(item);
        }

        AutoNormalize();
        if (!IsInTransaction) LayoutChanged?.Invoke();

        // Build the floating group (outside the layout tree)
        var floatingGroup = new DockGroupNode();
        foreach (var item in items)
            floatingGroup.AddItem(item);

        if (originalActive is not null && items.Contains(originalActive))
            floatingGroup.ActiveItem = originalActive;

        GroupFloated?.Invoke(floatingGroup);
    }

    private void AutoNormalize()
    {
        if (!IsInTransaction)
            NormalizeTree();
    }
}
