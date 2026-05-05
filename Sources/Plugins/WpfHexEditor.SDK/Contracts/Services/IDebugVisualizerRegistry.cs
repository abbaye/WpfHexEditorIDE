// ==========================================================
// Project: WpfHexEditor.SDK
// File: Contracts/Services/IDebugVisualizerRegistry.cs
// Description: Service for registering and querying debug variable visualizers.
// Architecture: SDK contract — implemented in App layer (DebugVisualizerRegistry).
// ==========================================================

using WpfHexEditor.SDK.Contracts;

namespace WpfHexEditor.SDK.Contracts.Services;

/// <summary>
/// Service exposed on <see cref="IIDEHostContext"/> for registering and discovering
/// debug variable visualizers.
/// </summary>
public interface IDebugVisualizerRegistry
{
    /// <summary>Registers a visualizer contributed by a plugin.</summary>
    void Register(IDebugVisualizer visualizer);

    /// <summary>Removes a previously registered visualizer.</summary>
    void Unregister(IDebugVisualizer visualizer);

    /// <summary>Returns all visualizers that can handle the given variable context.</summary>
    IReadOnlyList<IDebugVisualizer> GetVisualizers(DebugVariableContext context);
}
