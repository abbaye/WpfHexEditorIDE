// ==========================================================
// Project: WpfHexEditor.App
// File: Services/DocumentHostService.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Concrete implementation of IDocumentHostService.
//     Bridges the high-level document API (open by path, navigate to line)
//     to the existing MainWindow docking infrastructure via a callback delegate.
//
// Architecture Notes:
//     Pattern: Facade + Callback Bridge
//     - openFileHandler: Func<string, string?, Task> delegated from MainWindow.
//       This avoids a direct MainWindow reference in the service layer.
//     - activateTabHandler: Action<string> delegated from MainWindow.
//       Called with a ContentId to bring an already-open docking tab to front.
//     - ActivateAndNavigateTo: applies INavigableDocument.NavigateTo() when the
//       editor is already loaded; uses a pending-navigation approach for lazy
//       tabs (tab opened but content not yet created by the docking engine).
//     - SaveAll() iterates IDocumentManager.GetDirty() and calls editor.Save().
// ==========================================================

using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.Core.Documents;
using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.App.Services;

/// <summary>
/// Facade over the docking / document lifecycle system.
/// Opened via MainWindow.InitDocumentHostService().
/// </summary>
public sealed class DocumentHostService : IDocumentHostService
{
    private readonly IDocumentManager _documentManager;
    private readonly Func<string, string?, Task> _openFileHandler;
    private readonly Action<string>? _activateTabHandler;

    // Pending navigations: FilePath → (line, column)
    // Set when ActivateAndNavigateTo is called for a file that is not yet open
    // or whose editor is not yet loaded (lazy-dock content creation).
    private readonly Dictionary<string, (int Line, int Column)> _pendingNavigations
        = new(StringComparer.OrdinalIgnoreCase);

    // Pending opens: file paths for which _openFileHandler has been called but the
    // tab is not yet registered in _documentManager (lazy content creation window).
    // Guards against TOCTOU duplicates: a second OpenDocument call that arrives
    // before RegisterDocumentFromItem runs would bypass FindDocumentModelByPath.
    private readonly HashSet<string> _pendingOpens
        = new(StringComparer.OrdinalIgnoreCase);

    public DocumentHostService(
        IDocumentManager documentManager,
        Func<string, string?, Task> openFileHandler,
        Action<string>? activateTabHandler = null)
    {
        _documentManager    = documentManager ?? throw new ArgumentNullException(nameof(documentManager));
        _openFileHandler    = openFileHandler  ?? throw new ArgumentNullException(nameof(openFileHandler));
        _activateTabHandler = activateTabHandler;

        // When a document becomes active, apply any pending navigation for that file.
        _documentManager.ActiveDocumentChanged += OnActiveDocumentChanged;
    }

    // -- IDocumentHostService : State -------------------------------------

    public IDocumentManager Documents => _documentManager;

    // -- IDocumentHostService : Operations --------------------------------

    /// <inheritdoc/>
    public void OpenDocument(string filePath, string? preferredEditorId = null)
    {
        if (string.IsNullOrEmpty(filePath)) return;

        // When a specific editor is requested, delegate to MainWindow which handles
        // per-editor deduplication (same file can be open in multiple editors simultaneously).
        // Guard against TOCTOU: use composite key so same file+editor isn't opened twice.
        if (preferredEditorId is not null)
        {
            var key = $"{filePath}|{preferredEditorId}";
            if (_pendingOpens.Contains(key)) return;
            _pendingOpens.Add(key);
            _ = OpenAndTrackKeyedAsync(filePath, preferredEditorId, key);
            return;
        }

        // No editor preference — activate the existing tab if the file is already open.
        var existing = FindDocumentModelByPath(filePath);
        if (existing is not null)
        {
            _documentManager.SetActive(existing.ContentId);
            return;
        }

        // Guard against TOCTOU: _openFileHandler is async and tab registration is lazy.
        // A second call before the first tab is registered would create a duplicate.
        if (_pendingOpens.Contains(filePath)) return;
        _pendingOpens.Add(filePath);
        _ = OpenAndTrackAsync(filePath, null);
    }

    /// <inheritdoc/>
    public void ActivateAndNavigateTo(string filePath, int line, int column)
    {
        if (string.IsNullOrEmpty(filePath)) return;

        var model = FindDocumentModelByPath(filePath);

        if (model is not null)
        {
            // Tab already registered — bring it to front in the dock UI, then navigate.
            _activateTabHandler?.Invoke(model.ContentId);
            _documentManager.SetActive(model.ContentId);
            TryNavigate(model, line, column);
        }
        else
        {
            // File not yet open — store pending navigation and open the tab.
            _pendingNavigations[filePath] = (line, column);
            if (_pendingOpens.Contains(filePath)) return;
            _pendingOpens.Add(filePath);
            _ = OpenAndTrackAsync(filePath, null);
        }
    }

    /// <inheritdoc/>
    public void SaveAll()
    {
        foreach (var model in _documentManager.GetDirty())
        {
            if (model.AssociatedEditor is { } editor)
                editor.Save();
        }
    }

    // -- Internal ---------------------------------------------------------

    /// <summary>
    /// Calls <see cref="_openFileHandler"/> and removes the file from
    /// <see cref="_pendingOpens"/> once the tab is registered (task completes).
    /// </summary>
    private async Task OpenAndTrackAsync(string filePath, string? editorId)
    {
        try   { await _openFileHandler(filePath, editorId); }
        finally { _pendingOpens.Remove(filePath); }
    }

    private async Task OpenAndTrackKeyedAsync(string filePath, string editorId, string key)
    {
        try   { await _openFileHandler(filePath, editorId); }
        finally { _pendingOpens.Remove(key); }
    }

    private DocumentModel? FindDocumentModelByPath(string filePath)
        => _documentManager.OpenDocuments.FirstOrDefault(
            d => string.Equals(d.FilePath, filePath, StringComparison.OrdinalIgnoreCase));

    private static void TryNavigate(DocumentModel model, int line, int column)
    {
        if (model.AssociatedEditor is INavigableDocument nav)
            nav.NavigateTo(line, column);
    }

    private void OnActiveDocumentChanged(object? sender, DocumentModel? model)
    {
        if (model?.FilePath is null) return;
        if (!_pendingNavigations.TryGetValue(model.FilePath, out var nav)) return;

        _pendingNavigations.Remove(model.FilePath);

        // Give the docking engine one dispatcher tick to finish attaching the editor.
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(
            () => TryNavigate(model, nav.Line, nav.Column),
            System.Windows.Threading.DispatcherPriority.Loaded);
    }
}
