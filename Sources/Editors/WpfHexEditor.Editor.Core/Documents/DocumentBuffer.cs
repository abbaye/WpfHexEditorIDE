// ==========================================================
// Project: WpfHexEditor.Editor.Core
// File: Documents/DocumentBuffer.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     Thread-safe, Dispatcher-aware implementation of IDocumentBuffer.
//     Owned exclusively by DocumentManager — internal visibility
//     prevents external instantiation.
// ==========================================================

using System.Windows.Threading;
using WpfHexEditor.Editor.Core.Undo;

namespace WpfHexEditor.Editor.Core.Documents;

/// <summary>
/// Thread-safe shared content buffer for one open file.
/// <see cref="Changed"/> is always raised on the WPF Dispatcher thread.
/// </summary>
internal sealed class DocumentBuffer : IDocumentBuffer
{
    private readonly Dispatcher _dispatcher;
    private readonly object     _lock = new();
    private string              _text;
    private int                 _version;

    // -- IDocumentBuffer : Identity ----------------------------------------

    public string FilePath   { get; }
    public string LanguageId { get; }

    // -- IDocumentBuffer : State -------------------------------------------

    public int    Version { get { lock (_lock) return _version; } }
    public string Text    { get { lock (_lock) return _text;    } }

    // -- IDocumentBuffer : Shared undo engine ------------------------------

    private UndoEngine? _sharedUndoEngine;

    /// <inheritdoc/>
    public UndoEngine? SharedUndoEngine => _sharedUndoEngine;

    /// <summary>
    /// Returns the shared <see cref="UndoEngine"/> for this buffer, creating it
    /// lazily on first call. Only <c>DocumentManager</c> should call this.
    /// </summary>
    internal UndoEngine GetOrCreateSharedUndoEngine()
        => _sharedUndoEngine ??= new UndoEngine();

    // -- IDocumentBuffer : Event -------------------------------------------

    public event EventHandler<DocumentBufferChangedEventArgs>? Changed;

    // -- Construction ------------------------------------------------------

    internal DocumentBuffer(string filePath, string languageId, string initialText, Dispatcher dispatcher)
    {
        FilePath    = filePath    ?? throw new ArgumentNullException(nameof(filePath));
        LanguageId  = languageId  ?? string.Empty;
        _dispatcher = dispatcher  ?? throw new ArgumentNullException(nameof(dispatcher));
        _text       = initialText ?? string.Empty;
        _version    = 1;
    }

    // -- IDocumentBuffer : Mutation ----------------------------------------

    public void SetText(string text, IDocumentEditor? source = null)
    {
        text ??= string.Empty;

        int newVersion;
        lock (_lock)
        {
            _text    = text;
            newVersion = ++_version;
        }

        var args = new DocumentBufferChangedEventArgs
        {
            NewText    = text,
            NewVersion = newVersion,
            Source     = source,
        };

        // Always fire on the Dispatcher thread so WPF bindings and editor updates
        // are safe to call without explicit marshalling on the subscriber side.
        _dispatcher.InvokeAsync(() => Changed?.Invoke(this, args));
    }
}
