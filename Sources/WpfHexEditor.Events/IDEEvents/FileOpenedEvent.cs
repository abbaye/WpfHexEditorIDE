// ==========================================================
// Project: WpfHexEditor.Events
// File: IDEEvents/FileOpenedEvent.cs
// Created: 2026-03-15
// Description:
//     Published by HexEditorService (via MainWindow) whenever a file is opened.
//     Triggers lazy-loaded plugins whose activation.fileExtensions match.
// ==========================================================

namespace WpfHexEditor.Events.IDEEvents;

/// <summary>Published when a file is opened in the hex editor.</summary>
public sealed record FileOpenedEvent : IDEEventBase
{
    public string FilePath { get; init; } = string.Empty;
    public string FileExtension { get; init; } = string.Empty;
    public long FileSize { get; init; }
}
