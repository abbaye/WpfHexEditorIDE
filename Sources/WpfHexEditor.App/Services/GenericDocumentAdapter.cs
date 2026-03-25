// ==========================================================
// Project: WpfHexEditor.App
// File: Services/GenericDocumentAdapter.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     Lightweight IIDEEventBus bridge for non-CodeEditor document editors
//     (MarkdownEditor, TblEditor, ClassDiagramEditor, XamlDesigner, etc.).
//     Subscribes to IDocumentEditor.ModifiedChanged and publishes
//     DocumentSavedEvent when the dirty flag is cleared (i.e. after a save).
//
// Architecture Notes:
//     Pattern: Adapter / Event Bridge
//     - EditorEventAdapter covers CodeEditor tabs with rich payloads.
//     - GenericDocumentAdapter covers every other IDocumentEditor: only
//       the save signal is bridged (no caret, folding, or selection).
//     - Both implement IDisposable and are stored in the same
//       Dictionary<string, IDisposable> _editorEventAdapters in MainWindow.
// ==========================================================

using WpfHexEditor.Editor.Core;
using WpfHexEditor.Core.Events;
using WpfHexEditor.Core.Events.IDEEvents;

namespace WpfHexEditor.App.Services;

/// <summary>
/// Bridges <see cref="IDocumentEditor.ModifiedChanged"/> to the
/// <see cref="IIDEEventBus"/> for non-CodeEditor document tabs.
/// Dispose when the document tab is closed.
/// </summary>
public sealed class GenericDocumentAdapter : IDisposable
{
    private readonly IDocumentEditor _editor;
    private readonly IIDEEventBus    _bus;
    private readonly string          _filePath;
    private          bool            _disposed;

    /// <param name="editor">The document editor to observe.</param>
    /// <param name="bus">Bus to publish events on.</param>
    /// <param name="filePath">Absolute path of the opened file.</param>
    public GenericDocumentAdapter(IDocumentEditor editor, IIDEEventBus bus, string filePath)
    {
        _editor   = editor   ?? throw new ArgumentNullException(nameof(editor));
        _bus      = bus      ?? throw new ArgumentNullException(nameof(bus));
        _filePath = filePath ?? string.Empty;

        _editor.ModifiedChanged += OnModifiedChanged;
    }

    /// <summary>Unsubscribes from all editor events.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _editor.ModifiedChanged -= OnModifiedChanged;
    }

    private void OnModifiedChanged(object? sender, EventArgs e)
    {
        // Dirty flag cleared → the file was just saved.
        if (!_editor.IsDirty)
            _bus.Publish(new DocumentSavedEvent { FilePath = _filePath });
    }
}
