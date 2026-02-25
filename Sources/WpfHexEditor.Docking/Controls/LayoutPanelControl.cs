// Apache 2.0 - 2026
// WpfHexEditor.Docking - VS-Like Docking Framework

using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfHexEditor.Docking.Model;

namespace WpfHexEditor.Docking.Controls;

/// <summary>
/// Renders a LayoutPanel as a Grid with GridSplitters between children.
/// For a horizontal LayoutPanel with 3 children:
///   Grid with 5 columns: [child0][splitter][child1][splitter][child2]
/// </summary>
public class LayoutPanelControl : ContentControl
{
    private Grid? _grid;
    private LayoutPanel? _subscribedModel;

    public static readonly DependencyProperty ModelProperty =
        DependencyProperty.Register(nameof(Model), typeof(LayoutPanel), typeof(LayoutPanelControl),
            new PropertyMetadata(null, OnModelChanged));

    /// <summary>The layout panel model this control renders.</summary>
    public LayoutPanel? Model
    {
        get => (LayoutPanel?)GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    private static void OnModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((LayoutPanelControl)d).OnModelChanged(
            e.OldValue as LayoutPanel,
            e.NewValue as LayoutPanel);
    }

    private void OnModelChanged(LayoutPanel? oldModel, LayoutPanel? newModel)
    {
        if (_subscribedModel != null)
        {
            _subscribedModel.Children.CollectionChanged -= OnModelChildrenChanged;
            _subscribedModel.PropertyChanged -= OnModelPropertyChanged;
            _subscribedModel = null;
        }

        if (newModel != null)
        {
            _subscribedModel = newModel;
            newModel.Children.CollectionChanged += OnModelChildrenChanged;
            newModel.PropertyChanged += OnModelPropertyChanged;
        }

        RebuildGrid();
    }

    private void OnModelChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildGrid();
    }

    private void OnModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LayoutPanel.Orientation))
            RebuildGrid();
    }

    /// <summary>
    /// Rebuilds the entire Grid from the model. Creates child controls recursively
    /// with GridSplitters between them.
    /// </summary>
    private void RebuildGrid()
    {
        _grid = new Grid();

        if (Model == null || Model.Children.Count == 0)
        {
            Content = _grid;
            return;
        }

        var isHorizontal = Model.Orientation == LayoutOrientation.Horizontal;
        var children = Model.Children;

        for (int i = 0; i < children.Count; i++)
        {
            var child = children[i];

            // Add the column/row for the child
            if (isHorizontal)
            {
                var width = GetDockWidth(child);
                _grid.ColumnDefinitions.Add(new ColumnDefinition { Width = width, MinWidth = GetMinWidth(child) });
            }
            else
            {
                var height = GetDockHeight(child);
                _grid.RowDefinitions.Add(new RowDefinition { Height = height, MinHeight = GetMinHeight(child) });
            }

            // Create the child control
            var childControl = CreateChildControl(child);
            if (isHorizontal)
                Grid.SetColumn(childControl, _grid.ColumnDefinitions.Count - 1);
            else
                Grid.SetRow(childControl, _grid.RowDefinitions.Count - 1);
            _grid.Children.Add(childControl);

            // Add splitter after each child except the last
            if (i < children.Count - 1)
            {
                if (isHorizontal)
                {
                    _grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4, GridUnitType.Pixel) });
                    var splitter = CreateSplitter(isHorizontal);
                    Grid.SetColumn(splitter, _grid.ColumnDefinitions.Count - 1);
                    _grid.Children.Add(splitter);
                }
                else
                {
                    _grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(4, GridUnitType.Pixel) });
                    var splitter = CreateSplitter(isHorizontal);
                    Grid.SetRow(splitter, _grid.RowDefinitions.Count - 1);
                    _grid.Children.Add(splitter);
                }
            }
        }

        Content = _grid;
    }

    private FrameworkElement CreateChildControl(LayoutElement child)
    {
        return child switch
        {
            LayoutPanel panel => new LayoutPanelControl { Model = panel },
            LayoutDocumentPane docPane => new LayoutDocumentPaneControl { Model = docPane },
            LayoutAnchorablePane anchPane => new LayoutAnchorablePaneControl { Model = anchPane },
            _ => new Border { Background = Brushes.Gray } // fallback
        };
    }

    private GridSplitter CreateSplitter(bool isHorizontal)
    {
        var splitter = new GridSplitter
        {
            ResizeDirection = isHorizontal ? GridResizeDirection.Columns : GridResizeDirection.Rows,
            HorizontalAlignment = isHorizontal ? HorizontalAlignment.Stretch : HorizontalAlignment.Stretch,
            VerticalAlignment = isHorizontal ? VerticalAlignment.Stretch : VerticalAlignment.Stretch,
            ShowsPreview = false,
            Background = (Brush)FindResource("DockSplitterBrush"),
            Cursor = isHorizontal
                ? System.Windows.Input.Cursors.SizeWE
                : System.Windows.Input.Cursors.SizeNS
        };

        // Hover effect
        splitter.MouseEnter += (s, e) =>
        {
            if (s is GridSplitter gs)
                gs.Background = (Brush)FindResource("DockSplitterHoverBrush");
        };
        splitter.MouseLeave += (s, e) =>
        {
            if (s is GridSplitter gs)
                gs.Background = (Brush)FindResource("DockSplitterBrush");
        };

        if (isHorizontal)
            splitter.Width = 4;
        else
            splitter.Height = 4;

        return splitter;
    }

    private static GridLength GetDockWidth(LayoutElement element) => element switch
    {
        LayoutDocumentPane dp => dp.DockWidth,
        LayoutAnchorablePane ap => ap.DockWidth,
        LayoutPanel => new GridLength(1, GridUnitType.Star),
        _ => new GridLength(1, GridUnitType.Star)
    };

    private static GridLength GetDockHeight(LayoutElement element) => element switch
    {
        LayoutDocumentPane dp => dp.DockHeight,
        LayoutAnchorablePane ap => ap.DockHeight,
        LayoutPanel => new GridLength(1, GridUnitType.Star),
        _ => new GridLength(1, GridUnitType.Star)
    };

    private static double GetMinWidth(LayoutElement element) => element switch
    {
        LayoutAnchorablePane ap => ap.DockMinWidth,
        _ => 50
    };

    private static double GetMinHeight(LayoutElement element) => element switch
    {
        LayoutAnchorablePane ap => ap.DockMinHeight,
        _ => 50
    };
}
