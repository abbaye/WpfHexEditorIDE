// ==========================================================
// Project: WpfHexEditor.Core.LSP
// File: Integration/EventBusIntegration.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Bridges LSP engine events (FileParsed, DiagnosticsUpdated, SymbolTableUpdated)
//     to the IDE-wide IIDEEventBus so other panels and plugins can react
//     to document analysis state changes.
//
// Architecture Notes:
//     Pattern: Adapter / Bridge
//     - Subscribes to SymbolTableManager.SymbolTableUpdated and
//       DiagnosticsEngine.DiagnosticsUpdated.
//     - TrackParser / UntrackParser allow the LSP pipeline to forward
//       IncrementalParser.ParseCompleted events to LspDocumentParsedEvent.
//     - Publishes IDE events using records defined in WpfHexEditor.Core.Events.
//     - IDisposable: unsubscribes on disposal to prevent memory leaks.
// ==========================================================

using WpfHexEditor.Core.Events;
using WpfHexEditor.Core.LSP.Diagnostics;
using WpfHexEditor.Core.LSP.Parsing;
using WpfHexEditor.Core.LSP.Symbols;

namespace WpfHexEditor.Core.LSP.Integration;

/// <summary>
/// Forwards LSP engine events to the IDE <see cref="IIDEEventBus"/>.
/// </summary>
public sealed class EventBusIntegration : IDisposable
{
    private readonly IIDEEventBus         _eventBus;
    private readonly SymbolTableManager   _symbolTableManager;
    private readonly DiagnosticsEngine    _diagnosticsEngine;

    public EventBusIntegration(
        IIDEEventBus       eventBus,
        SymbolTableManager symbolTableManager,
        DiagnosticsEngine  diagnosticsEngine)
    {
        _eventBus           = eventBus           ?? throw new ArgumentNullException(nameof(eventBus));
        _symbolTableManager = symbolTableManager ?? throw new ArgumentNullException(nameof(symbolTableManager));
        _diagnosticsEngine  = diagnosticsEngine  ?? throw new ArgumentNullException(nameof(diagnosticsEngine));

        _symbolTableManager.SymbolTableUpdated += OnSymbolTableUpdated;
        _diagnosticsEngine.DiagnosticsUpdated  += OnDiagnosticsUpdated;
    }

    public void Dispose()
    {
        _symbolTableManager.SymbolTableUpdated -= OnSymbolTableUpdated;
        _diagnosticsEngine.DiagnosticsUpdated  -= OnDiagnosticsUpdated;
    }

    // -----------------------------------------------------------------------
    // Parser tracking
    // -----------------------------------------------------------------------

    /// <summary>
    /// Subscribes to <paramref name="parser"/>'s <see cref="IncrementalParser.ParseCompleted"/>
    /// event so that a <see cref="LspDocumentParsedEvent"/> is published on the bus after
    /// each full or incremental parse. Call when a document's LSP parser is created.
    /// </summary>
    public void TrackParser(IncrementalParser parser)
    {
        if (parser is not null)
            parser.ParseCompleted += OnParsed;
    }

    /// <summary>
    /// Unsubscribes from <paramref name="parser"/>'s parse events.
    /// Call when the document is closed.
    /// </summary>
    public void UntrackParser(IncrementalParser parser)
    {
        if (parser is not null)
            parser.ParseCompleted -= OnParsed;
    }

    // -----------------------------------------------------------------------

    private void OnSymbolTableUpdated(object? sender, string filePath)
    {
        _eventBus.Publish(new LspSymbolTableUpdatedEvent { FilePath = filePath });
    }

    private void OnDiagnosticsUpdated(object? sender, DiagnosticsResult result)
    {
        _eventBus.Publish(new LspDiagnosticsUpdatedEvent
        {
            FilePath     = result.FilePath,
            ErrorCount   = result.Entries.Count(e => e.Severity == WpfHexEditor.Editor.Core.DiagnosticSeverity.Error),
            WarningCount = result.Entries.Count(e => e.Severity == WpfHexEditor.Editor.Core.DiagnosticSeverity.Warning),
        });
    }

    private void OnParsed(object? sender, ParseCompletedEventArgs e)
    {
        _eventBus.Publish(new LspDocumentParsedEvent
        {
            FilePath   = e.FilePath,
            LanguageId = e.LanguageId,
            LineCount  = e.LineCount,
        });
    }
}
