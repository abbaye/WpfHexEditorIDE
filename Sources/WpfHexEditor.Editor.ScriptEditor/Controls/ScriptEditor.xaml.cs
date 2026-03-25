//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.Editor.ScriptEditor.Controls;

/// <summary>
/// Script editor — opens C# script files (.csx, .cs) and provides F5 / Ctrl+F5
/// shortcuts to run or validate the script via <see cref="IScriptingService"/>.
/// Also supports game script formats (.scr, .msg, .evt, .script, .dec) in read/edit mode.
/// </summary>
public sealed partial class ScriptEditor : UserControl, IDocumentEditor, IOpenableDocument
{
    private string            _filePath      = string.Empty;
    private IScriptingService? _scripting;
    private CancellationTokenSource? _runCts;

    // ── Construction ──────────────────────────────────────────────────────────

    public ScriptEditor()
    {
        InitializeComponent();

        UndoCommand      = new RelayCommand(() => CodeBox.Undo(),  () => CodeBox.CanUndo);
        RedoCommand      = new RelayCommand(() => CodeBox.Redo(),  () => CodeBox.CanRedo);
        SaveCommand      = new RelayCommand(() => SaveFile(),      () => IsDirty);
        CopyCommand      = new RelayCommand(() => CodeBox.Copy(),  () => CodeBox.SelectionLength > 0);
        CutCommand       = new RelayCommand(() => CodeBox.Cut(),   () => CodeBox.SelectionLength > 0);
        PasteCommand     = new RelayCommand(() => CodeBox.Paste(), () => Clipboard.ContainsText());
        DeleteCommand    = new RelayCommand(() => CodeBox.SelectedText = string.Empty, () => CodeBox.SelectionLength > 0);
        SelectAllCommand = new RelayCommand(() => CodeBox.SelectAll());

        CodeBox.TextChanged += (_, _) => ModifiedChanged?.Invoke(this, EventArgs.Empty);
    }

    // ── Scripting injection ───────────────────────────────────────────────────

    /// <summary>
    /// Injects the scripting service. Called by the App layer after document creation.
    /// </summary>
    public void SetScriptingService(IScriptingService? service)
    {
        _scripting = service;
        UpdateHint();
    }

    // ── IDocumentEditor — State ───────────────────────────────────────────────

    public bool IsDirty    => CodeBox.CanUndo;
    public bool CanUndo    => CodeBox.CanUndo;
    public bool CanRedo    => CodeBox.CanRedo;
    public bool IsReadOnly { get => !CodeBox.IsEnabled; set => CodeBox.IsEnabled = !value; }
    public string Title { get; private set; } = "";
    public bool IsBusy { get; private set; }

    // ── IDocumentEditor — Commands ────────────────────────────────────────────

    public ICommand UndoCommand      { get; }
    public ICommand RedoCommand      { get; }
    public ICommand SaveCommand      { get; }
    public ICommand CopyCommand      { get; }
    public ICommand CutCommand       { get; }
    public ICommand PasteCommand     { get; }
    public ICommand DeleteCommand    { get; }
    public ICommand SelectAllCommand { get; }

    // ── IDocumentEditor — Events ──────────────────────────────────────────────

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

    // ── IDocumentEditor — Methods ─────────────────────────────────────────────

    public void Undo() => CodeBox.Undo();
    public void Redo() => CodeBox.Redo();

    public void Save()
    {
        if (!string.IsNullOrEmpty(_filePath))
            SaveFile();
    }

    public async Task SaveAsync(CancellationToken ct = default)
    {
        if (!string.IsNullOrEmpty(_filePath))
            await Task.Run(() => File.WriteAllText(_filePath, CodeBox.Text, Encoding.UTF8), ct)
                      .ConfigureAwait(false);
    }

    public async Task SaveAsAsync(string filePath, CancellationToken ct = default)
    {
        _filePath = filePath;
        await SaveAsync(ct).ConfigureAwait(false);
        Title = Path.GetFileName(filePath);
        TitleChanged?.Invoke(this, Title);
    }

    public void Copy()       => CodeBox.Copy();
    public void Cut()        => CodeBox.Cut();
    public void Paste()      => CodeBox.Paste();
    public void Delete()     => CodeBox.SelectedText = string.Empty;
    public void SelectAll()  => CodeBox.SelectAll();
    public void Close()      { _runCts?.Cancel(); }
    public void CancelOperation() { _runCts?.Cancel(); }

    // ── IOpenableDocument ─────────────────────────────────────────────────────

    public async Task OpenAsync(string filePath, CancellationToken ct = default)
    {
        _filePath = filePath;
        Title = Path.GetFileName(filePath);
        TitleChanged?.Invoke(this, Title);

        try
        {
            var text = await Task.Run(() => File.ReadAllText(filePath, Encoding.UTF8), ct)
                                 .ConfigureAwait(false);
            Dispatcher.Invoke(() =>
            {
                CodeBox.Text = text;
                CodeBox.ScrollToHome();
            });
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() => OutputBox.Text = $"[Error loading file] {ex.Message}");
        }

        OperationCompleted?.Invoke(this, new DocumentOperationCompletedEventArgs { Success = true });
    }

    // ── Keyboard shortcuts ────────────────────────────────────────────────────

    private void OnCodeBoxKeyDown(object sender, KeyEventArgs e)
    {
        // F5 — Run script
        if (e.Key == Key.F5 && Keyboard.Modifiers == ModifierKeys.None)
        {
            _ = RunScriptAsync(validate: false);
            e.Handled = true;
        }
        // Ctrl+F5 — Validate only
        else if (e.Key == Key.F5 && Keyboard.Modifiers == ModifierKeys.Control)
        {
            _ = RunScriptAsync(validate: true);
            e.Handled = true;
        }
        // Escape — Cancel running script
        else if (e.Key == Key.Escape)
        {
            _runCts?.Cancel();
            e.Handled = true;
        }
    }

    // ── Script execution ──────────────────────────────────────────────────────

    private async Task RunScriptAsync(bool validate)
    {
        if (_scripting is null)
        {
            OutputBox.Text = "Scripting engine not available. Ensure WpfHexEditor.Core.Scripting is loaded.";
            return;
        }

        if (IsBusy) return;

        var code = CodeBox.Text;
        if (string.IsNullOrWhiteSpace(code)) return;

        IsBusy = true;
        _runCts = new CancellationTokenSource();
        var label = validate ? "Validating" : "Running";
        StatusBar.Text  = $"{label}…";
        OutputBox.Text  = string.Empty;

        try
        {
            var result = validate
                ? await _scripting.ValidateAsync(code, _runCts.Token).ConfigureAwait(false)
                : await _scripting.RunAsync(code, _runCts.Token).ConfigureAwait(false);

            await Dispatcher.InvokeAsync(() =>
            {
                var sb = new StringBuilder();

                if (!string.IsNullOrEmpty(result.Output))
                    sb.AppendLine(result.Output);

                foreach (var d in result.Diagnostics)
                {
                    var prefix = d.IsWarning ? "⚠" : "✗";
                    sb.AppendLine($"{prefix} ({d.Line},{d.Column}): {d.Message}");
                }

                OutputBox.Text = sb.ToString().TrimEnd();
                OutputBox.ScrollToEnd();

                StatusBar.Text = result.Success
                    ? $"{(validate ? "Valid" : "Done")} — {result.Duration.TotalMilliseconds:F0} ms"
                    : $"Failed — {result.Diagnostics.Count(x => !x.IsWarning)} error(s)";
            });
        }
        catch (OperationCanceledException)
        {
            await Dispatcher.InvokeAsync(() => StatusBar.Text = "Cancelled.");
        }
        catch (Exception ex)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                OutputBox.Text = $"✗ Unexpected error: {ex.Message}";
                StatusBar.Text = "Error.";
            });
        }
        finally
        {
            _runCts?.Dispose();
            _runCts = null;
            IsBusy  = false;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SaveFile()
    {
        try
        {
            File.WriteAllText(_filePath, CodeBox.Text, Encoding.UTF8);
            StatusBar.Text = "Saved.";
        }
        catch (Exception ex)
        {
            StatusBar.Text = $"Save failed: {ex.Message}";
        }
    }

    private void UpdateHint()
    {
        if (ScriptLabel is null) return;
        ScriptLabel.Text = _scripting is not null
            ? "Press F5 to run · Ctrl+F5 to validate"
            : "Scripting engine not available";
    }
}

// ── Minimal RelayCommand ──────────────────────────────────────────────────────

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
