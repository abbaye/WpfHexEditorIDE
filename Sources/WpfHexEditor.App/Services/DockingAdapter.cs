
//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Windows;
using WpfHexEditor.Docking.Core;
using WpfHexEditor.Docking.Core.Nodes;
using DockItemState = WpfHexEditor.Docking.Core.DockItemState;
using WpfHexEditor.Shell;
using WpfHexEditor.PluginHost.Adapters;
using WpfHexEditor.SDK.Descriptors;

namespace WpfHexEditor.App.Services;

/// <summary>
/// Bridges the PluginHost IDockingAdapter contract to the concrete WpfHexEditor docking engine.
/// Allows plugins to add/remove dockable panels and document tabs without referencing App internals.
/// </summary>
/// <remarks>
/// Uses a <paramref name="storeContent"/> callback to pre-populate MainWindow's content cache before
/// docking, so ContentFactory returns the plugin-provided UIElement on first display.
/// </remarks>
public sealed class DockingAdapter : IDockingAdapter
{
    private DockEngine _engine;
    private DockLayoutRoot _layout;
    private readonly DockControl _dockHost;
    private readonly Action<string, UIElement> _storeContent;

    // Tracks the first panel docked on each side so subsequent panels on the same
    // side are grouped as tabs (DockDirection.Center) rather than creating new splits.
    private readonly Dictionary<DockDirection, string> _sideAnchorIds = new();

    // When the session was restored from a saved layout, panels absent from that layout
    // were intentionally closed by the user. They are deferred here and only docked when
    // the user explicitly opens them (e.g. via View menu / ShowPanel).
    private readonly bool _isRestoredFromFile;
    private readonly Dictionary<string, (UIElement Content, PanelDescriptor Descriptor)> _deferredPanels = new();

    // Tracks every panel ever registered so RebindLayout() can re-defer panels that
    // are absent from a new layout (e.g. after Reset Layout or Load Layout).
    private readonly Dictionary<string, (UIElement Content, PanelDescriptor Descriptor)> _allKnownPanels = new();

    // Bulk-load optimization: when suspended, all RebuildVisualTree() calls are skipped.
    // A single rebuild is performed by ResumeRebuild() at the end of the batch.
    private bool _rebuildSuspended;

    public DockingAdapter(
        DockEngine engine,
        DockLayoutRoot layout,
        DockControl dockHost,
        Action<string, UIElement> storeContent,
        bool isRestoredFromFile = false)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        _dockHost = dockHost ?? throw new ArgumentNullException(nameof(dockHost));
        _storeContent = storeContent ?? throw new ArgumentNullException(nameof(storeContent));
        _isRestoredFromFile = isRestoredFromFile;
    }

    /// <summary>
    /// Suppresses <see cref="DockControl.RebuildVisualTree"/> calls during a bulk operation
    /// (e.g. plugin startup). Call <see cref="ResumeRebuild"/> to flush a single rebuild.
    /// Must be called on the UI thread.
    /// </summary>
    public void SuspendRebuild() => _rebuildSuspended = true;

    /// <summary>
    /// Re-enables visual tree rebuilds and performs a single rebuild to apply all
    /// pending layout changes accumulated since <see cref="SuspendRebuild"/> was called.
    /// Must be called on the UI thread.
    /// </summary>
    public void ResumeRebuild()
    {
        _rebuildSuspended = false;
        _dockHost.RebuildVisualTree();
    }

    /// <inheritdoc />
    public void AddDockablePanel(string uiId, UIElement content, PanelDescriptor descriptor)
    {
        // Always track for RebindLayout re-defer logic (covers initial dock, deferred, and restored panels).
        _allKnownPanels[uiId] = (content, descriptor);

        var existing = _layout.FindItemByContentId(uiId);
        if (existing is not null)
        {
            // Item was restored from a saved layout — override persisted flags with the current
            // plugin descriptor so stale values (e.g. CanClose=false from an old layout) do not
            // prevent the panel from being closed or hide property changes from plugin updates.
            existing.CanClose = descriptor.CanClose;
            existing.Title    = descriptor.Title;
            _storeContent(uiId, content);
            // Evict the stale placeholder from DockControl's internal cache so ContentFactory
            // is called again on the next rebuild, returning the real plugin UIElement.
            _dockHost.InvalidateContent(uiId);
            if (!_rebuildSuspended) _dockHost.RebuildVisualTree();
            return;
        }

        // Layout was restored from file and this panel was absent from it — the user had it
        // closed (or did a Reset Layout). Defer docking until the user explicitly opens it.
        if (_isRestoredFromFile)
        {
            _storeContent(uiId, content);
            _deferredPanels[uiId] = (content, descriptor);
            return;
        }

        DockNewPanel(uiId, content, descriptor);
    }

    /// <inheritdoc />
    public void RemoveDockablePanel(string uiId)
    {
        _deferredPanels.Remove(uiId);
        var item = _layout.FindItemByContentId(uiId);
        if (item is not null) _engine.Close(item);
    }

    // Scans the layout tree for an existing non-document tool-panel group whose items are
    // anchored on the given side. Used as a fallback when _sideAnchorIds has no entry for
    // the requested direction — typically after a layout restore where _sideAnchorIds was
    // not seeded because restored panels never pass through DockNewPanel.
    private DockGroupNode? FindExistingToolPanelGroup(DockDirection direction)
    {
        var targetSide = direction switch
        {
            DockDirection.Left   => DockSide.Left,
            DockDirection.Right  => DockSide.Right,
            DockDirection.Top    => DockSide.Top,
            _                    => DockSide.Bottom
        };

        return _layout.GetAllGroups()
            .Where(g => g is not DocumentHostNode && g.Items.Count > 0)
            .FirstOrDefault(g => g.Items.Any(i => i.LastDockSide == targetSide));
    }

    // Performs the actual docking of a new panel at its default side.
    // Used both for first-run auto-dock and for on-demand deferred dock.
    private void DockNewPanel(string uiId, UIElement content, PanelDescriptor descriptor)
    {
        var direction = descriptor.DefaultDockSide?.ToLowerInvariant() switch
        {
            "left"   => DockDirection.Left,
            "bottom" => DockDirection.Bottom,
            "top"    => DockDirection.Top,
            _        => DockDirection.Right
        };

        var item = new DockItem
        {
            ContentId = uiId,
            Title     = descriptor.Title,
            CanClose  = descriptor.CanClose
        };

        _storeContent(uiId, content);

        // Group panels on the same side into a single tab strip.
        // Priority:
        //   1. Registered anchor (explicit seeding or prior DockNewPanel on same side).
        //   2. Fallback scan: find any existing tool-panel group on the same side in the layout.
        //      This covers the case where a layout was restored and _sideAnchorIds was not seeded
        //      for that side — deferred panels added later (e.g. via View menu) tab alongside
        //      the already-restored panels rather than creating a new split beside the document host.
        //   3. New split beside the document host (first panel on that side).
        // Auto-hide panels are always docked first then immediately collapsed — they never serve as anchors.
        DockGroupNode? anchorGroup = null;
        if (!descriptor.DefaultAutoHide)
        {
            if (_sideAnchorIds.TryGetValue(direction, out var anchorId))
                anchorGroup = _layout.FindItemByContentId(anchorId)?.Owner;

            anchorGroup ??= FindExistingToolPanelGroup(direction);
        }

        if (anchorGroup is not null)
        {
            _engine.Dock(item, anchorGroup, DockDirection.Center);
        }
        else
        {
            _engine.Dock(item, _layout.MainDocumentHost, direction);
            if (!descriptor.DefaultAutoHide)
                _sideAnchorIds[direction] = uiId;
        }

        // Collapse to the side-bar if the plugin requests auto-hide by default.
        if (descriptor.DefaultAutoHide)
            _engine.AutoHide(item);

        if (!_rebuildSuspended) _dockHost.RebuildVisualTree();
    }

    /// <inheritdoc />
    public void AddDocumentTab(string uiId, UIElement content, DocumentDescriptor descriptor)
    {
        if (_layout.FindItemByContentId(uiId) is not null) return;

        var item = new DockItem
        {
            ContentId = uiId,
            Title = descriptor.Title,
            CanClose = descriptor.CanClose
        };

        _storeContent(uiId, content);
        _engine.Dock(item, _layout.MainDocumentHost, DockDirection.Center);
        if (!_rebuildSuspended) _dockHost.RebuildVisualTree();
    }

    /// <inheritdoc />
    public void RemoveDocumentTab(string uiId)
    {
        var item = _layout.FindItemByContentId(uiId);
        if (item is not null) _engine.Close(item);
    }

    public void ShowDockablePanel(string uiId)
    {
        // If the panel was deferred (absent from the restored layout), dock it now on first show.
        if (_deferredPanels.TryGetValue(uiId, out var deferred))
        {
            _deferredPanels.Remove(uiId);
            DockNewPanel(uiId, deferred.Content, deferred.Descriptor);
            return;
        }

        var item = _layout.FindItemByContentId(uiId);
        if (item is not null) _engine.Show(item);
    }

    public void HideDockablePanel(string uiId)
    {
        var item = _layout.FindItemByContentId(uiId);
        if (item is not null) _engine.Hide(item);
    }

    public void ToggleDockablePanel(string uiId)
    {
        // Deferred panel: first toggle = dock and show it.
        if (_deferredPanels.ContainsKey(uiId))
        {
            ShowDockablePanel(uiId);
            return;
        }

        var item = _layout.FindItemByContentId(uiId);
        if (item is null) return;
        if (item.State != DockItemState.Hidden) _engine.Hide(item); else _engine.Show(item);
    }

    public void FocusDockablePanel(string uiId)
    {
        // Deferred panel: focusing it for the first time docks and shows it.
        if (_deferredPanels.ContainsKey(uiId))
        {
            ShowDockablePanel(uiId);
            return;
        }

        var item = _layout.FindItemByContentId(uiId);
        if (item is not null) _engine.Show(item);
    }

    /// <summary>
    /// Seeds the side-anchor table with an existing host-level panel (e.g. Solution Explorer)
    /// so that plugin panels requesting the same side are automatically tabbed with it.
    /// Call this after <see cref="SetupDefaultLayout"/> for each built-in panel that should
    /// serve as a tab-group host for plugins.
    /// </summary>
    public void SeedSideAnchor(DockDirection direction, string anchorContentId)
    {
        _sideAnchorIds[direction] = anchorContentId;
    }

    /// <summary>
    /// Updates the engine/layout references when the host replaces the docking layout
    /// (e.g. Reset Layout or Load Layout). All previously known panels that are absent
    /// from the new layout are re-queued as deferred so ToggleDockablePanel can re-dock them.
    /// Must be called on the UI thread after <see cref="DockControl.Layout"/> is replaced.
    /// </summary>
    public void RebindLayout(DockEngine engine, DockLayoutRoot layout)
    {
        _engine = engine;
        _layout = layout;
        _sideAnchorIds.Clear();

        // Re-defer panels absent from the new layout so ToggleDockablePanel can re-dock them.
        foreach (var (uiId, panelEntry) in _allKnownPanels)
        {
            if (_layout.FindItemByContentId(uiId) is null)
                _deferredPanels[uiId] = panelEntry;
            else
                _deferredPanels.Remove(uiId);
        }
    }

    /// <inheritdoc />
    public bool IsPanelVisible(string uiId)
    {
        // Panel was never shown (deferred after layout restore) — treat as not visible.
        if (_deferredPanels.ContainsKey(uiId)) return false;

        var item = _layout.FindItemByContentId(uiId);

        // Panel absent from layout (not yet registered or already removed) — fail-open so
        // plugins do not permanently skip work during early startup before layout is ready.
        if (item is null) return true;

        // Hidden state means the user explicitly closed or hid the panel.
        // Docked, Float and AutoHide are all reachable by the user without opening a file again.
        return item.State != DockItemState.Hidden;
    }
}
