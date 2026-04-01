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
