// ==========================================================
// Project: WpfHexEditor.Core.Events
// File: IDEEvents/GitEvents.cs
// Description:
//     IDE-wide git/VCS event payloads published on IIDEEventBus.
//     Subscribers: MainWindow (status bar), CodeEditor (blame gutter).
// ==========================================================

namespace WpfHexEditor.Core.Events.IDEEvents;

/// <summary>
/// Published by GitPlugin whenever repository status refreshes
/// (branch change, dirty flag toggle, changed-file count update).
/// </summary>
public sealed record GitStatusChangedEvent(
    string? Branch,
    bool    IsDirty,
    int     ChangedFileCount);

/// <summary>
/// Published by GitPlugin after blame data is fetched for an open file.
/// Consumers (e.g. BlameGutterControl) listen to update rendering.
/// </summary>
public sealed record GitBlameLoadedEvent(
    string FilePath,
    int    EntryCount);

/// <summary>
/// Published when a long-running git operation begins (push, pull, fetch).
/// Consumers show progress indicators.
/// </summary>
public sealed record GitOperationStartedEvent(string OperationName);

/// <summary>
/// Published when a git operation completes, successfully or not.
/// </summary>
public sealed record GitOperationCompletedEvent(
    string  OperationName,
    bool    Success,
    string? ErrorMessage);

/// <summary>
/// Published after push/pull/fetch when ahead/behind counts change.
/// </summary>
public sealed record GitAheadBehindChangedEvent(int Ahead, int Behind);

/// <summary>
/// Published when the user clicks a blame entry and wants to navigate
/// to that commit in the history panel.
/// </summary>
public sealed record GitBlameNavigateEvent(string FilePath, string CommitHash);

/// <summary>
/// Published by MainWindow when the user clicks the branch name in the status bar.
/// The Git plugin subscribes and shows the BranchPickerPopup.
/// </summary>
public sealed record GitBranchClickRequestedEvent(object PlacementTarget);

/// <summary>
/// Published by GitPlugin when the user activates "Toggle Blame Gutter".
/// MainWindow.Git.cs subscribes and toggles ShowBlameGutter on the active CodeEditor.
/// </summary>
public sealed record GitBlameToggleRequestedEvent();
