//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6, Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.Docking.Core;
using WpfHexEditor.Docking.Core.Nodes;

namespace WpfHexEditor.Docking.Wpf;

/// <summary>
/// Minimal contract for a dock host, enabling decoupled access to the
/// layout engine and visual tree without depending on <see cref="DockControl"/> directly.
/// </summary>
public interface IDockHost
{
    DockLayoutRoot? Layout { get; set; }
    DockEngine? Engine { get; }
    Func<DockItem, object>? ContentFactory { get; set; }
    Func<DockItem, bool>? BeforeCloseCallback { get; set; }

    void RebuildVisualTree();

    event Action<DockItem>? TabCloseRequested;
}
