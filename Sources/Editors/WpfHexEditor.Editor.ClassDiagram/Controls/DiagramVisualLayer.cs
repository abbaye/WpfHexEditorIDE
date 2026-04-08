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
    private const double HeaderHeight  = 44.0;
    private const double MemberHeight  = 20.0;
    private const double MemberPadding = 4.0;
    private const double IconWidth     = 18.0;
    private const double HorizPadding  = 8.0;
    private const double CornerRadius  = 3.0;
    private const double BoxMinWidth   = 160.0;
    private const double MaxNodeHeight = 380.0;  // cap — "N more" footer shown when exceeded
    private const double FooterHeight  = 18.0;

    // Nodes the user has explicitly expanded past MaxNodeHeight
    private readonly HashSet<string> _expandedNodes = new(StringComparer.Ordinal);

    private const double ArrowHeadLen  = 12.0;
    private const double ArrowHalfAng  = 25.0 * Math.PI / 180.0;
    private const double DiamondSz     = 10.0;

    private static readonly DashStyle DashedStyle = new([6, 3], 0);

    // ── Visual children ──────────────────────────────────────────────────────

    private readonly DrawingVisual                  _arrowLayer  = new();
    private readonly Dictionary<string, DrawingVisual> _nodeVisuals = [];
    private readonly DrawingVisual                     _swimlaneLayer = new();
    private readonly List<Visual>                      _visuals       = [];

    // ── State ────────────────────────────────────────────────────────────────

    private DiagramDocument?  _doc;
    private string?           _selectedNodeId;
    private string?           _hoveredNodeId;

    // ── Focus mode (Phase 12 filter) ─────────────────────────────────────────
    private HashSet<string>?  _focusedNodeIds;   // null = all visible; empty = all dimmed

    // ── Collapsible sections ──────────────────────────────────────────────────
    // key: nodeId, value: set of collapsed section names ("Fields","Properties","Methods","Events")
    private readonly Dictionary<string, HashSet<string>> _collapsedSections = [];

    // ── Toggles (set by ClassDiagramSplitHost toolbar) ───────────────────────
    public bool ShowSwimLanes { get; set; } = false;

    // ── FrameworkElement visual tree overrides ───────────────────────────────

    protected override int    VisualChildrenCount         => _visuals.Count;
    protected override Visual GetVisualChild(int index)   => _visuals[index];

    // ── Constructor ──────────────────────────────────────────────────────────

    public DiagramVisualLayer()
    {
        _visuals.Add(_swimlaneLayer); // swimlane lanes behind everything
        AddVisualChild(_swimlaneLayer);
        _visuals.Add(_arrowLayer);    // arrows above swimlanes, below nodes
        AddVisualChild(_arrowLayer);
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

        // Remove visuals for nodes that no longer exist
        var currentIds = doc.Classes.Select(n => n.Id).ToHashSet();
        foreach (var id in _nodeVisuals.Keys.Except(currentIds).ToList())
        {
            RemoveVisualChild(_nodeVisuals[id]);
            _visuals.Remove(_nodeVisuals[id]);
            _nodeVisuals.Remove(id);
        }

        // Ensure a DrawingVisual exists for every node
        foreach (var node in doc.Classes)
        {
            if (!_nodeVisuals.ContainsKey(node.Id))
            {
                var dv = new DrawingVisual();
                _nodeVisuals[node.Id] = dv;
                _visuals.Add(dv);
                AddVisualChild(dv);
            }
            RenderNode(node);
        }

        RenderAllArrows();
        RenderSwimLanes();
    }

    /// <summary>
    /// Partial repaint: only re-renders the specified nodes (and arrows).
    /// </summary>
    public void InvalidateNodes(DiagramDocument doc, IEnumerable<string> dirtyIds,
        string? selectedId = null, string? hoveredId = null)
    {
        _doc            = doc;
        _selectedNodeId = selectedId;
        _hoveredNodeId  = hoveredId;

        foreach (var id in dirtyIds)
        {
            var node = doc.Classes.FirstOrDefault(n => n.Id == id);
            if (node is not null) RenderNode(node);
        }
        RenderAllArrows();
        RenderSwimLanes();
    }

    /// <summary>
    /// Updates selection/hover state and re-renders affected nodes only.
    /// </summary>
    public void UpdateSelection(string? newSelectedId, string? newHoveredId)
    {
        if (_doc is null) return;

        var needRepaint = new HashSet<string?> { _selectedNodeId, _hoveredNodeId, newSelectedId, newHoveredId };
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
        double relY = pt.Y - node.Y - HeaderHeight - MemberPadding;
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
            if (new Rect(node.X, node.Y, node.Width, node.Height).Contains(pt))
                return node;
        }
        return null;
    }

    /// <summary>Returns the member row at <paramref name="pt"/> within <paramref name="node"/>, or null.</summary>
    public ClassMember? HitTestMember(Point pt, ClassNode node)
    {
        double relY = pt.Y - node.Y - HeaderHeight - MemberPadding;
        if (relY < 0) return null;
        int idx = (int)(relY / MemberHeight);
        return idx >= 0 && idx < node.Members.Count ? node.Members[idx] : null;
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

    private void RenderNode(ClassNode node)
    {
        if (!_nodeVisuals.TryGetValue(node.Id, out var dv)) return;

        bool isSelected = node.Id == _selectedNodeId;
        bool isHovered  = node.Id == _hoveredNodeId;
        bool isDimmed   = _focusedNodeIds is not null && !_focusedNodeIds.Contains(node.Id);

        double width  = ComputeNodeWidth(node);
        double height = ComputeNodeHeight(node);

        // Keep node model in sync with computed size
        node.Width  = width;
        node.Height = height;

        // Offset the DrawingVisual so it sits at node.X, node.Y on the canvas
        dv.Offset = new Vector(node.X, node.Y);

        using var dc = dv.RenderOpen();

        if (isDimmed) dc.PushOpacity(0.2);

        Brush boxBg     = Res("CD_ClassBoxBackground",       Color.FromRgb(50, 50, 60));
        Brush headerBg  = Res("CD_ClassBoxHeaderBackground", Color.FromRgb(40, 40, 70));
        Brush nameColor = Res("CD_ClassNameForeground",      Color.FromRgb(220, 220, 255));
        Brush sterColor = Res("CD_StereotypeForeground",     Color.FromRgb(160, 160, 200));
        Brush memberFg  = Res("CD_MemberTextForeground",     Color.FromRgb(200, 200, 210));
        Brush divBrush  = Res("CD_ClassBoxSectionDivider",   Color.FromRgb(70, 70, 90));
        Brush boxBorder = isSelected
            ? Res("CD_ClassBoxSelectedBorderBrush", Color.FromRgb(0, 120, 215))
            : (isHovered
                ? Res("CD_ClassBoxHoverBorderBrush", Color.FromRgb(110, 110, 150))
                : Res("CD_ClassBoxBorderBrush", Color.FromRgb(80, 80, 100)));

        double borderThk = isSelected ? 2.0 : 1.0;
        var boxPen  = new Pen(boxBorder, borderThk);
        var divPen  = new Pen(divBrush, 0.5);

        var boxRect    = new Rect(0, 0, width, height);
        var headerRect = new Rect(0, 0, width, HeaderHeight);

        // B6 — Drop shadow (offset semi-transparent rect drawn before main box)
        var shadowBrush = new SolidColorBrush(Color.FromArgb(55, 0, 0, 0));
        dc.DrawRoundedRectangle(shadowBrush, null, new Rect(3, 4, width, height), CornerRadius, CornerRadius);

        // Box background
        dc.DrawRoundedRectangle(boxBg, boxPen, boxRect, CornerRadius, CornerRadius);

        // B5 — Gradient header (top lighter → base color)
        Color headerBase = headerBg is SolidColorBrush scb ? scb.Color : Color.FromRgb(37, 40, 64);
        byte lr = (byte)Math.Min(255, headerBase.R + 22);
        byte lg = (byte)Math.Min(255, headerBase.G + 22);
        byte lb = (byte)Math.Min(255, headerBase.B + 28);
        var gradHeader = new LinearGradientBrush(
            Color.FromRgb(lr, lg, lb), headerBase, new Point(0, 0), new Point(0, 1));
        dc.DrawRoundedRectangle(gradHeader, null, headerRect, CornerRadius, CornerRadius);
        dc.DrawRectangle(gradHeader, null, new Rect(0, HeaderHeight / 2, width, HeaderHeight / 2));

        // Stereotype
        string stereotype = GetStereotype(node);
        double textY = 6.0;
        if (stereotype.Length > 0)
        {
            var sterFt = MakeFT(stereotype, sterColor, 10.0, italic: true);
            dc.DrawText(sterFt, new Point((width - sterFt.Width) / 2, textY));
            textY += sterFt.Height + 1.0;
        }

        // Attributes (e.g. «Serializable, DataContract»)
        if (node.Attributes.Count > 0)
        {
            string attrText = "«" + string.Join(", ", node.Attributes) + "»";
            var attrFt = MakeFT(attrText, sterColor, 8.5, italic: true);
            attrFt.MaxTextWidth = width - HorizPadding * 2;
            attrFt.Trimming     = System.Windows.TextTrimming.CharacterEllipsis;
            double attrX = (width - Math.Min(attrFt.Width, attrFt.MaxTextWidth)) / 2;
            dc.DrawText(attrFt, new Point(attrX, textY));
            textY += attrFt.Height + 1.0;
        }

        // Class name (bold)
        var nameFt = MakeFT(node.Name, nameColor, 13.0, bold: true);
        dc.DrawText(nameFt, new Point((width - nameFt.Width) / 2, textY));

        // Namespace pill (small, below name)
        if (!string.IsNullOrEmpty(node.Namespace))
        {
            double nsY = textY + nameFt.Height + 1.0;
            if (nsY + 10 < HeaderHeight - 2)
            {
                var nsFt = MakeFT(node.Namespace, sterColor, 8.5);
                nsFt.MaxTextWidth  = width - HorizPadding * 2;
                nsFt.Trimming      = System.Windows.TextTrimming.CharacterEllipsis;
                double nsX = (width - Math.Min(nsFt.Width, nsFt.MaxTextWidth)) / 2;
                dc.DrawText(nsFt, new Point(nsX, nsY));
            }
        }

        // Header divider
        dc.DrawLine(divPen, new Point(0, HeaderHeight), new Point(width, HeaderHeight));

        // Metrics badge (bottom-right corner of header)
        if (node.Metrics != ClassMetrics.Empty)
        {
            DrawMetricsBadge(dc, node, width);
        }

        // Member rows
        double memberY = HeaderHeight + MemberPadding;
        MemberKind? lastKind   = null;
        double textClipW       = width - HorizPadding - IconWidth - HorizPadding;
        var    divPenDashed    = new Pen(divBrush, 0.5) { DashStyle = new DashStyle([2, 2], 0) };

        foreach (var member in node.Members)
        {
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
        if (!_expandedNodes.Contains(node.Id))
        {
            int hidden = CountHiddenMembers(node);
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

        if (isDimmed) dc.Pop();
    }

    private void DrawMetricsBadge(DrawingContext dc, ClassNode node, double boxWidth)
    {
        const double badgeW = 36, badgeH = 12, gap = 2, badgeX_offset = 4;
        double badgeX = boxWidth - badgeW - badgeX_offset;

        // Badge 1: Instability (I=x.xx)
        double instability = node.Metrics.Instability;
        Color instColor = instability switch
        {
            < 0.35 => Color.FromRgb(0, 180, 80),
            < 0.65 => Color.FromRgb(200, 160, 0),
            _      => Color.FromRgb(200, 60, 60)
        };
        double badgeY1 = 2;
        dc.DrawRoundedRectangle(
            new SolidColorBrush(Color.FromArgb(180, instColor.R, instColor.G, instColor.B)),
            null, new Rect(badgeX, badgeY1, badgeW, badgeH), 3, 3);
        var ft1 = MakeFT($"I={instability:F2}", Brushes.White, 7.5);
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
            new SolidColorBrush(Color.FromArgb(180, cntColor.R, cntColor.G, cntColor.B)),
            null, new Rect(badgeX, badgeY2, badgeW, badgeH), 3, 3);
        var ft2 = MakeFT($"M:{memberCount}", Brushes.White, 7.5);
        dc.DrawText(ft2, new Point(badgeX + (badgeW - ft2.Width) / 2, badgeY2 + (badgeH - ft2.Height) / 2));
    }

    // ── Arrow rendering ──────────────────────────────────────────────────────

    private void RenderAllArrows()
    {
        if (_doc is null) return;

        using var dc = _arrowLayer.RenderOpen();

        foreach (var rel in _doc.Relationships)
        {
            var src = _doc.FindById(rel.SourceId);
            var tgt = _doc.FindById(rel.TargetId);
            if (src is null || tgt is null) continue;

            var srcRect = new Rect(src.X, src.Y, src.Width, src.Height);
            var tgtRect = new Rect(tgt.X, tgt.Y, tgt.Width, tgt.Height);

            Point p1 = NearestEdgePoint(srcRect, tgtRect);
            Point p2 = NearestEdgePoint(tgtRect, srcRect);

            Brush lineBrush = GetArrowBrush(rel.Kind);
            var pen = new Pen(lineBrush, 1.5);
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
                DrawArrowHead(dc, lastWp, p2, rel.Kind, lineBrush, 1.5);
                DrawTailDecoration(dc, p1, new Point(wayPts[0].X, wayPts[0].Y), rel.Kind, lineBrush, 1.5);
            }
            else
            {
                dc.DrawLine(pen, p1, p2);
                DrawArrowHead(dc, p1, p2, rel.Kind, lineBrush, 1.5);
                DrawTailDecoration(dc, p1, p2, rel.Kind, lineBrush, 1.5);
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
            var portBrush = new SolidColorBrush(Color.FromArgb(180, lineBrush is SolidColorBrush sb ? sb.Color.R : (byte)140,
                lineBrush is SolidColorBrush sb2 ? sb2.Color.G : (byte)140,
                lineBrush is SolidColorBrush sb3 ? sb3.Color.B : (byte)180));
            dc.DrawEllipse(portBrush, null, p1, 2.5, 2.5);
            dc.DrawEllipse(portBrush, null, p2, 2.5, 2.5);
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
        const double Pad = 12.0;

        foreach (var grp in groups)
        {
            var nodes = grp.ToList();
            double minX = nodes.Min(n => n.X) - Pad;
            double minY = nodes.Min(n => n.Y) - Pad - 16;  // space for header
            double maxX = nodes.Max(n => n.X + n.Width)  + Pad;
            double maxY = nodes.Max(n => n.Y + n.Height) + Pad;

            var laneRect = new Rect(minX, minY, maxX - minX, maxY - minY);
            dc.DrawRoundedRectangle(laneBrush, lanePen, laneRect, 4, 4);

            // Namespace label header
            var ft = MakeFT(grp.Key, new SolidColorBrush(Color.FromArgb(160, 180, 200, 240)), 10.0);
            dc.DrawText(ft, new Point(minX + Pad, minY + 2));
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    public double ComputeNodeHeight(ClassNode node)
    {
        double h = HeaderHeight + MemberPadding * 2;
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

    private int CountHiddenMembers(ClassNode node)
    {
        double h = HeaderHeight + MemberPadding * 2;
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
                if (h > MaxNodeHeight) { capping = true; hidden++; }
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
    }

    /// <summary>Returns the node whose "N more" footer pill is at <paramref name="pt"/>, or null.</summary>
    public ClassNode? HitTestMoreFooter(IReadOnlyList<ClassNode> classes, Point pt)
    {
        foreach (var node in classes)
        {
            if (_expandedNodes.Contains(node.Id)) continue;
            double fullH = ComputeNodeHeightFull(node);
            if (fullH <= MaxNodeHeight) continue;
            // Footer occupies the last FooterHeight pixels of the capped box
            var footerRect = new Rect(node.X, node.Y + MaxNodeHeight - FooterHeight, node.Width, FooterHeight);
            if (footerRect.Contains(pt)) return node;
        }
        return null;
    }

    private double ComputeNodeHeightFull(ClassNode node)
    {
        double h = HeaderHeight + MemberPadding * 2;
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
        double max = node.Name.Length * 8.0;
        foreach (var m in node.Members)
            max = Math.Max(max, BuildMemberLabel(m).Length * 7.5 + IconWidth + HorizPadding * 2 + 16);
        return Math.Max(BoxMinWidth, max);
    }

    private static string BuildMemberLabel(ClassMember m)
    {
        var sb = new System.Text.StringBuilder();
        if (m.IsStatic)   sb.Append("static ");
        if (m.IsAsync)    sb.Append("async ");
        if (m.IsAbstract) sb.Append("abstract ");
        if (m.IsOverride) sb.Append("↑");
        sb.Append(m.DisplayLabel);
        return sb.ToString();
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
