// Apache 2.0 - 2026
// WpfHexEditor.Docking - VS-Like Docking Framework

using System.Windows.Input;

namespace WpfHexEditor.Docking.Controls;

/// <summary>
/// Static routed commands for docking operations.
/// Used in Generic.xaml templates to avoid code-behind event handlers.
/// </summary>
public static class DockCommands
{
    public static RoutedCommand SelectDocumentCommand { get; } = new(nameof(SelectDocumentCommand), typeof(DockCommands));
    public static RoutedCommand CloseDocumentCommand { get; } = new(nameof(CloseDocumentCommand), typeof(DockCommands));
    public static RoutedCommand SelectAnchorableCommand { get; } = new(nameof(SelectAnchorableCommand), typeof(DockCommands));
    public static RoutedCommand CloseAnchorableCommand { get; } = new(nameof(CloseAnchorableCommand), typeof(DockCommands));
    public static RoutedCommand ToggleAutoHideCommand { get; } = new(nameof(ToggleAutoHideCommand), typeof(DockCommands));
    public static RoutedCommand PinAnchorableCommand { get; } = new(nameof(PinAnchorableCommand), typeof(DockCommands));
    public static RoutedCommand FloatCommand { get; } = new(nameof(FloatCommand), typeof(DockCommands));
    public static RoutedCommand ShowAutoHidePopupCommand { get; } = new(nameof(ShowAutoHidePopupCommand), typeof(DockCommands));
}
