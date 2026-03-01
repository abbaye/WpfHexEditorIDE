//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6, Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.Docking.Core;
using WpfHexEditor.Docking.Core.Commands;
using WpfHexEditor.Docking.Core.Nodes;
using WpfHexEditor.Docking.Core.Serialization;

namespace WpfHexEditor.Docking.Wpf;

/// <summary>
/// High-level facade aggregating a <see cref="DockControl"/> with optional
/// profile management and layout serialization.
/// Provides a simplified API for common dock operations.
/// </summary>
public class DockWorkspace
{
    private readonly IDockHost _host;
    private readonly DockCommandStack _commandStack = new();

    public DockWorkspace(IDockHost host)
    {
        _host = host;
    }

    public IDockHost Host => _host;
    public DockEngine? Engine => _host.Engine;
    public DockLayoutRoot? Layout => _host.Layout;
    public DockCommandStack CommandStack => _commandStack;
    public DockLayoutProfileStore Profiles { get; } = new();

    /// <summary>
    /// Saves the current layout as a named profile.
    /// </summary>
    public void SaveProfile(string name)
    {
        if (_host.Layout is not null)
            Profiles.SaveProfile(name, _host.Layout);
    }

    /// <summary>
    /// Loads a named profile and applies it as the current layout.
    /// </summary>
    public bool LoadProfile(string name)
    {
        var layout = Profiles.LoadProfile(name);
        if (layout is null) return false;
        _host.Layout = layout;
        return true;
    }

    /// <summary>
    /// Saves the current layout to a JSON string.
    /// </summary>
    public string? SaveLayout() =>
        _host.Layout is not null ? DockLayoutSerializer.Serialize(_host.Layout) : null;

    /// <summary>
    /// Restores a layout from a JSON string.
    /// </summary>
    public void LoadLayout(string json)
    {
        var layout = DockLayoutSerializer.Deserialize(json);
        _host.Layout = layout;
    }

    /// <summary>
    /// Docks an item to the specified direction relative to the main document host.
    /// </summary>
    public void Dock(DockItem item, DockDirection direction)
    {
        if (Engine is null || Layout is null) return;
        Engine.Dock(item, Layout.MainDocumentHost, direction);
        _host.RebuildVisualTree();
    }

    /// <summary>
    /// Floats an item into a floating window.
    /// </summary>
    public void Float(DockItem item)
    {
        Engine?.Float(item);
        _host.RebuildVisualTree();
    }

    /// <summary>
    /// Sends an item to auto-hide on its last docked side.
    /// </summary>
    public void AutoHide(DockItem item)
    {
        Engine?.AutoHide(item);
        _host.RebuildVisualTree();
    }

    /// <summary>
    /// Hides an item (keeps it in memory for re-activation via <see cref="Show"/>).
    /// </summary>
    public void Hide(DockItem item)
    {
        Engine?.Hide(item);
        _host.RebuildVisualTree();
    }

    /// <summary>
    /// Shows a previously hidden item, docking it to the specified target and direction.
    /// </summary>
    public void Show(DockItem item, DockGroupNode? target = null, DockDirection direction = DockDirection.Center)
    {
        Engine?.Show(item, target, direction);
        _host.RebuildVisualTree();
    }

    /// <summary>
    /// Docks an item as a tabbed document in the main document host.
    /// </summary>
    public void DockAsDocument(DockItem item)
    {
        Engine?.DockAsDocument(item);
        _host.RebuildVisualTree();
    }

    /// <summary>
    /// Closes an item (respects <see cref="IDockHost.BeforeCloseCallback"/>).
    /// </summary>
    public void Close(DockItem item)
    {
        if (_host.BeforeCloseCallback is not null && !_host.BeforeCloseCallback(item))
            return;
        Engine?.Close(item);
        _host.RebuildVisualTree();
    }

    /// <summary>
    /// Executes an undoable layout operation via the command stack.
    /// </summary>
    public void ExecuteUndoable(string description, Action action)
    {
        var cmd = new LayoutSnapshotCommand(
            description,
            () => _host.Layout,
            layout => _host.Layout = layout,
            action);
        _commandStack.Execute(cmd);
        _host.RebuildVisualTree();
    }

    public bool CanUndo => _commandStack.CanUndo;
    public bool CanRedo => _commandStack.CanRedo;

    public void Undo()
    {
        _commandStack.Undo();
        _host.RebuildVisualTree();
    }

    public void Redo()
    {
        _commandStack.Redo();
        _host.RebuildVisualTree();
    }
}
