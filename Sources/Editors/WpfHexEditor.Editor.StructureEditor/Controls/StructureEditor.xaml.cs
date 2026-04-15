//////////////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Project: WpfHexEditor.Editor.StructureEditor
// File: Controls/StructureEditor.xaml.cs
// Description: Interactive .whfmt editor — IDocumentEditor implementation.
//              Thin code-behind; all state lives in StructureEditorViewModel.
//////////////////////////////////////////////////////

using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using WpfHexEditor.Editor.CodeEditor.Controls;
using WpfHexEditor.Editor.CodeEditor.Services;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.Core.Validation;
using WpfHexEditor.Editor.StructureEditor.Services;
using WpfHexEditor.Editor.StructureEditor.ViewModels;

namespace WpfHexEditor.Editor.StructureEditor.Controls;

/// <summary>
/// Interactive editor for <c>.whfmt</c> format definition files.
/// Implements <see cref="IDocumentEditor"/> and <see cref="IOpenableDocument"/>.
/// </summary>
public sealed partial class StructureEditor : UserControl, IDocumentEditor, IOpenableDocument
{
    // ── State ─────────────────────────────────────────────────────────────────

    private readonly StructureEditorViewModel _vm = new();
    private readonly FormatSchemaValidator    _schemaValidator = new();
    private string _filePath = string.Empty;

    // ── Live code view ────────────────────────────────────────────────────────

    private readonly CodeEditorSplitHost _codeView  = new();
    private LiveWhfmtBuffer?             _liveBuffer;
    private DispatcherTimer?             _refreshTimer;

    // ── Constructor ───────────────────────────────────────────────────────────

    public StructureEditor()
    {
        InitializeComponent();

        UndoCommand      = new ViewModels.RelayCommand(() => _vm.Undo(), () => _vm.UndoRedo.CanUndo);
        RedoCommand      = new ViewModels.RelayCommand(() => _vm.Redo(), () => _vm.UndoRedo.CanRedo);
        SaveCommand      = new ViewModels.RelayCommand(SaveFile, () => _vm.IsDirty);
        CopyCommand      = new ViewModels.RelayCommand(() => { }, () => false);
        CutCommand       = new ViewModels.RelayCommand(() => { }, () => false);
        PasteCommand     = new ViewModels.RelayCommand(() => { }, () => false);
        DeleteCommand    = new ViewModels.RelayCommand(() => { }, () => false);
        SelectAllCommand = new ViewModels.RelayCommand(() => { }, () => false);

        // Wire FormatSchemaValidator (4-layer: JSON syntax, schema, rules, semantic)
        _vm.SetValidator(async json =>
        {
            var errors = await Task.Run(() => _schemaValidator.Validate(json));
            return errors.Select(e => new ValidationSummaryItem
            {
                Severity = (ValidationSeverity)(int)e.Severity,
                Message  = e.Message,
                Line     = e.Line,
                Column   = e.Column,
                Layer    = e.Layer.ToString(),
            }).ToList();
        });

        // Bind child tabs through DataContext
        MetadataTabCtrl.DataContext  = _vm.Metadata;
        DetectionTabCtrl.DataContext = _vm.Detection;
        BlocksTabCtrl.DataContext    = _vm.Blocks;
        VariablesTabCtrl.DataContext = _vm.Variables;
        V2TabCtrl.DataContext        = _vm;
        QualityTabCtrl.DataContext   = _vm.QualityMetrics;

        // Dirty + validation tracking
        _vm.DirtyChanged        += OnVmDirtyChanged;
        _vm.ValidationCompleted += OnValidationCompleted;

        // Undo/redo state tracking
        _vm.UndoRedo.StateChanged += (_, _) =>
        {
            CanUndoChanged?.Invoke(this, EventArgs.Empty);
            CanRedoChanged?.Invoke(this, EventArgs.Empty);
            UpdateToolbarState();
            SyncPopToolbarState();
        };

        // IDE contributors
        InitToolbarItems();
        InitStatusBarItems();

        // Tab switch → status bar + pop-toolbar context update + code view navigation
        MainTabs.SelectionChanged += (_, _) =>
        {
            RefreshStatusBarItems();
            UpdateToolbarState();
            PopToolbar.SetBlockOperationsVisible(MainTabs.SelectedIndex == 2);
            if (MainTabs.SelectedIndex == 5) // Quality tab
                _vm.QualityMetrics.Refresh(_vm.Blocks, _vm.Variables);

            // Navigate live code view to the selected tab's JSON section root
            var key = MainTabs.SelectedIndex switch
            {
                0 => "\"formatName\"",
                1 => "\"detection\"",
                2 => "\"blocks\"",
                3 => "\"variables\"",
                4 => "\"assertions\"",
                5 => "\"qualityMetrics\"",
                _ => (string?)null,
            };
            if (key is not null) NavigateCodeViewTo(key);
        };

        // Block selection → navigate code view to the block's JSON entry
        _vm.Blocks.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(BlocksViewModel.SelectedBlock)
                && _vm.Blocks.SelectedBlock is { } blk)
                NavigateCodeViewTo($"\"name\": \"{blk.Name}\"");
        };

        // Pop-toolbar events
        PopToolbar.SaveRequested           += (_, _) => SaveFile();
        PopToolbar.ValidateRequested       += (_, _) => _vm.TriggerValidationNow();
        PopToolbar.UndoRequested           += (_, _) => _vm.Undo();
        PopToolbar.RedoRequested           += (_, _) => _vm.Redo();
        PopToolbar.AddBlockRequested       += (_, _) => BlocksTabCtrl.RequestAddBlock();
        PopToolbar.DuplicateRequested      += (_, _) => _vm.Blocks.DuplicateCommand.Execute(null);
        PopToolbar.ToggleCodeViewRequested += (_, _) => ToggleCodeView();

        // Preload schema for tooltips
        WhfmtSchemaProvider.Instance.EnsureLoaded();

        // Keyboard shortcuts
        InputBindings.Add(new KeyBinding(SaveCommand, Key.S, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(UndoCommand, Key.Z, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(RedoCommand, Key.Y, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(
            new ViewModels.RelayCommand(() => _vm.TriggerValidationNow()),
            Key.V, ModifierKeys.Control | ModifierKeys.Shift));

        // Live code view — debounce timer (400 ms)
        _refreshTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(400),
        };
        _refreshTimer.Tick += (_, _) =>
        {
            _refreshTimer.Stop();
            PushJsonToCodeView();
        };

        // Insert CodeEditorSplitHost into the XAML placeholder border
        CodeViewBorder.Child = _codeView;

        // Restart refresh timer on every content change (not just dirty transitions)
        _vm.ContentChanged += (_, _) =>
        {
            _refreshTimer?.Stop();
            _refreshTimer?.Start();
        };
    }

    // ── IDocumentEditor — State ───────────────────────────────────────────────

    public bool IsDirty    => _vm.IsDirty;
    public bool CanUndo    => _vm.UndoRedo.CanUndo;
    public bool CanRedo    => _vm.UndoRedo.CanRedo;
    public bool IsReadOnly { get => false; set { } }
    public string Title    { get; private set; } = "";
    public bool IsBusy     { get; private set; }

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

    public event EventHandler?         ModifiedChanged;
    public event EventHandler<string>? TitleChanged;
    public event EventHandler<string>? StatusMessage;
    public event EventHandler<DocumentOperationEventArgs>?          OperationStarted;
    public event EventHandler<DocumentOperationCompletedEventArgs>? OperationCompleted;

    public event EventHandler?         CanUndoChanged;
    public event EventHandler?         CanRedoChanged;
#pragma warning disable CS0067
    public event EventHandler<string>? OutputMessage;
    public event EventHandler?         SelectionChanged;
    public event EventHandler<DocumentOperationEventArgs>? OperationProgress;
#pragma warning restore CS0067

    // ── IDocumentEditor — Methods ─────────────────────────────────────────────

    public void Undo()  => _vm.Undo();
    public void Redo()  => _vm.Redo();
    public void Copy()  { }
    public void Cut()   { }
    public void Paste() { }
    public void Delete() { }
    public void SelectAll() { }
    public void CancelOperation() { }

    public void Save() => SaveFile();

    public Task SaveAsync(CancellationToken ct = default)
    {
        SaveFile();
        return Task.CompletedTask;
    }

    public Task SaveAsAsync(string filePath, CancellationToken ct = default)
    {
        _filePath = filePath;
        SaveFile();
        return Task.CompletedTask;
    }

    public void Close()
    {
        _refreshTimer?.Stop();
        _codeView.DetachBuffer();
        _liveBuffer = null;
        _vm.Reset();
        ClearDiagnostics();
    }

    // ── IOpenableDocument ─────────────────────────────────────────────────────

    public async Task OpenAsync(string filePath, CancellationToken ct = default)
    {
        IsBusy    = true;
        _filePath = filePath;
        Title     = Path.GetFileName(filePath);
        TitleChanged?.Invoke(this, Title);
        OperationStarted?.Invoke(this, new DocumentOperationEventArgs { Message = $"Loading {Title}…" });

        try
        {
            var json = await File.ReadAllTextAsync(filePath, ct);
            await Dispatcher.InvokeAsync(() =>
            {
                _vm.LoadFromJson(json);
                SetStatus($"Loaded — {_vm.Blocks.BlockTree.Count} block(s)");

                // (Re)create the live buffer for the new file path and attach it
                _liveBuffer = new LiveWhfmtBuffer(filePath);
                _codeView.AttachBuffer(_liveBuffer);
                _codeView.IsReadOnly = true;
                PushJsonToCodeView();
            });

            OperationCompleted?.Invoke(this, new DocumentOperationCompletedEventArgs { Success = true });
        }
        catch (Exception ex)
        {
            StatusMessage?.Invoke(this, $"Error loading {Title}: {ex.Message}");
            OperationCompleted?.Invoke(this, new DocumentOperationCompletedEventArgs
            {
                Success      = false,
                ErrorMessage = ex.Message,
            });
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ── Pop-toolbar ───────────────────────────────────────────────────────────

    private void OnPopTriggerMouseEnter(object sender, MouseEventArgs e)
    {
        SyncPopToolbarState();
        PopToolbarPopup.IsOpen = true;
    }

    private void OnPopToolbarMouseLeave(object sender, MouseEventArgs e)
    {
        PopToolbarPopup.IsOpen = false;
    }

    private void SyncPopToolbarState()
    {
        PopToolbar.UpdateButtonStates(
            canSave: _vm.IsDirty,
            canUndo: _vm.UndoRedo.CanUndo,
            canRedo: _vm.UndoRedo.CanRedo,
            canDuplicate: _vm.Blocks.SelectedBlock is not null);
    }

    // ── Dirty tracking ────────────────────────────────────────────────────────

    private void OnVmDirtyChanged(object? sender, EventArgs e)
    {
        Dispatcher.InvokeAsync(() =>
        {
            var dirty = _vm.IsDirty;
            DirtyIndicator.Visibility = dirty ? Visibility.Visible : Visibility.Collapsed;
            ModifiedChanged?.Invoke(this, EventArgs.Empty);
            TitleChanged?.Invoke(this, dirty ? $"{Title} *" : Title);

            UpdateToolbarState();
            UpdateDirtyStatus();
            SyncPopToolbarState();
        });
    }

    private void OnValidationCompleted(object? sender, EventArgs e)
    {
        Dispatcher.InvokeAsync(() =>
        {
            UpdateValidationStatus();
            PublishDiagnostics();

            var summary = _vm.ErrorCount > 0
                ? $"{_vm.ErrorCount} error(s), {_vm.WarningCount} warning(s)"
                : _vm.WarningCount > 0
                    ? $"{_vm.WarningCount} warning(s)"
                    : "Validation passed";
            SetStatus(summary);
            StatusMessage?.Invoke(this, summary);
        });
    }

    // ── Save ──────────────────────────────────────────────────────────────────

    private void SaveFile()
    {
        if (string.IsNullOrEmpty(_filePath)) return;

        try
        {
            var json = _vm.SerializeToJson();
            File.WriteAllText(_filePath, json);

            _vm.ClearDirty();
            DirtyIndicator.Visibility = Visibility.Collapsed;
            ModifiedChanged?.Invoke(this, EventArgs.Empty);
            TitleChanged?.Invoke(this, Title);
            SetStatus("Saved.");
            StatusMessage?.Invoke(this, $"Saved: {Title}");
        }
        catch (Exception ex)
        {
            SetStatus($"Save failed: {ex.Message}");
            StatusMessage?.Invoke(this, $"Save failed: {ex.Message}");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SetStatus(string msg) => StatusText.Text = msg;

    private void PushJsonToCodeView()
    {
        if (_liveBuffer is null) return;
        _liveBuffer.SetText(_vm.SerializeToJson());
    }

    // ── Live code view navigation ─────────────────────────────────────────────

    /// <summary>
    /// Finds the 1-based line number of the first occurrence of <paramref name="searchText"/>
    /// in the current live buffer. Returns 1 if not found.
    /// </summary>
    private int FindLineInJson(string searchText)
    {
        var text = _liveBuffer?.Text;
        if (string.IsNullOrEmpty(text)) return 1;
        var idx = text.IndexOf(searchText, StringComparison.Ordinal);
        if (idx < 0) return 1;
        return text[..idx].Count(c => c == '\n') + 1;
    }

    /// <summary>
    /// Scrolls the live code view to the line containing <paramref name="searchText"/>.
    /// No-op when the code view is hidden or the buffer is not yet attached.
    /// </summary>
    private void NavigateCodeViewTo(string searchText)
    {
        if (_liveBuffer is null || !_codeViewVisible) return;
        var line = FindLineInJson(searchText);
        ((INavigableDocument)_codeView).NavigateTo(line, 1);
    }
}
