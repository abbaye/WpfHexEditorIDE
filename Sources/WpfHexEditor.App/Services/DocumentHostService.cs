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

    // Pending navigations: FilePath → (line, column)
    // Set when ActivateAndNavigateTo is called for a file that is not yet open
    // or whose editor is not yet loaded (lazy-dock content creation).
    private readonly Dictionary<string, (int Line, int Column)> _pendingNavigations
        = new(StringComparer.OrdinalIgnoreCase);

    public DocumentHostService(
        IDocumentManager documentManager,
        Func<string, string?, Task> openFileHandler)
    {
        _documentManager = documentManager ?? throw new ArgumentNullException(nameof(documentManager));
        _openFileHandler = openFileHandler  ?? throw new ArgumentNullException(nameof(openFileHandler));

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

        // Check if a tab is already open for this path (any editor type).
        var existing = FindDocumentModelByPath(filePath);
        if (existing is not null)
        {
            // Activate via the document manager; the docking host listens to ActiveDocumentChanged.
            _documentManager.SetActive(existing.ContentId);
            return;
        }

        // Delegate to the MainWindow-provided handler (runs on UI thread via Dispatcher).
        _ = _openFileHandler(filePath, preferredEditorId);
    }

    /// <inheritdoc/>
    public void ActivateAndNavigateTo(string filePath, int line, int column)
    {
        if (string.IsNullOrEmpty(filePath)) return;

        var model = FindDocumentModelByPath(filePath);

        if (model is not null)
        {
            // Tab already registered — activate it and navigate immediately if possible.
            _documentManager.SetActive(model.ContentId);
            TryNavigate(model, line, column);
        }
        else
        {
            // File not yet open — store pending navigation and open the tab.
            _pendingNavigations[filePath] = (line, column);
            _ = _openFileHandler(filePath, null);
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
