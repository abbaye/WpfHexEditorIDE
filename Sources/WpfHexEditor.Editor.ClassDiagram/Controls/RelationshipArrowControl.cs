// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram
// File: Controls/RelationshipArrowControl.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     FrameworkElement that renders a directed UML relationship arrow
//     between two class box bounds. Supports all 5 relationship kinds
//     with appropriate arrow-head styles and line dash patterns.
//
// Architecture Notes:
//     Pattern: Custom Rendering (OnRender override).
//     Nearest-edge connection point computation avoids line overlap.
//     All colors via DynamicResource CD_* tokens with SolidColorBrush fallbacks.
//     Context menu provides Edit Label / Delete / Change Type / Reverse actions.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Editor.ClassDiagram.Core.Model;

namespace WpfHexEditor.Editor.ClassDiagram.Controls;

/// <summary>
/// Renders a UML relationship arrow between two class box bounds.
/// </summary>
public sealed class RelationshipArrowControl : FrameworkElement
{
    private static readonly DashStyle DashedStyle  = new([6, 3], 0);
    private const double ArrowHeadLength = 12.0;
    private const double ArrowHeadAngle  = 25.0;
    private const double DiamondSize     = 10.0;
    private const double SelectionExtra  = 2.0;

    // ---------------------------------------------------------------------------
    // Dependency Properties
    // ---------------------------------------------------------------------------

    public static readonly DependencyProperty RelationshipProperty =
        DependencyProperty.Register(nameof(Relationship), typeof(ClassRelationship), typeof(RelationshipArrowControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SourceBoundsProperty =
        DependencyProperty.Register(nameof(SourceBounds), typeof(Rect), typeof(RelationshipArrowControl),
            new FrameworkPropertyMetadata(Rect.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TargetBoundsProperty =
        DependencyProperty.Register(nameof(TargetBounds), typeof(Rect), typeof(RelationshipArrowControl),
            new FrameworkPropertyMetadata(Rect.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(RelationshipArrowControl),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    // ---------------------------------------------------------------------------
    // Events
    // ---------------------------------------------------------------------------

    public event EventHandler? DeleteRequested;
    public event EventHandler? EditLabelRequested;
    public event EventHandler<RelationshipKind>? ChangeTypeRequested;
    public event EventHandler? ReverseDirectionRequested;

    // ---------------------------------------------------------------------------
    // CLR wrappers
    // ---------------------------------------------------------------------------

    public ClassRelationship? Relationship
    {
        get => (ClassRelationship?)GetValue(RelationshipProperty);
        set => SetValue(RelationshipProperty, value);
    }

    public Rect SourceBounds
    {
        get => (Rect)GetValue(SourceBoundsProperty);
        set => SetValue(SourceBoundsProperty, value);
    }

    public Rect TargetBounds
    {
        get => (Rect)GetValue(TargetBoundsProperty);
        set => SetValue(TargetBoundsProperty, value);
    }

    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    // ---------------------------------------------------------------------------
    // Constructor
    // ---------------------------------------------------------------------------

    public RelationshipArrowControl()
    {
        IsHitTestVisible = true;
        MouseRightButtonUp += OnMouseRightButtonUp;
    }

    // ---------------------------------------------------------------------------
    // Rendering
    // ---------------------------------------------------------------------------

    protected override void OnRender(DrawingContext dc)
    {
        if (Relationship is null || SourceBounds == Rect.Empty || TargetBounds == Rect.Empty)
            return;

        // Compute nearest-edge connection points
        Point src = NearestEdgePoint(SourceBounds, TargetBounds);
        Point tgt = NearestEdgePoint(TargetBounds, SourceBounds);

        Brush lineBrush = GetLineBrush(Relationship.Kind);
        double thickness = IsSelected ? 2.5 : 1.5;
        var linePen = new Pen(lineBrush, thickness);

        if (Relationship.Kind == RelationshipKind.Dependency)
            linePen.DashStyle = DashedStyle;

        // Draw the main line
        dc.DrawLine(linePen, src, tgt);

        // Draw arrow head at target
        DrawArrowHead(dc, src, tgt, Relationship.Kind, lineBrush, thickness);

        // Draw tail decoration (diamond for Aggregation/Composition)
        DrawTailDecoration(dc, tgt, src, Relationship.Kind, lineBrush, thickness);

        // Draw label at midpoint
        if (!string.IsNullOrEmpty(Relationship.Label))
        {
            Point mid = new((src.X + tgt.X) / 2.0, (src.Y + tgt.Y) / 2.0);
            var ft = new FormattedText(
                Relationship.Label,
                System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                11.0,
                lineBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            dc.DrawText(ft, new Point(mid.X - ft.Width / 2, mid.Y - ft.Height / 2));
        }
    }

    // ---------------------------------------------------------------------------
    // Arrow drawing helpers
    // ---------------------------------------------------------------------------

    private static void DrawArrowHead(
        DrawingContext dc, Point from, Point to,
        RelationshipKind kind, Brush brush, double thickness)
    {
        Vector direction = to - from;
        direction.Normalize();

        double angleRad = ArrowHeadAngle * Math.PI / 180.0;

        Vector left  = RotateVector(direction, angleRad);
        Vector right = RotateVector(direction, -angleRad);

        Point p1 = to - left  * ArrowHeadLength;
        Point p2 = to - right * ArrowHeadLength;

        var pen = new Pen(brush, thickness);

        if (kind == RelationshipKind.Inheritance)
        {
            // Hollow closed triangle
            var triangle = new StreamGeometry();
            using (StreamGeometryContext ctx = triangle.Open())
            {
                ctx.BeginFigure(to, true, true);
                ctx.LineTo(p1, true, false);
                ctx.LineTo(p2, true, false);
            }
            triangle.Freeze();
            dc.DrawGeometry(Brushes.White, pen, triangle);
        }
        else
        {
            // Open arrowhead
            dc.DrawLine(pen, to, p1);
            dc.DrawLine(pen, to, p2);
        }
    }

    private static void DrawTailDecoration(
        DrawingContext dc, Point targetEnd, Point sourceEnd,
        RelationshipKind kind, Brush brush, double thickness)
    {
        if (kind != RelationshipKind.Aggregation && kind != RelationshipKind.Composition)
            return;

        Vector direction = sourceEnd - targetEnd;
        direction.Normalize();

        // Diamond centred at sourceEnd
        Point tip    = sourceEnd;
        Point left   = sourceEnd + RotateVector(direction, -Math.PI / 2) * DiamondSize * 0.5;
        Point right  = sourceEnd + RotateVector(direction,  Math.PI / 2) * DiamondSize * 0.5;
        Point back   = sourceEnd + direction * DiamondSize;

        var diamond = new StreamGeometry();
        using (StreamGeometryContext ctx = diamond.Open())
        {
            ctx.BeginFigure(tip, true, true);
            ctx.LineTo(left,  true, false);
            ctx.LineTo(back,  true, false);
            ctx.LineTo(right, true, false);
        }
        diamond.Freeze();

        Brush fill = kind == RelationshipKind.Composition ? brush : Brushes.White;
        dc.DrawGeometry(fill, new Pen(brush, thickness), diamond);
    }

    // ---------------------------------------------------------------------------
    // Geometry helpers
    // ---------------------------------------------------------------------------

    private static Point NearestEdgePoint(Rect from, Rect to)
    {
        Point fromCenter = new(from.Left + from.Width / 2, from.Top + from.Height / 2);
        Point toCenter   = new(to.Left   + to.Width   / 2, to.Top   + to.Height   / 2);

        Vector direction = toCenter - fromCenter;
        double absX = Math.Abs(direction.X);
        double absY = Math.Abs(direction.Y);

        if (absX > absY)
            return direction.X > 0
                ? new Point(from.Right, fromCenter.Y)
                : new Point(from.Left,  fromCenter.Y);
        else
            return direction.Y > 0
                ? new Point(fromCenter.X, from.Bottom)
                : new Point(fromCenter.X, from.Top);
    }

    private static Vector RotateVector(Vector v, double angle)
    {
        double cos = Math.Cos(angle);
        double sin = Math.Sin(angle);
        return new Vector(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
    }

    // ---------------------------------------------------------------------------
    // Theme brushes
    // ---------------------------------------------------------------------------

    private Brush GetLineBrush(RelationshipKind kind)
    {
        string token = kind switch
        {
            RelationshipKind.Inheritance => "CD_InheritanceArrowBrush",
            RelationshipKind.Association => "CD_AssociationArrowBrush",
            RelationshipKind.Dependency  => "CD_DependencyArrowBrush",
            RelationshipKind.Aggregation => "CD_AggregationArrowBrush",
            RelationshipKind.Composition => "CD_CompositionArrowBrush",
            _                            => "CD_AssociationArrowBrush"
        };

        return TryFindResource(token) as Brush
            ?? new SolidColorBrush(Color.FromRgb(100, 100, 100));
    }

    // ---------------------------------------------------------------------------
    // Context menu
    // ---------------------------------------------------------------------------

    private void OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (Relationship is null) return;

        var menu = new ContextMenu();
        menu.SetResourceReference(System.Windows.Controls.Control.BackgroundProperty, "DockMenuBackgroundBrush");
        menu.SetResourceReference(System.Windows.Controls.Control.ForegroundProperty, "DockMenuForegroundBrush");

        menu.Items.Add(MakeMenuItem("\uE70F", "Edit Label",         () => EditLabelRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(new Separator());

        // Change Type submenu
        var changeTypeMenu = new MenuItem { Header = "Change Type" };
        foreach (RelationshipKind rk in Enum.GetValues<RelationshipKind>())
        {
            RelationshipKind captured = rk;
            changeTypeMenu.Items.Add(MakeMenuItem("\uE8AB", rk.ToString(),
                () => ChangeTypeRequested?.Invoke(this, captured)));
        }

        menu.Items.Add(changeTypeMenu);
        menu.Items.Add(MakeMenuItem("\uE7A8", "Reverse Direction",  () => ReverseDirectionRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(new Separator());
        menu.Items.Add(MakeMenuItem("\uE74D", "Delete",             () => DeleteRequested?.Invoke(this, EventArgs.Empty)));

        menu.IsOpen = true;
        e.Handled = true;
    }

    private static MenuItem MakeMenuItem(string icon, string header, Action action)
    {
        var item = new MenuItem
        {
            Header = header,
            Icon   = new TextBlock
            {
                Text       = icon,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize   = 14
            }
        };
        item.Click += (_, _) => action();
        return item;
    }
}
