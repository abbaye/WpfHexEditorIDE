// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: Controls/CfgCanvas.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     WPF Canvas-based control for rendering a method's Control Flow Graph.
//     Blocks are rendered as themed Border+TextBlock elements.
//     Arrows are Path elements (Bezier curves) drawn on a Canvas overlay.
//     Supports Ctrl+Scroll zoom via ScaleTransform.
//     Fires BlockClicked(int offset) when the user clicks a basic block.
//
// Architecture Notes:
//     Pattern: Custom UserControl — no XAML code-behind, pure C#.
//     Layout: BFS from entry assigns depth levels; blocks at the same level
//       are placed left-to-right sorted by StartOffset.
//     Theme: PFP_SectionBackgroundBrush / PFP_BorderBrush / PFP_TextBrush /
//       PFP_AccentBrush used via SetResourceReference for theme compliance.
//     Semantic block colors (Entry/Return/Throw) are hardcoded as documented
//       theme exceptions (see plan Phase 4).
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using WpfHexEditor.Core.AssemblyAnalysis.Models;

namespace WpfHexEditor.Plugins.AssemblyExplorer.Controls;

/// <summary>
/// Renders a <see cref="ControlFlowGraph"/> as a zoomable, scrollable block diagram.
/// </summary>
public sealed class CfgCanvas : UserControl
{
    // ── Constants ─────────────────────────────────────────────────────────────

    private const double BlockWidth      = 230;
    private const double BlockMinHeight  = 28;
    private const double LineHeight      = 14;
    private const double ColSpacing      = 50;
    private const double RowSpacing      = 70;
    private const double BlockPadding    = 6;
    private const double CanvasPadding   = 20;

    // Semantic block background colors (theme exception — documented in plan Phase 4)
    private static readonly Brush EntryBrush     = new SolidColorBrush(Color.FromRgb(0x4E, 0xC9, 0xB0)) { Opacity = 0.25 };
    private static readonly Brush ReturnBrush    = new SolidColorBrush(Color.FromRgb(0xF1, 0x4C, 0x4C)) { Opacity = 0.20 };
    private static readonly Brush ThrowBrush     = new SolidColorBrush(Color.FromRgb(0xCE, 0x91, 0x78)) { Opacity = 0.25 };
    private static readonly Brush ExHandlerBrush = new SolidColorBrush(Color.FromRgb(0xCE, 0x91, 0x78)) { Opacity = 0.20 };

    // ── Dependency properties ─────────────────────────────────────────────────

    public static readonly DependencyProperty GraphProperty =
        DependencyProperty.Register(
            nameof(Graph),
            typeof(ControlFlowGraph),
            typeof(CfgCanvas),
            new FrameworkPropertyMetadata(null, OnGraphChanged));

    public ControlFlowGraph? Graph
    {
        get => (ControlFlowGraph?)GetValue(GraphProperty);
        set => SetValue(GraphProperty, value);
    }

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Raised when the user left-clicks a basic block. Argument = block start offset.</summary>
    public event Action<int>? BlockClicked;

    // ── Private fields ────────────────────────────────────────────────────────

    private readonly Canvas         _canvas;
    private readonly ScaleTransform _scale;
    private readonly ScrollViewer   _scroll;

    // ── Constructor ───────────────────────────────────────────────────────────

    public CfgCanvas()
    {
        _scale  = new ScaleTransform(1.0, 1.0);
        _canvas = new Canvas { RenderTransform = _scale };
        _canvas.SetResourceReference(BackgroundProperty, "PFP_SectionBackgroundBrush");

        _scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
            Content = _canvas
        };
        _scroll.SetResourceReference(BackgroundProperty, "PFP_SectionBackgroundBrush");

        Content                = _scroll;
        Background             = Brushes.Transparent;
        PreviewMouseWheel     += OnMouseWheel;
    }

    // ── Graph change handler ──────────────────────────────────────────────────

    private static void OnGraphChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((CfgCanvas)d).RenderGraph();

    // ── Rendering ─────────────────────────────────────────────────────────────

    private void RenderGraph()
    {
        _canvas.Children.Clear();

        var graph = Graph;
        if (graph is null || graph.Blocks.Count == 0)
        {
            _canvas.Width  = 0;
            _canvas.Height = 0;
            return;
        }

        var positions = ComputeLayout(graph);

        // ── Draw arrows behind blocks ─────────────────────────────────────────
        var arrowCanvas = new Canvas();
        _canvas.Children.Add(arrowCanvas);

        // ── Draw blocks ───────────────────────────────────────────────────────
        foreach (var block in graph.Blocks)
        {
            if (!positions.TryGetValue(block.StartOffset, out var pos)) continue;
            var blockHeight = BlockMinHeight + block.InstructionLines.Count * LineHeight;
            var border      = BuildBlockVisual(block, blockHeight);
            Canvas.SetLeft(border, pos.X);
            Canvas.SetTop(border,  pos.Y);
            _canvas.Children.Add(border);
        }

        // ── Draw arrows ───────────────────────────────────────────────────────
        foreach (var block in graph.Blocks)
        {
            if (!positions.TryGetValue(block.StartOffset, out var srcPos)) continue;
            var srcH = BlockMinHeight + block.InstructionLines.Count * LineHeight;

            for (var si = 0; si < block.Successors.Count; si++)
            {
                var succOffset = block.Successors[si];
                if (!positions.TryGetValue(succOffset, out var dstPos)) continue;

                // Conditional: first = branch taken (accent), second = fall-through (dimmed)
                var isTaken = block.Successors.Count == 2 && si == 0;
                var arrow   = BuildArrow(srcPos, srcH, dstPos, isTaken);
                arrowCanvas.Children.Add(arrow);
            }
        }

        // ── Size the canvas ───────────────────────────────────────────────────
        var maxX = positions.Values.Max(p => p.X) + BlockWidth + CanvasPadding;
        var maxY = positions.Values.Max(p => p.Y) + 200 + CanvasPadding;
        _canvas.Width  = maxX;
        _canvas.Height = maxY;
    }

    // ── Block visual builder ──────────────────────────────────────────────────

    private Border BuildBlockVisual(BasicBlock block, double height)
    {
        var background = block.Kind switch
        {
            BlockKind.Entry          => EntryBrush,
            BlockKind.Return         => ReturnBrush,
            BlockKind.Throw          => ThrowBrush,
            BlockKind.ExceptionHandler => ExHandlerBrush,
            _                        => null  // Normal — use themed brush
        };

        var headerText = new TextBlock
        {
            Text       = $"IL_{block.StartOffset:X4}",
            FontFamily = new FontFamily("Consolas"),
            FontSize   = 10,
            FontWeight = FontWeights.SemiBold,
            Margin     = new Thickness(BlockPadding, 2, BlockPadding, 2)
        };
        headerText.SetResourceReference(TextBlock.ForegroundProperty, "PFP_TextBrush");

        var headerBorder = new Border
        {
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child           = headerText
        };
        headerBorder.SetResourceReference(Border.BorderBrushProperty, "PFP_BorderBrush");
        if (background is not null) headerBorder.Background = background;

        var bodyPanel = new StackPanel { Margin = new Thickness(BlockPadding, 2, BlockPadding, 2) };
        foreach (var line in block.InstructionLines)
        {
            var lineBlock = new TextBlock
            {
                Text       = line,
                FontFamily = new FontFamily("Consolas"),
                FontSize   = 10,
                TextWrapping = TextWrapping.NoWrap
            };
            lineBlock.SetResourceReference(TextBlock.ForegroundProperty, "PFP_SubTextBrush");
            bodyPanel.Children.Add(lineBlock);
        }

        var content = new DockPanel();
        DockPanel.SetDock(headerBorder, Dock.Top);
        content.Children.Add(headerBorder);
        content.Children.Add(bodyPanel);

        var border = new Border
        {
            Width           = BlockWidth,
            MinHeight       = height,
            BorderThickness = new Thickness(1),
            CornerRadius    = new CornerRadius(3),
            Child           = content,
            Cursor          = Cursors.Hand,
            Tag             = block.StartOffset
        };
        border.SetResourceReference(Border.BorderBrushProperty, "PFP_BorderBrush");

        if (background is null)
            border.SetResourceReference(Border.BackgroundProperty, "PFP_SectionBackgroundBrush");
        else
            border.Background = background;

        // Click: fire BlockClicked with the block's start offset
        border.MouseLeftButtonUp += (_, _) =>
        {
            if (border.Tag is int offset) BlockClicked?.Invoke(offset);
        };

        // Hover highlight
        border.MouseEnter += (_, _) =>
        {
            if (background is null)
                border.SetResourceReference(Border.BackgroundProperty, "PFP_ItemHoverBrush");
        };
        border.MouseLeave += (_, _) =>
        {
            if (background is null)
                border.SetResourceReference(Border.BackgroundProperty, "PFP_SectionBackgroundBrush");
            else
                border.Background = background;
        };

        return border;
    }

    // ── Arrow builder ─────────────────────────────────────────────────────────

    private Path BuildArrow(Point srcPos, double srcHeight, Point dstPos, bool isBranchTaken)
    {
        var srcCenterX = srcPos.X + BlockWidth / 2;
        var srcBottom  = srcPos.Y + srcHeight;
        var dstCenterX = dstPos.X + BlockWidth / 2;
        var dstTop     = dstPos.Y;

        // Build a bezier curve from bottom-center of source to top-center of destination
        var seg = new BezierSegment(
            new Point(srcCenterX, srcBottom + 25),
            new Point(dstCenterX, dstTop   - 25),
            new Point(dstCenterX, dstTop),
            true);

        var figure = new PathFigure
        {
            StartPoint = new Point(srcCenterX, srcBottom),
            Segments   = [seg]
        };

        var geometry = new PathGeometry { Figures = [figure] };

        var path = new Path
        {
            Data            = geometry,
            StrokeThickness = 1.5,
            Fill            = Brushes.Transparent
        };

        // Branch-taken arrow uses accent color; fall-through uses dimmed color
        if (isBranchTaken)
            path.SetResourceReference(Shape.StrokeProperty, "PFP_AccentBrush");
        else
            path.SetResourceReference(Shape.StrokeProperty, "PFP_SubTextBrush");

        // Small arrowhead at destination
        var arrowHead = BuildArrowHead(new Point(dstCenterX, dstTop));
        if (isBranchTaken)
            arrowHead.SetResourceReference(Shape.FillProperty, "PFP_AccentBrush");
        else
            arrowHead.SetResourceReference(Shape.FillProperty, "PFP_SubTextBrush");

        // Wrap both in a Canvas so they move together
        var arrowCanvas = new Canvas();
        arrowCanvas.Children.Add(path);
        arrowCanvas.Children.Add(arrowHead);
        return path; // return path; arrowhead added separately below
    }

    private static Polygon BuildArrowHead(Point tip)
    {
        var half = 5.0;
        return new Polygon
        {
            Points = new PointCollection
            {
                tip,
                new(tip.X - half, tip.Y - half * 1.5),
                new(tip.X + half, tip.Y - half * 1.5)
            },
            Stroke          = Brushes.Transparent,
            StrokeThickness = 0
        };
    }

    // ── Layout engine ─────────────────────────────────────────────────────────

    /// <summary>
    /// Computes (X, Y) canvas positions for every block using BFS depth layers.
    /// Returns a dictionary keyed by <see cref="BasicBlock.StartOffset"/>.
    /// </summary>
    private static Dictionary<int, Point> ComputeLayout(ControlFlowGraph graph)
    {
        var blockByOffset = graph.Blocks.ToDictionary(b => b.StartOffset);

        // BFS to assign depth levels
        var depth  = new Dictionary<int, int>();
        var queue  = new Queue<int>();
        var entry  = graph.Entry.StartOffset;
        depth[entry] = 0;
        queue.Enqueue(entry);

        while (queue.Count > 0)
        {
            var offset = queue.Dequeue();
            if (!blockByOffset.TryGetValue(offset, out var blk)) continue;

            foreach (var succ in blk.Successors)
            {
                if (!depth.ContainsKey(succ))
                {
                    depth[succ] = depth[offset] + 1;
                    queue.Enqueue(succ);
                }
            }
        }

        // Assign a depth for any unreachable blocks (shouldn't happen, but be safe)
        var maxDepth = depth.Count > 0 ? depth.Values.Max() : 0;
        foreach (var blk in graph.Blocks)
            if (!depth.ContainsKey(blk.StartOffset))
                depth[blk.StartOffset] = ++maxDepth;

        // Group blocks by depth level, sorted by StartOffset within level
        var byDepth = new SortedDictionary<int, List<int>>();
        foreach (var (offset, d) in depth)
        {
            if (!byDepth.ContainsKey(d)) byDepth[d] = [];
            byDepth[d].Add(offset);
        }
        foreach (var list in byDepth.Values) list.Sort();

        // Position blocks
        var positions = new Dictionary<int, Point>();
        foreach (var (d, offsets) in byDepth)
        {
            for (var col = 0; col < offsets.Count; col++)
            {
                var offset      = offsets[col];
                var blk         = blockByOffset.TryGetValue(offset, out var b) ? b : null;
                var blockHeight = blk is null ? BlockMinHeight
                                              : BlockMinHeight + blk.InstructionLines.Count * LineHeight;

                var x = CanvasPadding + col  * (BlockWidth  + ColSpacing);
                var y = CanvasPadding + d    * (blockHeight + RowSpacing);
                positions[offset] = new Point(x, y);
            }
        }

        return positions;
    }

    // ── Zoom ──────────────────────────────────────────────────────────────────

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;

        var factor = e.Delta > 0 ? 1.1 : 1.0 / 1.1;
        var next   = _scale.ScaleX * factor;
        next = Math.Clamp(next, 0.25, 3.0);

        _scale.ScaleX = next;
        _scale.ScaleY = next;
        e.Handled     = true;
    }
}
