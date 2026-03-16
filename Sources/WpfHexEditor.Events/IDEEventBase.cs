// ==========================================================
// Project: WpfHexEditor.Events
// File: IDEEventBase.cs
// Created: 2026-03-15
// Description:
//     Abstract base record for all IDE-level events published on IIDEEventBus.
//     Provides common metadata: identity, source, timestamp, and correlation.
//
// Architecture Notes:
//     All well-known IDE events inherit this record.
//     Using record (not class) for value semantics, deconstruct, and with-expressions.
//     abstract prevents direct instantiation — always use a concrete event subtype.
// ==========================================================

namespace WpfHexEditor.Events;

/// <summary>
/// Base record for all IDE-level events published on <see cref="IIDEEventBus"/>.
/// Concrete events inherit this and add their payload properties.
/// </summary>
public abstract record IDEEventBase
{
    /// <summary>Unique identifier for this specific event instance (GUID, short form).</summary>
    public string EventId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Label of the component that published this event (e.g. "MainWindow", "HexEditorService",
    /// or a plugin ID like "WpfHexEditor.Plugins.AssemblyExplorer").
    /// </summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>UTC timestamp when this event was created.</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Optional correlation ID used to link a chain of related events
    /// (e.g. FileOpened → BinaryAnalysisStarted → BinaryAnalysisCompleted).
    /// Empty string means no correlation.
    /// </summary>
    public string CorrelationId { get; init; } = string.Empty;
}
