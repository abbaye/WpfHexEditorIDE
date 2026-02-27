//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Windows.Input;

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// Contrat commun pour tout éditeur de document embarquable (TBL, JSON, …).
/// Implémenté par un UserControl ou FrameworkElement; le host interagit via cette interface.
/// </summary>
public interface IDocumentEditor
{
    // ── État ─────────────────────────────────────────────────────────────
    bool IsDirty    { get; }
    bool CanUndo    { get; }
    bool CanRedo    { get; }
    bool IsReadOnly { get; set; }   // DP-backed dans les implémentations WPF

    // ── Commandes bindables (host : MenuItem.Command, toolbar…) ──────────
    ICommand UndoCommand { get; }
    ICommand RedoCommand { get; }
    ICommand SaveCommand { get; }

    // ── Méthodes ─────────────────────────────────────────────────────────
    void Undo();
    void Redo();
    void Save();
    Task SaveAsync(CancellationToken ct = default);
    Task SaveAsAsync(string filePath, CancellationToken ct = default);

    // ── Événements (host met à jour son propre menu/statusbar) ────────────
    event EventHandler?         ModifiedChanged;  // IsDirty a changé
    event EventHandler?         CanUndoChanged;
    event EventHandler?         CanRedoChanged;
    event EventHandler<string>? TitleChanged;     // "file.tbl *" — host met à jour l'onglet
    event EventHandler<string>? StatusMessage;    // toast / statusbar du host
}
