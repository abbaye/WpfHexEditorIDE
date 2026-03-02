//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfHexEditor.Decompiler.Core;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.Editor.DisassemblyViewer.Controls;

/// <summary>
/// Disassembly viewer — shows decompiled output from a registered <see cref="IDecompiler"/>.
/// Implements <see cref="IDocumentEditor"/> and <see cref="IOpenableDocument"/>.
/// </summary>
public sealed partial class DisassemblyViewer : UserControl, IDocumentEditor, IOpenableDocument
{
    private string _filePath = string.Empty;
    private IDecompiler? _decompiler;
    private CancellationTokenSource? _cts;

    public DisassemblyViewer()
    {
        InitializeComponent();

        UndoCommand      = new RelayCommand(() => { }, () => false);
        RedoCommand      = new RelayCommand(() => { }, () => false);
        SaveCommand      = new RelayCommand(() => { }, () => false);
        CopyCommand      = new RelayCommand(CopyAll, () => !string.IsNullOrEmpty(OutputBox.Text));
        CutCommand       = new RelayCommand(() => { }, () => false);
        PasteCommand     = new RelayCommand(() => { }, () => false);
        DeleteCommand    = new RelayCommand(() => { }, () => false);
        SelectAllCommand = new RelayCommand(() => OutputBox.SelectAll(), () => !string.IsNullOrEmpty(OutputBox.Text));
    }

    // ── IDocumentEditor — State ──────────────────────────────────────────

    public bool IsDirty    => false;
    public bool CanUndo    => false;
    public bool CanRedo    => false;
    public bool IsReadOnly { get => true; set { } }
    public string Title { get; private set; } = "";
    public bool IsBusy { get; private set; }

    // ── IDocumentEditor — Commands ───────────────────────────────────────

    public ICommand UndoCommand      { get; }
    public ICommand RedoCommand      { get; }
    public ICommand SaveCommand      { get; }
    public ICommand CopyCommand      { get; }
    public ICommand CutCommand       { get; }
    public ICommand PasteCommand     { get; }
    public ICommand DeleteCommand    { get; }
    public ICommand SelectAllCommand { get; }

    // ── IDocumentEditor — Events ─────────────────────────────────────────

#pragma warning disable CS0067
    public event EventHandler?         ModifiedChanged;
    public event EventHandler?         CanUndoChanged;
    public event EventHandler?         CanRedoChanged;
    public event EventHandler<string>? TitleChanged;
    public event EventHandler<string>? StatusMessage;
    public event EventHandler<string>? OutputMessage;
    public event EventHandler?         SelectionChanged;
    public event EventHandler<DocumentOperationEventArgs>?          OperationStarted;
    public event EventHandler<DocumentOperationEventArgs>?          OperationProgress;
    public event EventHandler<DocumentOperationCompletedEventArgs>? OperationCompleted;
#pragma warning restore CS0067

    // ── IDocumentEditor — Methods ────────────────────────────────────────

    public void Undo() { }
    public void Redo() { }
    public void Save() { }
    public Task SaveAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task SaveAsAsync(string filePath, CancellationToken ct = default) => Task.CompletedTask;
    public void Copy()      => CopyAll();
    public void Cut()       { }
    public void Paste()     { }
    public void Delete()    { }
    public void SelectAll() => OutputBox.SelectAll();
    public void CancelOperation() => _cts?.Cancel();

    public void Close()
    {
        _cts?.Cancel();
        _filePath   = string.Empty;
        _decompiler = null;
        ShowState(ViewerState.Empty);
    }

    // ── IOpenableDocument ────────────────────────────────────────────────

    public async Task OpenAsync(string filePath, CancellationToken ct = default)
    {
        // Cancel any previous decompilation
        _cts?.Cancel();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        _filePath = filePath;
        Title     = Path.GetFileName(filePath);
        TitleChanged?.Invoke(this, Title);

        _decompiler = DecompilerRegistry.All.FirstOrDefault(d => d.CanDecompile(filePath));

        if (_decompiler is null)
        {
            ShowState(ViewerState.NoDecompiler);
            StatusMessage?.Invoke(this, $"No decompiler registered for '{Path.GetExtension(filePath)}'");
            return;
        }

        await RunDecompileAsync(_cts.Token);
    }

    // ── Private ──────────────────────────────────────────────────────────

    private async Task RunDecompileAsync(CancellationToken ct)
    {
        if (_decompiler is null || string.IsNullOrEmpty(_filePath)) return;

        ShowState(ViewerState.Busy);
        OperationStarted?.Invoke(this, new DocumentOperationEventArgs { Title = "Decompiling…", IsIndeterminate = true });
        StatusMessage?.Invoke(this, $"Decompiling with {_decompiler.DisplayName}…");

        try
        {
            var output = await _decompiler.DecompileAsync(_filePath, ct);
            ct.ThrowIfCancellationRequested();

            OutputBox.Text = output;
            ArchLabel.Text = _decompiler.Architecture;
            ShowState(ViewerState.Output);
            StatusMessage?.Invoke(this, $"{_decompiler.DisplayName}  ·  {CountLines(output)} lines");
            OperationCompleted?.Invoke(this, new DocumentOperationCompletedEventArgs { Success = true });
        }
        catch (OperationCanceledException)
        {
            // Cancelled — leave current state as-is
        }
        catch (Exception ex)
        {
            OutputBox.Text = $"Decompilation failed:\n{ex.Message}";
            ShowState(ViewerState.Output);
            StatusMessage?.Invoke(this, $"Error: {ex.Message}");
            OperationCompleted?.Invoke(this, new DocumentOperationCompletedEventArgs { Success = false, ErrorMessage = ex.Message });
        }
    }

    private void ShowState(ViewerState state)
    {
        IsBusy = state == ViewerState.Busy;
        RefreshButton.IsEnabled = !IsBusy && !string.IsNullOrEmpty(_filePath);

        OutputBox.Visibility          = state == ViewerState.Output      ? Visibility.Visible : Visibility.Collapsed;
        BusyOverlay.Visibility        = state == ViewerState.Busy        ? Visibility.Visible : Visibility.Collapsed;
        NoDecompilerOverlay.Visibility = state == ViewerState.NoDecompiler ? Visibility.Visible : Visibility.Collapsed;

        if (state == ViewerState.NoDecompiler)
        {
            var ext = string.IsNullOrEmpty(_filePath) ? "" : Path.GetExtension(_filePath);
            NoDecompilerText.Text = string.IsNullOrEmpty(ext)
                ? "No decompiler registered."
                : $"No decompiler registered for '{ext}'.";
            ArchLabel.Text = "";
        }
        else if (state is ViewerState.Empty or ViewerState.Busy)
        {
            ArchLabel.Text = state == ViewerState.Busy && _decompiler is not null
                ? _decompiler.Architecture
                : "";
        }
    }

    private void CopyAll()
    {
        if (!string.IsNullOrEmpty(OutputBox.Text))
            Clipboard.SetText(OutputBox.Text);
    }

    private static int CountLines(string text)
        => text.Length == 0 ? 0 : text.Count(c => c == '\n') + 1;

    // ── Event handlers ───────────────────────────────────────────────────

    private async void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_filePath) || _decompiler is null) return;
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        await RunDecompileAsync(_cts.Token);
    }

    private void OnCopyAllClick(object sender, RoutedEventArgs e) => CopyAll();

    private enum ViewerState { Empty, Busy, Output, NoDecompiler }
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
