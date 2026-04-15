//////////////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project: WpfHexEditor.Editor.StructureEditor
// File: Controls/StructureEditor.xaml.cs
// Description: Interactive .whfmt editor — IDocumentEditor implementation.
//              Thin code-behind; all state lives in StructureEditorViewModel.
//////////////////////////////////////////////////////

using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfHexEditor.Editor.CodeEditor.Services;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.Core.Validation;
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

    // ── Constructor ───────────────────────────────────────────────────────────

    public StructureEditor()
    {
        InitializeComponent();

        UndoCommand      = new ViewModels.RelayCommand(() => { }, () => false);
        RedoCommand      = new ViewModels.RelayCommand(() => { }, () => false);
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

        // Validation bar
        ValBar.ItemsSource = _vm.ValidationSummary;

        // Dirty + validation tracking
        _vm.DirtyChanged        += OnVmDirtyChanged;
        _vm.ValidationCompleted += OnValidationCompleted;

        // Keyboard save shortcut
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
            {
                SaveFile();
                e.Handled = true;
            }
        };
    }

    // ── IDocumentEditor — State ───────────────────────────────────────────────

    public bool IsDirty    => _vm.IsDirty;
    public bool CanUndo    => false;
    public bool CanRedo    => false;
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

#pragma warning disable CS0067
    public event EventHandler?         CanUndoChanged;
    public event EventHandler?         CanRedoChanged;
    public event EventHandler<string>? OutputMessage;
    public event EventHandler?         SelectionChanged;
    public event EventHandler<DocumentOperationEventArgs>? OperationProgress;
#pragma warning restore CS0067

    // ── IDocumentEditor — Stubs ───────────────────────────────────────────────

    public void Undo()  { }
    public void Redo()  { }
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
        _vm.Reset();
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

    // ── Toolbar handlers ──────────────────────────────────────────────────────

    private void OnSaveClick(object sender, RoutedEventArgs e) => SaveFile();

    private void OnValidateClick(object sender, RoutedEventArgs e)
        => _vm.TriggerValidationNow();

    // ── Dirty tracking ────────────────────────────────────────────────────────

    private void OnVmDirtyChanged(object? sender, EventArgs e)
    {
        Dispatcher.InvokeAsync(() =>
        {
            var dirty = _vm.IsDirty;
            SaveBtn.IsEnabled        = dirty;
            DirtyIndicator.Visibility = dirty ? Visibility.Visible : Visibility.Collapsed;
            ModifiedChanged?.Invoke(this, EventArgs.Empty);
            TitleChanged?.Invoke(this, dirty ? $"{Title} *" : Title);

            // Show/hide validation bar
            ValBar.Visibility = _vm.ValidationSummary.Count > 0
                ? Visibility.Visible
                : Visibility.Collapsed;
            ValBar.ErrorCount   = _vm.ErrorCount;
            ValBar.WarningCount = _vm.WarningCount;
        });
    }

    private void OnValidationCompleted(object? sender, EventArgs e)
    {
        Dispatcher.InvokeAsync(() =>
        {
            ValBar.ItemsSource  = _vm.ValidationSummary;
            ValBar.ErrorCount   = _vm.ErrorCount;
            ValBar.WarningCount = _vm.WarningCount;
            ValBar.Visibility   = _vm.ValidationSummary.Count > 0
                ? Visibility.Visible
                : Visibility.Collapsed;

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
            SaveBtn.IsEnabled        = false;
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
}
