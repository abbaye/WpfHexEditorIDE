//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
//////////////////////////////////////////////

using WpfHexEditor.Docking.Core.Nodes;

namespace WpfHexEditor.Docking.Wpf;

/// <summary>
/// Manages event subscriptions on a <see cref="DockTabControl"/> with deterministic cleanup.
/// Each instance wires Close/Drag/Float/AutoHide events and stores named delegates
/// so they can be reliably unsubscribed in <see cref="Dispose"/>.
/// </summary>
internal sealed class DockTabEventWirer : IDisposable
{
    private readonly DockTabControl _tabControl;
    private readonly Action<DockItem> _closeHandler;
    private readonly Action<DockItem> _dragHandler;
    private readonly Action<DockItem> _floatHandler;
    private readonly Action<DockItem> _autoHideHandler;

    public DockTabEventWirer(DockTabControl tabControl, DockControl host)
    {
        _tabControl = tabControl;

        _closeHandler = item => host.RaiseTabCloseRequested(item);

        _dragHandler = item => host.DragManager?.BeginDrag(item);

        _floatHandler = item =>
        {
            if (host.Engine is null) return;
            host.Engine.Float(item);
            host.RebuildVisualTree();
        };

        _autoHideHandler = item =>
        {
            if (host.Engine is null) return;
            host.Engine.AutoHide(item);
            host.RebuildVisualTree();
        };

        _tabControl.TabCloseRequested    += _closeHandler;
        _tabControl.TabDragStarted       += _dragHandler;
        _tabControl.TabFloatRequested    += _floatHandler;
        _tabControl.TabAutoHideRequested += _autoHideHandler;
    }

    public void Dispose()
    {
        _tabControl.TabCloseRequested    -= _closeHandler;
        _tabControl.TabDragStarted       -= _dragHandler;
        _tabControl.TabFloatRequested    -= _floatHandler;
        _tabControl.TabAutoHideRequested -= _autoHideHandler;
    }
}
