// ==========================================================
// Project: WpfHexEditor.Plugins.Debugger
// File: Panels/ParallelStacksPanel.xaml.cs
// Description: Code-behind for Parallel Stacks panel.
//              Listens to VM.Groups changes and redraws the canvas
//              with VS-style thread boxes connected by arrows.
// Architecture:
//     Each ThreadStackGroup → a Border with an ItemsControl inside.
//     Arrows drawn as Path elements on the Canvas after layout.
// ==========================================================

using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using WpfHexEditor.Plugins.Debugger.ViewModels;

namespace WpfHexEditor.Plugins.Debugger.Panels;

public partial class ParallelStacksPanel : UserControl
{
    private const double BoxWidth   = 280;
    private const double BoxPadX    = 40;
    private const double BoxPadY    = 30;
    private const double RowHeight  = 20;
    private const double HeaderH    = 22;

    public ParallelStacksPanel()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ParallelStacksPanelViewModel old)
            old.Groups.CollectionChanged -= OnGroupsChanged;

        if (e.NewValue is ParallelStacksPanelViewModel vm)
            vm.Groups.CollectionChanged += OnGroupsChanged;
    }

    private void OnGroupsChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        Dispatcher.InvokeAsync(Redraw);

    private void Redraw()
    {
        StacksCanvas.Children.Clear();

        if (DataContext is not ParallelStacksPanelViewModel vm || vm.Groups.Count == 0) return;

        // Layout: arrange boxes left-to-right
        double x = BoxPadX;
        var boxes = new List<(Rect rect, ThreadStackGroup group)>();

        foreach (var group in vm.Groups)
        {
            double boxH = HeaderH + group.Frames.Count * RowHeight + 12;
            var    rect = new Rect(x, BoxPadY, BoxWidth, boxH);
            boxes.Add((rect, group));
            group.X = x;
            group.Y = BoxPadY;
            x      += BoxWidth + BoxPadX;
        }

        // Update canvas size
        StacksCanvas.Width  = x;
        StacksCanvas.Height = boxes.Max(b => b.rect.Bottom) + BoxPadY;

        // Draw connectors first (behind boxes)
        for (int i = 0; i < boxes.Count - 1; i++)
        {
            var (r1, _) = boxes[i];
            var (r2, _) = boxes[i + 1];
            DrawArrow(r1.Right, r1.Top + r1.Height / 2,
                      r2.Left,  r2.Top + r2.Height / 2);
        }

        // Draw boxes on top
        foreach (var (rect, group) in boxes)
            DrawBox(rect, group);
    }

    private void DrawBox(Rect rect, ThreadStackGroup group)
    {
        var border = new Border
        {
            Width             = rect.Width,
            Height            = rect.Height,
            BorderBrush       = (Brush)TryFindResource("DB_CallStackActiveBrush")
                                ?? Brushes.CornflowerBlue,
            BorderThickness   = new Thickness(1),
            CornerRadius      = new CornerRadius(4),
            Background        = (Brush)TryFindResource("PanelBackground") ?? Brushes.DimGray,
        };

        var stack = new StackPanel();

        // Thread header
        var header = new TextBlock
        {
            Text              = $"[{group.ThreadId}] {group.ThreadName}",
            FontWeight        = FontWeights.SemiBold,
            FontSize          = 11,
            Foreground        = (Brush)TryFindResource("DB_CallStackActiveBrush") ?? Brushes.CornflowerBlue,
            Padding           = new Thickness(6, 3, 6, 3),
            Background        = (Brush)TryFindResource("ToolbarBackground") ?? Brushes.Gray,
            TextTrimming      = TextTrimming.CharacterEllipsis,
        };
        stack.Children.Add(header);

        // Frame rows
        foreach (var frame in group.Frames)
        {
            var row = new TextBlock
            {
                Text         = frame.DisplayText,
                FontSize     = 11,
                Foreground   = (Brush)TryFindResource("PanelForeground") ?? Brushes.White,
                Padding      = new Thickness(10, 1, 6, 1),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Height       = RowHeight,
            };
            stack.Children.Add(row);
        }

        border.Child = stack;
        Canvas.SetLeft(border, rect.X);
        Canvas.SetTop(border, rect.Y);
        StacksCanvas.Children.Add(border);
    }

    private void DrawArrow(double x1, double y1, double x2, double y2)
    {
        var brush = (Brush)TryFindResource("PanelForeground") ?? Brushes.Gray;
        var line  = new Line
        {
            X1              = x1,
            Y1              = y1,
            X2              = x2 - 8,
            Y2              = y2,
            Stroke          = brush,
            StrokeThickness = 1,
            StrokeDashArray = [4, 2],
        };
        StacksCanvas.Children.Add(line);

        // Arrowhead
        var arrow = new Polygon
        {
            Fill = brush,
            Points = new PointCollection
            {
                new(x2,     y2),
                new(x2 - 8, y2 - 4),
                new(x2 - 8, y2 + 4),
            }
        };
        StacksCanvas.Children.Add(arrow);
    }
}
