//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.TextEditor.Highlighting;
using WpfHexEditor.Editor.TextEditor.Services;
using WpfHexEditor.Editor.TextEditor.ViewModels;

namespace WpfHexEditor.Editor.TextEditor.Controls;

/// <summary>
/// Full-featured text editor with syntax highlighting.
/// Implements <see cref="IDocumentEditor"/>, <see cref="IOpenableDocument"/>,
/// and <see cref="IEditorPersistable"/> so the project system can save and
/// restore per-file state (caret position, syntax language override).
/// </summary>
public sealed partial class TextEditor : UserControl, IDocumentEditor, IOpenableDocument, IEditorPersistable
{
    // -----------------------------------------------------------------------
    // Fields
    // -----------------------------------------------------------------------

    private readonly TextEditorViewModel _vm = new();
    private CancellationTokenSource? _cts = null; // reserved for future async operations

    // -----------------------------------------------------------------------
    // Constructor
    // -----------------------------------------------------------------------

    /// <summary>
    /// Creates a new <see cref="TextEditor"/>.
    /// </summary>
    public TextEditor()
    {
        InitializeComponent();

        // Wire ViewModel
        Viewport.Attach(_vm);
        _vm.PropertyChanged += OnVmPropertyChanged;

        // Commands
        UndoCommand      = new RelayCommand(() => Undo(),      () => CanUndo);
        RedoCommand      = new RelayCommand(() => Redo(),      () => CanRedo);
        SaveCommand      = new RelayCommand(() => Save(),      () => IsDirty && !string.IsNullOrEmpty(_vm.FilePath));
        CopyCommand      = new RelayCommand(() => Copy(),      () => _vm.HasSelection);
        CutCommand       = new RelayCommand(() => Cut(),       () => _vm.HasSelection && !IsReadOnly);
        PasteCommand     = new RelayCommand(() => Paste(),     () => !IsReadOnly);
        DeleteCommand    = new RelayCommand(() => Delete(),    () => _vm.HasSelection && !IsReadOnly);
        SelectAllCommand = new RelayCommand(() => SelectAll(), () => true);

        Loaded   += (_, _) => Viewport.Focus();
        Unloaded += (_, _) => Viewport.StopCursorBlink();
    }

    // -----------------------------------------------------------------------
    // IDocumentEditor — State
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public bool IsDirty    => _vm.IsDirty;

    /// <inheritdoc/>
    public bool CanUndo    => _vm.CanUndo;

    /// <inheritdoc/>
    public bool CanRedo    => _vm.CanRedo;

    /// <inheritdoc/>
    public bool IsBusy     { get; private set; }

    /// <inheritdoc/>
    public string Title    => _vm.Title;

    /// <inheritdoc/>
    public bool IsReadOnly
    {
        get => _vm.IsReadOnly;
        set => _vm.IsReadOnly = value;
    }

    // -----------------------------------------------------------------------
    // IDocumentEditor — Commands
    // -----------------------------------------------------------------------

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

    // -----------------------------------------------------------------------
    // IDocumentEditor — Events
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public event EventHandler? ModifiedChanged;

    /// <inheritdoc/>
    public event EventHandler? CanUndoChanged;

    /// <inheritdoc/>
    public event EventHandler? CanRedoChanged;

    /// <inheritdoc/>
    public event EventHandler<string>? TitleChanged;

    /// <inheritdoc/>
    public event EventHandler<string>? StatusMessage;
    /// <inheritdoc/>
    public event EventHandler<string>? OutputMessage;

    /// <inheritdoc/>
    public event EventHandler? SelectionChanged;

    /// <inheritdoc/>
    public event EventHandler<DocumentOperationEventArgs>? OperationStarted;

    /// <inheritdoc/>
    public event EventHandler<DocumentOperationEventArgs>? OperationProgress;

    /// <inheritdoc/>
    public event EventHandler<DocumentOperationCompletedEventArgs>? OperationCompleted;

    // -----------------------------------------------------------------------
    // IDocumentEditor — Methods
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public void Undo()
    {
        _vm.Undo();
        RefreshCommands();
    }

    /// <inheritdoc/>
    public void Redo()
    {
        _vm.Redo();
        RefreshCommands();
    }

    /// <inheritdoc/>
    public void Save()
    {
        if (string.IsNullOrEmpty(_vm.FilePath)) return;
        _vm.SaveFileAsync(_vm.FilePath).GetAwaiter().GetResult();
        StatusMessage?.Invoke(this, $"Saved: {Path.GetFileName(_vm.FilePath)}");
    }

    /// <inheritdoc/>
    public async Task SaveAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_vm.FilePath)) return;
        await _vm.SaveFileAsync(_vm.FilePath, ct);
        StatusMessage?.Invoke(this, $"Saved: {Path.GetFileName(_vm.FilePath)}");
    }

    /// <inheritdoc/>
    public async Task SaveAsAsync(string filePath, CancellationToken ct = default)
    {
        await _vm.SaveFileAsync(filePath, ct);
        StatusMessage?.Invoke(this, $"Saved: {Path.GetFileName(filePath)}");
    }

    /// <inheritdoc/>
    public void Copy()
    {
        var text = _vm.GetSelectedText();
        if (!string.IsNullOrEmpty(text)) Clipboard.SetText(text);
    }

    /// <inheritdoc/>
    public void Cut()
    {
        Copy();
        if (_vm.HasSelection) _vm.ClearSelection();
    }

    /// <inheritdoc/>
    public void Paste()
    {
        if (!IsReadOnly && Clipboard.ContainsText())
        {
            var text = Clipboard.GetText();
            foreach (var c in text) _vm.InsertChar(c);
        }
    }

    /// <inheritdoc/>
    public void Delete()
    {
        if (!IsReadOnly) _vm.DeleteForward();
    }

    /// <inheritdoc/>
    public void SelectAll()
    {
        // Simple full selection: from start of doc to end.
        _vm.SelectionAnchorLine   = 0;
        _vm.SelectionAnchorColumn = 0;
        _vm.CaretLine   = Math.Max(0, _vm.LineCount - 1);
        _vm.CaretColumn = _vm.GetLine(_vm.CaretLine).Length;
    }

    /// <inheritdoc/>
    public void Close()
    {
        Viewport.Detach();
        _vm.PropertyChanged -= OnVmPropertyChanged;
        _cts?.Cancel();
        _cts?.Dispose();
    }

    /// <inheritdoc/>
    public void CancelOperation()
    {
        _cts?.Cancel();
    }

    // -----------------------------------------------------------------------
    // IOpenableDocument
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public async Task OpenAsync(string filePath, CancellationToken ct = default)
    {
        IsBusy = true;
        OperationStarted?.Invoke(this, new DocumentOperationEventArgs
        {
            Title = "Opening", Message = Path.GetFileName(filePath), IsIndeterminate = true
        });

        try
        {
            await _vm.LoadFileAsync(filePath, null, ct);
            UpdateStatusBar();
            StatusMessage?.Invoke(this, $"Opened: {Path.GetFileName(filePath)}");
            OperationCompleted?.Invoke(this, new DocumentOperationCompletedEventArgs { Success = true });
        }
        catch (OperationCanceledException)
        {
            OperationCompleted?.Invoke(this, new DocumentOperationCompletedEventArgs { WasCancelled = true });
        }
        catch (Exception ex)
        {
            OperationCompleted?.Invoke(this, new DocumentOperationCompletedEventArgs { ErrorMessage = ex.Message });
        }
        finally
        {
            IsBusy = false;
        }
    }

    // -----------------------------------------------------------------------
    // Public extras
    // -----------------------------------------------------------------------

    /// <summary>
    /// Sets the active syntax definition (overrides auto-detection).
    /// </summary>
    public void SetSyntaxDefinition(SyntaxDefinition? def)
    {
        _vm.SyntaxDefinition = def;
        LanguageText.Text = def?.Name ?? "Plain Text";
    }

    /// <summary>
    /// Sets the text encoding (UTF-8, Latin-1, Shift-JIS, …).
    /// </summary>
    public void SetEncoding(Encoding enc)
    {
        _vm.Encoding = enc;
        EncodingText.Text = enc.WebName.ToUpperInvariant();
    }

    /// <summary>
    /// Loads a raw text string into the editor.
    /// </summary>
    public void SetText(string text) => _vm.SetText(text);

    /// <summary>
    /// Returns the full document text.
    /// </summary>
    public string GetText() => _vm.GetText();

    /// <summary>
    /// Moves the caret to the given 1-based line and column and scrolls it into view.
    /// </summary>
    public void GoToLine(int line, int column = 1)
    {
        // ViewModel uses 0-based indices; DTO / public API is 1-based
        _vm.CaretLine   = Math.Max(0, line   - 1);
        _vm.CaretColumn = Math.Max(0, column - 1);
    }

    // -----------------------------------------------------------------------
    // IEditorPersistable
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public EditorConfigDto GetEditorConfig() => new()
    {
        // Text-editor specific fields
        SyntaxLanguageId  = _vm.SyntaxDefinition?.Name,
        CaretLine         = _vm.CaretLine + 1,    // store 1-based
        CaretColumn       = _vm.CaretColumn + 1,
        FirstVisibleLine  = Viewport.FirstVisibleLine,
        // HexEditor fields are not applicable — leave at defaults
        SelectionStart    = -1,
    };

    /// <inheritdoc/>
    public void ApplyEditorConfig(EditorConfigDto config)
    {
        if (config is null) return;

        // Restore caret position (convert 1-based back to 0-based)
        if (config.CaretLine > 0)
        {
            _vm.CaretLine   = config.CaretLine - 1;
            _vm.CaretColumn = Math.Max(0, config.CaretColumn - 1);
        }

        // Restore scroll position after layout
        if (config.FirstVisibleLine > 0)
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () =>
            {
                if (Viewport.LineHeight > 0)
                    ScrollView.ScrollToVerticalOffset(config.FirstVisibleLine * Viewport.LineHeight);
            });
        }

        // Restore syntax language override
        if (!string.IsNullOrEmpty(config.SyntaxLanguageId))
        {
            var def = SyntaxDefinitionCatalog.Instance.FindByName(config.SyntaxLanguageId);
            if (def is not null) SetSyntaxDefinition(def);
        }
    }

    /// <inheritdoc/>
    /// <remarks>Text editors do not have binary unsaved modifications.</remarks>
    public byte[]? GetUnsavedModifications() => null;

    /// <inheritdoc/>
    /// <remarks>Not applicable for text editors.</remarks>
    public void ApplyUnsavedModifications(byte[] data) { }

    /// <inheritdoc/>
    /// <remarks>Text editors do not use binary bookmarks.</remarks>
    public IReadOnlyList<BookmarkDto>? GetBookmarks() => null;

    /// <inheritdoc/>
    public void ApplyBookmarks(IReadOnlyList<BookmarkDto> bookmarks) { }

    /// <inheritdoc/>
    /// <remarks>Text editors do not have binary changeset edits.</remarks>
    public ChangesetSnapshot GetChangesetSnapshot() => ChangesetSnapshot.Empty;

    /// <inheritdoc/>
    /// <remarks>Not applicable for text editors.</remarks>
    public void ApplyChangeset(ChangesetDto changeset) { }

    // -----------------------------------------------------------------------
    // Scroll sync
    // -----------------------------------------------------------------------

    private void ScrollView_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_vm is null || Viewport.LineHeight <= 0) return;

        int firstLine = (int)(e.VerticalOffset / Viewport.LineHeight);
        Viewport.FirstVisibleLine   = firstLine;
        Viewport.HorizontalOffset   = e.HorizontalOffset;

        // Adjust the viewport height to fill the scroll area
        Viewport.Width  = Math.Max(Viewport.EstimatedMaxWidth, ScrollView.ViewportWidth);
        Viewport.Height = Math.Max(Viewport.TotalHeight + Viewport.LineHeight, ScrollView.ViewportHeight);
    }

    // -----------------------------------------------------------------------
    // VM change handler
    // -----------------------------------------------------------------------

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        Dispatcher.InvokeAsync(() =>
        {
            switch (e.PropertyName)
            {
                case nameof(TextEditorViewModel.IsDirty):
                    ModifiedChanged?.Invoke(this, EventArgs.Empty);
                    TitleChanged?.Invoke(this, _vm.Title);
                    RefreshCommands();
                    break;
                case nameof(TextEditorViewModel.CanUndo):
                    CanUndoChanged?.Invoke(this, EventArgs.Empty);
                    RefreshCommands();
                    break;
                case nameof(TextEditorViewModel.CanRedo):
                    CanRedoChanged?.Invoke(this, EventArgs.Empty);
                    RefreshCommands();
                    break;
                case nameof(TextEditorViewModel.CaretStatus):
                    CaretText.Text = _vm.CaretStatus;
                    StatusMessage?.Invoke(this, _vm.CaretStatus);
                    break;
                case nameof(TextEditorViewModel.Title):
                    TitleChanged?.Invoke(this, _vm.Title);
                    break;
                case nameof(TextEditorViewModel.HasSelection):
                    SelectionChanged?.Invoke(this, EventArgs.Empty);
                    RefreshCommands();
                    break;
            }
        });
    }

    private void RefreshCommands()
    {
        (UndoCommand      as RelayCommand)?.RaiseCanExecuteChanged();
        (RedoCommand      as RelayCommand)?.RaiseCanExecuteChanged();
        (SaveCommand      as RelayCommand)?.RaiseCanExecuteChanged();
        (CopyCommand      as RelayCommand)?.RaiseCanExecuteChanged();
        (CutCommand       as RelayCommand)?.RaiseCanExecuteChanged();
        (PasteCommand     as RelayCommand)?.RaiseCanExecuteChanged();
        (DeleteCommand    as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void UpdateStatusBar()
    {
        LanguageText.Text  = _vm.SyntaxDefinition?.Name ?? "Plain Text";
        CaretText.Text     = _vm.CaretStatus;
        EncodingText.Text  = _vm.Encoding.WebName.ToUpperInvariant();
    }
}

// -----------------------------------------------------------------------
// Minimal RelayCommand (avoids external dependency)
// -----------------------------------------------------------------------

internal sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute    = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
    public void Execute(object? parameter)    => _execute();

    public event EventHandler? CanExecuteChanged;
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
