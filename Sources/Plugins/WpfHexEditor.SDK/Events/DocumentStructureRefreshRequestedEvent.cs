// ==========================================================
// Project: WpfHexEditor.SDK
// File: Events/DocumentStructureRefreshRequestedEvent.cs
// Created: 2026-04-05
// Description:
//     Event published on IPluginEventBus to request a refresh of the
//     Document Structure panel for a specific file or the active document.
//
// Architecture Notes:
//     Published by editors or other plugins when the document content changes
//     in a way that may affect the structure (e.g. after refactoring).
// ==========================================================

namespace WpfHexEditor.SDK.Events;

/// <summary>
/// Requests a refresh of the Document Structure panel.
/// Publish on <c>IPluginEventBus</c> to trigger an outline re-parse.
/// </summary>
public sealed class DocumentStructureRefreshRequestedEvent
{
    /// <summary>File path to refresh, or null for the currently active document.</summary>
    public string? FilePath { get; init; }
}
