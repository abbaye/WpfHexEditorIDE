// Apache 2.0 - 2026
// WpfHexEditor.Docking - VS-Like Docking Framework

using System.Windows;
using WpfHexEditor.Docking.Model;

namespace WpfHexEditor.Docking.Drag;

/// <summary>
/// Type of drop operation.
/// </summary>
public enum DropType
{
    SplitLeft,
    SplitRight,
    SplitTop,
    SplitBottom,
    TabInto,
    RootLeft,
    RootRight,
    RootTop,
    RootBottom
}

/// <summary>
/// Describes WHERE a dragged item will be placed if dropped.
/// Contains both the target info and the logic to execute the dock operation.
/// </summary>
public class DockDropTarget
{
    /// <summary>The pane/panel we're dropping into/next to.</summary>
    public LayoutElement TargetElement { get; }

    /// <summary>Which side of the target to dock to.</summary>
    public DockSide Side { get; }

    /// <summary>The type of drop operation.</summary>
    public DropType Type { get; }

    /// <summary>Screen-space rectangle showing where content will appear.</summary>
    public Rect PreviewBounds { get; set; }

    public DockDropTarget(LayoutElement target, DockSide side, DropType type)
    {
        TargetElement = target;
        Side = side;
        Type = type;
    }

    /// <summary>
    /// Execute the dock operation: remove the dragged element from its source
    /// and insert it at the target location per the DropType.
    /// </summary>
    public void Execute(LayoutContent draggedContent)
    {
        // For TabInto, verify type compatibility first - don't remove from parent if drop would fail
        if (Type == DropType.TabInto && !IsTabIntoCompatible(draggedContent))
        {
            // Incompatible types: convert to a split instead
            ExecuteAsSplit(draggedContent);
            return;
        }

        // Remove from source
        draggedContent.RemoveFromParent();

        switch (Type)
        {
            case DropType.TabInto:
                ExecuteTabInto(draggedContent);
                break;

            case DropType.SplitLeft:
            case DropType.SplitRight:
            case DropType.SplitTop:
            case DropType.SplitBottom:
                ExecuteSplit(draggedContent);
                break;

            case DropType.RootLeft:
            case DropType.RootRight:
            case DropType.RootTop:
            case DropType.RootBottom:
                ExecuteRootDock(draggedContent);
                break;
        }
    }

    private bool IsTabIntoCompatible(LayoutContent content) =>
        (content is LayoutAnchorable && TargetElement is LayoutAnchorablePane) ||
        (content is LayoutDocument && TargetElement is LayoutDocumentPane);

    /// <summary>
    /// Fallback: when TabInto is incompatible, do a bottom split instead.
    /// E.g. dropping anchorable onto document pane → split below document.
    /// </summary>
    private void ExecuteAsSplit(LayoutContent content)
    {
        draggedContent_RemoveAndSplit(content, DropType.SplitBottom, DockSide.Bottom);
    }

    private void draggedContent_RemoveAndSplit(LayoutContent content, DropType splitType, DockSide side)
    {
        content.RemoveFromParent();

        var isHorizontalSplit = splitType is DropType.SplitLeft or DropType.SplitRight;
        var insertBefore = splitType is DropType.SplitLeft or DropType.SplitTop;
        var newOrientation = isHorizontalSplit ? LayoutOrientation.Horizontal : LayoutOrientation.Vertical;

        LayoutElement newPane;
        if (content is LayoutAnchorable anchorable)
        {
            var anchPane = new LayoutAnchorablePane
            {
                DockWidth = new GridLength(200, GridUnitType.Pixel),
                DockHeight = new GridLength(200, GridUnitType.Pixel),
                DockSide = side
            };
            anchPane.Children.Add(anchorable);
            newPane = anchPane;
        }
        else if (content is LayoutDocument document)
        {
            var docPane = new LayoutDocumentPane();
            docPane.Children.Add(document);
            newPane = docPane;
        }
        else return;

        if (TargetElement.Parent is LayoutPanel parentPanel && parentPanel.Orientation == newOrientation)
        {
            var targetIndex = parentPanel.Children.IndexOf(TargetElement);
            if (targetIndex >= 0)
            {
                var insertIndex = insertBefore ? targetIndex : targetIndex + 1;
                parentPanel.InsertChildAt(insertIndex, newPane);
                return;
            }
        }

        if (TargetElement.Parent is LayoutPanel parent)
        {
            var wrapper = new LayoutPanel(newOrientation);
            var targetIndex = parent.Children.IndexOf(TargetElement);
            parent.Children[targetIndex] = wrapper;

            if (insertBefore)
            {
                wrapper.Children.Add(newPane);
                wrapper.Children.Add(TargetElement);
            }
            else
            {
                wrapper.Children.Add(TargetElement);
                wrapper.Children.Add(newPane);
            }
        }
    }

    private void ExecuteTabInto(LayoutContent content)
    {
        if (content is LayoutAnchorable anchorable)
        {
            if (TargetElement is LayoutAnchorablePane pane)
            {
                pane.Children.Add(anchorable);
                pane.SelectedContent = anchorable;
            }
        }
        else if (content is LayoutDocument document)
        {
            if (TargetElement is LayoutDocumentPane pane)
            {
                pane.Children.Add(document);
                pane.SelectedContent = document;
            }
        }
    }

    private void ExecuteSplit(LayoutContent content)
    {
        var isHorizontalSplit = Type is DropType.SplitLeft or DropType.SplitRight;
        var insertBefore = Type is DropType.SplitLeft or DropType.SplitTop;
        var newOrientation = isHorizontalSplit ? LayoutOrientation.Horizontal : LayoutOrientation.Vertical;

        // Create a new pane for the dragged content
        LayoutElement newPane;
        if (content is LayoutAnchorable anchorable)
        {
            var anchPane = new LayoutAnchorablePane
            {
                DockWidth = new GridLength(200, GridUnitType.Pixel),
                DockHeight = new GridLength(200, GridUnitType.Pixel),
                DockSide = Side
            };
            anchPane.Children.Add(anchorable);
            newPane = anchPane;
        }
        else if (content is LayoutDocument document)
        {
            var docPane = new LayoutDocumentPane();
            docPane.Children.Add(document);
            newPane = docPane;
        }
        else return;

        // If the target's parent is already a panel with the right orientation, just insert
        if (TargetElement.Parent is LayoutPanel parentPanel && parentPanel.Orientation == newOrientation)
        {
            var targetIndex = parentPanel.Children.IndexOf(TargetElement);
            if (targetIndex >= 0)
            {
                var insertIndex = insertBefore ? targetIndex : targetIndex + 1;
                parentPanel.InsertChildAt(insertIndex, newPane);
                return;
            }
        }

        // Otherwise, wrap the target in a new panel
        if (TargetElement.Parent is LayoutPanel parent)
        {
            var wrapper = new LayoutPanel(newOrientation);
            var targetIndex = parent.Children.IndexOf(TargetElement);

            // Replace target with wrapper
            parent.Children[targetIndex] = wrapper;

            // Add target and new pane in the right order
            if (insertBefore)
            {
                wrapper.Children.Add(newPane);
                wrapper.Children.Add(TargetElement);
            }
            else
            {
                wrapper.Children.Add(TargetElement);
                wrapper.Children.Add(newPane);
            }
        }
    }

    private void ExecuteRootDock(LayoutContent content)
    {
        var root = TargetElement as LayoutRoot ?? TargetElement.Root;
        if (root == null) return;

        var isHorizontal = Type is DropType.RootLeft or DropType.RootRight;
        var insertBefore = Type is DropType.RootLeft or DropType.RootTop;
        var newOrientation = isHorizontal ? LayoutOrientation.Horizontal : LayoutOrientation.Vertical;

        // Create pane for content
        LayoutElement newPane;
        if (content is LayoutAnchorable anchorable)
        {
            var anchPane = new LayoutAnchorablePane
            {
                DockWidth = new GridLength(250, GridUnitType.Pixel),
                DockHeight = new GridLength(200, GridUnitType.Pixel),
                DockSide = Side
            };
            anchPane.Children.Add(anchorable);
            newPane = anchPane;
        }
        else if (content is LayoutDocument document)
        {
            var docPane = new LayoutDocumentPane();
            docPane.Children.Add(document);
            newPane = docPane;
        }
        else return;

        var currentRoot = root.RootPanel;

        if (currentRoot.Orientation == newOrientation)
        {
            // Just insert into existing root panel
            if (insertBefore)
                currentRoot.InsertChildAt(0, newPane);
            else
                currentRoot.Children.Add(newPane);
        }
        else
        {
            // Wrap current root panel in a new one
            var newRoot = new LayoutPanel(newOrientation);
            root.RootPanel = newRoot;

            if (insertBefore)
            {
                newRoot.Children.Add(newPane);
                newRoot.Children.Add(currentRoot);
            }
            else
            {
                newRoot.Children.Add(currentRoot);
                newRoot.Children.Add(newPane);
            }
        }
    }
}
