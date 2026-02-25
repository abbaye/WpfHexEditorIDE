// Apache 2.0 - 2026
// WpfHexEditor.Docking - VS-Like Docking Framework

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WpfHexEditor.Docking.Drag;

/// <summary>
/// A translucent, non-focusable window that shows the drop preview zone.
/// Displayed at the target position to show where content will dock.
/// </summary>
internal class DragPreviewWindow : Window
{
    public DragPreviewWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        ShowInTaskbar = false;
        IsHitTestVisible = false;
        Focusable = false;
        ResizeMode = ResizeMode.NoResize;
        ShowActivated = false;
        Background = new SolidColorBrush(Color.FromArgb(77, 0, 122, 204)); // AccentColor at 30%
        BorderBrush = new SolidColorBrush(Color.FromArgb(102, 0, 122, 204)); // AccentColor at 40%
        BorderThickness = new Thickness(1);
    }

    /// <summary>Show the preview at the given screen-space bounds.</summary>
    public void ShowPreview(Rect screenBounds)
    {
        Left = screenBounds.Left;
        Top = screenBounds.Top;
        Width = screenBounds.Width;
        Height = screenBounds.Height;

        if (!IsVisible)
            Show();
    }

    /// <summary>Hide the preview.</summary>
    public void HidePreview()
    {
        if (IsVisible)
            Hide();
    }
}
