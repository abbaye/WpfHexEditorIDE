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
using System.Windows.Threading;

namespace WpfHexEditor.Editor.Core.Documents;

/// <summary>
/// Manages the full lifecycle of open document tabs.
/// </summary>
public sealed class DocumentManager : IDocumentManager
{
    private readonly List<DocumentModel>                _documents = new();
    private readonly Dictionary<string, DocumentBuffer> _buffers
        = new(StringComparer.OrdinalIgnoreCase);   // keyed by file path
    private readonly Dispatcher                         _dispatcher;
    private DocumentModel? _activeDocument;

    /// <summary>
    /// Creates a DocumentManager capturing the current thread's Dispatcher.
    /// Always construct on the WPF UI thread.
    /// </summary>
    public DocumentManager() => _dispatcher = Dispatcher.CurrentDispatcher;

    // Extension-to-languageId map (10 common entries — no external dependency).
    private static readonly Dictionary<string, string> s_langMap
        = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".cs",   "csharp"     },
        { ".vb",   "vbnet"      },
        { ".fs",   "fsharp"     },
        { ".fsx",  "fsharp"     },
        { ".fsi",  "fsharp"     },
        { ".py",   "python"     },
        { ".js",   "javascript" },
        { ".ts",   "typescript" },
        { ".json", "json"       },
        { ".xml",  "xml"        },
        { ".xaml", "xml"        },
        { ".html", "html"       },
        { ".md",   "markdown"   },
    };

    private static string ResolveLanguageId(string? filePath)
    {
        if (filePath is null) return string.Empty;
        var ext = System.IO.Path.GetExtension(filePath);
        return s_langMap.TryGetValue(ext, out var lang) ? lang : string.Empty;
    }

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

        // Wire shared buffer when the editor opts in and a file path is known.
        if (editor is IBufferAwareEditor bufAware && model.FilePath is not null)
        {
            if (!_buffers.TryGetValue(model.FilePath, out var buf))
            {
                var lang = ResolveLanguageId(model.FilePath);
                buf = new DocumentBuffer(model.FilePath, lang, string.Empty, _dispatcher);
                _buffers[model.FilePath] = buf;
            }
            model.Buffer = buf;
            bufAware.AttachBuffer(buf);
        }
    }

    public void Unregister(string contentId)
    {
        var model = Find(contentId);
        if (model is null) return;

        // Detach buffer before DetachEditor so the buffer ref is cleared cleanly.
        if (model.AssociatedEditor is IBufferAwareEditor bufAware)
            bufAware.DetachBuffer();

        // Remove the buffer entry when no remaining tab shares this file path.
        if (model.Buffer is DocumentBuffer buf)
        {
            var stillOpen = _documents.Any(d => !ReferenceEquals(d, model) && d.Buffer == buf);
            if (!stillOpen) _buffers.Remove(model.FilePath!);
            model.Buffer = null;
        }

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

    // -- IDocumentManager : Buffer access ---------------------------------

    public IDocumentBuffer? GetBuffer(string contentId)
        => Find(contentId)?.Buffer;

    public IDocumentBuffer? GetBufferForFile(string filePath)
        => _buffers.TryGetValue(filePath, out var buf) ? buf : null;

    public DocumentModel? FindDocumentByBuffer(IDocumentBuffer buffer)
    {
        foreach (var doc in _documents)
            if (ReferenceEquals(doc.Buffer, buffer)) return doc;
        return null;
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
