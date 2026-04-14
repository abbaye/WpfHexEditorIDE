// ==========================================================
// Project: WpfHexEditor.Editor.Core
// File: Documents/DocumentModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-08
// Description:
//     Observable model representing one open document tab.
//     Holds identity (ContentId, FilePath, EditorId) and live
//     state (Title, IsDirty, CanUndo, CanRedo, IsBusy) mirrored
//     from the attached IDocumentEditor via event subscription.
//
// Architecture Notes:
//     Pattern: Observer / Proxy
//     - AttachEditor() wires IDocumentEditor events → model properties
//     - DetachEditor() cleanly unsubscribes to prevent memory leaks
//     - All mutable properties raise PropertyChanged (MVVM-friendly)
//     - Identity properties are immutable after construction
// ==========================================================

using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfHexEditor.Editor.Core.Undo;

namespace WpfHexEditor.Editor.Core.Documents;

/// <summary>
/// Observable model for one open document tab.
/// Owns the live state mirror (Title, IsDirty, CanUndo, CanRedo, IsBusy)
/// sourced from the associated <see cref="IDocumentEditor"/> via event wiring.
/// </summary>
public sealed class DocumentModel : INotifyPropertyChanged
{
    // -- Identity (immutable after construction) ---------------------------

    /// <summary>The DockItem.ContentId that uniquely identifies this tab.</summary>
    public string ContentId { get; }

    /// <summary>Absolute file path, or <c>null</c> for untitled documents.</summary>
    public string? FilePath { get; }

    /// <summary>Project item GUID, or <c>null</c> for standalone files.</summary>
    public string? ProjectItemId { get; }

    /// <summary>Resolved editor factory ID (e.g. "code-editor"), or <c>null</c> when unknown.</summary>
    public string? EditorId { get; }

    // -- Live state (observable) -------------------------------------------

    private string _title     = string.Empty;
    private bool   _isDirty;
    private bool   _isReadOnly;
    private bool   _canUndo;
    private bool   _canRedo;
    private bool   _isActive;
    private bool   _isBusy;

    /// <summary>Tab title, including the dirty marker ("file.bin *").</summary>
    public string Title
    {
        get => _title;
        private set { if (_title == value) return; _title = value; OnPropertyChanged(); }
    }

    /// <summary><c>true</c> when the document has unsaved changes.</summary>
    public bool IsDirty
    {
        get => _isDirty;
        private set { if (_isDirty == value) return; _isDirty = value; OnPropertyChanged(); }
    }

    /// <summary><c>true</c> when the document is in read-only mode.</summary>
    public bool IsReadOnly
    {
        get => _isReadOnly;
        private set { if (_isReadOnly == value) return; _isReadOnly = value; OnPropertyChanged(); }
    }

    /// <summary><c>true</c> when an undo operation is available.</summary>
    public bool CanUndo
    {
        get => _canUndo;
        private set { if (_canUndo == value) return; _canUndo = value; OnPropertyChanged(); }
    }

    /// <summary><c>true</c> when a redo operation is available.</summary>
    public bool CanRedo
    {
        get => _canRedo;
        private set { if (_canRedo == value) return; _canRedo = value; OnPropertyChanged(); }
    }

    /// <summary><c>true</c> when this is the currently focused document tab.</summary>
    public bool IsActive
    {
        get => _isActive;
        internal set { if (_isActive == value) return; _isActive = value; OnPropertyChanged(); }
    }

    /// <summary><c>true</c> while a long-running operation is running on this document.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set { if (_isBusy == value) return; _isBusy = value; OnPropertyChanged(); }
    }

    // -- Editor reference --------------------------------------------------

    private IDocumentEditor? _associatedEditor;

    /// <summary>The live editor instance, or <c>null</c> before the tab is first opened (lazy).</summary>
    public IDocumentEditor? AssociatedEditor => _associatedEditor;

    // -- Shared buffer (set by DocumentManager when editor is IBufferAwareEditor) --

    /// <summary>
    /// Shared content buffer, non-null only when the editor implements
    /// <see cref="IBufferAwareEditor"/> and a <see cref="FilePath"/> is known.
    /// </summary>
    public IDocumentBuffer? Buffer { get; internal set; }

    // -- Shared undo engine (set by DocumentManager when editor is IUndoAwareEditor) --

    /// <summary>
    /// Shared undo/redo engine, non-null only when the editor implements
    /// <see cref="IUndoAwareEditor"/> and a <see cref="FilePath"/> is known.
    /// Exposed so the shell (menu/toolbar) can derive undo/redo headers from the
    /// shared engine rather than from the per-editor <see cref="IDocumentEditor"/> state.
    /// </summary>
    public UndoEngine? SharedUndoEngine { get; internal set; }

    // -- Construction ------------------------------------------------------

    public DocumentModel(string contentId, string? filePath, string? projectItemId, string? editorId)
    {
        ContentId     = contentId     ?? throw new ArgumentNullException(nameof(contentId));
        FilePath      = filePath;
        ProjectItemId = projectItemId;
        EditorId      = editorId;

        // Derive an initial title from the file path so the model is never empty
        _title = filePath is not null
            ? System.IO.Path.GetFileName(filePath)
            : contentId;
    }

    // -- Editor wiring -----------------------------------------------------

    /// <summary>
    /// Subscribes to <paramref name="editor"/> events so that live state
    /// (Title, IsDirty, CanUndo, CanRedo, IsBusy) is kept in sync.
    /// Idempotent: detaches any previously attached editor first.
    /// </summary>
    internal void AttachEditor(IDocumentEditor editor)
    {
        if (_associatedEditor != null)
            DetachEditor();

        _associatedEditor = editor;

        editor.TitleChanged       += OnEditorTitleChanged;
        editor.ModifiedChanged    += OnEditorModifiedChanged;
        editor.CanUndoChanged     += OnEditorCanUndoChanged;
        editor.CanRedoChanged     += OnEditorCanRedoChanged;
        editor.OperationStarted   += OnEditorOperationStarted;
        editor.OperationCompleted += OnEditorOperationCompleted;

        // Eagerly sync all state from the current editor values
        SyncFromEditor(editor);
    }

    /// <summary>
    /// Unsubscribes from the current editor's events. Safe to call even when
    /// no editor is attached.
    /// </summary>
    internal void DetachEditor()
    {
        if (_associatedEditor is null) return;

        _associatedEditor.TitleChanged       -= OnEditorTitleChanged;
        _associatedEditor.ModifiedChanged    -= OnEditorModifiedChanged;
        _associatedEditor.CanUndoChanged     -= OnEditorCanUndoChanged;
        _associatedEditor.CanRedoChanged     -= OnEditorCanRedoChanged;
        _associatedEditor.OperationStarted   -= OnEditorOperationStarted;
        _associatedEditor.OperationCompleted -= OnEditorOperationCompleted;

        _associatedEditor = null;
    }

    // -- Event handlers (IDocumentEditor → DocumentModel) -----------------

    private void OnEditorTitleChanged(object? sender, string title)
        => Title = title;

    private void OnEditorModifiedChanged(object? sender, EventArgs e)
    {
        if (_associatedEditor is null) return;
        IsDirty    = _associatedEditor.IsDirty;
        IsReadOnly = _associatedEditor.IsReadOnly;
    }

    private void OnEditorCanUndoChanged(object? sender, EventArgs e)
    {
        if (_associatedEditor is null) return;
        CanUndo = _associatedEditor.CanUndo;
    }

    private void OnEditorCanRedoChanged(object? sender, EventArgs e)
    {
        if (_associatedEditor is null) return;
        CanRedo = _associatedEditor.CanRedo;
    }

    private void OnEditorOperationStarted(object? sender, DocumentOperationEventArgs e)
        => IsBusy = true;

    private void OnEditorOperationCompleted(object? sender, DocumentOperationCompletedEventArgs e)
        => IsBusy = false;

    // -- Helpers -----------------------------------------------------------

    private void SyncFromEditor(IDocumentEditor editor)
    {
        Title      = editor.Title;
        IsDirty    = editor.IsDirty;
        IsReadOnly = editor.IsReadOnly;
        CanUndo    = editor.CanUndo;
        CanRedo    = editor.CanRedo;
        IsBusy     = editor.IsBusy;
    }

    // -- INotifyPropertyChanged --------------------------------------------

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
