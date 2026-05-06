// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: Services/CodeGen/EventArgTypeResolver.cs
// Author: Derek Tremblay
// Created: 2026-05-05
// Description:
//     Maps WPF routed event attribute names to their correct System.Windows
//     EventArgs subtype, so generated handler stubs have properly typed parameters.
//
// Architecture Notes:
//     Stateless lookup table — IEventArgTypeProvider allows plugins to register
//     additional event→args mappings for custom controls.
// ==========================================================

namespace WpfHexEditor.Editor.XamlDesigner.Services.CodeGen;

/// <summary>
/// Allows plugins to contribute additional event-name → EventArgs-type mappings.
/// </summary>
public interface IEventArgTypeProvider
{
    /// <summary>
    /// Returns the fully-qualified or short EventArgs type name for
    /// <paramref name="eventAttributeName"/>, or null when not handled.
    /// </summary>
    string? ResolveArgsType(string eventAttributeName);
}

/// <summary>
/// Resolves the EventArgs subtype for a WPF routed event attribute name.
/// Falls back to <c>RoutedEventArgs</c> when no specific mapping exists.
/// </summary>
public sealed class EventArgTypeResolver
{
    // ── Built-in mapping table ────────────────────────────────────────────────

    private static readonly Dictionary<string, string> BuiltIn =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Mouse
            ["Click"]                           = "RoutedEventArgs",
            ["PreviewMouseDown"]                = "MouseButtonEventArgs",
            ["MouseDown"]                       = "MouseButtonEventArgs",
            ["PreviewMouseUp"]                  = "MouseButtonEventArgs",
            ["MouseUp"]                         = "MouseButtonEventArgs",
            ["PreviewMouseLeftButtonDown"]       = "MouseButtonEventArgs",
            ["MouseLeftButtonDown"]             = "MouseButtonEventArgs",
            ["PreviewMouseRightButtonDown"]      = "MouseButtonEventArgs",
            ["MouseRightButtonDown"]            = "MouseButtonEventArgs",
            ["PreviewMouseLeftButtonUp"]         = "MouseButtonEventArgs",
            ["MouseLeftButtonUp"]               = "MouseButtonEventArgs",
            ["PreviewMouseRightButtonUp"]        = "MouseButtonEventArgs",
            ["MouseRightButtonUp"]              = "MouseButtonEventArgs",
            ["PreviewMouseMove"]                = "MouseEventArgs",
            ["MouseMove"]                       = "MouseEventArgs",
            ["MouseEnter"]                      = "MouseEventArgs",
            ["MouseLeave"]                      = "MouseEventArgs",
            ["PreviewMouseWheel"]               = "MouseWheelEventArgs",
            ["MouseWheel"]                      = "MouseWheelEventArgs",

            // Keyboard
            ["PreviewKeyDown"]                  = "KeyEventArgs",
            ["KeyDown"]                         = "KeyEventArgs",
            ["PreviewKeyUp"]                    = "KeyEventArgs",
            ["KeyUp"]                           = "KeyEventArgs",
            ["PreviewTextInput"]                = "TextCompositionEventArgs",
            ["TextInput"]                       = "TextCompositionEventArgs",

            // Touch / Stylus
            ["TouchDown"]                       = "TouchEventArgs",
            ["TouchMove"]                       = "TouchEventArgs",
            ["TouchUp"]                         = "TouchEventArgs",
            ["StylusDown"]                      = "StylusDownEventArgs",
            ["StylusMove"]                      = "StylusEventArgs",
            ["StylusUp"]                        = "StylusEventArgs",

            // Drag & Drop
            ["PreviewDragEnter"]                = "DragEventArgs",
            ["DragEnter"]                       = "DragEventArgs",
            ["PreviewDragLeave"]                = "DragEventArgs",
            ["DragLeave"]                       = "DragEventArgs",
            ["PreviewDragOver"]                 = "DragEventArgs",
            ["DragOver"]                        = "DragEventArgs",
            ["PreviewDrop"]                     = "DragEventArgs",
            ["Drop"]                            = "DragEventArgs",
            ["GiveFeedback"]                    = "GiveFeedbackEventArgs",
            ["QueryContinueDrag"]               = "QueryContinueDragEventArgs",

            // Focus
            ["GotFocus"]                        = "RoutedEventArgs",
            ["LostFocus"]                       = "RoutedEventArgs",
            ["PreviewGotKeyboardFocus"]         = "KeyboardFocusChangedEventArgs",
            ["GotKeyboardFocus"]                = "KeyboardFocusChangedEventArgs",
            ["PreviewLostKeyboardFocus"]        = "KeyboardFocusChangedEventArgs",
            ["LostKeyboardFocus"]               = "KeyboardFocusChangedEventArgs",

            // Layout / Lifetime
            ["Loaded"]                          = "RoutedEventArgs",
            ["Unloaded"]                        = "RoutedEventArgs",
            ["SizeChanged"]                     = "SizeChangedEventArgs",
            ["LayoutUpdated"]                   = "EventArgs",
            ["Initialized"]                     = "EventArgs",

            // Window / Navigation
            ["Activated"]                       = "EventArgs",
            ["Deactivated"]                     = "EventArgs",
            ["Closing"]                         = "System.ComponentModel.CancelEventArgs",
            ["Closed"]                          = "EventArgs",
            ["ContentRendered"]                 = "EventArgs",
            ["StateChanged"]                    = "EventArgs",
            ["LocationChanged"]                 = "EventArgs",

            // ScrollViewer
            ["ScrollChanged"]                   = "ScrollChangedEventArgs",

            // TextBox / TextBlock
            ["TextChanged"]                     = "TextChangedEventArgs",

            // Selector / ItemsControl
            ["SelectionChanged"]                = "SelectionChangedEventArgs",

            // ToggleButton / CheckBox / RadioButton
            ["Checked"]                         = "RoutedEventArgs",
            ["Unchecked"]                       = "RoutedEventArgs",
            ["Indeterminate"]                   = "RoutedEventArgs",

            // TreeView / Expander
            ["Expanded"]                        = "RoutedEventArgs",
            ["Collapsed"]                       = "RoutedEventArgs",
            ["Selected"]                        = "RoutedEventArgs",
            ["Unselected"]                      = "RoutedEventArgs",

            // RangeBase / Slider / ProgressBar
            ["ValueChanged"]                    = "RoutedPropertyChangedEventArgs<double>",

            // MenuItem
            ["Opened"]                          = "RoutedEventArgs",
            ["Closed"]                          = "RoutedEventArgs",
            ["SubmenuOpened"]                   = "RoutedEventArgs",
            ["SubmenuClosed"]                   = "RoutedEventArgs",

            // MediaElement
            ["MediaOpened"]                     = "RoutedEventArgs",
            ["MediaEnded"]                      = "RoutedEventArgs",
            ["MediaFailed"]                     = "ExceptionRoutedEventArgs",

            // WebBrowser / Frame navigation
            ["Navigating"]                      = "NavigatingCancelEventArgs",
            ["Navigated"]                       = "NavigationEventArgs",
            ["NavigationFailed"]                = "NavigationFailedEventArgs",

            // Command
            ["Executed"]                        = "ExecutedRoutedEventArgs",
            ["CanExecute"]                      = "CanExecuteRoutedEventArgs",
            ["PreviewExecuted"]                 = "ExecutedRoutedEventArgs",
            ["PreviewCanExecute"]               = "CanExecuteRoutedEventArgs",

            // ContextMenu
            ["ContextMenuOpening"]              = "ContextMenuEventArgs",
            ["ContextMenuClosing"]              = "ContextMenuEventArgs",

            // ToolTip
            ["ToolTipOpening"]                  = "ToolTipEventArgs",
            ["ToolTipClosing"]                  = "ToolTipEventArgs",
        };

    // ── Plugin-contributed providers ──────────────────────────────────────────

    private readonly List<IEventArgTypeProvider> _providers = [];

    /// <summary>Registers a plugin-contributed event-arg type provider.</summary>
    public void Register(IEventArgTypeProvider provider)
        => _providers.Add(provider);

    // ── Resolution ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the EventArgs type name for <paramref name="eventAttributeName"/>.
    /// Checks plugin providers first, then the built-in table.
    /// Falls back to <c>RoutedEventArgs</c>.
    /// </summary>
    public string Resolve(string eventAttributeName)
    {
        // Plugin-contributed mappings take priority.
        foreach (var provider in _providers)
        {
            var result = provider.ResolveArgsType(eventAttributeName);
            if (result is not null)
                return result;
        }

        if (BuiltIn.TryGetValue(eventAttributeName, out var builtin))
            return builtin;

        // For Preview* variants, strip prefix and retry.
        if (eventAttributeName.StartsWith("Preview", StringComparison.Ordinal))
        {
            var base_ = eventAttributeName["Preview".Length..];
            if (BuiltIn.TryGetValue(base_, out var baseResult))
                return baseResult;
        }

        return "RoutedEventArgs";
    }
}
