
//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Windows;
using WpfHexEditor.Docking.Core;
using WpfHexEditor.Docking.Core.Nodes;
using WpfHexEditor.Docking.Wpf;
using WpfHexEditor.PluginHost.Adapters;
using WpfHexEditor.SDK.Descriptors;

namespace WpfHexEditor.App.Services;

/// <summary>
/// Bridges the PluginHost IDockingAdapter contract to the concrete WpfHexEditor docking engine.
/// Allows plugins to add/remove dockable panels and document tabs without referencing App internals.
/// </summary>
/// <remarks>
/// Uses a <paramref name="storeContent"/> callback to pre-populate MainWindow's content cache before
/// docking, so ContentFactory returns the plugin-provided UIElement on first display.
/// </remarks>
public sealed class DockingAdapter : IDockingAdapter
{
    private readonly DockEngine _engine;
    private readonly DockLayoutRoot _layout;
    private readonly DockControl _dockHost;
    private readonly Action<string, UIElement> _storeContent;

    public DockingAdapter(
        DockEngine engine,
        DockLayoutRoot layout,
        DockControl dockHost,
        Action<string, UIElement> storeContent)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        _dockHost = dockHost ?? throw new ArgumentNullException(nameof(dockHost));
        _storeContent = storeContent ?? throw new ArgumentNullException(nameof(storeContent));
    }

    /// <inheritdoc />
    public void AddDockablePanel(string uiId, UIElement content, PanelDescriptor descriptor)
    {
        if (_layout.FindItemByContentId(uiId) is not null) return;

        var direction = descriptor.DefaultDockSide?.ToLowerInvariant() switch
        {
            "left" => DockDirection.Left,
            "bottom" => DockDirection.Bottom,
            "top" => DockDirection.Top,
            _ => DockDirection.Right
        };

        var item = new DockItem
        {
            ContentId = uiId,
            Title = descriptor.Title,
            CanClose = descriptor.CanClose
        };

        _storeContent(uiId, content);
        _engine.Dock(item, _layout.MainDocumentHost, direction);
        _dockHost.RebuildVisualTree();
    }

    /// <inheritdoc />
    public void RemoveDockablePanel(string uiId)
    {
        var item = _layout.FindItemByContentId(uiId);
        if (item is not null) _engine.Close(item);
    }

    /// <inheritdoc />
    public void AddDocumentTab(string uiId, UIElement content, DocumentDescriptor descriptor)
    {
        if (_layout.FindItemByContentId(uiId) is not null) return;

        var item = new DockItem
        {
            ContentId = uiId,
            Title = descriptor.Title,
            CanClose = descriptor.CanClose
        };

        _storeContent(uiId, content);
        _engine.Dock(item, _layout.MainDocumentHost, DockDirection.Center);
        _dockHost.RebuildVisualTree();
    }

    /// <inheritdoc />
    public void RemoveDocumentTab(string uiId)
    {
        var item = _layout.FindItemByContentId(uiId);
        if (item is not null) _engine.Close(item);
    }

    public void ShowDockablePanel(string uiId)
    {
        var item = _layout.FindItemByContentId(uiId);
        if (item is not null) _engine.Activate(item);
    }

    public void HideDockablePanel(string uiId)
    {
        var item = _layout.FindItemByContentId(uiId);
        if (item is not null) _engine.Hide(item);
    }

    public void ToggleDockablePanel(string uiId)
    {
        var item = _layout.FindItemByContentId(uiId);
        if (item is null) return;
        if (item.IsVisible) _engine.Hide(item); else _engine.Activate(item);
    }

    public void FocusDockablePanel(string uiId)
    {
        var item = _layout.FindItemByContentId(uiId);
        if (item is not null) _engine.Activate(item);
    }
}
