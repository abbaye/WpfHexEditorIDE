//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.Docking.Core.Nodes;

namespace WpfHexEditor.Docking.Core;

/// <summary>
/// Root of the dock layout tree. Guarantees a <see cref="MainDocumentHost"/> always exists.
/// </summary>
public class DockLayoutRoot
{
    /// <summary>
    /// Creates a new layout with a main document host as the root node.
    /// </summary>
    public DockLayoutRoot()
    {
        MainDocumentHost = new DocumentHostNode { IsMain = true };
        RootNode = MainDocumentHost;
    }

    /// <summary>
    /// The root node of the layout tree. Never null.
    /// </summary>
    public DockNode RootNode { get; set; }

    /// <summary>
    /// The main (central) document host. Cannot be removed.
    /// </summary>
    public DocumentHostNode MainDocumentHost { get; }

    /// <summary>
    /// All floating items tracked by the layout.
    /// </summary>
    public List<DockItem> FloatingItems { get; } = [];

    /// <summary>
    /// All auto-hidden items tracked by the layout.
    /// </summary>
    public List<DockItem> AutoHideItems { get; } = [];

    /// <summary>
    /// All hidden (but not closed) items tracked by the layout.
    /// Hidden items are kept in memory for re-activation via Show().
    /// </summary>
    public List<DockItem> HiddenItems { get; } = [];

    // --- Window state (set by the host before serialization) --------

    /// <summary>
    /// Main window state: 0 = Normal, 1 = Minimized, 2 = Maximized.
    /// </summary>
    public int? WindowState { get; set; }

    /// <summary>
    /// Main window restore bounds (normal-state position/size even when maximized).
    /// </summary>
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }

    // --- Tab bar settings ----------------------------------------------------

    /// <summary>
    /// Runtime settings for the document tab bar (placement, multi-row, colorization, etc.).
    /// This is the shared <see cref="DocumentTabBarSettings"/> instance; persisted in the
    /// layout JSON and kept in sync with <c>DockControl.TabBarSettings</c>.
    /// </summary>
    public DocumentTabBarSettings? TabBarSettings { get; set; }

    /// <summary>
    /// Finds a node by its Id in the tree.
    /// </summary>
    public DockNode? FindNode(Guid id)
    {
        return FindNodeRecursive(RootNode, id);
    }

    /// <summary>
    /// Finds a dock item by its Id across all groups in the tree.
    /// </summary>
    public DockItem? FindItem(Guid itemId)
    {
        return FindItemRecursive(RootNode, itemId)
               ?? FloatingItems.FirstOrDefault(i => i.Id == itemId)
               ?? AutoHideItems.FirstOrDefault(i => i.Id == itemId)
               ?? HiddenItems.FirstOrDefault(i => i.Id == itemId);
    }

    /// <summary>
    /// Finds a dock item by its ContentId.
    /// </summary>
    public DockItem? FindItemByContentId(string contentId)
    {
        return FindItemByContentIdRecursive(RootNode, contentId)
               ?? FloatingItems.FirstOrDefault(i => i.ContentId == contentId)
               ?? AutoHideItems.FirstOrDefault(i => i.ContentId == contentId)
               ?? HiddenItems.FirstOrDefault(i => i.ContentId == contentId);
    }

    /// <summary>
    /// Returns all DocumentHostNodes in the tree.
    /// </summary>
    public IEnumerable<DocumentHostNode> GetAllDocumentHosts()
    {
        return GetNodesOfType<DocumentHostNode>(RootNode);
    }

    /// <summary>
    /// Returns all DockGroupNodes (including DocumentHostNodes) in the tree.
    /// </summary>
    public IEnumerable<DockGroupNode> GetAllGroups()
    {
        return GetNodesOfType<DockGroupNode>(RootNode);
    }

    private static DockNode? FindNodeRecursive(DockNode node, Guid id)
    {
        if (node.Id == id) return node;

        if (node is DockSplitNode split)
        {
            foreach (var child in split.Children)
            {
                var found = FindNodeRecursive(child, id);
                if (found is not null) return found;
            }
        }

        return null;
    }

    private static DockItem? FindItemRecursive(DockNode node, Guid itemId)
    {
        switch (node)
        {
            case DockGroupNode group:
                var item = group.Items.FirstOrDefault(i => i.Id == itemId);
                if (item is not null) return item;
                break;

            case DockSplitNode split:
                foreach (var child in split.Children)
                {
                    var found = FindItemRecursive(child, itemId);
                    if (found is not null) return found;
                }
                break;
        }

        return null;
    }

    private static DockItem? FindItemByContentIdRecursive(DockNode node, string contentId)
    {
        switch (node)
        {
            case DockGroupNode group:
                var item = group.Items.FirstOrDefault(i => i.ContentId == contentId);
                if (item is not null) return item;
                break;

            case DockSplitNode split:
                foreach (var child in split.Children)
                {
                    var found = FindItemByContentIdRecursive(child, contentId);
                    if (found is not null) return found;
                }
                break;
        }

        return null;
    }

    private static IEnumerable<T> GetNodesOfType<T>(DockNode node) where T : DockNode
    {
        if (node is T typed)
            yield return typed;

        if (node is DockSplitNode split)
        {
            foreach (var child in split.Children)
            foreach (var found in GetNodesOfType<T>(child))
                yield return found;
        }
    }
}
