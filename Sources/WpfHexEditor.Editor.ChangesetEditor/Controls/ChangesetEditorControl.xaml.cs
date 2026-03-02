// Apache 2.0 - 2026
// Contributors: Claude Sonnet 4.6

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfHexEditor.Editor.ChangesetEditor.ViewModels;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.Editor.ChangesetEditor.Controls;

/// <summary>
/// Ultra-fast changeset viewer / editor for <c>.whchg</c> companion files.
/// Implements <see cref="IDocumentEditor"/>, <see cref="IOpenableDocument"/>,
/// and <see cref="IEditorToolbarContributor"/>.
/// Three virtualized DataGrid tabs: Modified / Inserted / Deleted.
/// All contextual actions (Apply to Disk, Discard) are exposed via
/// <see cref="IEditorToolbarContributor.ToolbarItems"/> — no embedded toolbar.
/// </summary>
public sealed partial class ChangesetEditorControl : UserControl,
    IDocumentEditor, IOpenableDocument, IEditorToolbarContributor,
    INotifyPropertyChanged
{
    // ── INotifyPropertyChanged ───────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // ── IDocumentEditor — State ───────────────────────────────────────────────

    public string? FilePath { get; private set; }

    private bool _isDirty;
    public bool IsDirty
    {
        get => _isDirty;
        private set
        {
            if (_isDirty == value) return;
            _isDirty = value;
            ModifiedChanged?.Invoke(this, EventArgs.Empty);
            TitleChanged?.Invoke(this, Title);
        }
    }

    public bool CanUndo => false;
    public bool CanRedo => false;
    public bool IsBusy  => false;

    private bool _isReadOnly;
    public bool IsReadOnly
    {
        get => _isReadOnly;
        set => _isReadOnly = value;
    }

    public string Title
    {
        get
        {
            var name = FilePath is null ? "Changeset" : Path.GetFileName(FilePath);
            return IsDirty ? name + " *" : name;
        }
    }

    // ── IDocumentEditor — Commands ────────────────────────────────────────────

    public ICommand? SaveCommand      { get; }
    public ICommand? UndoCommand      { get; }
    public ICommand? RedoCommand      { get; }
    public ICommand? CutCommand       { get; }
    public ICommand? CopyCommand      { get; }
    public ICommand? PasteCommand     { get; }
    public ICommand? DeleteCommand    { get; }
    public ICommand? SelectAllCommand { get; }

    // ── IDocumentEditor — Events ──────────────────────────────────────────────

    public event EventHandler?          ModifiedChanged;
    public event EventHandler?          CanUndoChanged;
    public event EventHandler?          CanRedoChanged;
    public event EventHandler<string>?  TitleChanged;
    public event EventHandler<string>?  StatusMessage;
    public event EventHandler<string>?  OutputMessage;
    public event EventHandler?          SelectionChanged;

    public event EventHandler<DocumentOperationEventArgs>?          OperationStarted;
    public event EventHandler<DocumentOperationEventArgs>?          OperationProgress;
    public event EventHandler<DocumentOperationCompletedEventArgs>? OperationCompleted;

    // ── IDocumentEditor — Methods ─────────────────────────────────────────────

    public void Undo() { }
    public void Redo() { }
    public void Save() { }
    public Task SaveAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task SaveAsAsync(string filePath, CancellationToken ct = default) => Task.CompletedTask;
    public void Copy() { }
    public void Cut() { }
    public void Paste() { }
    public void Delete() { }
    public void SelectAll() { }
    public void Close() => ViewModel.Clear();
    public void CancelOperation() { }

    // ── ViewModel ─────────────────────────────────────────────────────────────

    public ChangesetEditorViewModel ViewModel { get; } = new();

    // ── Computed tab headers ───────────────────────────────────────────────────

    public string ModifiedHeader =>
        $"Modified ({ViewModel.ModifiedEntries.Count})";
    public string InsertedHeader =>
        $"Inserted ({ViewModel.InsertedEntries.Count})";
    public string DeletedHeader =>
        $"Deleted ({ViewModel.DeletedRanges.Count})";

    public string StatusText =>
        $"Modified: {ViewModel.ModifiedEntries.Count}  |  " +
        $"Inserted: {ViewModel.InsertedEntries.Count}  |  " +
        $"Deleted: {ViewModel.DeletedRanges.Count}" +
        (string.IsNullOrEmpty(ViewModel.SourceHash) ? "" : $"  |  {ViewModel.SourceHash}");

    // ── IEditorToolbarContributor ─────────────────────────────────────────────

    private readonly ObservableCollection<EditorToolbarItem> _toolbarItems = [];
    public ObservableCollection<EditorToolbarItem> ToolbarItems => _toolbarItems;

    // ── Commands for toolbar ──────────────────────────────────────────────────

    private readonly RelayCommand _applyToDiskCommand;
    private readonly RelayCommand _discardCommand;

    public ICommand ApplyToDiskCommand => _applyToDiskCommand;
    public ICommand DiscardCommand     => _discardCommand;

    // ── Constructor ───────────────────────────────────────────────────────────

    public ChangesetEditorControl()
    {
        InitializeComponent();

        _applyToDiskCommand = new RelayCommand(_ => OnApplyToDisk(), _ => FilePath != null);
        _discardCommand     = new RelayCommand(_ => OnDiscard(),     _ => FilePath != null);

        _toolbarItems.Add(new EditorToolbarItem
        {
            Icon     = "\uE9F5",   // Segoe MDL2 "PublishContent" (disk + arrow)
            Label    = "Apply to Disk",
            Tooltip  = "Write changes to the physical file (Ctrl+Shift+W)",
            Command  = _applyToDiskCommand,
        });
        _toolbarItems.Add(new EditorToolbarItem
        {
            Icon     = "\uE711",   // Segoe MDL2 "Delete"
            Label    = "Discard",
            Tooltip  = "Discard all pending changes",
            Command  = _discardCommand,
        });
    }

    // ── IOpenableDocument ──────────────────────────────────────────────────────

    public Task OpenAsync(string filePath, CancellationToken ct = default)
    {
        FilePath = filePath;
        LoadFromDisk();
        return Task.CompletedTask;
    }

    // ── File I/O ─────────────────────────────────────────────────────────────

    private void LoadFromDisk()
    {
        if (FilePath is null || !File.Exists(FilePath))
        {
            ViewModel.Clear();
            NotifyHeadersChanged();
            return;
        }

        try
        {
            using var fs = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var dto = ChangesetSerializer.ReadAsync(fs).GetAwaiter().GetResult();
            if (dto != null)
                ViewModel.Load(dto);
            else
                ViewModel.Clear();

            IsDirty = false;
            NotifyHeadersChanged();
            TitleChanged?.Invoke(this, Title);
            StatusMessage?.Invoke(this, StatusText);
        }
        catch (Exception ex)
        {
            OutputMessage?.Invoke(this, $"ChangesetEditor: failed to load '{FilePath}': {ex.Message}");
        }
    }

    // ── Toolbar command handlers ───────────────────────────────────────────────

    private void OnApplyToDisk()
    {
        // The host (App/MainWindow) handles the actual disk write via OnWriteToDisk / Ctrl+Shift+W.
        StatusMessage?.Invoke(this, "Apply to Disk: use Ctrl+Shift+W or the Solution Explorer context menu.");
    }

    private void OnDiscard()
    {
        StatusMessage?.Invoke(this, "Discard: use the Solution Explorer context menu.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void NotifyHeadersChanged()
    {
        OnPropertyChanged(nameof(ModifiedHeader));
        OnPropertyChanged(nameof(InsertedHeader));
        OnPropertyChanged(nameof(DeletedHeader));
        OnPropertyChanged(nameof(StatusText));
    }

#pragma warning disable CS0067   // events declared but never raised (no-op for this editor)
    // CanUndoChanged, CanRedoChanged, SelectionChanged, OperationStarted,
    // OperationProgress, OperationCompleted are part of IDocumentEditor contract
    // but not raised by this read-only viewer.
#pragma warning restore CS0067
}

// ── Simple RelayCommand ───────────────────────────────────────────────────────

internal sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute    = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter)    => _execute(parameter);
    public event EventHandler? CanExecuteChanged
    {
        add    => System.Windows.Input.CommandManager.RequerySuggested += value;
        remove => System.Windows.Input.CommandManager.RequerySuggested -= value;
    }
}
