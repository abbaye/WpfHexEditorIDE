// Apache 2.0 - 2026
// WpfHexEditor.Docking - VS-Like Docking Framework

using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Docking.Controls;
using WpfHexEditor.Docking.Model;

namespace WpfHexEditor.Docking.Drag;

/// <summary>
/// Attached behavior that makes a FrameworkElement a drag source for docking operations.
/// Attach to tab headers and pane title bars.
///
/// Usage in XAML:
///   <Border drag:DragBehavior.DragContent="{Binding}"/>
///
/// When the user clicks and drags, this initiates a dock drag operation
/// via the ancestor DockManager's DragManager.
/// </summary>
public static class DragBehavior
{
    /// <summary>
    /// Attached property: the LayoutContent to drag when this element is dragged.
    /// Setting this to a non-null value activates the drag behavior.
    /// </summary>
    public static readonly DependencyProperty DragContentProperty =
        DependencyProperty.RegisterAttached(
            "DragContent",
            typeof(LayoutContent),
            typeof(DragBehavior),
            new PropertyMetadata(null, OnDragContentChanged));

    public static LayoutContent? GetDragContent(DependencyObject obj) =>
        (LayoutContent?)obj.GetValue(DragContentProperty);

    public static void SetDragContent(DependencyObject obj, LayoutContent? value) =>
        obj.SetValue(DragContentProperty, value);

    private static void OnDragContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element) return;

        // Remove old handler
        element.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;

        // Add handler if content is set
        if (e.NewValue != null)
        {
            element.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
        }
    }

    private static void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement element) return;

        // Don't initiate drag when clicking on buttons inside the drag area
        // (e.g., pin/close buttons in the tool window header)
        if (IsClickOnButton(e.OriginalSource as DependencyObject, element))
            return;

        var content = GetDragContent(element);
        if (content == null) return;

        // Find ancestor DockManager
        var manager = FindAncestor<DockManager>(element);
        if (manager == null) return;

        // Get screen point
        Point screenPoint;
        try
        {
            screenPoint = element.PointToScreen(e.GetPosition(element));
        }
        catch
        {
            return;
        }

        // Start drag tracking (won't activate until threshold is reached)
        manager.DragManager.BeginDragTracking(content, screenPoint, element);

        // Don't mark as handled - allow the click to also select the tab
    }

    /// <summary>
    /// Check if the click originated from within a ButtonBase between the source and the drag element.
    /// </summary>
    private static bool IsClickOnButton(DependencyObject? source, DependencyObject dragElement)
    {
        var current = source;
        while (current != null && current != dragElement)
        {
            if (current is System.Windows.Controls.Primitives.ButtonBase)
                return true;
            current = VisualTreeHelper.GetParent(current);
        }
        return false;
    }

    private static T? FindAncestor<T>(DependencyObject? obj) where T : DependencyObject
    {
        while (obj != null)
        {
            if (obj is T target) return target;
            obj = VisualTreeHelper.GetParent(obj);
        }
        return null;
    }
}
