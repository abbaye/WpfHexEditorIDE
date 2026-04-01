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
//     - When the editor is a CodeEditor instance, the adapter also subscribes
//       to CaretMoved and FoldingEngine.RegionsChanged for richer payloads.
//     - Optional IDiagnosticSource subscription is established via an is-check
//       so the adapter works with any IDocumentEditor, not just CodeEditor.
// ==========================================================

using CodeEditorControl = WpfHexEditor.Editor.CodeEditor.Controls.CodeEditor;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Core.Events;
using WpfHexEditor.Core.Events.IDEEvents;

namespace WpfHexEditor.Editor.CodeEditor.Services;

/// <summary>
/// Bridges an <see cref="IDocumentEditor"/> to the <see cref="IIDEEventBus"/>,
/// publishing IDE-level events whenever the editor fires its internal events.
/// Dispose when the document tab is closed.
/// </summary>
public sealed class EditorEventAdapter : IDisposable
{
    private readonly IDocumentEditor    _editor;
    private readonly IIDEEventBus       _bus;
    private readonly string             _filePath;
    private readonly IDiagnosticSource? _diagnosticSource;
    private readonly CodeEditorControl? _codeEditor;
    private          bool               _opened;
    private          bool               _disposed;

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
        _codeEditor       = editor as CodeEditorControl;

        // Subscribe to IDocumentEditor events.
        _editor.TitleChanged     += OnTitleChanged;
        _editor.ModifiedChanged  += OnModifiedChanged;
        _editor.SelectionChanged += OnSelectionChanged;

        if (_diagnosticSource is not null)
            _diagnosticSource.DiagnosticsChanged += OnDiagnosticsChanged;

        // Subscribe to CodeEditor-specific events when the editor is a CodeEditor.
        if (_codeEditor is not null)
        {
            _codeEditor.CaretMoved += OnCaretMoved;

            if (_codeEditor.FoldingEngine is not null)
                _codeEditor.FoldingEngine.RegionsChanged += OnFoldingRegionsChanged;
        }

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

        if (_codeEditor is not null)
        {
            _codeEditor.CaretMoved -= OnCaretMoved;

            if (_codeEditor.FoldingEngine is not null)
                _codeEditor.FoldingEngine.RegionsChanged -= OnFoldingRegionsChanged;
        }

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
        var selectedText = _codeEditor?.SelectedText ?? string.Empty;
        _bus.Publish(new CodeEditorTextSelectionChangedEvent
        {
            FilePath        = _filePath,
            SelectedText    = selectedText,
            SelectionStart  = 0,                      // linear offset not exposed; length is sufficient for consumers
            SelectionLength = selectedText.Length,
        });
    }

    private void OnCaretMoved(object? sender, EventArgs e)
    {
        _bus.Publish(new CodeEditorCursorMovedEvent
        {
            FilePath = _filePath,
            Line     = _codeEditor!.CursorLine   + 1,  // 0-based → 1-based
            Column   = _codeEditor!.CursorColumn + 1,
        });
    }

    private void OnFoldingRegionsChanged(object? sender, EventArgs e)
    {
        var collapsed = _codeEditor!.FoldingEngine!.Regions.Count(r => r.IsCollapsed);
        _bus.Publish(new CodeEditorFoldingChangedEvent
        {
            FilePath      = _filePath,
            CollapsedCount = collapsed,
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
            LanguageId = string.Empty,  // language detection wired via LanguageDefinitionManager (Phase 2)
        });
    }
}
