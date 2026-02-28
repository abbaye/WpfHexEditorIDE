//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using WpfHexEditor.Docking.Core;
using WpfHexEditor.Docking.Core.Nodes;

namespace WpfHexEditor.Docking.Wpf;

/// <summary>
/// WPF projection of <see cref="DockSplitNode"/>: a Grid with GridSplitters between children.
/// After the first layout pass, non-document panels are converted from Star to Pixel sizing
/// so that side/bottom panels keep their absolute size on window maximize/minimize (VS-style).
/// </summary>
public class DockSplitPanel : Grid
{
    private readonly List<int> _contentDefinitionIndices = [];
    private int _documentChildIndex = -1;
    private bool _convertedToFixed;

    public DockSplitNode? Node { get; private set; }

    /// <summary>
    /// Minimum pane size in DIPs. Prevents panels from being collapsed to zero.
    /// </summary>
    public double MinPaneSize { get; set; } = 60;

    public void Bind(DockSplitNode node, Func<DockNode, UIElement> nodeFactory)
    {
        Node = node;
        Children.Clear();
        ColumnDefinitions.Clear();
        RowDefinitions.Clear();
        _contentDefinitionIndices.Clear();
        _documentChildIndex = -1;
        _convertedToFixed = false;

        if (node.Children.Count == 0) return;

        var isHorizontal = node.Orientation == SplitOrientation.Horizontal;

        // Identify which child contains the DocumentHostNode (directly or nested).
        // That child will keep Star sizing; all others get Pixel sizing after first render.
        for (var i = 0; i < node.Children.Count; i++)
        {
            if (ContainsDocumentHost(node.Children[i]))
            {
                _documentChildIndex = i;
                break;
            }
        }

        for (var i = 0; i < node.Children.Count; i++)
        {
            // Add splitter before each child (except the first)
            if (i > 0)
            {
                if (isHorizontal)
                    ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4, GridUnitType.Pixel) });
                else
                    RowDefinitions.Add(new RowDefinition { Height = new GridLength(4, GridUnitType.Pixel) });

                var splitter = new GridSplitter
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    ResizeBehavior = GridResizeBehavior.PreviousAndNext,
                    Background = System.Windows.Media.Brushes.Gray
                };

                if (isHorizontal)
                {
                    splitter.Width = 4;
                    SetColumn(splitter, ColumnDefinitions.Count - 1);
                }
                else
                {
                    splitter.Height = 4;
                    SetRow(splitter, RowDefinitions.Count - 1);
                }

                splitter.DragCompleted += OnSplitterDragCompleted;
                Children.Add(splitter);
            }

            // Add content column/row with minimum size constraint
            var ratio = i < node.Ratios.Count ? node.Ratios[i] : 1.0 / node.Children.Count;
            if (isHorizontal)
                ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width    = new GridLength(ratio, GridUnitType.Star),
                    MinWidth = MinPaneSize
                });
            else
                RowDefinitions.Add(new RowDefinition
                {
                    Height    = new GridLength(ratio, GridUnitType.Star),
                    MinHeight = MinPaneSize
                });

            _contentDefinitionIndices.Add(isHorizontal ? ColumnDefinitions.Count - 1 : RowDefinitions.Count - 1);

            var childElement = nodeFactory(node.Children[i]);

            if (isHorizontal)
                SetColumn(childElement, ColumnDefinitions.Count - 1);
            else
                SetRow(childElement, RowDefinitions.Count - 1);

            Children.Add(childElement);
        }

        // After the first layout pass, convert non-document panels to Pixel sizing
        // so they keep their absolute size on window resize (VS-style behavior).
        if (_documentChildIndex >= 0)
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, ConvertToFixedSizing);
    }

    /// <summary>
    /// Converts non-document columns/rows from Star to Pixel sizing after the first layout pass.
    /// The document-hosting child keeps Star sizing so it absorbs all window resize changes.
    /// </summary>
    private void ConvertToFixedSizing()
    {
        if (_convertedToFixed || Node is null || _documentChildIndex < 0) return;
        _convertedToFixed = true;

        var isHorizontal = Node.Orientation == SplitOrientation.Horizontal;

        for (var i = 0; i < _contentDefinitionIndices.Count; i++)
        {
            var defIndex = _contentDefinitionIndices[i];

            if (i == _documentChildIndex)
            {
                // Document area: Star sizing (absorbs all resize)
                if (isHorizontal)
                    ColumnDefinitions[defIndex].Width = new GridLength(1, GridUnitType.Star);
                else
                    RowDefinitions[defIndex].Height = new GridLength(1, GridUnitType.Star);
            }
            else
            {
                // Side/bottom panel: fixed pixel sizing
                if (isHorizontal)
                    ColumnDefinitions[defIndex].Width = new GridLength(
                        ColumnDefinitions[defIndex].ActualWidth, GridUnitType.Pixel);
                else
                    RowDefinitions[defIndex].Height = new GridLength(
                        RowDefinitions[defIndex].ActualHeight, GridUnitType.Pixel);
            }
        }
    }

    /// <summary>
    /// Syncs the Grid's actual column/row sizes back to the DockSplitNode ratios after a splitter drag.
    /// Uses ActualWidth/ActualHeight to handle both Pixel and Star columns correctly.
    /// </summary>
    private void OnSplitterDragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (Node is null || _contentDefinitionIndices.Count != Node.Children.Count) return;

        var isHorizontal = Node.Orientation == SplitOrientation.Horizontal;
        var newRatios = new double[_contentDefinitionIndices.Count];
        var totalSize = 0.0;

        for (var i = 0; i < _contentDefinitionIndices.Count; i++)
        {
            var defIndex = _contentDefinitionIndices[i];
            var size = isHorizontal
                ? ColumnDefinitions[defIndex].ActualWidth
                : RowDefinitions[defIndex].ActualHeight;
            newRatios[i] = size;
            totalSize += size;
        }

        if (totalSize <= 0) return;

        // Clamp ratios: never allow less than 5% for any pane
        const double minRatio = 0.05;
        for (var i = 0; i < newRatios.Length; i++)
            newRatios[i] = Math.Max(minRatio, newRatios[i] / totalSize);

        // Re-normalize after clamping
        var clampedSum = newRatios.Sum();
        Node.SetRatios(newRatios.Select(r => r / clampedSum).ToArray());
    }

    /// <summary>
    /// Returns true if the given node is or contains a <see cref="DocumentHostNode"/>.
    /// </summary>
    private static bool ContainsDocumentHost(DockNode node) => node switch
    {
        DocumentHostNode => true,
        DockSplitNode split => split.Children.Any(ContainsDocumentHost),
        _ => false
    };
}
