//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// Common contract for any embeddable document editor (Hex, TBL, JSON, …).
/// Implemented by a UserControl or FrameworkElement; the host (docking, main window)
/// interacts via this interface to drive the editor uniformly.
/// </summary>
public interface IDocumentEditor
{
    // -- State ------------------------------------------------------------
    bool IsDirty    { get; }
    bool CanUndo    { get; }
    bool CanRedo    { get; }
    bool IsReadOnly { get; set; }   // DP-backed in WPF implementations

    /// <summary>Number of available undo steps (0 if unsupported).</summary>
    int UndoCount { get => 0; }

    /// <summary>Number of available redo steps (0 if unsupported).</summary>
    int RedoCount { get => 0; }

    /// <summary>
    /// Human-readable description of the next undo step (e.g. "Paste 50 bytes").
    /// Empty string or "Undo" when unsupported or nothing to undo.
    /// </summary>
    string UndoDescription { get => "Undo"; }

    /// <summary>
    /// Human-readable description of the next redo step.
    /// Empty string or "Redo" when unsupported or nothing to redo.
    /// </summary>
    string RedoDescription { get => "Redo"; }

    /// <summary>
    /// Title displayed in the host tab ("file.bin", "file.bin *" if dirty).
    /// </summary>
    string Title { get; }

    // -- Bindable commands (host: MenuItem.Command, toolbar…) -------------
    // Nullable: editors that don't support a command may return null.
    ICommand? UndoCommand      { get; }
    ICommand? RedoCommand      { get; }
    ICommand? SaveCommand      { get; }
    ICommand? CopyCommand      { get; }
    ICommand? CutCommand       { get; }
    ICommand? PasteCommand     { get; }
    ICommand? DeleteCommand    { get; }
    ICommand? SelectAllCommand { get; }

    // -- Methods ----------------------------------------------------------
    void Undo();
    void Redo();
    void Save();
    Task SaveAsync(CancellationToken ct = default);
    Task SaveAsAsync(string filePath, CancellationToken ct = default);
    void Copy();
    void Cut();
    void Paste();
    void Delete();
    void SelectAll();
    void Close();

    // -- Save suppression callback ----------------------------------------

    /// <summary>
    /// When set by the host, editors should invoke this callback with the file path
    /// immediately before writing to disk, so the file watcher can suppress the event.
    /// Default implementation: no-op (null).
    /// </summary>
    Action<string>? BeforeSaveCallback { get => null; set { } }

    // -- Events (host updates its own menu/statusbar) ---------------------
    event EventHandler?         ModifiedChanged;  // IsDirty changed
    event EventHandler?         CanUndoChanged;
    event EventHandler?         CanRedoChanged;
    event EventHandler<string>? TitleChanged;     // "file.tbl *" — host updates the tab
    event EventHandler<string>? StatusMessage;    // toast / host statusbar
    event EventHandler<string>? OutputMessage;   // verbose log → host Output panel
    event EventHandler?         SelectionChanged; // host re-queries CanExecute on commands

    // -- Long-running operations -------------------------------------------
    /// <summary>
    /// True while a long-running operation is in progress on this document.
    /// </summary>
    bool IsBusy { get; }

    /// <summary>
    /// Requests cancellation of the current operation.
    /// No-op if no operation is running or if the operation is not cancellable.
    /// </summary>
    void CancelOperation();

    /// <summary>
    /// Raised when a long-running operation starts on this document.
    /// </summary>
    event EventHandler<DocumentOperationEventArgs>?          OperationStarted;

    /// <summary>
    /// Raised periodically with progress updates during a long-running operation.
    /// </summary>
    event EventHandler<DocumentOperationEventArgs>?          OperationProgress;

    /// <summary>
    /// Raised when the long-running operation completes (success, error or cancellation).
    /// </summary>
    event EventHandler<DocumentOperationCompletedEventArgs>? OperationCompleted;
}
