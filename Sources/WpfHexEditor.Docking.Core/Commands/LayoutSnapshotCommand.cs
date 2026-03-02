//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6, Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.Docking.Core.Serialization;

namespace WpfHexEditor.Docking.Core.Commands;

/// <summary>
/// Generic undoable command that captures a layout snapshot before executing an action.
/// Undo restores the layout to the snapshot. Redo re-executes the action on the current layout.
/// </summary>
public class LayoutSnapshotCommand : IDockCommand
{
    private readonly Action _execute;
    private readonly Func<DockLayoutRoot?> _getLayout;
    private readonly Action<DockLayoutRoot> _setLayout;
    private string? _beforeSnapshot;
    private string? _afterSnapshot;

    public string Description { get; }

    public LayoutSnapshotCommand(
        string description,
        Func<DockLayoutRoot?> getLayout,
        Action<DockLayoutRoot> setLayout,
        Action execute)
    {
        Description = description;
        _getLayout = getLayout;
        _setLayout = setLayout;
        _execute = execute;
    }

    public void Execute()
    {
        var layout = _getLayout();
        if (layout is not null)
            _beforeSnapshot = DockLayoutSerializer.Serialize(layout);

        _execute();

        var afterLayout = _getLayout();
        if (afterLayout is not null)
            _afterSnapshot = DockLayoutSerializer.Serialize(afterLayout);
    }

    public void Undo()
    {
        if (_beforeSnapshot is null) return;
        var restored = DockLayoutSerializer.Deserialize(_beforeSnapshot);
        _setLayout(restored);
    }
}
