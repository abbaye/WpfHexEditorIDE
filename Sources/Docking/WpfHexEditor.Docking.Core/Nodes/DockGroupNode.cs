//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Docking.Core.Nodes;

/// <summary>
/// A container of tabbed <see cref="DockItem"/>s (panels).
/// </summary>
public class DockGroupNode : DockNode
{
    private readonly List<DockItem> _items = [];

    public IReadOnlyList<DockItem> Items => _items;

    public DockItem? ActiveItem { get; set; }

    /// <summary>
    /// Adds an item to this group.
    /// </summary>
    public void AddItem(DockItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        item.Owner = this;
        // Always sync IsDocument to reflect the actual host type.
        // Setting only to true (never to false) corrupts tool panels dragged into DocumentHostNode.
        item.IsDocument = this is DocumentHostNode;
        _items.Add(item);
        ActiveItem ??= item;
    }

    /// <summary>
    /// Inserts an item at the specified index.
    /// </summary>
    public void InsertItem(int index, DockItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        item.Owner = this;
        // Always sync IsDocument to reflect the actual host type.
        item.IsDocument = this is DocumentHostNode;
        _items.Insert(index, item);
        ActiveItem ??= item;
    }

    /// <summary>
    /// Removes an item from this group.
    /// </summary>
    public bool RemoveItem(DockItem item)
    {
        if (!_items.Remove(item))
            return false;

        item.Owner = null;

        if (ActiveItem == item)
            ActiveItem = _items.Count > 0 ? _items[^1] : null;

        return true;
    }

    /// <summary>
    /// True if this group has no items.
    /// </summary>
    public bool IsEmpty => _items.Count == 0;
}
