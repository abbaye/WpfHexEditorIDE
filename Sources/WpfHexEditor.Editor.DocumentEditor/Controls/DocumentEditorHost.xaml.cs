// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor
// File: Controls/DocumentEditorHost.xaml.cs
// Description:
//     Main host control that implements IDocumentEditor + IOpenableDocument.
//     Manages the 3-column layout (TextPane / StructurePane / HexPane),
//     orchestrates DocumentViewMode transitions, and wires the
//     BinaryMapSyncService for bidirectional selection sync.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.DocumentEditor.Core.BinaryMap;
using WpfHexEditor.Editor.DocumentEditor.Core.Contracts;
using WpfHexEditor.Editor.DocumentEditor.Core.Forensic;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;
using WpfHexEditor.Editor.DocumentEditor.Core.Options;
using WpfHexEditor.Editor.DocumentEditor.ViewModels;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.Editor.DocumentEditor.Core;

namespace WpfHexEditor.Editor.DocumentEditor.Controls;

/// <summary>
/// Host control for the multi-format document editor.
/// Implements <see cref="IDocumentEditor"/> and <see cref="IOpenableDocument"/>.
/// </summary>
public partial class DocumentEditorHost : UserControl, IDocumentEditor, IOpenableDocument, IDocumentBinaryMapSource
{
    // ── Dependency Properties ───────────────────────────────────────────────

    public static readonly DependencyProperty ViewModeProperty =
        DependencyProperty.Register(
            nameof(ViewMode), typeof(DocumentViewMode), typeof(DocumentEditorHost),
            new PropertyMetadata(DocumentViewMode.Split, OnViewModeChanged));

    public static readonly DependencyProperty IsForensicModeProperty =
        DependencyProperty.Register(
            nameof(IsForensicMode), typeof(bool), typeof(DocumentEditorHost),
            new PropertyMetadata(false, OnIsForensicModeChanged));

    public static readonly DependencyProperty IsTextPaneVisibleProperty =
        DependencyProperty.Register(
            nameof(IsTextPaneVisible), typeof(bool), typeof(DocumentEditorHost),
            new PropertyMetadata(true));

    // ── Fields ──────────────────────────────────────────────────────────────

    private readonly IIDEHostContext? _ideContext;
    private DocumentEditorViewModel? _vm;
    private BinaryMapSyncService?    _syncService;
    private CancellationTokenSource  _loadCts = new();

    // ── Constructor ─────────────────────────────────────────────────────────

    public DocumentEditorHost(IIDEHostContext? ideContext = null)
    {
        _ideContext = ideContext;
        InitializeComponent();
        Loaded   += OnLoaded;
        Unloaded += OnUnloaded;
    }

    // ── Properties ──────────────────────────────────────────────────────────

    public DocumentViewMode ViewMode
    {
        get => (DocumentViewMode)GetValue(ViewModeProperty);
        set => SetValue(ViewModeProperty, value);
    }

    public bool IsForensicMode
    {
        get => (bool)GetValue(IsForensicModeProperty);
        set => SetValue(IsForensicModeProperty, value);
    }

    public bool IsTextPaneVisible
    {
        get => (bool)GetValue(IsTextPaneVisibleProperty);
        set => SetValue(IsTextPaneVisibleProperty, value);
    }

    // ── IDocumentEditor ─────────────────────────────────────────────────────

    // ── IDocumentBinaryMapSource ─────────────────────────────────────────────

    public BinaryMap? BinaryMap => _vm?.Model.BinaryMap;
    public event EventHandler? BinaryMapRebuilt;

    // ── IDocumentEditor ─────────────────────────────────────────────────────

    public bool IsDirty    => _vm?.Model.IsDirty ?? false;
    public bool CanUndo    => _vm?.Model.UndoEngine.CanUndo ?? false;
    public bool CanRedo    => _vm?.Model.UndoEngine.CanRedo ?? false;
    public bool IsReadOnly { get; set; }
    public bool IsBusy     { get; private set; }

    public string Title => _vm is null
        ? "Document"
        : Path.GetFileName(_vm.Model.FilePath) + (IsDirty ? " *" : string.Empty);

    public int    UndoCount       => _vm?.Model.UndoEngine.UndoCount ?? 0;
    public int    RedoCount       => _vm?.Model.UndoEngine.RedoCount ?? 0;
    public string UndoDescription => _vm?.Model.UndoEngine.PeekUndoDescription ?? "Undo";
    public string RedoDescription => _vm?.Model.UndoEngine.PeekRedoDescription ?? "Redo";

    public ICommand? UndoCommand      => null;
    public ICommand? RedoCommand      => null;
    public ICommand? SaveCommand      => null;
    public ICommand? CopyCommand      => null;
    public ICommand? CutCommand       => null;
    public ICommand? PasteCommand     => null;
    public ICommand? DeleteCommand    => null;
    public ICommand? SelectAllCommand => null;

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

    public void Undo()      => _vm?.Model.UndoEngine.Undo();
    public void Redo()      => _vm?.Model.UndoEngine.Redo();
    public void Save()      => _ = SaveAsync();
    public void Copy()      { }
    public void Cut()       { }
    public void Paste()     { }
    public void Delete()    { }
    public void SelectAll() { }
    public void Close()     { _loadCts.Cancel(); _syncService?.Dispose(); }
    public void CancelOperation() { _loadCts.Cancel(); }

    public async Task SaveAsync(CancellationToken ct = default)
    {
        if (_vm is null) return;
        // TODO: implement save via IDocumentLoader (reverse path)
        await Task.CompletedTask;
    }

    public async Task SaveAsAsync(string filePath, CancellationToken ct = default)
    {
        await Task.CompletedTask;
    }

    // ── IOpenableDocument ────────────────────────────────────────────────────

    public async Task OpenAsync(string filePath, CancellationToken ct = default)
    {
        _loadCts.Cancel();
        _loadCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var linked = _loadCts.Token;

        IsBusy = true;
        OperationStarted?.Invoke(this, new DocumentOperationEventArgs("Loading document…", 0));

        try
        {
            // Resolve loader via IExtensionRegistry
            var loaders = _ideContext?.ExtensionRegistry
                              .GetExtensions<IDocumentLoader>()
                          ?? [];
            var loader = loaders.FirstOrDefault(l => l.CanLoad(filePath))
                         ?? throw new NotSupportedException(
                             $"No document loader registered for '{Path.GetExtension(filePath)}'.");

            var model = new DocumentModel { FilePath = filePath };

            await using var stream = File.OpenRead(filePath);
            await loader.LoadAsync(filePath, stream, model, linked);

            // Run UI work on dispatcher
            await Dispatcher.InvokeAsync(() => BindModel(model));
        }
        catch (OperationCanceledException) { /* ignore cancel */ }
        catch (Exception ex)
        {
            StatusMessage?.Invoke(this, $"Failed to open document: {ex.Message}");
            OutputMessage?.Invoke(this, ex.ToString());
        }
        finally
        {
            IsBusy = false;
            OperationCompleted?.Invoke(this, new DocumentOperationCompletedEventArgs(
                success: !linked.IsCancellationRequested));
        }
    }

    // ── View mode ────────────────────────────────────────────────────────────

    private static void OnViewModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DocumentEditorHost host)
            host.ApplyViewMode((DocumentViewMode)e.NewValue);
    }

    private void ApplyViewMode(DocumentViewMode mode)
    {
        // Reset all pane visibilities, then show required ones
        SetPaneVisibility(
            text:      mode is DocumentViewMode.TextOnly or DocumentViewMode.Split or DocumentViewMode.Structure,
            structure: mode == DocumentViewMode.Structure,
            hex:       mode is DocumentViewMode.Split or DocumentViewMode.HexOnly);

        // Update toggle button checked states
        if (PART_TextModeBtn   is not null) PART_TextModeBtn.IsChecked   = mode == DocumentViewMode.TextOnly;
        if (PART_SplitModeBtn  is not null) PART_SplitModeBtn.IsChecked  = mode == DocumentViewMode.Split;
        if (PART_HexModeBtn    is not null) PART_HexModeBtn.IsChecked    = mode == DocumentViewMode.HexOnly;
        if (PART_StructModeBtn is not null) PART_StructModeBtn.IsChecked = mode == DocumentViewMode.Structure;
    }

    private void SetPaneVisibility(bool text, bool structure, bool hex)
    {
        PART_TextPane.Visibility      = text      ? Visibility.Visible   : Visibility.Collapsed;
        PART_Splitter1.Visibility     = text && (structure || hex) ? Visibility.Visible : Visibility.Collapsed;
        PART_StructurePane.Visibility = structure ? Visibility.Visible   : Visibility.Collapsed;
        PART_HexPane.Visibility       = hex       ? Visibility.Visible   : Visibility.Collapsed;
        PART_Splitter2.Visibility     = hex && structure ? Visibility.Visible : Visibility.Collapsed;

        // Adjust column widths
        PART_TextCol.Width   = text      ? new GridLength(2, GridUnitType.Star) : new GridLength(0);
        PART_StructCol.Width = structure ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        PART_HexCol.Width    = hex       ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        PART_Splitter1Col.Width = (text && (structure || hex)) ? new GridLength(4) : new GridLength(0);
        PART_Splitter2Col.Width = (structure && hex)           ? new GridLength(4) : new GridLength(0);
    }

    private static void OnIsForensicModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DocumentEditorHost host)
            host.ApplyForensicMode((bool)e.NewValue);
    }

    private void ApplyForensicMode(bool enabled)
    {
        if (_vm?.Model is null) return;
        _vm.Model.ForensicMode = enabled ? ForensicMode.Forensic : ForensicMode.Normal;
        PART_TextPane.SetForensicMode(enabled);
    }

    // ── Toolbar handlers ─────────────────────────────────────────────────────

    private void OnTextModeClicked(object sender, RoutedEventArgs e)      => ViewMode = DocumentViewMode.TextOnly;
    private void OnSplitModeClicked(object sender, RoutedEventArgs e)     => ViewMode = DocumentViewMode.Split;
    private void OnHexModeClicked(object sender, RoutedEventArgs e)       => ViewMode = DocumentViewMode.HexOnly;
    private void OnStructureModeClicked(object sender, RoutedEventArgs e) => ViewMode = DocumentViewMode.Structure;
    private void OnForensicModeClicked(object sender, RoutedEventArgs e)  => IsForensicMode = PART_ForensicBtn.IsChecked == true;
    private void OnSaveClicked(object sender, RoutedEventArgs e)          => Save();

    // ── Model binding ─────────────────────────────────────────────────────────

    private void BindModel(DocumentModel model)
    {
        _syncService?.Dispose();

        _vm = new DocumentEditorViewModel(model);
        DataContext = _vm;

        PART_TextPane.BindModel(model);
        PART_StructurePane.BindModel(model);
        PART_HexPane.BindModel(model);
        PART_MiniMap.BindModel(model);

        _syncService = new BinaryMapSyncService(model);
        _syncService.Wire(PART_TextPane, PART_HexPane);

        // Apply default view mode from options (loaded from AppSettings)
        ApplyViewMode(ViewMode);

        // Hook model events
        model.BinaryMap.MapRebuilt += (_, _) => BinaryMapRebuilt?.Invoke(this, EventArgs.Empty);

        model.UndoEngine.StateChanged += (_, _) =>
        {
            CanUndoChanged?.Invoke(this, EventArgs.Empty);
            CanRedoChanged?.Invoke(this, EventArgs.Empty);
        };

        model.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(DocumentModel.IsDirty))
            {
                ModifiedChanged?.Invoke(this, EventArgs.Empty);
                TitleChanged?.Invoke(this, Title);
            }
        };

        TitleChanged?.Invoke(this, Title);
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyViewMode(ViewMode);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Do not dispose VM — docking may reattach. Only cancel pending loads.
        _loadCts.Cancel();
    }
}
