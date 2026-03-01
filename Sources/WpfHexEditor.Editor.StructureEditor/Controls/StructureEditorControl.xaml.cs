//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.IO;
using System.Windows.Controls;
using System.Windows.Input;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.Editor.StructureEditor.Controls;

/// <summary>
/// Stub structure editor — planned for a future sprint.
/// Reads .whfmt structure definitions.
/// Implements <see cref="IDocumentEditor"/> and <see cref="IOpenableDocument"/>.
/// </summary>
public sealed partial class StructureEditorControl : UserControl, IDocumentEditor, IOpenableDocument
{
    private string _filePath = string.Empty;

    /// <summary>
    /// Creates a new <see cref="StructureEditorControl"/>.
    /// </summary>
    public StructureEditorControl()
    {
        InitializeComponent();

        UndoCommand      = new RelayCommand(() => { }, () => false);
        RedoCommand      = new RelayCommand(() => { }, () => false);
        SaveCommand      = new RelayCommand(() => { }, () => false);
        CopyCommand      = new RelayCommand(() => { }, () => false);
        CutCommand       = new RelayCommand(() => { }, () => false);
        PasteCommand     = new RelayCommand(() => { }, () => false);
        DeleteCommand    = new RelayCommand(() => { }, () => false);
        SelectAllCommand = new RelayCommand(() => { }, () => false);
    }

    // ── IDocumentEditor — State ──────────────────────────────────────────

    /// <inheritdoc/>
    public bool IsDirty    => false;

    /// <inheritdoc/>
    public bool CanUndo    => false;

    /// <inheritdoc/>
    public bool CanRedo    => false;

    /// <inheritdoc/>
    public bool IsReadOnly { get => true; set { } }

    /// <inheritdoc/>
    public string Title { get; private set; } = "";

    /// <inheritdoc/>
    public bool IsBusy { get; private set; }

    // ── IDocumentEditor — Commands ───────────────────────────────────────

    /// <inheritdoc/>
    public ICommand UndoCommand      { get; }

    /// <inheritdoc/>
    public ICommand RedoCommand      { get; }

    /// <inheritdoc/>
    public ICommand SaveCommand      { get; }

    /// <inheritdoc/>
    public ICommand CopyCommand      { get; }

    /// <inheritdoc/>
    public ICommand CutCommand       { get; }

    /// <inheritdoc/>
    public ICommand PasteCommand     { get; }

    /// <inheritdoc/>
    public ICommand DeleteCommand    { get; }

    /// <inheritdoc/>
    public ICommand SelectAllCommand { get; }

    // ── IDocumentEditor — Events ─────────────────────────────────────────

#pragma warning disable CS0067
    /// <inheritdoc/>
    public event EventHandler?         ModifiedChanged;

    /// <inheritdoc/>
    public event EventHandler?         CanUndoChanged;

    /// <inheritdoc/>
    public event EventHandler?         CanRedoChanged;

    /// <inheritdoc/>
    public event EventHandler<string>? TitleChanged;

    /// <inheritdoc/>
    public event EventHandler<string>? StatusMessage;

    /// <inheritdoc/>
    public event EventHandler?         SelectionChanged;

    /// <inheritdoc/>
    public event EventHandler<DocumentOperationEventArgs>?          OperationStarted;

    /// <inheritdoc/>
    public event EventHandler<DocumentOperationEventArgs>?          OperationProgress;

    /// <inheritdoc/>
    public event EventHandler<DocumentOperationCompletedEventArgs>? OperationCompleted;
#pragma warning restore CS0067

    // ── IDocumentEditor — Methods (no-ops for read-only stub) ────────────

    /// <inheritdoc/>
    public void Undo() { }

    /// <inheritdoc/>
    public void Redo() { }

    /// <inheritdoc/>
    public void Save() { }

    /// <inheritdoc/>
    public Task SaveAsync(CancellationToken ct = default) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task SaveAsAsync(string filePath, CancellationToken ct = default) => Task.CompletedTask;

    /// <inheritdoc/>
    public void Copy() { }

    /// <inheritdoc/>
    public void Cut() { }

    /// <inheritdoc/>
    public void Paste() { }

    /// <inheritdoc/>
    public void Delete() { }

    /// <inheritdoc/>
    public void SelectAll() { }

    /// <inheritdoc/>
    public void Close() { }

    /// <inheritdoc/>
    public void CancelOperation() { }

    // ── IOpenableDocument ────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task OpenAsync(string filePath, CancellationToken ct = default)
    {
        _filePath = filePath;
        Title = Path.GetFileName(filePath);
        TitleChanged?.Invoke(this, Title);
        OperationCompleted?.Invoke(this, new DocumentOperationCompletedEventArgs { Success = true });
        await Task.CompletedTask;
    }
}

// ── Minimal RelayCommand (no external dep) ───────────────────────────────────

internal sealed class RelayCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add    => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;
    public void Execute(object? parameter)    => execute();
}
