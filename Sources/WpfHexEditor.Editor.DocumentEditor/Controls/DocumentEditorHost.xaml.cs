// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor
// File: Controls/DocumentEditorHost.xaml.cs
// Description:
//     Main host control that implements IDocumentEditor + IOpenableDocument.
//     Manages the 5-row layout (Toolbar / Breadcrumb / Content / MiniMap / StatusBar),
//     orchestrates DocumentViewMode transitions, and wires the
//     BinaryMapSyncService for bidirectional selection sync.
//     v2: ZoomLevel DP, breadcrumb path, pop-toolbar, status bar wiring.
// ==========================================================

using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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

    public static readonly DependencyProperty ZoomLevelProperty =
        DependencyProperty.Register(
            nameof(ZoomLevel), typeof(double), typeof(DocumentEditorHost),
            new PropertyMetadata(1.0, OnZoomLevelChanged));

    // ── Fields ──────────────────────────────────────────────────────────────

    private IIDEHostContext?         _ideContext;
    private DocumentEditorViewModel? _vm;
    private BinaryMapSyncService?    _syncService;
    private CancellationTokenSource  _loadCts = new();
    private string?                  _pendingFilePath;
    private string                   _currentFileExtension = string.Empty;

    // ── Constructor ─────────────────────────────────────────────────────────

    public DocumentEditorHost(IIDEHostContext? ideContext = null)
    {
        _ideContext = ideContext;
        InitializeComponent();
        Loaded   += OnLoaded;
        Unloaded += OnUnloaded;
    }

    // ── Context injection (deferred) ────────────────────────────────────────

    public void SetContext(IIDEHostContext context)
    {
        if (_ideContext is not null) return;
        _ideContext = context;
        if (_pendingFilePath is not null)
        {
            var path = _pendingFilePath;
            _pendingFilePath = null;
            OutputMessage?.Invoke(this, $"[DocEditor] SetContext — retrying deferred open for '{System.IO.Path.GetFileName(path)}'");
            _ = OpenAsync(path);
        }
        else
        {
            OutputMessage?.Invoke(this, "[DocEditor] SetContext — no pending file to retry");
        }
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

    public double ZoomLevel
    {
        get => (double)GetValue(ZoomLevelProperty);
        set => SetValue(ZoomLevelProperty, value);
    }

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

    public void Undo()      => _vm?.Model.UndoEngine.TryUndo();
    public void Redo()      => _vm?.Model.UndoEngine.TryRedo();
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

        _currentFileExtension = System.IO.Path.GetExtension(filePath);
        TitleChanged?.Invoke(this, System.IO.Path.GetFileName(filePath) + " …");
        await Dispatcher.InvokeAsync(() => PART_TextPane.ShowLoading());

        IsBusy = true;
        OperationStarted?.Invoke(this, new DocumentOperationEventArgs { Title = "Loading document…" });

        var fileName = System.IO.Path.GetFileName(filePath);
        var ctxState = _ideContext is null ? "NULL" : "OK";
        OutputMessage?.Invoke(this, $"[DocEditor] OpenAsync START — file='{fileName}' ideContext={ctxState}");

        bool loadSucceeded = false;
        try
        {
            var loaders = _ideContext?.ExtensionRegistry
                              .GetExtensions<IDocumentLoader>()
                          ?? [];

            OutputMessage?.Invoke(this, $"[DocEditor] loaders found={loaders.Count}  ideContext={ctxState}");

            if (loaders.Count == 0 && _ideContext is null)
            {
                _pendingFilePath = filePath;
                OutputMessage?.Invoke(this, $"[DocEditor] DEFERRED — waiting for IDE context. file='{fileName}'");
                await Dispatcher.InvokeAsync(() =>
                    PART_TextPane.ShowLoading("Waiting for IDE to initialize…"));
                return;
            }

            var loader = loaders.FirstOrDefault(l => l.CanLoad(filePath))
                         ?? throw new NotSupportedException(
                             $"No document loader registered for '{System.IO.Path.GetExtension(filePath)}'.");

            OutputMessage?.Invoke(this, $"[DocEditor] using loader='{loader.GetType().Name}'");

            var model = new DocumentModel { FilePath = filePath };

            await using var stream = File.OpenRead(filePath);
            await loader.LoadAsync(filePath, stream, model, linked);

            OutputMessage?.Invoke(this, $"[DocEditor] LoadAsync done — blocks={model.Blocks.Count}  metadata='{model.Metadata.Title}'");

            await Dispatcher.InvokeAsync(() => BindModel(model));
            loadSucceeded = true;
            OutputMessage?.Invoke(this, $"[DocEditor] BindModel done — SUCCESS");
        }
        catch (OperationCanceledException) { OutputMessage?.Invoke(this, $"[DocEditor] CANCELLED — '{fileName}'"); }
        catch (Exception ex)
        {
            OutputMessage?.Invoke(this, $"[DocEditor] ERROR — {ex.GetType().Name}: {ex.Message}");
            StatusMessage?.Invoke(this, $"Failed to open document: {ex.Message}");
            _ideContext?.Output.Error($"[DocumentEditor] Failed to open '{fileName}': {ex}");
            await Dispatcher.InvokeAsync(() =>
            {
                PART_TextPane.ShowError(ex.Message);
                PART_HexPane.LoadFile(filePath);
            });
            TitleChanged?.Invoke(this, fileName + " ⚠");
        }
        finally
        {
            IsBusy = false;
            if (_pendingFilePath is null)
            {
                OperationCompleted?.Invoke(this, new DocumentOperationCompletedEventArgs
                {
                    Success = loadSucceeded,
                });
            }
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
        SetPaneVisibility(
            text:      mode is DocumentViewMode.TextOnly or DocumentViewMode.Split or DocumentViewMode.Structure,
            structure: mode == DocumentViewMode.Structure,
            hex:       mode is DocumentViewMode.Split or DocumentViewMode.HexOnly);

        if (PART_TextModeBtn   is not null) PART_TextModeBtn.IsChecked   = mode == DocumentViewMode.TextOnly;
        if (PART_SplitModeBtn  is not null) PART_SplitModeBtn.IsChecked  = mode == DocumentViewMode.Split;
        if (PART_HexModeBtn    is not null) PART_HexModeBtn.IsChecked    = mode == DocumentViewMode.HexOnly;
        if (PART_StructModeBtn is not null) PART_StructModeBtn.IsChecked = mode == DocumentViewMode.Structure;

        PART_StatusBar.ViewModeText = mode switch
        {
            DocumentViewMode.TextOnly  => "Text",
            DocumentViewMode.Split     => "Split",
            DocumentViewMode.HexOnly   => "Hex",
            DocumentViewMode.Structure => "Structure",
            _ => "Split"
        };
    }

    private void SetPaneVisibility(bool text, bool structure, bool hex)
    {
        PART_TextPane.Visibility      = text      ? Visibility.Visible   : Visibility.Collapsed;
        PART_Splitter1.Visibility     = text && (structure || hex) ? Visibility.Visible : Visibility.Collapsed;
        PART_StructurePane.Visibility = structure ? Visibility.Visible   : Visibility.Collapsed;
        PART_HexPane.Visibility       = hex       ? Visibility.Visible   : Visibility.Collapsed;
        PART_Splitter2.Visibility     = hex && structure ? Visibility.Visible : Visibility.Collapsed;

        if (text && hex && !structure)
        {
            Grid.SetColumn(PART_HexPane, 2);
            PART_TextCol.Width      = new GridLength(2, GridUnitType.Star);
            PART_Splitter1Col.Width = new GridLength(4);
            PART_StructCol.Width    = new GridLength(1, GridUnitType.Star);
            PART_Splitter2Col.Width = new GridLength(0);
            PART_HexCol.Width       = new GridLength(0);
        }
        else
        {
            Grid.SetColumn(PART_HexPane, 4);
            PART_TextCol.Width      = text      ? new GridLength(2, GridUnitType.Star) : new GridLength(0);
            PART_StructCol.Width    = structure ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
            PART_HexCol.Width       = hex       ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
            PART_Splitter1Col.Width = (text && (structure || hex)) ? new GridLength(4) : new GridLength(0);
            PART_Splitter2Col.Width = (structure && hex)           ? new GridLength(4) : new GridLength(0);
        }
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
        PART_ForensicBtn.IsChecked = enabled;
    }

    // ── Zoom ─────────────────────────────────────────────────────────────────

    private static void OnZoomLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DocumentEditorHost host)
            host.ApplyZoom((double)e.NewValue);
    }

    private void ApplyZoom(double level)
    {
        level = Math.Clamp(level, 0.5, 2.0);
        PART_TextPane.SetZoom(level);
        PART_ZoomLabel.Text    = $"{level * 100:0}%";
        PART_StatusBar.ZoomPercent = level * 100;
    }

    // ── Toolbar handlers ─────────────────────────────────────────────────────

    private void OnTextModeClicked(object sender, RoutedEventArgs e)      => ViewMode = DocumentViewMode.TextOnly;
    private void OnSplitModeClicked(object sender, RoutedEventArgs e)     => ViewMode = DocumentViewMode.Split;
    private void OnHexModeClicked(object sender, RoutedEventArgs e)       => ViewMode = DocumentViewMode.HexOnly;
    private void OnStructureModeClicked(object sender, RoutedEventArgs e) => ViewMode = DocumentViewMode.Structure;
    private void OnForensicModeClicked(object sender, RoutedEventArgs e)  => IsForensicMode = PART_ForensicBtn.IsChecked == true;
    private void OnSaveClicked(object sender, RoutedEventArgs e)          => Save();
    private void OnExportClicked(object sender, RoutedEventArgs e)        { /* TODO: export dialog */ }
    private void OnMetadataClicked(object sender, RoutedEventArgs e)      { /* TODO: metadata flyout */ }

    private void OnZoomInClicked(object sender, RoutedEventArgs e)
    {
        ZoomLevel = Math.Min(2.0, Math.Round(ZoomLevel + 0.1, 1));
    }

    private void OnZoomOutClicked(object sender, RoutedEventArgs e)
    {
        ZoomLevel = Math.Max(0.5, Math.Round(ZoomLevel - 0.1, 1));
    }

    // ── Breadcrumb ────────────────────────────────────────────────────────────

    private void OnTextPaneSelectedBlockChanged(object? sender, DocumentBlock? block)
    {
        PART_Breadcrumb.SetPath(block, _vm?.Model);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnBreadcrumbBlockSelected(object? sender, DocumentBlock block)
    {
        PART_TextPane.ScrollToBlock(block);
    }

    private void OnStructureBlockNavigated(object? sender, DocumentBlock block)
    {
        PART_TextPane.ScrollToBlock(block);
        PART_Breadcrumb.SetPath(block, _vm?.Model);
    }

    // ── Pop-toolbar ───────────────────────────────────────────────────────────

    private void OnPopToolbarRequested(object? sender, PopToolbarRequestedArgs args)
    {
        PART_PopToolbarContent.SetContext(args.Block);
        PART_PopToolbar.HorizontalOffset = args.SelectionRect.Left;
        PART_PopToolbar.VerticalOffset   = args.SelectionRect.Top - 36;
        PART_PopToolbar.IsOpen = true;
    }

    private void OnPopToolbarFormat(object? sender, string format)
    {
        PART_TextPane.ApplyFormat(format);
    }

    private void OnPopToolbarCopyText(object? sender, DocumentBlock? block)
    {
        if (block is not null)
            System.Windows.Clipboard.SetText(block.Text);
        PART_PopToolbar.IsOpen = false;
    }

    private void OnPopToolbarCopyHex(object? sender, DocumentBlock? block)
    {
        if (block is not null)
            System.Windows.Clipboard.SetText(
                $"0x{block.RawOffset:X8} +0x{block.RawLength:X4}");
        PART_PopToolbar.IsOpen = false;
    }

    private void OnPopToolbarInspect(object? sender, DocumentBlock? block)
    {
        if (block is not null && ViewMode != DocumentViewMode.Structure)
            ViewMode = DocumentViewMode.Structure;
        PART_PopToolbar.IsOpen = false;
    }

    private void OnPopToolbarJumpHex(object? sender, DocumentBlock? block)
    {
        if (block is not null)
            PART_HexPane.ScrollToBlock(block);
        PART_PopToolbar.IsOpen = false;
    }

    // ── Status bar ───────────────────────────────────────────────────────────

    private void OnStatusBarForensicClicked(object? sender, EventArgs e)
    {
        IsForensicMode = !IsForensicMode;
    }

    private void OnStatusBarZoomChanged(object? sender, double zoomPercent)
    {
        ZoomLevel = zoomPercent / 100.0;
    }

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
        PART_StatusBar.BindModel(model, _currentFileExtension);

        // Apply initial zoom
        PART_TextPane.SetZoom(ZoomLevel);

        _syncService = new BinaryMapSyncService(model);
        _syncService.Wire(PART_TextPane, PART_HexPane);

        ApplyViewMode(ViewMode);

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

        model.ForensicAlertsChanged += (_, _) =>
            Dispatcher.InvokeAsync(() => PART_StatusBar.UpdateForensicCount(model));

        TitleChanged?.Invoke(this, Title);
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyViewMode(ViewMode);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Do NOT cancel _loadCts here — docking system unloads/reloads during tab switches.
    }
}
