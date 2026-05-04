// ==========================================================
// Project: WpfHexEditor.App
// File: Services/DebugVisualizerRegistry.cs
// Description: Implements IDebugVisualizerRegistry — central store for all
//              debug variable visualizers contributed by plugins.
// Architecture: Thread-safe list; created by IDEHostContextImpl and exposed
//               on IIDEHostContext.DebugVisualizers.
// ==========================================================

using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.App.Services;

internal sealed class DebugVisualizerRegistry : IDebugVisualizerRegistry
{
    private readonly List<IDebugVisualizer> _visualizers = [];
    private readonly Lock _lock = new();

    public void Register(IDebugVisualizer visualizer)
    {
        lock (_lock) _visualizers.Add(visualizer);
    }

    public void Unregister(IDebugVisualizer visualizer)
    {
        lock (_lock) _visualizers.Remove(visualizer);
    }

    public IReadOnlyList<IDebugVisualizer> GetVisualizers(DebugVariableContext context)
    {
        lock (_lock)
            return _visualizers.Where(v => v.CanVisualize(context)).ToList();
    }
}
