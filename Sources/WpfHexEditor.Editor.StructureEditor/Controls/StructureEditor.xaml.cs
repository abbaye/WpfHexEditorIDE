//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Core.FormatDetection;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.Editor.StructureEditor.Controls;

/// <summary>
/// Visual editor for <c>.whfmt</c> format definition files.
/// Implements <see cref="IDocumentEditor"/> and <see cref="IOpenableDocument"/>.
/// </summary>
public sealed partial class StructureEditor : UserControl, IDocumentEditor, IOpenableDocument
{
    // -- State ----------------------------------------------------------------

    private string            _filePath   = string.Empty;
    private FormatDefinition? _definition;
    private bool              _isDirty;
    private bool              _suppressDescriptionChange;

    private readonly ObservableCollection<BlockViewModel> _blocks = [];

    // -- JSON options ---------------------------------------------------------

    private static readonly JsonSerializerOptions LoadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling         = JsonCommentHandling.Skip,
        AllowTrailingCommas         = true,
    };

    private static readonly JsonSerializerOptions SaveOptions = new()
    {
        WriteIndented          = true,
        PropertyNamingPolicy   = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // -- Constructor ----------------------------------------------------------

    /// <summary>Creates a new <see cref="StructureEditor"/>.</summary>
    public StructureEditor()
    {
        InitializeComponent();

        UndoCommand      = new RelayCommand(() => { }, () => false);
        RedoCommand      = new RelayCommand(() => { }, () => false);
        SaveCommand      = new RelayCommand(SaveFile, () => _isDirty);
        CopyCommand      = new RelayCommand(() => { }, () => false);
        CutCommand       = new RelayCommand(() => { }, () => false);
        PasteCommand     = new RelayCommand(() => { }, () => false);
        DeleteCommand    = new RelayCommand(() => { }, () => false);
        SelectAllCommand = new RelayCommand(() => { }, () => false);

        BlocksGrid.ItemsSource = _blocks;
        _blocks.CollectionChanged += (_, _) => MarkDirty();

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
            {
                SaveFile();
                e.Handled = true;
            }
        };
    }

    // -- IDocumentEditor — State ----------------------------------------------

    /// <inheritdoc/>
    public bool IsDirty => _isDirty;

    /// <inheritdoc/>
    public bool CanUndo => false;

    /// <inheritdoc/>
    public bool CanRedo => false;

    /// <inheritdoc/>
    public bool IsReadOnly { get => false; set { } }

    /// <inheritdoc/>
    public string Title { get; private set; } = "";

    /// <inheritdoc/>
    public bool IsBusy { get; private set; }

    // -- IDocumentEditor — Commands -------------------------------------------

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

    // -- IDocumentEditor — Events ---------------------------------------------

    /// <inheritdoc/>
    public event EventHandler?         ModifiedChanged;

    /// <inheritdoc/>
    public event EventHandler<string>? TitleChanged;

    /// <inheritdoc/>
    public event EventHandler<string>? StatusMessage;

    /// <inheritdoc/>
    public event EventHandler<DocumentOperationEventArgs>?          OperationStarted;

    /// <inheritdoc/>
    public event EventHandler<DocumentOperationCompletedEventArgs>? OperationCompleted;

#pragma warning disable CS0067
    // Interface-required events not used internally (undo/redo/output not supported in V1)
    /// <inheritdoc/>
    public event EventHandler?         CanUndoChanged;

    /// <inheritdoc/>
    public event EventHandler?         CanRedoChanged;

    /// <inheritdoc/>
    public event EventHandler<string>? OutputMessage;

    /// <inheritdoc/>
    public event EventHandler?         SelectionChanged;

    /// <inheritdoc/>
    public event EventHandler<DocumentOperationEventArgs>? OperationProgress;
#pragma warning restore CS0067

    // -- IDocumentEditor — Editing stubs --------------------------------------

    /// <inheritdoc/>
    public void Undo() { }

    /// <inheritdoc/>
    public void Redo() { }

    /// <inheritdoc/>
    public void Save() => SaveFile();

    /// <inheritdoc/>
    public Task SaveAsync(CancellationToken ct = default)
    {
        SaveFile();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task SaveAsAsync(string filePath, CancellationToken ct = default)
    {
        _filePath = filePath;
        SaveFile();
        return Task.CompletedTask;
    }

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
    public void Close()
    {
        _definition = null;
        _blocks.Clear();
    }

    /// <inheritdoc/>
    public void CancelOperation() { }

    // -- IOpenableDocument ----------------------------------------------------

    /// <inheritdoc/>
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
            _definition = JsonSerializer.Deserialize<FormatDefinition>(json, LoadOptions);

            await Dispatcher.InvokeAsync(() => PopulateFromDefinition());

            StatusMessage?.Invoke(this, $"Loaded: {Title} — {_blocks.Count} block(s)");
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

    // -- Private — load --------------------------------------------------------

    private void PopulateFromDefinition()
    {
        if (_definition is null) return;

        // Header Expander
        HdrName.Text        = _definition.FormatName ?? "";
        HdrCategory.Text    = _definition.Category   ?? "";
        HdrExtensions.Text  = _definition.Extensions is { Count: > 0 }
                              ? string.Join(", ", _definition.Extensions)
                              : "";
        HdrVersion.Text     = _definition.Version    ?? "";
        HdrDescription.Text = _definition.Description ?? "";

        // Blocks DataGrid
        _blocks.Clear();
        if (_definition.Blocks is not null)
        {
            foreach (var block in _definition.Blocks)
            {
                var vm = new BlockViewModel(block);
                vm.Changed += (_, _) => MarkDirty();
                _blocks.Add(vm);
            }
        }
        RenumberRows();

        _isDirty = false;
        UpdateToolbarState(null);
        SetStatus($"{_blocks.Count} block(s)");
    }

    // -- Private — save --------------------------------------------------------

    private void SaveFile()
    {
        if (string.IsNullOrEmpty(_filePath) || _definition is null) return;

        // Commit any in-progress DataGrid edit
        BlocksGrid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);

        // Rebuild Blocks list from ViewModels
        _definition.Blocks = [.._blocks.Select(vm => vm.Source)];

        try
        {
            var json = JsonSerializer.Serialize(_definition, SaveOptions);
            File.WriteAllText(_filePath, json);

            _isDirty = false;
            ModifiedChanged?.Invoke(this, EventArgs.Empty);
            SaveBtn.IsEnabled = false;
            SetStatus("Saved.");
            StatusMessage?.Invoke(this, $"Saved: {Title}");
        }
        catch (Exception ex)
        {
            SetStatus($"Save failed: {ex.Message}");
            StatusMessage?.Invoke(this, $"Save failed: {ex.Message}");
        }
    }

    // -- Toolbar handlers ------------------------------------------------------

    private void OnAddBlock(object sender, RoutedEventArgs e)
    {
        var block = new BlockDefinition { Type = "field", Name = "NewField" };
        var vm    = new BlockViewModel(block);
        vm.Changed += (_, _) => MarkDirty();
        _blocks.Add(vm);
        RenumberRows();

        BlocksGrid.SelectedItem   = vm;
        BlocksGrid.ScrollIntoView(vm);
        MarkDirty();
    }

    private void OnDeleteBlock(object sender, RoutedEventArgs e)
    {
        if (BlocksGrid.SelectedItem is not BlockViewModel vm) return;
        _blocks.Remove(vm);
        RenumberRows();
        MarkDirty();
    }

    private void OnMoveUp(object sender, RoutedEventArgs e)
    {
        if (BlocksGrid.SelectedItem is not BlockViewModel vm) return;
        var idx = _blocks.IndexOf(vm);
        if (idx <= 0) return;
        _blocks.Move(idx, idx - 1);
        RenumberRows();
        MarkDirty();
    }

    private void OnMoveDown(object sender, RoutedEventArgs e)
    {
        if (BlocksGrid.SelectedItem is not BlockViewModel vm) return;
        var idx = _blocks.IndexOf(vm);
        if (idx < 0 || idx >= _blocks.Count - 1) return;
        _blocks.Move(idx, idx + 1);
        RenumberRows();
        MarkDirty();
    }

    private void OnSaveClick(object sender, RoutedEventArgs e) => SaveFile();

    // -- DataGrid / Description handlers --------------------------------------

    private void OnBlockSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var vm = BlocksGrid.SelectedItem as BlockViewModel;
        UpdateToolbarState(vm);

        _suppressDescriptionChange = true;
        try
        {
            DescriptionEditor.Text = vm?.Description ?? "";
            DescriptionEditor.IsEnabled = vm is not null;
        }
        finally
        {
            _suppressDescriptionChange = false;
        }

        if (vm is not null)
            SetStatus($"Block {vm.RowIndex}: {vm.Type} '{vm.Name}'");
    }

    private void OnDescriptionChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressDescriptionChange) return;
        if (BlocksGrid.SelectedItem is not BlockViewModel vm) return;

        vm.Description = DescriptionEditor.Text;
        MarkDirty();
    }

    // -- Helpers ---------------------------------------------------------------

    private void RenumberRows()
    {
        for (var i = 0; i < _blocks.Count; i++)
            _blocks[i].RowIndex = i + 1;
    }

    private void MarkDirty()
    {
        if (_isDirty) return;
        _isDirty = true;
        SaveBtn.IsEnabled = true;
        ModifiedChanged?.Invoke(this, EventArgs.Empty);
        TitleChanged?.Invoke(this, $"{Title} *");
    }

    private void UpdateToolbarState(BlockViewModel? vm)
    {
        var selected = vm is not null;
        DeleteBtn.IsEnabled  = selected;
        MoveUpBtn.IsEnabled  = selected && _blocks.IndexOf(vm!) > 0;
        MoveDownBtn.IsEnabled = selected && _blocks.IndexOf(vm!) < _blocks.Count - 1;
    }

    private void SetStatus(string msg) => StatusText.Text = msg;
}

// -- BlockViewModel ------------------------------------------------------------

internal sealed class BlockViewModel : INotifyPropertyChanged
{
    private readonly BlockDefinition _block;

    internal event EventHandler? Changed;

    public BlockViewModel(BlockDefinition block) => _block = block;

    /// <summary>The underlying <see cref="BlockDefinition"/> (for serialization).</summary>
    public BlockDefinition Source => _block;

    private int _rowIndex;
    public int RowIndex
    {
        get => _rowIndex;
        set { _rowIndex = value; Notify(); }
    }

    public string Type
    {
        get => _block.Type ?? "";
        set { if (_block.Type != value) { _block.Type = value; Notify(); Changed?.Invoke(this, EventArgs.Empty); } }
    }

    public string Name
    {
        get => _block.Name ?? "";
        set { if (_block.Name != value) { _block.Name = value; Notify(); Changed?.Invoke(this, EventArgs.Empty); } }
    }

    public string OffsetText
    {
        get => DisplayObj(_block.Offset);
        set
        {
            var p = ParseObj(value);
            if (p?.ToString() != _block.Offset?.ToString())
            {
                _block.Offset = p;
                Notify();
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public string LengthText
    {
        get => DisplayObj(_block.Length);
        set
        {
            var p = ParseObj(value);
            if (p?.ToString() != _block.Length?.ToString())
            {
                _block.Length = p;
                Notify();
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public string ValueType
    {
        get => _block.ValueType ?? "";
        set { if (_block.ValueType != value) { _block.ValueType = value; Notify(); Changed?.Invoke(this, EventArgs.Empty); } }
    }

    public string StoreAs
    {
        get => _block.StoreAs ?? "";
        set { if (_block.StoreAs != value) { _block.StoreAs = value; Notify(); Changed?.Invoke(this, EventArgs.Empty); } }
    }

    public string Color
    {
        get => _block.Color ?? "";
        set
        {
            if (_block.Color != value)
            {
                _block.Color = value;
                Notify();
                Notify(nameof(ColorBrush));
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public Brush ColorBrush
    {
        get
        {
            if (!string.IsNullOrEmpty(_block.Color))
            {
                try
                {
                    return new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString(_block.Color));
                }
                catch { /* fall through */ }
            }
            return Brushes.Transparent;
        }
    }

    public string Description
    {
        get => _block.Description ?? "";
        set { if (_block.Description != value) { _block.Description = value; Notify(); Changed?.Invoke(this, EventArgs.Empty); } }
    }

    // -- Helpers ---------------------------------------------------------------

    private static string DisplayObj(object? val) => val switch
    {
        null => "",
        JsonElement je => je.ValueKind == JsonValueKind.Number
                          ? je.GetRawText()
                          : je.GetString() ?? "",
        _ => val.ToString() ?? "",
    };

    private static object? ParseObj(string? s)
    {
        if (string.IsNullOrEmpty(s)) return null;
        if (long.TryParse(s, out var n)) return n;
        return s;
    }

    // -- INotifyPropertyChanged ------------------------------------------------

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Notify([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

// -- Minimal RelayCommand (no external dep) ------------------------------------

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
