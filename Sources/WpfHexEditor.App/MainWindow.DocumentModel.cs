// ==========================================================
// Project: WpfHexEditor.App
// File: MainWindow.DocumentModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-08
// Description:
//     Partial class that wires the DocumentManager service into
//     MainWindow's tab lifecycle (StoreContent, CloseTab,
//     OnActiveDocumentChanged) and re-fires DocumentManager
//     events as the existing MainWindow handlers
//     (OnEditorTitleChanged, OnEditorModifiedChanged).
//
// Architecture Notes:
//     Pattern: Facade / Mediator
//     - DocumentManager is additive — _contentCache/_displayContent unchanged.
//     - RegisterDocumentFromItem() is called in CreateContentForItem (lazy-load)
//       and at each direct-dock call site for IDocumentEditor-based tabs.
//     - TitleChanged / ModifiedChanged are routed through DocumentManager events
//       so ALL open editors propagate changes, not just the active one.
//     - StatusMessage, OutputMessage, Operation* events remain on the
//       ActiveDocumentEditor setter (host-routing concerns, not doc state).
// ==========================================================

using System.Windows.Input;
using WpfHexEditor.Docking.Core.Nodes;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.Core.Documents;

namespace WpfHexEditor.App;

public partial class MainWindow
{
    // -- DocumentManager --------------------------------------------------

    private readonly DocumentManager _documentManager = new();

    /// <summary>Exposes the document manager for external consumers (e.g. ViewModels).</summary>
    public IDocumentManager DocumentManager => _documentManager;

    /// <summary>
    /// Initialises DocumentManager event subscriptions.
    /// Must be called once during MainWindow startup (OnLoaded).
    /// </summary>
    private void InitDocumentManager()
    {
        // Route title changes from ANY open editor (not just the active one)
        // to the corresponding DockItem.Title — replaces the per-active-editor
        // TitleChanged subscription that was in the ActiveDocumentEditor setter.
        _documentManager.DocumentTitleChanged += OnDocumentManagerTitleChanged;

        // Invalidate command bindings whenever any editor's dirty state changes.
        _documentManager.DocumentDirtyChanged += OnDocumentManagerDirtyChanged;
    }

    // -- Registration helpers ---------------------------------------------

    /// <summary>
    /// Registers a document tab with the DocumentManager after the content
    /// has been stored in both caches. Only registers tabs whose content
    /// implements <see cref="IDocumentEditor"/>; viewer-only tabs are skipped.
    /// </summary>
    private void RegisterDocumentFromItem(DockItem item, System.Windows.UIElement displayElement)
    {
        // Only register document tabs (not tool panels)
        if (!item.ContentId.StartsWith("doc-")) return;

        var editor = UnwrapEditor(displayElement) as IDocumentEditor;
        if (editor is null) return;  // Non-editor viewer (DiffViewer, EntropyViewer…)

        item.Metadata.TryGetValue("FilePath",       out var filePath);
        item.Metadata.TryGetValue("ItemId",         out var itemId);
        item.Metadata.TryGetValue("ActiveEditorId", out var editorId);

        _documentManager.Register(item.ContentId, filePath, editorId, itemId);
        _documentManager.AttachEditor(item.ContentId, editor);
    }

    /// <summary>
    /// Unregisters a document tab from the DocumentManager.
    /// Must be called in CloseTab before the content caches are cleared.
    /// </summary>
    private void UnregisterDocument(string contentId)
        => _documentManager.Unregister(contentId);

    /// <summary>
    /// Marks the given ContentId as the active document in the DocumentManager.
    /// Must be called at the end of OnActiveDocumentChanged.
    /// </summary>
    private void SyncActiveDocument(string contentId)
        => _documentManager.SetActive(contentId);

    // -- DocumentManager event handlers -----------------------------------

    /// <summary>
    /// Propagates title changes from DocumentManager to the DockItem.Title.
    /// This replaces the per-editor TitleChanged subscription that lived in
    /// the ActiveDocumentEditor setter, extending coverage to all open editors.
    /// </summary>
    private void OnDocumentManagerTitleChanged(object? sender, DocumentModel model)
    {
        var dockItem = _layout?.FindItemByContentId(model.ContentId);
        if (dockItem is not null) dockItem.Title = model.Title;
    }

    /// <summary>
    /// Invalidates WPF command bindings whenever any editor's dirty state changes.
    /// This replaces OnEditorModifiedChanged which was bound only to the active editor.
    /// </summary>
    private void OnDocumentManagerDirtyChanged(object? sender, DocumentModel model)
        => CommandManager.InvalidateRequerySuggested();
}
