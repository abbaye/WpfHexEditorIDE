// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram
// File: Controls/DiagramVisualLayer.cs
// Contributors: Claude Sonnet 4.6
// Created: 2026-04-07
// Description:
//     Single FrameworkElement that renders all class nodes and
//     relationship arrows via DrawingVisual children.
//
//     Each ClassNode gets one DrawingVisual whose RenderOpen()
//     is called only when that node is dirty — O(dirty) repaint
//     instead of O(total). All arrows share a single DrawingVisual
//     that is invalidated on any structural change.
//
// Architecture Notes:
//     Pattern: Custom Visual Tree (VisualChildrenCount + GetVisualChild).
//     DrawingVisuals are lightweight WPF visuals with no layout pass.
//     DynamicResource (CD_* tokens) is resolved via TryFindResource on
//     this FrameworkElement, which IS in the logical tree.
//     Hit-testing is manual coordinate math — no per-node FrameworkElement
//     is needed for WPF to route mouse events.
// ==========================================================

using System.Globalization;
using System.Windows;
using System.Windows.Media;
using WpfHexEditor.Editor.ClassDiagram.Core.Model;

namespace WpfHexEditor.Editor.ClassDiagram.Controls;

/// <summary>
/// Renders all diagram nodes and arrows using a pool of <see cref="DrawingVisual"/> children.
/// </summary>
public sealed class DiagramVisualLayer : FrameworkElement
{
    // ── Layout constants (same as ClassBoxControl for visual consistency) ────
    private const double HeaderBaseHeight = 44.0;  // minimum header height (name only)
    private const double MemberHeight  = 20.0;
    private const double MemberPadding = 4.0;
    private const double IconWidth     = 18.0;
    private const double HorizPadding  = 8.0;
    private const double CornerRadius  = 3.0;
    private const double BoxMinWidth   = 160.0;
    private const double MaxNodeHeight  = 380.0;  // cap — "N more" footer shown when exceeded
    private const double FooterHeight   = 18.0;
    private const double GripperHeight  = 6.0;    // bottom-edge drag zone height
    private const double AccentBarWidth = 6.0;    // left accent strip width
    private const double TypeIconSize   = 16.0;   // type icon circle diameter (inline with name)
    private const double NsDashGap      = 6.0;    // gap between namespace dashes and text

    // Nodes the user has explicitly expanded past MaxNodeHeight
    private readonly HashSet<string> _expandedNodes = new(StringComparer.Ordinal);

    // Nodes explicitly collapsed to header-only (double-click or context menu)
    private readonly HashSet<string> _collapsedNodes = new(StringComparer.Ordinal);

    // Per-node custom heights set by the resize gripper drag
    private readonly Dictionary<string, double> _customHeights = new(StringComparer.Ordinal);

    private const double ArrowHeadLen  = 12.0;
    private const double ArrowHalfAng  = 25.0 * Math.PI / 180.0;
    private const double DiamondSz     = 10.0;

    private static readonly DashStyle DashedStyle = new([6, 3], 0);

    // ── Visual children ──────────────────────────────────────────────────────

    private readonly DrawingVisual                  _arrowLayer           = new();
    private readonly DrawingVisual                  _arrowHighlightLayer  = new(); // selected-arrow highlight — repainted on selection only
    private readonly Dictionary<string, DrawingVisual> _nodeVisuals       = [];
    private readonly DrawingVisual                     _swimlaneLayer     = new();
    private readonly DrawingVisual                     _selectionVisual   = new();
    private readonly DrawingVisual                     _rubberBandVisual  = new(); // top-most: rubber-band drag rect
    private readonly List<Visual>                      _visuals           = [];

    // ── State ────────────────────────────────────────────────────────────────

    private DiagramDocument?  _doc;
    private string?           _selectedNodeId;
    private string?           _hoveredNodeId;

    // ── Focus mode (Phase 12 filter) ─────────────────────────────────────────
    private HashSet<string>?  _focusedNodeIds;   // null = all visible; empty = all dimmed

    // ── Multi-selection set (for border highlight on all selected nodes) ──────
    private HashSet<string> _multiSelectedIds = [];

    // ── Pre-selection set (live rubber-band hover — Explorer-style) ───────────
    private HashSet<string> _preSelectedIds = [];

    // ── Member hover + selection ──────────────────────────────────────────────
    private string? _hoveredMemberNodeId;
    private string? _hoveredMemberId;       // ClassMember.Name (unique within node)
    private string? _selectedMemberNodeId;
    private string? _selectedMemberId;

    // ── Collapsible sections ──────────────────────────────────────────────────
    // key: nodeId, value: set of collapsed section names ("Fields","Properties","Methods","Events")
    private readonly Dictionary<string, HashSet<string>> _collapsedSections = [];

    // ── Highlighted relationship (set by Relationships panel selection) ─────
    private string? _highlightedRelId;

    // ── Performance: arrow dirty flag ────────────────────────────────────────
    // When false, RenderAllArrows() skips the base arrow layer repaint.
    // Set true after structural changes (add/delete/move nodes), false after repaint.
    private bool _arrowsDirty = true;

    // ── Performance: node size caches ────────────────────────────────────────
    // Keyed by node.Id. Invalidated on state changes (collapse/expand/section/resize).
    private readonly Dictionary<string, double> _heightCache = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double> _widthCache  = new(StringComparer.Ordinal);

    // ── Performance: viewport culling ────────────────────────────────────────
    // When set, nodes and arrows outside this rect are not painted.
    private Rect? _cullingViewport;
    // Tracks which node DrawingVisuals are currently blanked (offscreen).
    private readonly HashSet<string> _blankNodeIds = new(StringComparer.Ordinal);

    // ── Performance: Level of Detail ─────────────────────────────────────────
    // Current zoom factor forwarded from DiagramCanvas. At low zoom, simplified
    // node cards are rendered (name only, no members list).
    internal const double LodThreshold = 0.28;
    internal double CurrentZoom { get; set; } = 1.0;

    /// <summary>
    /// Highlights the relationship with the given "sourceId:targetId" key in the arrow layer (accent pen).
    /// Pass null to clear the highlight.
    /// </summary>
    public void HighlightRelationship(string? relId)
    {
        if (_highlightedRelId == relId) return;
        _highlightedRelId = relId;
        // Only repaint the highlight layer — base arrows (_arrowLayer) are unchanged.
        RenderArrowHighlight();
    }

    // ── Performance API ───────────────────────────────────────────────────────

    /// <summary>
    /// Marks the arrow layer dirty so the next <see cref="RenderAllArrows"/> call
    /// actually repaints. Called after nodes are moved or added/deleted.
    /// </summary>
    internal void MarkArrowsDirty() => _arrowsDirty = true;

    /// <summary>Forces an immediate full arrow repaint (dirty flag + render).</summary>
    internal void RefreshArrows()
    {
        _arrowsDirty = true;
        RenderAllArrows();
    }

    /// <summary>Exposes swimlane repaint for DiagramCanvas.ApplyDocumentAsync.</summary>
    internal void RenderSwimLanesPublic() => RenderSwimLanes();

    /// <summary>
    /// Creates <see cref="DrawingVisual"/> slots for all nodes in <paramref name="doc"/>
    /// without painting anything. Called by <see cref="DiagramCanvas.ApplyDocumentAsync"/>
    /// before batched rendering begins.
    /// </summary>
    public void PrepareVisualSlots(DiagramDocument doc)
    {
        _doc = doc;
        _heightCache.Clear();
        _widthCache.Clear();
        _blankNodeIds.Clear();
        _arrowsDirty = true;

        // Remove stale visuals for nodes that no longer exist
        var currentIds = doc.Classes.Select(n => n.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var id in _nodeVisuals.Keys.Except(currentIds).ToList())
        {
            RemoveVisualChild(_nodeVisuals[id]);
            _visuals.Remove(_nodeVisuals[id]);
            _nodeVisuals.Remove(id);
        }
        // Create empty DrawingVisual placeholders for new nodes
        foreach (var node in doc.Classes)
        {
            if (!_nodeVisuals.ContainsKey(node.Id))
            {
                var dv = new DrawingVisual();
                _nodeVisuals[node.Id] = dv;
                _visuals.Add(dv);
                AddVisualChild(dv);
            }
        }
        EnsureSelectionVisualOnTop();
        EnsureRubberBandVisualOnTop();
    }

    /// <summary>
    /// Updates the culling viewport and selectively re-renders nodes that
    /// entered or left the visible area. Called by DiagramCanvas on pan/zoom.
    /// </summary>
    public void InvalidateViewport(DiagramDocument doc, Rect viewport)
    {
        _cullingViewport = viewport;
        if (_doc is null) return;

        bool anyChange = false;
        foreach (var node in doc.Classes)
        {
            if (!_nodeVisuals.TryGetValue(node.Id, out var dv)) continue;
            double w = node.Width  > 4 ? node.Width  : 180;
            double h = node.Height > 4 ? node.Height : HeaderBaseHeight;
            var nb = new Rect(node.X - 4, node.Y - 4, w + 8, h + 8); // small padding for shadow
            bool inVp    = nb.IntersectsWith(viewport);
            bool wasBlank = _blankNodeIds.Contains(node.Id);

            if (inVp && wasBlank)
            {
                _blankNodeIds.Remove(node.Id);
                RenderNode(node);
                anyChange = true;
            }
            else if (!inVp && !wasBlank)
            {
                _blankNodeIds.Add(node.Id);
                using var dc = dv.RenderOpen(); // write empty = blank
                anyChange = true;
            }
        }

        if (anyChange || _arrowsDirty)
        {
            _arrowsDirty = true;
            RenderAllArrows();
        }
    }

    // ── Toggles (set by ClassDiagramSplitHost toolbar) ───────────────────────
    public bool ShowSwimLanes { get; set; } = false;

    // ── Persistence accessors (used by ClassDiagramSplitHost.GetWhcdState) ───

    /// <summary>Read-only snapshot of per-node custom heights for .whcd persistence.</summary>
    public IReadOnlyDictionary<string, double> CustomHeights => _customHeights;

    /// <summary>Read-only snapshot of collapsed sections for .whcd persistence.</summary>
    public IReadOnlyDictionary<string, HashSet<string>> CollapsedSectionMap => _collapsedSections;

    /// <summary>
    /// Restores collapsed-section state from a deserialized .whcd document.
    /// Replaces any existing in-memory state; call before RenderAll.
    /// </summary>
    public void RestoreCollapsedSections(Dictionary<string, HashSet<string>> map)
    {
        _collapsedSections.Clear();
        foreach (var kv in map)
            _collapsedSections[kv.Key] = kv.Value;
    }

    // ── FrameworkElement visual tree overrides ───────────────────────────────

    protected override int    VisualChildrenCount         => _visuals.Count;
    protected override Visual GetVisualChild(int index)   => _visuals[index];

    // ── Constructor ──────────────────────────────────────────────────────────

    public DiagramVisualLayer()
    {
        _visuals.Add(_swimlaneLayer);       // swimlane lanes behind everything
        AddVisualChild(_swimlaneLayer);
        _visuals.Add(_arrowLayer);          // base arrows above swimlanes, below nodes
        AddVisualChild(_arrowLayer);
        _visuals.Add(_arrowHighlightLayer); // selected-arrow highlight on top of base arrows
        AddVisualChild(_arrowHighlightLayer);
        // _selectionVisual added AFTER all node visuals so it renders on top
        // It is appended in EnsureSelectionVisualOnTop() after nodes are built.
    }

    // ── Selection visual (diagram-space DrawingVisual — no adorner needed) ─────

    /// <summary>
    /// Updates the multi-selection set and re-renders all affected nodes
    /// (previously selected + newly selected) so border colors update immediately.
    /// </summary>
    public void SetMultiSelection(HashSet<string> ids)
    {
        // Always copy — caller may clear the source set after this call.
        var toRepaint = new HashSet<string>(_multiSelectedIds, StringComparer.Ordinal);
        toRepaint.UnionWith(ids);
        _multiSelectedIds = new HashSet<string>(ids, StringComparer.Ordinal);
        if (_doc is null) return;
        foreach (var node in _doc.Classes.Where(n => toRepaint.Contains(n.Id)))
            RenderNode(node);
    }

    /// <summary>
    /// Updates the live pre-selection set (Explorer-style rubber-band hover) and
    /// re-renders all affected nodes in real-time. Nodes that were pre-selected but are
    /// no longer in <paramref name="ids"/> revert to their normal appearance.
    /// </summary>
    public void SetPreSelection(HashSet<string> ids)
    {
        var toRepaint = new HashSet<string>(_preSelectedIds, StringComparer.Ordinal);
        toRepaint.UnionWith(ids);
        _preSelectedIds = new HashSet<string>(ids, StringComparer.Ordinal);
        if (_doc is null) return;
        foreach (var node in _doc.Classes.Where(n => toRepaint.Contains(n.Id)))
            RenderNode(node);
    }

    /// <summary>Clears the live pre-selection set and reverts all pre-selected nodes.</summary>
    public void ClearPreSelection()
    {
        if (_preSelectedIds.Count == 0) return;
        SetPreSelection([]);
    }

    /// <summary>Draws selection rects for a set of nodes (multi-select).</summary>
    public void DrawSelectionSet(IEnumerable<Rect> nodeBounds)
    {
        EnsureSelectionVisualOnTop();
        const double HandlePx = 5.0;

        Brush borderBrush = TryFindResource("CD_SelectionBorderBrush") as Brush
            ?? new SolidColorBrush(Color.FromRgb(0, 120, 215));
        Brush handleFill = TryFindResource("CD_SelectionHandleFill") as Brush
            ?? Brushes.White;
        var pen  = new Pen(borderBrush, 1.5) { DashStyle = DashStyles.Solid };
        var hPen = new Pen(borderBrush, 1.0);

        using var dc = _selectionVisual.RenderOpen();
        foreach (var bounds in nodeBounds)
        {
            dc.DrawRectangle(null, pen, bounds);
            double l = bounds.Left,  r = bounds.Right;
            double t = bounds.Top,   b = bounds.Bottom;
            double mx = (l + r) / 2, my = (t + b) / 2;
            double h = HandlePx;
            void Handle(double cx, double cy) =>
                dc.DrawRectangle(handleFill, hPen, new Rect(cx - h, cy - h, h * 2, h * 2));
            Handle(l, t); Handle(mx, t); Handle(r, t);
            Handle(r, my);
            Handle(r, b); Handle(mx, b); Handle(l, b);
            Handle(l, my);
        }
    }

    /// <summary>Draws the selection rectangle for a single node.</summary>
    public void DrawSelection(Rect bounds) => DrawSelectionSet([bounds]);

    /// <summary>Clears the selection visual.</summary>
    public void ClearSelection()
    {
        using var dc = _selectionVisual.RenderOpen();
        // empty — clears the visual
    }

    // ── Rubber-band (Windows-Explorer-style drag selection) ───────────────────

    /// <summary>Draws the rubber-band rectangle between two diagram-space points.</summary>
    public void DrawRubberBand(Point start, Point end)
    {
        EnsureRubberBandVisualOnTop();
        var rect = new Rect(
            Math.Min(start.X, end.X), Math.Min(start.Y, end.Y),
            Math.Abs(end.X - start.X), Math.Abs(end.Y - start.Y));

        var fillBrush = new SolidColorBrush(Color.FromArgb(40, 0, 120, 215));
        fillBrush.Freeze();
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(200, 0, 120, 215)), 1.0);
        pen.Freeze();

        using var dc = _rubberBandVisual.RenderOpen();
        dc.DrawRectangle(fillBrush, pen, rect);
    }

    /// <summary>Returns the normalized Rect between start and end in diagram coords.</summary>
    public static Rect GetRubberBandRect(Point start, Point end) =>
        new(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y),
            Math.Abs(end.X - start.X), Math.Abs(end.Y - start.Y));

    /// <summary>Clears the rubber-band visual.</summary>
    public void ClearRubberBand()
    {
        using var dc = _rubberBandVisual.RenderOpen();
        // empty
    }

    private void EnsureRubberBandVisualOnTop()
    {
        if (!_visuals.Contains(_rubberBandVisual))
        {
            _visuals.Add(_rubberBandVisual);
            AddVisualChild(_rubberBandVisual);
        }
        else if (_visuals[^1] != _rubberBandVisual)
        {
            _visuals.Remove(_rubberBandVisual);
            _visuals.Add(_rubberBandVisual);
        }
    }

    // ── Member hover + selection public API ───────────────────────────────────

    /// <summary>
    /// Updates the hovered member and re-renders the affected node(s).
    /// Pass null/null to clear.
    /// </summary>
    public void SetHoveredMember(string? nodeId, string? memberId)
    {
        if (_hoveredMemberNodeId == nodeId && _hoveredMemberId == memberId) return;
        var toRepaint = new HashSet<string?> { _hoveredMemberNodeId, nodeId };
        _hoveredMemberNodeId = nodeId;
        _hoveredMemberId     = memberId;
        if (_doc is null) return;
        foreach (var id in toRepaint)
        {
            var node = _doc.Classes.FirstOrDefault(n => n.Id == id);
            if (node is not null) RenderNode(node);
        }
    }

    /// <summary>
    /// Updates the selected member and re-renders the affected node(s).
    /// Pass null/null to clear.
    /// </summary>
    public void SetSelectedMember(string? nodeId, string? memberId)
    {
        if (_selectedMemberNodeId == nodeId && _selectedMemberId == memberId) return;
        var toRepaint = new HashSet<string?> { _selectedMemberNodeId, nodeId };
        _selectedMemberNodeId = nodeId;
        _selectedMemberId     = memberId;
        if (_doc is null) return;
        foreach (var id in toRepaint)
        {
            var node = _doc.Classes.FirstOrDefault(n => n.Id == id);
            if (node is not null) RenderNode(node);
        }
    }

    private void EnsureSelectionVisualOnTop()
    {
        if (!_visuals.Contains(_selectionVisual))
        {
            _visuals.Add(_selectionVisual);
            AddVisualChild(_selectionVisual);
        }
        else if (_visuals[^1] != _selectionVisual)
        {
            // Move to top
            _visuals.Remove(_selectionVisual);
            _visuals.Add(_selectionVisual);
        }
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Full repaint: rebuilds all visual children from the document.
    /// Call after ApplyDocument.
    /// </summary>
    public void RenderAll(DiagramDocument doc, string? selectedId = null, string? hoveredId = null)
    {
        _doc            = doc;
        _selectedNodeId = selectedId;
        _hoveredNodeId  = hoveredId;
        _arrowsDirty    = true;
        _blankNodeIds.Clear();
        _heightCache.Clear();
        _widthCache.Clear();

        // Remove visuals for nodes that no longer exist
        var currentIds = doc.Classes.Select(n => n.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var id in _nodeVisuals.Keys.Except(currentIds).ToList())
        {
            RemoveVisualChild(_nodeVisuals[id]);
            _visuals.Remove(_nodeVisuals[id]);
            _nodeVisuals.Remove(id);
        }

        // Ensure a DrawingVisual exists for every node and render (with viewport culling)
        var vp = _cullingViewport;
        foreach (var node in doc.Classes)
        {
            if (!_nodeVisuals.ContainsKey(node.Id))
            {
                var dv = new DrawingVisual();
                _nodeVisuals[node.Id] = dv;
                _visuals.Add(dv);
                AddVisualChild(dv);
            }

            // Viewport culling: skip off-screen nodes (blank their visual)
            if (vp.HasValue)
            {
                double w = node.Width  > 4 ? node.Width  : 180;
                double h = node.Height > 4 ? node.Height : HeaderBaseHeight;
                var nb = new Rect(node.X - 4, node.Y - 4, w + 8, h + 8);
                if (!nb.IntersectsWith(vp.Value))
                {
                    _blankNodeIds.Add(node.Id);
                    using var emptyDc = _nodeVisuals[node.Id].RenderOpen(); // blank
                    continue;
                }
            }

            RenderNode(node);
        }

        RenderAllArrows();
        RenderSwimLanes();
    }

    /// <summary>
    /// Partial repaint: only re-renders the specified nodes and optionally arrows/swimlanes.
    /// Pass <paramref name="refreshArrows"/> = false during drag to avoid repainting all
    /// 438 arrows on every frame — call <see cref="RefreshArrows"/> once on drag end.
    /// </summary>
    public void InvalidateNodes(DiagramDocument doc, IEnumerable<string> dirtyIds,
        string? selectedId = null, string? hoveredId = null, bool refreshArrows = true)
    {
        _doc            = doc;
        _selectedNodeId = selectedId;
        _hoveredNodeId  = hoveredId;

        foreach (var id in dirtyIds)
        {
            var node = doc.Classes.FirstOrDefault(n => n.Id == id);
            if (node is not null) RenderNode(node);
        }
        if (refreshArrows)
        {
            _arrowsDirty = true;
            RenderAllArrows();
            RenderSwimLanes();
        }
        else
        {
            _arrowsDirty = true; // mark dirty so next RefreshArrows() paints
        }
    }

    /// <summary>
    /// Updates selection/hover state and re-renders affected nodes only.
    /// </summary>
    public void UpdateSelection(string? newSelectedId, string? newHoveredId)
    {
        if (_doc is null) return;

        // In LOD mode the simplified pill does not show hover highlighting, so
        // re-rendering for hover-only changes would incorrectly convert a node
        // that was rendered at full detail into a header-only LOD pill (BUG fix).
        bool lodMode = CurrentZoom < LodThreshold;

        var needRepaint = new HashSet<string?> { _selectedNodeId, newSelectedId };
        if (!lodMode) { needRepaint.Add(_hoveredNodeId); needRepaint.Add(newHoveredId); }

        _selectedNodeId = newSelectedId;
        _hoveredNodeId  = newHoveredId;

        foreach (var id in needRepaint)
        {
            if (id is null) continue;
            var node = _doc.Classes.FirstOrDefault(n => n.Id == id);
            if (node is not null) RenderNode(node);
        }
    }

    // ── Focus / filter mode (Phase 12) ──────────────────────────────────────

    /// <summary>
    /// Sets the nodes that are "in focus". Nodes outside the set are rendered at 20% opacity.
    /// Pass <c>null</c> to clear focus mode and show all nodes at full opacity.
    /// </summary>
    public void SetFocusNodes(HashSet<string>? focusedIds)
    {
        _focusedNodeIds = focusedIds;
        if (_doc is null) return;
        foreach (var node in _doc.Classes)
            RenderNode(node);
    }

    // ── Collapsible sections ─────────────────────────────────────────────────

    /// <summary>
    /// Toggles the collapsed state of the named member section in <paramref name="nodeId"/>.
    /// Re-renders only the affected node.
    /// </summary>
    public void ToggleSection(string nodeId, string sectionName)
    {
        if (!_collapsedSections.TryGetValue(nodeId, out var set))
        {
            set = [];
            _collapsedSections[nodeId] = set;
        }
        if (!set.Add(sectionName)) set.Remove(sectionName);
        InvalidateNodeSizeCache(nodeId);

        if (_doc is null) return;
        var node = _doc.Classes.FirstOrDefault(n => n.Id == nodeId);
        if (node is not null) RenderNode(node);
    }

    private bool IsSectionCollapsed(string nodeId, string sectionName) =>
        _collapsedSections.TryGetValue(nodeId, out var s) && s.Contains(sectionName);

    /// <summary>
    /// Hit-tests a section header row within the given node.
    /// Returns the section name if the point lands on a section header, or null.
    /// </summary>
    public string? HitTestSectionHeader(Point pt, ClassNode node)
    {
        double relY = pt.Y - node.Y - ComputeHeaderHeight(node) - MemberPadding;
        if (relY < 0) return null;

        double memberY = 0;
        MemberKind? lastKind = null;

        foreach (var member in node.Members)
        {
            if (lastKind.HasValue && member.Kind != lastKind)
            {
                string name = GetSectionName(member.Kind);
                // Section header strip: ~14px tall
                double secH = 14.0;
                if (relY >= memberY && relY < memberY + secH)
                    return name;
                memberY += secH;

                if (IsSectionCollapsed(node.Id, name))
                {
                    // Skip over this entire section
                    lastKind = member.Kind;
                    continue;
                }
            }
            lastKind = member.Kind;
            if (!IsSectionCollapsed(node.Id, GetSectionName(member.Kind)))
                memberY += MemberHeight;
        }
        return null;
    }

    private static string GetSectionName(MemberKind kind) => kind switch
    {
        MemberKind.Field    => "Fields",
        MemberKind.Property => "Properties",
        MemberKind.Method   => "Methods",
        MemberKind.Event    => "Events",
        _                   => string.Empty
    };

    // ── Hit-testing ──────────────────────────────────────────────────────────

    /// <summary>Returns the node at <paramref name="pt"/>, or null if none.</summary>
    public ClassNode? HitTestNode(Point pt)
    {
        if (_doc is null) return null;
        foreach (var node in _doc.Classes)
        {
            if (new Rect(node.X, node.Y, ComputeNodeWidth(node), ComputeNodeHeight(node)).Contains(pt))
                return node;
        }
        return null;
    }

    /// <summary>Returns the member row at <paramref name="pt"/> within <paramref name="node"/>, or null.</summary>
    public ClassMember? HitTestMember(Point pt, ClassNode node)
    {
        double relY = pt.Y - node.Y - ComputeHeaderHeight(node) - MemberPadding;
        if (relY < 0) return null;

        // Walk the same render loop so section headers (14px) and collapsed sections are respected.
        double memberY  = 0;
        MemberKind? lastKind = null;
        foreach (var member in node.Members)
        {
            if (lastKind.HasValue && member.Kind != lastKind)
            {
                string sec = GetSectionName(member.Kind);
                memberY += 14.0; // section header strip
                if (IsSectionCollapsed(node.Id, sec))
                {
                    lastKind = member.Kind;
                    continue;
                }
            }
            lastKind = member.Kind;
            if (IsSectionCollapsed(node.Id, GetSectionName(member.Kind))) continue;

            if (relY >= memberY && relY < memberY + MemberHeight) return member;
            memberY += MemberHeight;
        }
        return null;
    }

    /// <summary>Returns the relationship whose line is within 6px of <paramref name="pt"/>, or null.</summary>
    public ClassRelationship? HitTestArrow(Point pt)
    {
        if (_doc is null) return null;
        foreach (var rel in _doc.Relationships)
        {
            var src = _doc.FindById(rel.SourceId);
            var tgt = _doc.FindById(rel.TargetId);
            if (src is null || tgt is null) continue;

            Point p1 = NearestEdgePoint(new Rect(src.X, src.Y, src.Width, src.Height),
                                         new Rect(tgt.X, tgt.Y, tgt.Width, tgt.Height));
            Point p2 = NearestEdgePoint(new Rect(tgt.X, tgt.Y, tgt.Width, tgt.Height),
                                         new Rect(src.X, src.Y, src.Width, src.Height));

            if (PointToSegmentDistance(pt, p1, p2) <= 6.0)
                return rel;
        }
        return null;
    }

    // ── Node rendering ───────────────────────────────────────────────────────

    internal void RenderNode(ClassNode node)
    {
        if (!_nodeVisuals.TryGetValue(node.Id, out var dv)) return;
        _blankNodeIds.Remove(node.Id); // mark as rendered (not blank)

        bool isSelected    = node.Id == _selectedNodeId || _multiSelectedIds.Contains(node.Id);
        bool isPreSelected = !isSelected && _preSelectedIds.Contains(node.Id);
        bool isHovered     = node.Id == _hoveredNodeId;
        bool isDimmed      = _focusedNodeIds is not null && !_focusedNodeIds.Contains(node.Id);

        double width  = ComputeNodeWidth(node);
        double height = ComputeNodeHeight(node);

        // Keep node model in sync with computed size
        node.Width  = width;
        node.Height = height;

        // Offset the DrawingVisual so it sits at node.X, node.Y on the canvas
        dv.Offset = new Vector(node.X, node.Y);

        using var dc = dv.RenderOpen();

        // ── Level of Detail (LOD) ─────────────────────────────────────────────
        // At very low zoom levels render a simplified pill (name + type only)
        // to avoid the cost of member layout, section headers and shadows.
        if (CurrentZoom < LodThreshold)
        {
            Brush lodHeaderBg  = Res("CD_ClassBoxHeaderBackground", Color.FromRgb(40, 40, 70));
            Brush lodBorder    = isSelected
                ? Res("CD_ClassBoxSelectedBorderBrush", Color.FromRgb(0, 120, 215))
                : Res("CD_ClassBoxBorderBrush",         Color.FromRgb(80, 80, 100));
            var lodRect = new Rect(0, 0, width, HeaderBaseHeight);
            if (isDimmed) dc.PushOpacity(0.2);
            dc.DrawRoundedRectangle(lodHeaderBg, new Pen(lodBorder, isSelected ? 2.0 : 1.0),
                lodRect, CornerRadius, CornerRadius);
            // LOD accent bar
            Color lodAccent = GetAccentColor(node);
            var lodAccentBrush = new SolidColorBrush(lodAccent);
            lodAccentBrush.Freeze();
            var lodClip = new System.Windows.Media.RectangleGeometry(lodRect, CornerRadius, CornerRadius);
            dc.PushClip(lodClip);
            dc.DrawRectangle(lodAccentBrush, null, new Rect(0, 0, AccentBarWidth, HeaderBaseHeight));
            dc.Pop();
            Brush lodName = Res("CD_ClassNameForeground", Color.FromRgb(220, 220, 255));
            var lodFt = MakeFT(node.Name, lodName, 10.0, bold: true);
            dc.DrawText(lodFt, new Point((width - lodFt.Width) / 2,
                (HeaderBaseHeight - lodFt.Height) / 2));
            if (isDimmed) dc.Pop();
            return; // skip full member rendering at this zoom level
        }

        if (isDimmed) dc.PushOpacity(0.2);

        Brush boxBg     = Res("CD_ClassBoxBackground",       Color.FromRgb(50, 50, 60));
        Brush headerBg  = Res("CD_ClassBoxHeaderBackground", Color.FromRgb(40, 40, 70));
        Brush nameColor = Res("CD_ClassNameForeground",      Color.FromRgb(220, 220, 255));
        Brush sterColor = Res("CD_StereotypeForeground",     Color.FromRgb(160, 160, 200));
        Brush memberFg  = Res("CD_MemberTextForeground",     Color.FromRgb(200, 200, 210));
        Brush divBrush  = Res("CD_ClassBoxSectionDivider",   Color.FromRgb(70, 70, 90));
        Brush boxBorder = isSelected
            ? Res("CD_ClassBoxSelectedBorderBrush", Color.FromRgb(0, 120, 215))
            : (isPreSelected
                ? Res("CD_ClassBoxSelectedBorderBrush", Color.FromRgb(0, 120, 215))
                : (isHovered
                    ? Res("CD_ClassBoxHoverBorderBrush", Color.FromRgb(110, 110, 150))
                    : Res("CD_ClassBoxBorderBrush", Color.FromRgb(80, 80, 100))));

        double borderThk = isSelected ? 2.0 : (isPreSelected ? 1.5 : 1.0);
        var boxPen  = new Pen(boxBorder, borderThk);
        var divPen  = new Pen(divBrush, 0.5);

        double headerH = ComputeHeaderHeight(node);
        var boxRect    = new Rect(0, 0, width, height);
        var headerRect = new Rect(0, 0, width, headerH);

        // B6 — Drop shadow (offset semi-transparent rect drawn before main box)
        var shadowBrush = new SolidColorBrush(Color.FromArgb(55, 0, 0, 0));
        dc.DrawRoundedRectangle(shadowBrush, null, new Rect(3, 4, width, height), CornerRadius, CornerRadius);

        // Box background
        dc.DrawRoundedRectangle(boxBg, boxPen, boxRect, CornerRadius, CornerRadius);

        // Explorer-style pre-selection overlay (rubber-band hover, live feedback)
        if (isPreSelected)
        {
            var preBrush = new SolidColorBrush(Color.FromArgb(55, 0, 120, 215));
            preBrush.Freeze();
            dc.DrawRoundedRectangle(preBrush, null, boxRect, CornerRadius, CornerRadius);
        }

        // ── Gradient header with accent bar ──────────────────────────────────
        Color accentColor = GetAccentColor(node);
        Color headerBase = headerBg is SolidColorBrush scb ? scb.Color : Color.FromRgb(37, 40, 64);
        byte lr = (byte)Math.Min(255, headerBase.R + 22);
        byte lg = (byte)Math.Min(255, headerBase.G + 22);
        byte lb = (byte)Math.Min(255, headerBase.B + 28);
        var gradHeader = new LinearGradientBrush(
            Color.FromRgb(lr, lg, lb), headerBase, new Point(0, 0), new Point(0, 1));
        dc.DrawRoundedRectangle(gradHeader, null, headerRect, CornerRadius, CornerRadius);
        dc.DrawRectangle(gradHeader, null, new Rect(0, headerH / 2, width, headerH / 2));

        // Left accent bar (6px, gradient fade bottom, clipped to header)
        var accentBrush = new SolidColorBrush(accentColor);
        accentBrush.Freeze();
        var accentGrad = new LinearGradientBrush(
            accentColor, Color.FromArgb(60, accentColor.R, accentColor.G, accentColor.B),
            new Point(0, 0), new Point(0, 1));
        var headerClip = new System.Windows.Media.RectangleGeometry(headerRect, CornerRadius, CornerRadius);
        dc.PushClip(headerClip);
        dc.DrawRectangle(accentGrad, null, new Rect(0, 0, AccentBarWidth, headerH));
        dc.Pop();

        // ── Row 1: [icon] Name ───────────────────────────────────────────────
        var nameFt = MakeFT(node.Name, nameColor, 13.0, bold: true);
        double nameRowH = Math.Max(TypeIconSize, nameFt.Height);
        double nameRowY = 6.0;

        // Type icon circle (inline left of name)
        double iconX = AccentBarWidth + 6.0;
        double iconCY = nameRowY + nameRowH / 2;
        var iconCircleBrush = new SolidColorBrush(Color.FromArgb(50, accentColor.R, accentColor.G, accentColor.B));
        iconCircleBrush.Freeze();
        dc.DrawEllipse(iconCircleBrush, new Pen(accentBrush, 1.2),
            new Point(iconX + TypeIconSize / 2, iconCY), TypeIconSize / 2, TypeIconSize / 2);
        var glyphFt = MakeFT(GetTypeGlyph(node), accentBrush, 9.5, bold: true);
        dc.DrawText(glyphFt, new Point(iconX + (TypeIconSize - glyphFt.Width) / 2,
            iconCY - glyphFt.Height / 2));

        // Name (centered in remaining space after accent bar)
        double nameAreaLeft = AccentBarWidth;
        double nameAreaW    = width - nameAreaLeft;
        dc.DrawText(nameFt, new Point(nameAreaLeft + (nameAreaW - nameFt.Width) / 2, nameRowY + (nameRowH - nameFt.Height) / 2));

        double textY = nameRowY + nameRowH + 2.0;

        // ── Row 2: «stereotype · Attr1, Attr2» (merged, centered) ───────────
        string stereotype = GetStereotype(node);
        string mergedLabel = BuildStereotypeLabel(node, stereotype);
        if (mergedLabel.Length > 0)
        {
            var sterFt = MakeFT(mergedLabel, sterColor, 9.0, italic: true);
            sterFt.MaxTextWidth = width - AccentBarWidth - HorizPadding * 2;
            sterFt.Trimming     = System.Windows.TextTrimming.CharacterEllipsis;
            double sterW = Math.Min(sterFt.Width, sterFt.MaxTextWidth);
            dc.DrawText(sterFt, new Point(nameAreaLeft + (nameAreaW - sterW) / 2, textY));
            textY += sterFt.Height + 2.0;
        }

        // ── Row 3: ── Namespace ── (with decorative dashes) ──────────────────
        if (!string.IsNullOrEmpty(node.Namespace))
        {
            var nsFt = MakeFT(node.Namespace, sterColor, 8.5);
            nsFt.MaxTextWidth = width - AccentBarWidth - HorizPadding * 2 - 40; // leave room for dashes
            nsFt.Trimming     = System.Windows.TextTrimming.CharacterEllipsis;
            double nsW   = Math.Min(nsFt.Width, nsFt.MaxTextWidth);
            double nsCX  = nameAreaLeft + nameAreaW / 2;
            double nsX   = nsCX - nsW / 2;
            double nsY   = textY;
            double nsMid = nsY + nsFt.Height / 2;

            // Decorative dashes left and right of namespace
            Brush nsDashBrush = Res("CD_NamespacePillBackground", Color.FromArgb(80, 160, 160, 200));
            var nsDashPen = new Pen(nsDashBrush, 0.8);
            double dashLeft  = AccentBarWidth + HorizPadding;
            double dashRight = width - HorizPadding;
            if (nsX - NsDashGap > dashLeft + 8)
                dc.DrawLine(nsDashPen, new Point(dashLeft, nsMid), new Point(nsX - NsDashGap, nsMid));
            if (nsX + nsW + NsDashGap < dashRight - 8)
                dc.DrawLine(nsDashPen, new Point(nsX + nsW + NsDashGap, nsMid), new Point(dashRight, nsMid));

            dc.DrawText(nsFt, new Point(nsX, nsY));
        }

        // ── Header divider (gradient fade at edges) ──────────────────────────
        var divGrad = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0.5),
            EndPoint   = new Point(1, 0.5)
        };
        Color divColor = divBrush is SolidColorBrush dvScb ? dvScb.Color : Color.FromRgb(70, 70, 90);
        divGrad.GradientStops.Add(new GradientStop(Colors.Transparent, 0.0));
        divGrad.GradientStops.Add(new GradientStop(divColor, 0.15));
        divGrad.GradientStops.Add(new GradientStop(divColor, 0.85));
        divGrad.GradientStops.Add(new GradientStop(Colors.Transparent, 1.0));
        dc.DrawRectangle(divGrad, null, new Rect(0, headerH - 0.5, width, 1.0));

        // Metrics badges (top-right corner of header)
        if (node.Metrics != ClassMetrics.Empty)
        {
            DrawMetricsBadge(dc, node, width);
        }

        // Skip member rendering for collapsed nodes — header already drawn above
        if (_collapsedNodes.Contains(node.Id))
        {
            if (isDimmed) dc.Pop();
            return;
        }

        // Member rows — clipped to the box height so content never overflows below the border
        double memberY = headerH + MemberPadding;
        MemberKind? lastKind   = null;
        double textClipW       = width - HorizPadding - IconWidth - HorizPadding;
        var    divPenDashed    = new Pen(divBrush, 0.5) { DashStyle = new DashStyle([2, 2], 0) };

        // Clip entire member section to [headerH … height] so capped nodes don't bleed
        dc.PushClip(new System.Windows.Media.RectangleGeometry(
            new Rect(0, headerH, width, Math.Max(0, height - headerH))));

        // isCapped = there are members that don't fit at the CURRENT display height
        // (custom height via gripper may already show all members — fullH <= height → not capped)
        bool isCapped  = !_expandedNodes.Contains(node.Id) && ComputeNodeHeightFull(node) > height;
        double memberYLimit = isCapped ? height - FooterHeight : height;

        foreach (var member in node.Members)
        {
            // Stop drawing members when we've reached the cap boundary
            if (isCapped && memberY + MemberHeight > memberYLimit) break;
            // B4 — Section group header when kind changes
            if (lastKind.HasValue && member.Kind != lastKind)
            {
                string sectionName = GetSectionName(member.Kind);
                bool collapsed = IsSectionCollapsed(node.Id, sectionName);

                // Dashed divider
                dc.DrawLine(divPenDashed,
                    new Point(HorizPadding, memberY), new Point(width - HorizPadding, memberY));

                const double secH = 14.0;
                // Section header background strip (subtle)
                dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(30, 150, 150, 200)),
                    null, new Rect(0, memberY, width, secH));

                // ▶/▼ triangle indicator
                string triangle = collapsed ? "▶" : "▼";
                var triFt = MakeFT(triangle, sterColor, 7.0);
                dc.DrawText(triFt, new Point(HorizPadding, memberY + (secH - triFt.Height) / 2));

                // Section label
                if (sectionName.Length > 0)
                {
                    var secFt = MakeFT(sectionName.ToUpperInvariant(), sterColor, 7.5);
                    dc.DrawText(secFt, new Point(HorizPadding + triFt.Width + 3, memberY + (secH - secFt.Height) / 2));
                }
                memberY += secH;
            }
            lastKind = member.Kind;

            // Skip rendering if section is collapsed
            if (IsSectionCollapsed(node.Id, GetSectionName(member.Kind)))
                continue;

            // Member row hover / selection highlight
            bool memberHovered  = node.Id == _hoveredMemberNodeId  && member.Name == _hoveredMemberId;
            bool memberSelected = node.Id == _selectedMemberNodeId && member.Name == _selectedMemberId;
            if (memberSelected)
                dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(60, 0, 120, 215)), null,
                    new Rect(0, memberY, width, MemberHeight));
            else if (memberHovered)
                dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(30, 160, 160, 255)), null,
                    new Rect(0, memberY, width, MemberHeight));

            // B3 — Visibility color circle
            Color circleColor = member.Visibility switch
            {
                MemberVisibility.Public    => Color.FromRgb(78, 201, 78),   // green
                MemberVisibility.Protected => Color.FromRgb(255, 152, 0),   // orange
                MemberVisibility.Private   => Color.FromRgb(244, 67, 54),   // red
                _                          => Color.FromRgb(33, 150, 243)   // blue (internal)
            };
            double circleR  = 3.5;
            double circleX  = HorizPadding + circleR;
            double circleY  = memberY + MemberHeight / 2.0;
            dc.DrawEllipse(new SolidColorBrush(circleColor), null,
                new Point(circleX, circleY), circleR, circleR);

            string prefix   = GetVisibilityPrefix(member.Visibility);
            string label    = BuildMemberLabel(member);
            Brush  iconBrush = GetMemberIconBrush(member.Kind);

            // Kind type indicator (small coloured initial: F/P/M/E) after circle
            string kindChar = member.Kind switch
            {
                MemberKind.Field    => "f",
                MemberKind.Property => "p",
                MemberKind.Method   => "m",
                MemberKind.Event    => "e",
                _                   => "·"
            };
            var kindFt = MakeFT(kindChar, iconBrush, 9.5, italic: true);
            double kindX = HorizPadding + circleR * 2 + 3.0;
            dc.DrawText(kindFt, new Point(kindX, memberY + (MemberHeight - kindFt.Height) / 2));

            double textStartX = kindX + kindFt.Width + 3.0;
            double textClipWK = Math.Max(1, width - textStartX - HorizPadding);
            var textFt = MakeFT(prefix + label, memberFg, 11.0);

            // Clip member text to prevent overflow beyond box right edge
            dc.PushClip(new System.Windows.Media.RectangleGeometry(
                new Rect(textStartX, memberY, textClipWK, MemberHeight)));
            dc.DrawText(textFt, new Point(textStartX, memberY + (MemberHeight - textFt.Height) / 2));
            dc.Pop();

            memberY += MemberHeight;
        }

        // "N more" footer when node is capped
        if (isCapped)
        {
            int hidden = CountHiddenMembers(node, height);
            if (hidden > 0)
            {
                double footerY = height - FooterHeight;
                var footerBg = new SolidColorBrush(Color.FromArgb(80, 120, 120, 180));
                dc.DrawRectangle(footerBg, null, new Rect(0, footerY, width, FooterHeight));
                string label = $"▼  {hidden} more member{(hidden > 1 ? "s" : "")}…";
                var footerFt = MakeFT(label, sterColor, 9.5);
                dc.DrawText(footerFt, new Point((width - footerFt.Width) / 2, footerY + (FooterHeight - footerFt.Height) / 2));
            }
        }

        dc.Pop(); // pop member-area clip

        if (isDimmed) dc.Pop();
    }

    private void DrawMetricsBadge(DrawingContext dc, ClassNode node, double boxWidth)
    {
        const double badgeW = 42, badgeH = 14, gap = 3, badgeX_offset = 5;
        double badgeX = boxWidth - badgeW - badgeX_offset;
        var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(60, 0, 0, 0)), 0.8);

        // Badge 1: Instability (I=x.xx)
        double instability = node.Metrics.Instability;
        Color instColor = instability switch
        {
            < 0.35 => Color.FromRgb(0, 180, 80),
            < 0.65 => Color.FromRgb(200, 160, 0),
            _      => Color.FromRgb(200, 60, 60)
        };
        double badgeY1 = 3;
        dc.DrawRoundedRectangle(
            new SolidColorBrush(Color.FromArgb(200, instColor.R, instColor.G, instColor.B)),
            borderPen, new Rect(badgeX, badgeY1, badgeW, badgeH), 3.5, 3.5);
        var ft1 = MakeFT($"I={instability:F2}", Brushes.White, 8.0, bold: true);
        dc.DrawText(ft1, new Point(badgeX + (badgeW - ft1.Width) / 2, badgeY1 + (badgeH - ft1.Height) / 2));

        // Badge 2: Member count (M:count)
        int memberCount = node.Members.Count;
        Color cntColor = memberCount switch
        {
            <= 15 => Color.FromRgb(0, 150, 100),
            <= 30 => Color.FromRgb(180, 140, 0),
            _     => Color.FromRgb(200, 60, 60)
        };
        double badgeY2 = badgeY1 + badgeH + gap;
        dc.DrawRoundedRectangle(
            new SolidColorBrush(Color.FromArgb(200, cntColor.R, cntColor.G, cntColor.B)),
            borderPen, new Rect(badgeX, badgeY2, badgeW, badgeH), 3.5, 3.5);
        var ft2 = MakeFT($"M:{memberCount}", Brushes.White, 8.0, bold: true);
        dc.DrawText(ft2, new Point(badgeX + (badgeW - ft2.Width) / 2, badgeY2 + (badgeH - ft2.Height) / 2));
    }

    // ── Arrow rendering ──────────────────────────────────────────────────────

    private void RenderAllArrows()
    {
        if (_doc is null) return;

        if (!_arrowsDirty)
        {
            // Base arrows unchanged — only re-render the highlight layer for selected arrow
            RenderArrowHighlight();
            return;
        }
        _arrowsDirty = false;

        var vp = _cullingViewport;
        using var dc = _arrowLayer.RenderOpen();

        foreach (var rel in _doc.Relationships)
        {
            var src = _doc.FindById(rel.SourceId);
            var tgt = _doc.FindById(rel.TargetId);
            if (src is null || tgt is null) continue;

            var srcRect = new Rect(src.X, src.Y, src.Width, src.Height);
            var tgtRect = new Rect(tgt.X, tgt.Y, tgt.Width, tgt.Height);

            // Viewport culling: skip arrows whose bounding box is entirely offscreen
            if (vp.HasValue)
            {
                double minX = Math.Min(srcRect.Left,  tgtRect.Left)  - 20;
                double minY = Math.Min(srcRect.Top,   tgtRect.Top)   - 20;
                double maxX = Math.Max(srcRect.Right, tgtRect.Right) + 20;
                double maxY = Math.Max(srcRect.Bottom,tgtRect.Bottom)+ 20;
                if (!new Rect(minX, minY, maxX - minX, maxY - minY).IntersectsWith(vp.Value))
                    continue;
            }

            Point p1 = NearestEdgePoint(srcRect, tgtRect);
            Point p2 = NearestEdgePoint(tgtRect, srcRect);

            bool isHighlighted = _highlightedRelId is not null
                && $"{rel.SourceId}:{rel.TargetId}" == _highlightedRelId;
            // Highlighted arrows are drawn on the separate _arrowHighlightLayer (not here)
            if (isHighlighted) continue;
            Brush lineBrush = GetArrowBrush(rel.Kind);
            double lineThickness = 1.5;
            var pen = new Pen(lineBrush, lineThickness);
            if (rel.Kind == RelationshipKind.Dependency || rel.Kind == RelationshipKind.Realization)
                pen.DashStyle = DashedStyle;

            // Build waypoint chain: p1 → waypoints → p2
            var wayPts = rel.Waypoints;
            if (wayPts.Count > 0)
            {
                Point prev = p1;
                foreach (var (wx, wy) in wayPts)
                {
                    var wp = new Point(wx, wy);
                    dc.DrawLine(pen, prev, wp);
                    prev = wp;
                }
                dc.DrawLine(pen, prev, p2);
                // Arrow points at the last segment direction.
                Point lastWp = new(wayPts[^1].X, wayPts[^1].Y);
                DrawArrowHead(dc, lastWp, p2, rel.Kind, lineBrush, lineThickness);
                DrawTailDecoration(dc, p1, new Point(wayPts[0].X, wayPts[0].Y), rel.Kind, lineBrush, lineThickness);
            }
            else
            {
                dc.DrawLine(pen, p1, p2);
                DrawArrowHead(dc, p1, p2, rel.Kind, lineBrush, lineThickness);
                DrawTailDecoration(dc, p1, p2, rel.Kind, lineBrush, lineThickness);
            }

            DrawMultiplicity(dc, p1, p2, rel, lineBrush);

            // Role labels
            if (!string.IsNullOrWhiteSpace(rel.SourceRole))
            {
                Vector d = p2 - p1; if (d.LengthSquared > 0.001) d.Normalize();
                Vector perp = new(-d.Y, d.X);
                Point labelPos = p1 + d * 20 - perp * 12;
                var ft = MakeFT(rel.SourceRole!, lineBrush, 9.0);
                dc.DrawText(ft, labelPos);
            }
            if (!string.IsNullOrWhiteSpace(rel.TargetRole))
            {
                Vector d = p1 - p2; if (d.LengthSquared > 0.001) d.Normalize();
                Vector perp = new(-d.Y, d.X);
                Point labelPos = p2 + d * 20 - perp * 12;
                var ft = MakeFT(rel.TargetRole!, lineBrush, 9.0);
                dc.DrawText(ft, labelPos);
            }

            if (!string.IsNullOrWhiteSpace(rel.Label))
            {
                Point mid = new((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2);
                var ft = MakeFT(rel.Label!, lineBrush, 10.0);
                double lx = mid.X - ft.Width / 2;
                double ly = mid.Y - ft.Height / 2 - 8;

                // B7 — Pill background behind label for readability
                var pillBrush = new SolidColorBrush(Color.FromArgb(170, 20, 22, 38));
                dc.DrawRoundedRectangle(pillBrush, null,
                    new Rect(lx - 4, ly - 2, ft.Width + 8, ft.Height + 4), 3, 3);
                dc.DrawText(ft, new Point(lx, ly));
            }

            // B8 — Connection port dots at arrow endpoints
            var lineColor  = (lineBrush as SolidColorBrush)?.Color ?? Color.FromRgb(140, 140, 180);
            var portBrush  = new SolidColorBrush(Color.FromArgb(180, lineColor.R, lineColor.G, lineColor.B));
            dc.DrawEllipse(portBrush, null, p1, 2.5, 2.5);
            dc.DrawEllipse(portBrush, null, p2, 2.5, 2.5);
        }

        // Always update the highlight layer after base arrows are repainted
        RenderArrowHighlight();
    }

    /// <summary>
    /// Repaints only the selected-arrow highlight layer (<see cref="_arrowHighlightLayer"/>).
    /// Called on selection change without touching the base <see cref="_arrowLayer"/>.
    /// </summary>
    private void RenderArrowHighlight()
    {
        using var hdc = _arrowHighlightLayer.RenderOpen();
        if (_doc is null || _highlightedRelId is null) return;

        var rel = _doc.Relationships.FirstOrDefault(r => $"{r.SourceId}:{r.TargetId}" == _highlightedRelId);
        if (rel is null) return;
        var src = _doc.FindById(rel.SourceId);
        var tgt = _doc.FindById(rel.TargetId);
        if (src is null || tgt is null) return;

        var srcRect = new Rect(src.X, src.Y, src.Width, src.Height);
        var tgtRect = new Rect(tgt.X, tgt.Y, tgt.Width, tgt.Height);
        Point p1 = NearestEdgePoint(srcRect, tgtRect);
        Point p2 = NearestEdgePoint(tgtRect, srcRect);

        Brush accentBrush = TryFindResource("CD_ClassBoxSelectedBorderBrush") as Brush
            ?? new SolidColorBrush(Color.FromRgb(0, 120, 215));
        var pen = new Pen(accentBrush, 2.5);
        if (rel.Kind == RelationshipKind.Dependency || rel.Kind == RelationshipKind.Realization)
            pen.DashStyle = DashedStyle;

        var wayPts = rel.Waypoints;
        if (wayPts.Count > 0)
        {
            Point prev = p1;
            foreach (var (wx, wy) in wayPts)
            {
                var wp = new Point(wx, wy);
                hdc.DrawLine(pen, prev, wp);
                prev = wp;
            }
            hdc.DrawLine(pen, prev, p2);
            DrawArrowHead(hdc, new Point(wayPts[^1].X, wayPts[^1].Y), p2, rel.Kind, accentBrush, 2.5);
            DrawTailDecoration(hdc, p1, new Point(wayPts[0].X, wayPts[0].Y), rel.Kind, accentBrush, 2.5);
        }
        else
        {
            hdc.DrawLine(pen, p1, p2);
            DrawArrowHead(hdc, p1, p2, rel.Kind, accentBrush, 2.5);
            DrawTailDecoration(hdc, p1, p2, rel.Kind, accentBrush, 2.5);
        }
    }

    private static void DrawArrowHead(DrawingContext dc, Point from, Point to,
        RelationshipKind kind, Brush brush, double thickness)
    {
        Vector dir = to - from;
        if (dir.LengthSquared < 0.001) return;
        dir.Normalize();

        Point p1 = to - RotateVec(dir,  ArrowHalfAng) * ArrowHeadLen;
        Point p2 = to - RotateVec(dir, -ArrowHalfAng) * ArrowHeadLen;
        var pen = new Pen(brush, thickness);

        if (kind is RelationshipKind.Inheritance)
        {
            // Hollow closed triangle (UML generalisation)
            var tri = new StreamGeometry();
            using (var ctx = tri.Open()) { ctx.BeginFigure(to, true, true); ctx.LineTo(p1, true, false); ctx.LineTo(p2, true, false); }
            tri.Freeze();
            dc.DrawGeometry(Brushes.White, pen, tri);
        }
        else if (kind is RelationshipKind.Realization)
        {
            // Hollow closed triangle (same shape but dashed line)
            var tri = new StreamGeometry();
            using (var ctx = tri.Open()) { ctx.BeginFigure(to, true, true); ctx.LineTo(p1, true, false); ctx.LineTo(p2, true, false); }
            tri.Freeze();
            dc.DrawGeometry(Brushes.White, pen, tri);
        }
        else
        {
            // Open arrowhead
            dc.DrawLine(pen, to, p1);
            dc.DrawLine(pen, to, p2);
        }
    }

    private static void DrawTailDecoration(DrawingContext dc, Point targetEnd, Point sourceEnd,
        RelationshipKind kind, Brush brush, double thickness)
    {
        if (kind is not (RelationshipKind.Aggregation or RelationshipKind.Composition)) return;

        Vector dir = sourceEnd - targetEnd;
        if (dir.LengthSquared < 0.001) return;
        dir.Normalize();

        Vector perp = new(-dir.Y, dir.X);
        Point tip   = sourceEnd;
        Point left  = sourceEnd + perp * DiamondSz * 0.5;
        Point right = sourceEnd - perp * DiamondSz * 0.5;
        Point back  = sourceEnd + dir * DiamondSz;

        var diamond = new StreamGeometry();
        using (var ctx = diamond.Open())
        { ctx.BeginFigure(tip, true, true); ctx.LineTo(left, true, false); ctx.LineTo(back, true, false); ctx.LineTo(right, true, false); }
        diamond.Freeze();

        Brush fill = kind == RelationshipKind.Composition ? brush : Brushes.White;
        dc.DrawGeometry(fill, new Pen(brush, thickness), diamond);
    }

    private static void DrawMultiplicity(DrawingContext dc, Point p1, Point p2,
        ClassRelationship rel, Brush brush)
    {
        const double Offset = 14.0;
        Vector dir = p2 - p1;
        if (dir.LengthSquared < 0.001) return;
        dir.Normalize();
        Vector perp = new(-dir.Y, dir.X);

        if (!string.IsNullOrWhiteSpace(rel.SourceMultiplicity))
        {
            Point labelPos = p1 + dir * Offset + perp * 6;
            var ft = new FormattedText(rel.SourceMultiplicity!, CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, new Typeface("Segoe UI"), 9.0, brush, 96.0);
            dc.DrawText(ft, labelPos);
        }
        if (!string.IsNullOrWhiteSpace(rel.TargetMultiplicity))
        {
            Point labelPos = p2 - dir * Offset + perp * 6;
            var ft = new FormattedText(rel.TargetMultiplicity!, CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, new Typeface("Segoe UI"), 9.0, brush, 96.0);
            dc.DrawText(ft, labelPos);
        }
    }

    // ── Swimlane rendering ────────────────────────────────────────────────────

    internal const double SwimLanePad = 12.0;

    private void RenderSwimLanes()
    {
        using var dc = _swimlaneLayer.RenderOpen();
        if (!ShowSwimLanes || _doc is null || _doc.Classes.Count == 0) return;

        var groups = _doc.Classes
            .Where(n => !string.IsNullOrEmpty(n.Namespace))
            .GroupBy(n => n.Namespace!)
            .OrderBy(g => g.Key);

        var laneBrush  = new SolidColorBrush(Color.FromArgb(20, 120, 160, 220));
        var lanePen    = new Pen(new SolidColorBrush(Color.FromArgb(80, 100, 140, 200)), 1.0);
        lanePen.DashStyle = DashStyles.Dash;

        foreach (var grp in groups)
        {
            var nodes = grp.ToList();
            double minX = nodes.Min(n => n.X) - SwimLanePad;
            double minY = nodes.Min(n => n.Y) - SwimLanePad - 16;  // space for header
            double maxX = nodes.Max(n => n.X + n.Width)  + SwimLanePad;
            double maxY = nodes.Max(n => n.Y + n.Height) + SwimLanePad;

            var laneRect = new Rect(minX, minY, maxX - minX, maxY - minY);
            dc.DrawRoundedRectangle(laneBrush, lanePen, laneRect, 4, 4);

            // Namespace label header
            var ft = MakeFT(grp.Key, new SolidColorBrush(Color.FromArgb(160, 180, 200, 240)), 10.0);
            dc.DrawText(ft, new Point(minX + SwimLanePad, minY + 2));
        }
    }

    /// <summary>
    /// Returns the namespace key of the swimlane at <paramref name="pt"/>, or null.
    /// Returns null when <paramref name="pt"/> is inside a node (nodes take priority).
    /// </summary>
    public string? HitTestSwimLane(Point pt)
    {
        if (!ShowSwimLanes || _doc is null || _doc.Classes.Count == 0) return null;

        // Nodes take priority — if point is inside a node, no swimlane hit
        if (HitTestNode(pt) is not null) return null;

        foreach (var grp in _doc.Classes
            .Where(n => !string.IsNullOrEmpty(n.Namespace))
            .GroupBy(n => n.Namespace!))
        {
            var nodes = grp.ToList();
            double minX = nodes.Min(n => n.X) - SwimLanePad;
            double minY = nodes.Min(n => n.Y) - SwimLanePad - 16;
            double maxX = nodes.Max(n => n.X + n.Width)  + SwimLanePad;
            double maxY = nodes.Max(n => n.Y + n.Height) + SwimLanePad;

            if (new Rect(minX, minY, maxX - minX, maxY - minY).Contains(pt))
                return grp.Key;
        }
        return null;
    }

    /// <summary>
    /// Returns all nodes belonging to the swimlane with the given namespace.
    /// </summary>
    public List<ClassNode> GetSwimLaneNodes(string ns)
    {
        if (_doc is null) return [];
        return _doc.Classes.Where(n => n.Namespace == ns).ToList();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    // ── Size cache invalidation ───────────────────────────────────────────────

    /// <summary>
    /// Invalidates cached height/width for a single node.
    /// Pass null to clear the entire cache (e.g., after <see cref="RenderAll"/>).
    /// </summary>
    private void InvalidateNodeSizeCache(string? nodeId)
    {
        if (nodeId is null) { _heightCache.Clear(); _widthCache.Clear(); }
        else { _heightCache.Remove(nodeId); _widthCache.Remove(nodeId); }
    }

    // ── Node height/width computation (with caching) ──────────────────────────

    public double ComputeNodeHeight(ClassNode node)
    {
        if (_heightCache.TryGetValue(node.Id, out double cached)) return cached;

        double result = ComputeNodeHeightCore(node);
        _heightCache[node.Id] = result;
        return result;
    }

    private double ComputeNodeHeightCore(ClassNode node)
    {
        double hdrH = ComputeHeaderHeight(node);

        // Collapsed nodes show only the header.
        if (_collapsedNodes.Contains(node.Id))
            return hdrH;

        // Custom height set by the resize gripper takes priority.
        if (_customHeights.TryGetValue(node.Id, out double custom))
            return Math.Max(custom, hdrH + MemberHeight);

        double h = hdrH + MemberPadding * 2;
        MemberKind? lastKind = null;
        foreach (var m in node.Members)
        {
            if (lastKind.HasValue && m.Kind != lastKind)
                h += 14.0; // section header strip
            string sec = GetSectionName(m.Kind);
            if (!IsSectionCollapsed(node.Id, sec))
                h += MemberHeight;
            lastKind = m.Kind;
        }
        if (!_expandedNodes.Contains(node.Id) && h > MaxNodeHeight)
            return MaxNodeHeight;  // capped; "N more" footer rendered at bottom
        return h;
    }

    /// <summary>Pins the node to a custom height (set by the resize gripper drag).</summary>
    public void SetCustomHeight(string nodeId, double height)
    {
        _customHeights[nodeId] = height;
        _expandedNodes.Remove(nodeId); // custom height overrides the expand toggle
        InvalidateNodeSizeCache(nodeId);
    }

    /// <summary>Removes a custom height override, restoring auto-computed height.</summary>
    public void ClearCustomHeight(string nodeId)
    {
        _customHeights.Remove(nodeId);
        InvalidateNodeSizeCache(nodeId);
    }

    /// <summary>
    /// Returns the node whose bottom-edge gripper is at <paramref name="pt"/>, or null.
    /// The gripper zone is GripperHeight pixels above and below the node bottom edge.
    /// </summary>
    public ClassNode? IsGripperHit(IReadOnlyList<ClassNode> nodes, Point pt)
    {
        foreach (var node in nodes)
        {
            double h    = ComputeNodeHeight(node);
            double edgeY = node.Y + h;
            if (pt.X >= node.X && pt.X <= node.X + node.Width
                && pt.Y >= edgeY - GripperHeight && pt.Y <= edgeY + GripperHeight)
                return node;
        }
        return null;
    }

    private int CountHiddenMembers(ClassNode node, double displayHeight)
    {
        double h = ComputeHeaderHeight(node) + MemberPadding * 2;
        double limit = displayHeight - FooterHeight; // room for the footer itself
        int hidden = 0;
        bool capping = false;
        MemberKind? lastKind = null;
        foreach (var m in node.Members)
        {
            if (lastKind.HasValue && m.Kind != lastKind) h += 14.0;
            string sec = GetSectionName(m.Kind);
            if (!IsSectionCollapsed(node.Id, sec))
            {
                h += MemberHeight;
                if (h > limit) { capping = true; hidden++; }
            }
            lastKind = m.Kind;
        }
        return capping ? hidden : 0;
    }

    /// <summary>Toggles the expanded state of a node (bypass MaxNodeHeight cap).</summary>
    public void ToggleExpanded(string nodeId)
    {
        if (!_expandedNodes.Remove(nodeId))
            _expandedNodes.Add(nodeId);
        InvalidateNodeSizeCache(nodeId);
    }

    /// <summary>Toggles the collapsed state of a node (header-only display).</summary>
    public void ToggleCollapsed(string nodeId)
    {
        if (!_collapsedNodes.Remove(nodeId))
            _collapsedNodes.Add(nodeId);
        // Clear expand/custom-height state when collapsing
        if (_collapsedNodes.Contains(nodeId))
        {
            _expandedNodes.Remove(nodeId);
            _customHeights.Remove(nodeId);
        }
        InvalidateNodeSizeCache(nodeId);
    }

    /// <summary>Returns whether the node is collapsed.</summary>
    public bool IsCollapsed(string nodeId) => _collapsedNodes.Contains(nodeId);

    /// <summary>Collapses all nodes.</summary>
    public void CollapseAll(IEnumerable<ClassNode> nodes)
    {
        foreach (var n in nodes) _collapsedNodes.Add(n.Id);
    }

    /// <summary>Expands all nodes (clears collapse state).</summary>
    public void ExpandAll() => _collapsedNodes.Clear();

    /// <summary>Returns the node whose "N more" footer pill is at <paramref name="pt"/>, or null.</summary>
    public ClassNode? HitTestMoreFooter(IReadOnlyList<ClassNode> classes, Point pt)
    {
        foreach (var node in classes)
        {
            if (_expandedNodes.Contains(node.Id)) continue;
            double height = ComputeNodeHeight(node);
            double fullH  = ComputeNodeHeightFull(node);
            if (fullH <= height) continue;
            // Footer occupies the last FooterHeight pixels of the capped box
            var footerRect = new Rect(node.X, node.Y + height - FooterHeight, node.Width, FooterHeight);
            if (footerRect.Contains(pt)) return node;
        }
        return null;
    }

    private double ComputeNodeHeightFull(ClassNode node)
    {
        double h = ComputeHeaderHeight(node) + MemberPadding * 2;
        MemberKind? lastKind = null;
        foreach (var m in node.Members)
        {
            if (lastKind.HasValue && m.Kind != lastKind) h += 14.0;
            string sec = GetSectionName(m.Kind);
            if (!IsSectionCollapsed(node.Id, sec)) h += MemberHeight;
            lastKind = m.Kind;
        }
        return h;
    }

    private double ComputeNodeWidth(ClassNode node)
    {
        if (_widthCache.TryGetValue(node.Id, out double cached)) return cached;
        double max = node.Name.Length * 8.0;
        foreach (var m in node.Members)
            max = Math.Max(max, BuildMemberLabel(m).Length * 7.5 + IconWidth + HorizPadding * 2 + 16);
        double result = Math.Max(BoxMinWidth, max);
        _widthCache[node.Id] = result;
        return result;
    }

    private static string BuildMemberLabel(ClassMember m)
    {
        // Fast path: most members have no modifiers
        if (!m.IsStatic && !m.IsAsync && !m.IsAbstract && !m.IsOverride)
            return m.DisplayLabel;

        string prefix = (m.IsStatic   ? "static "   : string.Empty)
                      + (m.IsAsync    ? "async "    : string.Empty)
                      + (m.IsAbstract ? "abstract " : string.Empty)
                      + (m.IsOverride ? "↑"         : string.Empty);
        return prefix + m.DisplayLabel;
    }

    private static string GetStereotype(ClassNode node)
    {
        if (node.IsRecord)  return "«record»";
        if (node.IsSealed && node.Kind == ClassKind.Class) return "«sealed»";
        return node.Kind switch
        {
            ClassKind.Interface => "«interface»",
            ClassKind.Enum      => "«enum»",
            ClassKind.Struct    => "«struct»",
            ClassKind.Abstract  => "«abstract»",
            ClassKind.Class when node.IsAbstract => "«abstract»",
            _ => string.Empty
        };
    }

    /// <summary>Builds the merged stereotype + attributes label for a node header.</summary>
    private static string BuildStereotypeLabel(ClassNode node, string stereotype)
    {
        bool hasSter = stereotype.Length > 0;
        bool hasAttr = node.Attributes.Count > 0;
        if (!hasSter && !hasAttr) return string.Empty;
        if (hasSter && !hasAttr) return stereotype;
        string attrs = string.Join(", ", node.Attributes);
        if (!hasSter) return "«" + attrs + "»";
        // Merge: «struct · StructLayout, Serializable»
        // Strip the « » from stereotype for merging
        string sterCore = stereotype[1..^1]; // remove « and »
        return "«" + sterCore + " · " + attrs + "»";
    }

    /// <summary>Computes dynamic header height: Row1=icon+name, Row2=stereotype, Row3=namespace.</summary>
    public double ComputeHeaderHeight(ClassNode node)
    {
        // Row 1: icon + name
        var nameFt = MakeFT(node.Name, Brushes.White, 13.0, bold: true);
        double y = 6.0 + Math.Max(TypeIconSize, nameFt.Height) + 2.0;

        // Row 2: merged stereotype + attributes
        string stereotype = GetStereotype(node);
        string merged = BuildStereotypeLabel(node, stereotype);
        if (merged.Length > 0)
        {
            var sterFt = MakeFT(merged, Brushes.White, 9.0, italic: true);
            y += sterFt.Height + 2.0;
        }

        // Row 3: namespace with dashes
        if (!string.IsNullOrEmpty(node.Namespace))
        {
            var nsFt = MakeFT(node.Namespace, Brushes.White, 8.5);
            y += nsFt.Height + 2.0;
        }

        return Math.Max(HeaderBaseHeight, y + 4.0); // 4px bottom padding
    }

    /// <summary>Returns the accent color for the node's type kind, or user-chosen custom color.</summary>
    private static Color GetAccentColor(ClassNode node)
    {
        if (node.CustomColor.HasValue)
        {
            var c = node.CustomColor.Value;
            return Color.FromRgb(c.R, c.G, c.B);
        }
        if (node.IsRecord)  return Color.FromRgb(156, 220, 254); // cyan
        return node.Kind switch
        {
            ClassKind.Interface => Color.FromRgb( 78, 201, 176), // green
            ClassKind.Enum      => Color.FromRgb(197, 134, 192), // violet
            ClassKind.Struct    => Color.FromRgb(220, 220, 170), // orange/yellow
            ClassKind.Abstract  => Color.FromRgb(86,  156, 214), // blue
            _ => node.IsAbstract
                ? Color.FromRgb(86,  156, 214)                   // blue (abstract class)
                : Color.FromRgb(79,  193, 255)                   // light blue (class)
        };
    }

    /// <summary>Returns a single-letter glyph representing the node's type kind.</summary>
    private static string GetTypeGlyph(ClassNode node)
    {
        if (node.IsRecord)  return "R";
        return node.Kind switch
        {
            ClassKind.Interface => "I",
            ClassKind.Enum      => "E",
            ClassKind.Struct    => "S",
            ClassKind.Abstract  => "A",
            _ => node.IsAbstract ? "A" : "C"
        };
    }

    private Brush GetMemberIconBrush(MemberKind kind) => kind switch
    {
        MemberKind.Field    => Res("CD_FieldForeground",    Color.FromRgb( 86, 156, 214)),
        MemberKind.Property => Res("CD_PropertyForeground", Color.FromRgb( 78, 201, 176)),
        MemberKind.Method   => Res("CD_MethodForeground",   Color.FromRgb(197, 134, 192)),
        MemberKind.Event    => Res("CD_EventForeground",    Color.FromRgb(220, 160,  80)),
        _                   => Res("CD_MemberTextForeground", Color.FromRgb(200, 200, 210))
    };

    private Brush GetArrowBrush(RelationshipKind kind)
    {
        string token = kind switch
        {
            RelationshipKind.Inheritance => "CD_InheritanceArrowBrush",
            RelationshipKind.Realization => "CD_InheritanceArrowBrush",
            RelationshipKind.Association => "CD_AssociationArrowBrush",
            RelationshipKind.Dependency  => "CD_DependencyArrowBrush",
            RelationshipKind.Uses        => "CD_DependencyArrowBrush",
            RelationshipKind.Aggregation => "CD_AggregationArrowBrush",
            RelationshipKind.Composition => "CD_CompositionArrowBrush",
            _                            => "CD_AssociationArrowBrush"
        };
        return Res(token, Color.FromRgb(120, 120, 140));
    }

    private static string GetMemberIcon(MemberKind kind) => kind switch
    {
        MemberKind.Field    => "\uE192",
        MemberKind.Property => "\uE10C",
        MemberKind.Method   => "\uE8F4",
        MemberKind.Event    => "\uECAD",
        _                   => "\uE192"
    };

    private static string GetVisibilityPrefix(MemberVisibility vis) => vis switch
    {
        MemberVisibility.Public    => "+ ",
        MemberVisibility.Private   => "- ",
        MemberVisibility.Protected => "# ",
        MemberVisibility.Internal  => "~ ",
        _                          => "  "
    };

    private static Point NearestEdgePoint(Rect from, Rect to)
    {
        Point fc = new(from.Left + from.Width / 2, from.Top + from.Height / 2);
        Point tc = new(to.Left   + to.Width   / 2, to.Top   + to.Height   / 2);
        Vector d = tc - fc;
        if (Math.Abs(d.X) > Math.Abs(d.Y))
            return d.X > 0 ? new Point(from.Right, fc.Y) : new Point(from.Left, fc.Y);
        return d.Y > 0 ? new Point(fc.X, from.Bottom) : new Point(fc.X, from.Top);
    }

    private static double PointToSegmentDistance(Point pt, Point a, Point b)
    {
        Vector ab = b - a, ap = pt - a;
        double t = Math.Clamp(Vector.Multiply(ap, ab) / ab.LengthSquared, 0, 1);
        Vector closest = a + ab * t - pt;
        return closest.Length;
    }

    private static Vector RotateVec(Vector v, double a) =>
        new(v.X * Math.Cos(a) - v.Y * Math.Sin(a),
            v.X * Math.Sin(a) + v.Y * Math.Cos(a));

    private Brush Res(string key, Color fallback) =>
        TryFindResource(key) as Brush ?? new SolidColorBrush(fallback);

    private FormattedText MakeFT(string text, Brush brush, double size,
        bool bold = false, bool italic = false, string? fontFamily = null)
    {
        var tf = new Typeface(
            new FontFamily(fontFamily ?? "Segoe UI"),
            italic ? FontStyles.Italic : FontStyles.Normal,
            bold   ? FontWeights.Bold  : FontWeights.Normal,
            FontStretches.Normal);
        return new FormattedText(text, CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, tf, size, brush, 96.0);
    }
}
