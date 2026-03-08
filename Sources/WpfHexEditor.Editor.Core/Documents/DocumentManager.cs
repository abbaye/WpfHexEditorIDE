// ==========================================================
// Project: WpfHexEditor.Editor.Core
// File: Documents/DocumentManager.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-08
// Description:
//     Concrete implementation of IDocumentManager.
//     Owns all open DocumentModel instances, maintains the active
//     document reference, and re-fires DocumentModel property changes
//     as typed manager-level events (DocumentTitleChanged, DocumentDirtyChanged).
//
// Architecture Notes:
//     Pattern: Service / Registry + Observer
//     - All calls are expected on the UI thread (MainWindow drives this service).
//     - Re-fires DocumentModel.PropertyChanged as typed service events so
//       MainWindow can subscribe once to the manager instead of per-model.
//     - GetDirty() is O(n) over registered models — n is the number of open tabs,
//       typically < 30, so no optimisation is required.
// ==========================================================

using System.ComponentModel;

namespace WpfHexEditor.Editor.Core.Documents;

/// <summary>
/// Manages the full lifecycle of open document tabs.
/// </summary>
public sealed class DocumentManager : IDocumentManager
{
    private readonly List<DocumentModel> _documents = new();
    private DocumentModel? _activeDocument;

    // -- IDocumentManager : State -----------------------------------------

    public IReadOnlyList<DocumentModel> OpenDocuments => _documents;

    public DocumentModel? ActiveDocument => _activeDocument;

    // -- IDocumentManager : Lifecycle -------------------------------------

    public DocumentModel Register(string contentId, string? filePath,
                                  string? editorId, string? projectItemId)
    {
        // Return existing model if already registered (idempotent)
        var existing = Find(contentId);
        if (existing is not null) return existing;

        var model = new DocumentModel(contentId, filePath, projectItemId, editorId);
        model.PropertyChanged += OnModelPropertyChanged;
        _documents.Add(model);

        DocumentRegistered?.Invoke(this, model);
        return model;
    }

    public void AttachEditor(string contentId, IDocumentEditor editor)
    {
        var model = Find(contentId);
        if (model is null) return;
        model.AttachEditor(editor);
    }

    public void Unregister(string contentId)
    {
        var model = Find(contentId);
        if (model is null) return;

        model.DetachEditor();
        model.PropertyChanged -= OnModelPropertyChanged;
        _documents.Remove(model);

        if (ReferenceEquals(_activeDocument, model))
        {
            _activeDocument = null;
            ActiveDocumentChanged?.Invoke(this, null);
        }

        DocumentUnregistered?.Invoke(this, model);
    }

    public void SetActive(string contentId)
    {
        var model = Find(contentId);

        if (ReferenceEquals(_activeDocument, model)) return;

        if (_activeDocument is not null)
            _activeDocument.IsActive = false;

        _activeDocument = model;

        if (_activeDocument is not null)
            _activeDocument.IsActive = true;

        ActiveDocumentChanged?.Invoke(this, _activeDocument);
    }

    // -- IDocumentManager : Dirty check -----------------------------------

    public IReadOnlyList<DocumentModel> GetDirty()
        => _documents.Where(m => m.IsDirty).ToList();

    // -- IDocumentManager : Events ----------------------------------------

    public event EventHandler<DocumentModel>?  DocumentRegistered;
    public event EventHandler<DocumentModel>?  DocumentUnregistered;
    public event EventHandler<DocumentModel?>? ActiveDocumentChanged;
    public event EventHandler<DocumentModel>?  DocumentDirtyChanged;
    public event EventHandler<DocumentModel>?  DocumentTitleChanged;

    // -- Internal ----------------------------------------------------------

    private DocumentModel? Find(string contentId)
        => _documents.FirstOrDefault(m => m.ContentId == contentId);

    /// <summary>
    /// Translates individual DocumentModel PropertyChanged notifications
    /// into typed manager events consumed by MainWindow.
    /// </summary>
    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not DocumentModel model) return;

        switch (e.PropertyName)
        {
            case nameof(DocumentModel.IsDirty):
                DocumentDirtyChanged?.Invoke(this, model);
                break;

            case nameof(DocumentModel.Title):
                DocumentTitleChanged?.Invoke(this, model);
                break;
        }
    }
}
