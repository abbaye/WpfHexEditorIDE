// ==========================================================
// Project: WpfHexEditor.SDK
// File: Contracts/IDebugVisualizer.cs
// Description: Extension point for custom debug variable visualizers.
//              Implement this interface in any plugin to contribute a visualizer
//              that appears in the "View as…" dropdown of Locals/Watch/Autos panels.
// Architecture: SDK contract only — no WPF types, no Core.Debugger references.
// ==========================================================

using System.Windows;

namespace WpfHexEditor.SDK.Contracts;

/// <summary>
/// Metadata about a variable value passed to a visualizer.
/// </summary>
public sealed record DebugVariableContext(
    string Name,
    string TypeName,
    string RawValue,
    Func<string, CancellationToken, Task<string?>>? EvaluateAsync
);

/// <summary>
/// A registered debug visualizer that can render a variable value in a custom panel.
/// </summary>
public interface IDebugVisualizer
{
    /// <summary>Short display label shown in the "View as…" menu (e.g. "Collection Visualizer").</summary>
    string DisplayName { get; }

    /// <summary>
    /// Returns true when this visualizer can handle the given variable context.
    /// Called to populate the "View as…" dropdown — must be synchronous and fast.
    /// </summary>
    bool CanVisualize(DebugVariableContext context);

    /// <summary>
    /// Creates the WPF panel that renders the visualized value.
    /// Called on the UI thread. The returned element is hosted in a floating popup or tool window.
    /// </summary>
    FrameworkElement CreateView(DebugVariableContext context);
}
