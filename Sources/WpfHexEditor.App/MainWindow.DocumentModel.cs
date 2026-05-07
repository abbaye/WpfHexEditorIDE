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

using System.Linq;
using System.Windows.Input;
using WpfHexEditor.Docking.Core.Nodes;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.Core.Documents;
using DocEditorModel = WpfHexEditor.Editor.DocumentEditor.Core.Model.DocumentBlock;

namespace WpfHexEditor.App;

public partial class MainWindow
{
    // -- DocumentManager --------------------------------------------------

    private readonly DocumentManager _documentManager = new();

    // Content IDs of project editors that are currently dirty and in Tracked save mode.
    // Maintained by OnDocumentManagerDirtyChanged; consumed by OnAutoSerializeTick
    // so the timer iterates only O(dirty) entries instead of O(all solution items).
    private readonly HashSet<string> _dirtyTrackedContentIds = [];

    /// <summary>Exposes the document manager for external consumers (e.g. ViewModels).</summary>
    public IDocumentManager DocumentManager => _documentManager;

    /// <summary>
    /// Exposes the document host service (open by path, navigate to line/column).
    /// Available after InitializePluginSystemAsync completes.
    /// </summary>
    public WpfHexEditor.SDK.Contracts.Services.IDocumentHostService? DocumentHost
        => _documentHostService;

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
        PushOpenDocumentsToErrorPanel();
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
    /// Invalidates WPF command bindings whenever any editor's dirty state changes,
    /// and maintains <see cref="_dirtyTrackedContentIds"/> for the auto-serialize timer.
    /// Only project document tabs (content IDs starting with "doc-proj-") are tracked.
    /// </summary>
    private void OnDocumentManagerDirtyChanged(object? sender, DocumentModel model)
    {
        // Keep dirty-set in sync so the timer iterates O(dirty) instead of O(all items).
        if (model.ContentId.StartsWith("doc-proj-", StringComparison.Ordinal))
        {
            if (model.IsDirty)
                _dirtyTrackedContentIds.Add(model.ContentId);
            else
                _dirtyTrackedContentIds.Remove(model.ContentId);
        }

        CommandManager.InvalidateRequerySuggested();
        PushChangedDocumentsToErrorPanel();
    }

    private void PushChangedDocumentsToErrorPanel()
    {
        if (_errorPanel is null) return;
        var paths = _documentManager.GetDirty()
            .Select(d => d.FilePath)
            .Where(p => !string.IsNullOrEmpty(p))
            .Cast<string>()
            .ToList();
        _errorPanel.SetChangedDocuments(paths);
    }

    /// <summary>
    /// Resolves an <see cref="IProjectItem"/> from a "doc-proj-{id}" content ID.
    /// Returns false when no matching item exists in the current solution.
    /// </summary>
    private bool TryGetProjectItemFromContentId(string contentId, out IProjectItem? item)
    {
        item = null;
        if (_solutionManager.CurrentSolution is null) return false;
        foreach (var project in _solutionManager.CurrentSolution.Projects)
        foreach (var pi in project.Items)
        {
            if ($"doc-proj-{pi.Id}" == contentId) { item = pi; return true; }
        }
        return false;
    }

    /// <summary>
    /// Returns true when the editor control participates in .whchg changeset tracking.
    /// HexEditor always participates; CodeEditor and TextEditor require their respective
    /// <c>ChangesetEnabled</c> option to be true.
    /// </summary>
    private static bool IsChangesetEnabledForEditor(
        System.Windows.UIElement ctrl,
        WpfHexEditor.Core.Options.AppSettings settings)
        => ctrl switch
        {
            WpfHexEditor.HexEditor.HexEditor                        => true,
            WpfHexEditor.Editor.CodeEditor.Controls.CodeEditor      => settings.CodeEditorDefaults.ChangesetEnabled,
            WpfHexEditor.Editor.TextEditor.Controls.TextEditor      => settings.TextEditorDefaults.ChangesetEnabled,
            _                                                        => false,
        };

    /// <summary>
    /// True when <paramref name="item"/> is a project document AND its editor type
    /// has changeset tracking enabled. Only items matching both conditions are silently
    /// auto-serialized to a .whchg file; others fall through to the normal Save dialog.
    /// </summary>
    private bool IsTrackedItemWithChangeset(WpfHexEditor.Docking.Core.Nodes.DockItem item)
    {
        if (!IsTrackedProjectItem(item)) return false;
        if (!_contentCache.TryGetValue(item.ContentId, out var ctrl)) return false;
        return IsChangesetEnabledForEditor(ctrl, WpfHexEditor.Core.Options.AppSettingsService.Instance.Current);
    }

    /// <summary>
    /// Handles "Inspect in Hex Editor" from a DocumentEditorHost.
    /// Opens the document file in the connected hex editor and navigates to the block's raw offset.
    /// </summary>
    private void OnDocumentBlockHexInspectRequested(object? sender, DocEditorModel block)
    {
        if (block.RawOffset <= 0) return;

        // Navigate the already-connected hex editor if available
        if (_connectedHexEditor != null)
        {
            _connectedHexEditor.SetPosition(block.RawOffset);
            return;
        }

        // No hex editor open — open the source file in a new hex tab then navigate
        if (sender is WpfHexEditor.Editor.DocumentEditor.Controls.DocumentEditorHost host &&
            !string.IsNullOrEmpty(host.FilePath))
            OpenFileWithOffset(host.FilePath, block.RawOffset);
    }

    /// <summary>
    /// Opens <paramref name="filePath"/> in a hex-editor tab and navigates to <paramref name="offset"/>
    /// once the tab content has been created.
    /// </summary>
    private void OpenFileWithOffset(string filePath, long offset)
    {
        OpenStandaloneFileWithEditor(filePath, "hex-editor");

        // Content is created lazily after Dock; defer the navigate until layout is complete.
        Dispatcher.InvokeAsync(() =>
        {
            var hex = ActiveHexEditor;
            if (hex is not null && offset < hex.Length)
                hex.SetPosition(offset);
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }
}
