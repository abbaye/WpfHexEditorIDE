// ==========================================================
// Project: WpfHexEditor.App
// File: Services/DocumentTabManager.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-29
// Description:
//     Centralises document tab lifecycle state previously scattered
//     across MainWindow.xaml.cs field declarations.
//     Owns: content ID generation, content/editor caches, and
//     the pending project-properties content ID set.
//
// Architecture Notes:
//     Initialized by MainWindow constructor (no DI — needs no external deps).
//     MainWindow initializes _contentCache and _displayContent from this
//     manager's exposed dictionary properties to preserve backward compat
//     with existing field-access patterns throughout the partial classes.
//     Full extraction of OpenFileDirectly / CreateDocumentTab logic is
//     deferred to a future dedicated refactor session.
// ==========================================================

using System.Windows;

namespace WpfHexEditor.App.Services;

/// <summary>
/// Manages document tab lifecycle state for the app shell.
/// </summary>
public sealed class DocumentTabManager
{
    // ── Content caches ────────────────────────────────────────────────────────

    /// <summary>
    /// Maps ContentId → unwrapped editor UIElement.
    /// Use this to iterate over open editors.
    /// </summary>
    public Dictionary<string, UIElement> ContentCache { get; } = new();

    /// <summary>
    /// Maps ContentId → display element (may be wrapped in an InfoBar).
    /// Use this when setting the DockControl content.
    /// </summary>
    public Dictionary<string, UIElement> DisplayContent { get; } = new();

    /// <summary>Pending content IDs for project-properties tabs awaiting solution load.</summary>
    public HashSet<string> PendingProjectPropertiesContentIds { get; } = new();

    // ── Content ID generation ─────────────────────────────────────────────────

    private int _counter;

    /// <summary>Generates the next unique content ID with the given prefix (e.g. "doc-diff").</summary>
    public string NextContentId(string prefix)
    {
        _counter++;
        return $"{prefix}-{_counter}";
    }

    /// <summary>
    /// Restores the counter to <paramref name="max"/> + 1 during layout restore.
    /// Prevents collisions with IDs loaded from persisted layout.
    /// </summary>
    public void RestoreCounterFrom(int max) => _counter = max;

    /// <summary>Current counter value.</summary>
    public int Counter
    {
        get => _counter;
        set => _counter = value;
    }

    // ── Lifecycle helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Registers a new document tab.
    /// <paramref name="displayElement"/> is the visual shown by docking (may include InfoBar).
    /// <paramref name="editorElement"/> is the unwrapped editor (or same as display if no InfoBar).
    /// </summary>
    public void Register(string contentId, UIElement displayElement, UIElement editorElement)
    {
        DisplayContent[contentId] = displayElement;
        ContentCache[contentId]   = editorElement;
    }

    /// <summary>Removes both cache entries for <paramref name="contentId"/>.</summary>
    public void Remove(string contentId)
    {
        ContentCache.Remove(contentId);
        DisplayContent.Remove(contentId);
        PendingProjectPropertiesContentIds.Remove(contentId);
    }

    /// <summary>Returns the unwrapped editor for <paramref name="contentId"/>, or null.</summary>
    public UIElement? GetEditor(string contentId)
        => ContentCache.TryGetValue(contentId, out var e) ? e : null;

    /// <summary>Returns the display element for <paramref name="contentId"/>, or null.</summary>
    public UIElement? GetDisplay(string contentId)
        => DisplayContent.TryGetValue(contentId, out var e) ? e : null;

    /// <summary>Returns all open editors as an enumerable of (contentId, editor) pairs.</summary>
    public IEnumerable<KeyValuePair<string, UIElement>> GetAllEditors()
        => ContentCache;
}
