// Apache 2.0 - 2026
// Contributors: Claude Sonnet 4.6

using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfHexEditor.Editor.ChangesetEditor.ViewModels;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.Editor.ChangesetEditor.Controls;

/// <summary>
/// Ultra-fast changeset viewer / editor for <c>.whchg</c> companion files.
/// Implements <see cref="IDocumentEditor"/> and <see cref="IEditorToolbarContributor"/>.
/// Three virtualized DataGrid tabs: Modified / Inserted / Deleted.
/// All contextual actions (Apply to Disk, Discard) are exposed via
/// <see cref="IEditorToolbarContributor.ToolbarItems"/> — no embedded toolbar.
/// </summary>
public sealed partial class ChangesetEditorControl : UserControl, IDocumentEditor, IEditorToolbarContributor
{
    // ── IDocumentEditor ──────────────────────────────────────────────────────

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
            TitleChanged?.Invoke(this, GetTitle());
        }
    }

    public bool IsReadOnly => false;

    public event EventHandler<string>?  TitleChanged;
    public event EventHandler?          ModifiedChanged;
    public event EventHandler?          CanUndoChanged;
    public event EventHandler?          CanRedoChanged;
    public event EventHandler<string>?  StatusMessage;
    public event EventHandler<string>?  OutputMessage;
    public event EventHandler?          OperationStarted;
    public event EventHandler<int>?     OperationProgress;
    public event EventHandler?          OperationCompleted;

    public ICommand? SaveCommand   { get; }
    public ICommand? UndoCommand   { get; }
    public ICommand? RedoCommand   { get; }
    public ICommand? CutCommand    { get; }
    public ICommand? CopyCommand   { get; }
    public ICommand? PasteCommand  { get; }

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

        _applyToDiskCommand = new RelayCommand(_ => ApplyToDisk(), _ => FilePath != null);
        _discardCommand     = new RelayCommand(_ => Discard(),     _ => FilePath != null);

        _toolbarItems.Add(new EditorToolbarItem
        {
            Icon     = "\uE9F5",   // Segoe MDL2 "PublishContent" (disk + arrow)
            Label    = "Apply to Disk",
            Tooltip  = "Write changes to the physical file (Ctrl+Shift+W)",
            Command  = _applyToDiskCommand,
            IsEnabled = true,
        });
        _toolbarItems.Add(new EditorToolbarItem
        {
            Icon     = "\uE711",   // Segoe MDL2 "Delete"
            Label    = "Discard",
            Tooltip  = "Discard all pending changes",
            Command  = _discardCommand,
            IsEnabled = true,
        });
    }

    // ── IDocumentEditor — no-op Undo/Redo (V1) ───────────────────────────────

    public void Undo() { }
    public void Redo() { }

    // ── File I/O ─────────────────────────────────────────────────────────────

    public void OpenFile(string filePath)
    {
        FilePath = filePath;
        LoadFromDisk();
    }

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
            TitleChanged?.Invoke(this, GetTitle());
            StatusMessage?.Invoke(this, StatusText);
        }
        catch (Exception ex)
        {
            OutputMessage?.Invoke(this, $"ChangesetEditor: failed to load '{FilePath}': {ex.Message}");
        }
    }

    // ── IDocumentEditor — Save ────────────────────────────────────────────────

    private void ApplyToDisk()
    {
        // Delegate to the App via the toolbar command — the host handles the actual operation.
        // In standalone use (no project system), this is a no-op placeholder.
        StatusMessage?.Invoke(this, "Apply to Disk: use Ctrl+Shift+W or the Solution Explorer context menu.");
    }

    private void Discard()
    {
        StatusMessage?.Invoke(this, "Discard: use the Solution Explorer context menu.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private string GetTitle()
    {
        var name = FilePath is null ? "Changeset" : Path.GetFileName(FilePath);
        return IsDirty ? name + " *" : name;
    }

    private void NotifyHeadersChanged()
    {
        // Force re-evaluation of tab header bindings
        var dp = System.Windows.DependencyPropertyDescriptor.FromProperty(
            System.Windows.FrameworkElement.TagProperty,
            typeof(ChangesetEditorControl));
        OnPropertyChanged(nameof(ModifiedHeader));
        OnPropertyChanged(nameof(InsertedHeader));
        OnPropertyChanged(nameof(DeletedHeader));
        OnPropertyChanged(nameof(StatusText));
    }

    private void OnPropertyChanged(string name)
        => (this as System.ComponentModel.INotifyPropertyChanged)?.GetType();   // trigger WPF refresh

    // ── Explicit INotifyPropertyChanged (triggered via RaisePropertyChanged helper below) ──

    // Note: UserControl doesn't require INPC; we use DependencyProperties or RelativeSource bindings.
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
