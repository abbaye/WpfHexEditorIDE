//////////////////////////////////////////////
// Project: WpfHexEditor.App
// File: Services/StatusBarSyncService.cs
// Description:
//     Manages the active status bar contributor and toolbar contributor
//     for the focused document editor. Extracted from MainWindow to
//     centralize contributor lifecycle.
// Architecture:
//     Lightweight state holder — MainWindow calls SyncToEditor() when
//     the active document changes. The service tracks the current
//     contributor and raises Changed when it differs.
//////////////////////////////////////////////

using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.App.Services;

/// <summary>
/// Tracks the active <see cref="IStatusBarContributor"/> and
/// <see cref="IEditorToolbarContributor"/> for the focused editor tab.
/// </summary>
internal sealed class StatusBarSyncService
{
    private IStatusBarContributor? _activeContributor;
    private IEditorToolbarContributor? _activeToolbarContributor;
    private IDocumentEditor? _activeEditor;

    /// <summary>Current status bar contributor (null when no editor is focused).</summary>
    public IStatusBarContributor? ActiveContributor => _activeContributor;

    /// <summary>Current toolbar contributor (null when editor doesn't provide one).</summary>
    public IEditorToolbarContributor? ActiveToolbarContributor => _activeToolbarContributor;

    /// <summary>Current active editor.</summary>
    public IDocumentEditor? ActiveEditor => _activeEditor;

    /// <summary>Raised when the active contributor changes.</summary>
    public event Action? ContributorChanged;

    /// <summary>
    /// Syncs the service to a new editor. Call when the active document tab changes.
    /// </summary>
    public void SyncToEditor(IDocumentEditor? editor)
    {
        var newContributor = editor as IStatusBarContributor;
        var newToolbar     = editor as IEditorToolbarContributor;

        bool changed = !ReferenceEquals(_activeContributor, newContributor)
                    || !ReferenceEquals(_activeToolbarContributor, newToolbar);

        _activeEditor              = editor;
        _activeContributor         = newContributor;
        _activeToolbarContributor  = newToolbar;

        if (changed)
        {
            // Refresh immediately so zoom/position/language values are current on tab switch,
            // not stale from the previous interaction in this editor session.
            newContributor?.RefreshStatusBarItems();
            ContributorChanged?.Invoke();
        }
    }

    /// <summary>Clears the active state (no editor focused).</summary>
    public void Clear()
    {
        if (_activeContributor is null && _activeToolbarContributor is null) return;
        _activeEditor = null;
        _activeContributor = null;
        _activeToolbarContributor = null;
        ContributorChanged?.Invoke();
    }
}
