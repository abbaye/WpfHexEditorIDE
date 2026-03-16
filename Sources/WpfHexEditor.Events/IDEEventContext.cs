// ==========================================================
// Project: WpfHexEditor.Events
// File: IDEEventContext.cs
// Created: 2026-03-15
// Description:
//     Contextual metadata injected into context-aware event handler signatures.
//     Carries publisher identity and sandbox origin flag for security-aware handlers.
// ==========================================================

namespace WpfHexEditor.Events;

/// <summary>
/// Contextual metadata provided to context-aware event handlers subscribed via
/// <c>Subscribe&lt;TEvent&gt;(Action&lt;IDEEventContext, TEvent&gt;)</c>.
/// </summary>
public sealed class IDEEventContext
{
    /// <summary>
    /// Plugin ID of the component that published the event,
    /// or empty string when published by IDE host code.
    /// </summary>
    public string PublisherPluginId { get; init; } = string.Empty;

    /// <summary>True when the event was forwarded from a sandboxed plugin process via IPC.</summary>
    public bool IsFromSandbox { get; init; }

    /// <summary>Cancellation token for async handlers to respect cooperative cancellation.</summary>
    public CancellationToken CancellationToken { get; init; }

    /// <summary>Pre-built context representing an IDE host (non-plugin) publisher.</summary>
    public static readonly IDEEventContext HostContext = new();
}
