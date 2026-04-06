//////////////////////////////////////////////
// Project      : WpfHexEditor.SDK
// File         : ITabGroupService.cs
// Description  : SDK service for tab group layout management.
//                Plugins receive this via IIDEHostContext.TabGroups.
// Architecture : Interface only — no WPF or docking dependencies.
//                Implemented by TabGroupService in WpfHexEditor.App.
//////////////////////////////////////////////

namespace WpfHexEditor.SDK.Contracts.Services;

/// <summary>
/// Provides plugin access to tab group operations (split, move, close, focus).
/// </summary>
public interface ITabGroupService
{
    /// <summary>Total number of currently active document tab groups (minimum 1).</summary>
    int GroupCount { get; }

    /// <summary>Zero-based index of the document tab group that currently has focus.</summary>
    int ActiveGroupIndex { get; }

    /// <summary>Opens a new vertical (side-by-side) tab group with the active document.</summary>
    void SplitVertical();

    /// <summary>Opens a new horizontal (stacked) tab group with the active document.</summary>
    void SplitHorizontal();

    /// <summary>Moves the active document to the next tab group (wraps around).</summary>
    void MoveActiveToNextGroup();

    /// <summary>Moves the active document to the previous tab group (wraps around).</summary>
    void MoveActiveToPreviousGroup();

    /// <summary>
    /// Closes all non-primary tab groups, moving their documents back to the primary group.
    /// </summary>
    void CloseAllGroups();

    /// <summary>
    /// Gives keyboard focus to the tab group at <paramref name="index"/> (0-based).
    /// No-op when <paramref name="index"/> is out of range.
    /// </summary>
    void FocusGroup(int index);

    /// <summary>Raised whenever the number of active tab groups changes.</summary>
    event EventHandler GroupCountChanged;
}
