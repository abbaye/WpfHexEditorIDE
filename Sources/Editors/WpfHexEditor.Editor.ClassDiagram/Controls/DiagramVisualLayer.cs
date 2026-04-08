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
        double height = HeaderHeight + node.Members.Count * MemberHeight + MemberPadding * 2;

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
                // Dashed divider
                dc.DrawLine(divPenDashed,
                    new Point(HorizPadding, memberY), new Point(width - HorizPadding, memberY));

                // Tiny section label above divider
                string sectionLabel = member.Kind switch
                {
                    MemberKind.Field    => "Fields",
                    MemberKind.Property => "Properties",
                    MemberKind.Method   => "Methods",
                    MemberKind.Event    => "Events",
                    _                   => string.Empty
                };
                if (sectionLabel.Length > 0)
                {
                    var secFt = MakeFT(sectionLabel.ToUpperInvariant(), sterColor, 7.5);
                    dc.DrawText(secFt, new Point(HorizPadding, memberY + 1.5));
                    memberY += secFt.Height + 2.0;
                }
            }
            lastKind = member.Kind;

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

        if (isDimmed) dc.Pop();
    }

    private void DrawMetricsBadge(DrawingContext dc, ClassNode node, double boxWidth)
    {
        double instability = node.Metrics.Instability;
        Color badgeColor = instability switch
        {
            < 0.35 => Color.FromRgb(0, 180, 80),     // green — stable
            < 0.65 => Color.FromRgb(200, 160, 0),    // yellow — neutral
            _      => Color.FromRgb(200, 60, 60)      // red — instable
        };

        double badgeW = 32, badgeH = 12;
        double badgeX = boxWidth - badgeW - 4;
        double badgeY = 2;

        var badgeBrush = new SolidColorBrush(Color.FromArgb(180, badgeColor.R, badgeColor.G, badgeColor.B));
        dc.DrawRoundedRectangle(badgeBrush, null, new Rect(badgeX, badgeY, badgeW, badgeH), 3, 3);

        string label = $"I={instability:F2}";
        var ft = MakeFT(label, Brushes.White, 7.5);
        dc.DrawText(ft, new Point(badgeX + (badgeW - ft.Width) / 2, badgeY + (badgeH - ft.Height) / 2));
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
