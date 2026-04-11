// ==========================================================
// Project: WpfHexEditor.Editor.Core
// File: Documents/IDocumentManager.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-08
// Description:
//     Contract for the document lifecycle manager.
//     Owns the registry of all open DocumentModel instances,
//     tracks the active document, and provides dirty-check
//     enumeration for save-on-close workflows.
//
// Architecture Notes:
//     Pattern: Service / Registry
//     The implementation (DocumentManager) is owned by MainWindow as a
//     private field — no DI container required. MainWindow delegates
//     document state into this service and subscribes to its events
//     instead of managing IDocumentEditor events per-tab-switch.
// ==========================================================

namespace WpfHexEditor.Editor.Core.Documents;

/// <summary>
/// Manages the lifecycle of all open document tabs, providing a central
/// registry of <see cref="DocumentModel"/> instances and active-document tracking.
/// </summary>
public interface IDocumentManager
{
    // -- State -----------------------------------------------------------------

    /// <summary>All currently open document tabs in registration order.</summary>
    IReadOnlyList<DocumentModel> OpenDocuments { get; }

    /// <summary>The currently focused document, or <c>null</c> when no document is active.</summary>
    DocumentModel? ActiveDocument { get; }

    // -- Lifecycle -------------------------------------------------------------

    /// <summary>
    /// Registers a new document tab. Creates a <see cref="DocumentModel"/> for the
    /// given <paramref name="contentId"/> and adds it to <see cref="OpenDocuments"/>.
    /// No-op if the contentId is already registered (returns the existing model).
    /// </summary>
    DocumentModel Register(string contentId, string? filePath,
                           string? editorId, string? projectItemId);

    /// <summary>
    /// Attaches a live <see cref="IDocumentEditor"/> to the registered model so that
    /// live state (Title, IsDirty, …) is kept in sync.
    /// Must be called after <see cref="Register"/> and before the tab becomes active.
    /// </summary>
    void AttachEditor(string contentId, IDocumentEditor editor);

    /// <summary>
    /// Unregisters the document tab. Detaches any attached editor and removes the model
    /// from <see cref="OpenDocuments"/>. Fires <see cref="DocumentUnregistered"/>.
    /// </summary>
    void Unregister(string contentId);

    /// <summary>
    /// Marks the document identified by <paramref name="contentId"/> as the active tab.
    /// Fires <see cref="ActiveDocumentChanged"/>.
    /// </summary>
    void SetActive(string contentId);

    // -- Buffer access ---------------------------------------------------------

    /// <summary>
    /// Returns the shared <see cref="IDocumentBuffer"/> for the registered document, or
    /// <c>null</c> if the editor does not implement <see cref="IBufferAwareEditor"/>.
    /// </summary>
    IDocumentBuffer? GetBuffer(string contentId);

    /// <summary>
    /// Returns the shared <see cref="IDocumentBuffer"/> for the given file path (any open tab),
    /// or <c>null</c> if no buffer exists for that path.
    /// </summary>
    IDocumentBuffer? GetBufferForFile(string filePath);

    /// <summary>
    /// Returns the <see cref="DocumentModel"/> whose buffer is the given instance,
    /// or <c>null</c> when no document owns that buffer.
    /// </summary>
    DocumentModel? FindDocumentByBuffer(IDocumentBuffer buffer);

    // -- Dirty check -----------------------------------------------------------

    /// <summary>Returns all open documents that have unsaved changes.</summary>
    IReadOnlyList<DocumentModel> GetDirty();

    // -- Events ----------------------------------------------------------------

    /// <summary>Raised after a new document is registered.</summary>
    event EventHandler<DocumentModel> DocumentRegistered;

    /// <summary>Raised after a document is unregistered (tab closed).</summary>
    event EventHandler<DocumentModel> DocumentUnregistered;

    /// <summary>Raised when the active document changes. Argument is <c>null</c> when no document is active.</summary>
    event EventHandler<DocumentModel?> ActiveDocumentChanged;

    /// <summary>Raised when a document's <see cref="DocumentModel.IsDirty"/> value changes.</summary>
    event EventHandler<DocumentModel> DocumentDirtyChanged;

    /// <summary>Raised when a document's <see cref="DocumentModel.Title"/> value changes.</summary>
    event EventHandler<DocumentModel> DocumentTitleChanged;

    /// <summary>Raised after an editor is attached to a registered document via <see cref="AttachEditor"/>.</summary>
    event EventHandler<DocumentModel> EditorAttached;
}
