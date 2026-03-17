// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Services/EditorEventAdapter.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Bridges IDocumentEditor lifecycle and state events to the IDE-level
//     IIDEEventBus so plugins and panels observe code editor activity without
//     coupling to the CodeEditor control type directly.
//
//     The adapter subscribes to IDocumentEditor events on construction and
//     publishes the corresponding IDE event records on the bus. It implements
//     IDisposable to allow clean unsubscription when a tab is closed.
//
// Architecture Notes:
//     Pattern: Adapter / Event Bridge
//     - Caller (MainWindow or DocumentHostService) creates one adapter per
//       open code-editor tab, passing the file path it already knows.
//     - Selection state (text, caret position) is not yet fully accessible
//       from IDocumentEditor. SelectionChanged publishes a lightweight event;
//       richer payload will be added when CodeEditor exposes caret state.
//     - Optional IDiagnosticSource subscription is established via an is-check
//       so the adapter works with any IDocumentEditor, not just CodeEditor.
// ==========================================================

using WpfHexEditor.Editor.Core;
using WpfHexEditor.Events;
using WpfHexEditor.Events.IDEEvents;

namespace WpfHexEditor.Editor.CodeEditor.Services;

/// <summary>
/// Bridges an <see cref="IDocumentEditor"/> to the <see cref="IIDEEventBus"/>,
/// publishing IDE-level events whenever the editor fires its internal events.
/// Dispose when the document tab is closed.
/// </summary>
public sealed class EditorEventAdapter : IDisposable
{
    private readonly IDocumentEditor  _editor;
    private readonly IIDEEventBus     _bus;
    private readonly string           _filePath;
    private readonly IDiagnosticSource? _diagnosticSource;
    private          bool             _opened;
    private          bool             _disposed;

    /// <summary>
    /// Creates an adapter for <paramref name="editor"/> and immediately
    /// publishes a <see cref="CodeEditorDocumentOpenedEvent"/> on the bus.
    /// </summary>
    /// <param name="editor">The document editor to observe.</param>
    /// <param name="eventBus">Bus to publish events on.</param>
    /// <param name="filePath">Absolute path of the opened file. Used as event payload.</param>
    public EditorEventAdapter(IDocumentEditor editor, IIDEEventBus eventBus, string filePath)
    {
        _editor   = editor    ?? throw new ArgumentNullException(nameof(editor));
        _bus      = eventBus  ?? throw new ArgumentNullException(nameof(eventBus));
        _filePath = filePath  ?? string.Empty;

        _diagnosticSource = editor as IDiagnosticSource;

        // Subscribe
        _editor.TitleChanged     += OnTitleChanged;
        _editor.ModifiedChanged  += OnModifiedChanged;
        _editor.SelectionChanged += OnSelectionChanged;

        if (_diagnosticSource is not null)
            _diagnosticSource.DiagnosticsChanged += OnDiagnosticsChanged;

        // Publish the open event immediately.
        PublishOpened();
    }

    // -- IDisposable -------------------------------------------------------

    /// <summary>
    /// Unsubscribes all event handlers and publishes
    /// <see cref="CodeEditorDocumentClosedEvent"/> on the bus.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _editor.TitleChanged     -= OnTitleChanged;
        _editor.ModifiedChanged  -= OnModifiedChanged;
        _editor.SelectionChanged -= OnSelectionChanged;

        if (_diagnosticSource is not null)
            _diagnosticSource.DiagnosticsChanged -= OnDiagnosticsChanged;

        _bus.Publish(new CodeEditorDocumentClosedEvent { FilePath = _filePath });
    }

    // -- Handlers ----------------------------------------------------------

    private void OnTitleChanged(object? sender, string title)
    {
        // A re-title (e.g. after SaveAs) means the document was already open.
        // Only re-publish if the document wasn't yet announced.
        if (!_opened) PublishOpened();
    }

    private void OnModifiedChanged(object? sender, EventArgs e)
    {
        // When the dirty flag is cleared the editor was just saved.
        if (!_editor.IsDirty)
            _bus.Publish(new DocumentSavedEvent { FilePath = _filePath });
    }

    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        // Publish a lightweight selection event. The rich text payload (selected
        // characters, caret line/column) requires CodeEditor to expose internal
        // selection state — tracked for a future enhancement.
        _bus.Publish(new CodeEditorTextSelectionChangedEvent
        {
            FilePath        = _filePath,
            SelectedText    = string.Empty,  // populated when CodeEditor exposes selection
            SelectionStart  = 0,
            SelectionLength = 0,
        });
    }

    private void OnDiagnosticsChanged(object? sender, EventArgs e)
    {
        var diagnostics = _diagnosticSource!.GetDiagnostics();
        int errors   = 0;
        int warnings = 0;

        foreach (var d in diagnostics)
        {
            if      (d.Severity == DiagnosticSeverity.Error)   errors++;
            else if (d.Severity == DiagnosticSeverity.Warning) warnings++;
        }

        _bus.Publish(new CodeEditorDiagnosticsUpdatedEvent
        {
            FilePath     = _filePath,
            ErrorCount   = errors,
            WarningCount = warnings,
        });
    }

    // -- Helpers -----------------------------------------------------------

    private void PublishOpened()
    {
        _opened = true;
        _bus.Publish(new CodeEditorDocumentOpenedEvent
        {
            FilePath   = _filePath,
            LanguageId = string.Empty,  // language detection will be wired via LanguageDefinitionManager (Phase 2)
        });
    }
}
