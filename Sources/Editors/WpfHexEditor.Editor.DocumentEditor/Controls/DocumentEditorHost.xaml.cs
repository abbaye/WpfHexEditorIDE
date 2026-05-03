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
using WpfHexEditor.Editor.DocumentEditor.Core;
using WpfHexEditor.Editor.DocumentEditor.Core.BinaryMap;
using WpfHexEditor.Editor.DocumentEditor.Core.Contracts;
using WpfHexEditor.Editor.DocumentEditor.Core.Editing;
using IDocumentSaver = WpfHexEditor.Editor.DocumentEditor.Core.IDocumentSaver;
using WpfHexEditor.Editor.DocumentEditor.Core.Forensic;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;
using WpfHexEditor.Editor.DocumentEditor.Core.Options;
using WpfHexEditor.Editor.DocumentEditor.ViewModels;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.Editor.DocumentEditor.Properties;
using WpfHexEditor.Core.Events.IDEEvents;

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

    public static readonly DependencyProperty IsReadOnlyProperty =
        DependencyProperty.Register(
            nameof(IsReadOnly), typeof(bool), typeof(DocumentEditorHost),
            new PropertyMetadata(false, OnIsReadOnlyChanged));

    public static readonly DependencyProperty RenderModeProperty =
        DependencyProperty.Register(
            nameof(RenderMode), typeof(DocumentRenderMode), typeof(DocumentEditorHost),
            new PropertyMetadata(DocumentRenderMode.Page, OnRenderModeChanged));

    // ── Fields ──────────────────────────────────────────────────────────────

    private IIDEHostContext?         _ideContext;
    private DocumentEditorViewModel? _vm;
    private BinaryMapSyncService?    _syncService;
    private DocumentMutator?         _mutator;
    private DocumentHexHighlightManager? _hexHighlightMgr;
    private CancellationTokenSource  _loadCts = new();
    private string?                  _pendingFilePath;
    private string                   _currentFileExtension = string.Empty;
    private bool                     _isFocusMode          = false;

    // ── Constructor ─────────────────────────────────────────────────────────

    public DocumentEditorHost(IIDEHostContext? ideContext = null)
    {
        _ideContext = ideContext;
        InitializeComponent();
        Loaded      += OnLoaded;
        Unloaded    += OnUnloaded;
        PreviewKeyDown += OnHostPreviewKeyDown;
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

    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    public DocumentRenderMode RenderMode
    {
        get => (DocumentRenderMode)GetValue(RenderModeProperty);
        set => SetValue(RenderModeProperty, value);
    }

    // ── IDocumentBinaryMapSource ─────────────────────────────────────────────

    public BinaryMap? BinaryMap => _vm?.Model.BinaryMap;
    public event EventHandler? BinaryMapRebuilt;

    // ── IDocumentEditor ─────────────────────────────────────────────────────

    public bool IsDirty    => _vm?.Model.IsDirty ?? false;
    public bool CanUndo    => _vm?.Model.UndoEngine.CanUndo ?? false;
    public bool CanRedo    => _vm?.Model.UndoEngine.CanRedo ?? false;
    public bool IsBusy     { get; private set; }

    public string Title => _vm is null
        ? DocumentEditorResources.DocEditorHost_DocumentTitle
        : Path.GetFileName(_vm.Model.FilePath) + (IsDirty ? " *" : string.Empty);

    public int    UndoCount       => _vm?.Model.UndoEngine.UndoCount ?? 0;
    public int    RedoCount       => _vm?.Model.UndoEngine.RedoCount ?? 0;
    public string UndoDescription => _vm?.Model.UndoEngine.PeekUndoDescription ?? "Undo";
    public string RedoDescription => _vm?.Model.UndoEngine.PeekRedoDescription ?? "Redo";

    public ICommand? UndoCommand      => new RelayCmd(Undo,      () => _vm?.Model.UndoEngine.CanUndo == true);
    public ICommand? RedoCommand      => new RelayCmd(Redo,      () => _vm?.Model.UndoEngine.CanRedo == true);
    public ICommand? SaveCommand      => new RelayCmd(Save,      () => _vm?.Model.IsDirty == true);
    public ICommand? CopyCommand      => new RelayCmd(Copy,      () => true);
    public ICommand? CutCommand       => new RelayCmd(Cut,       () => !IsReadOnly);
    public ICommand? PasteCommand     => new RelayCmd(Paste,     () => !IsReadOnly);
    public ICommand? DeleteCommand    => new RelayCmd(Delete,    () => !IsReadOnly);
    public ICommand? SelectAllCommand => new RelayCmd(SelectAll, () => true);

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

    public void Undo()      => _mutator?.TryUndo();
    public void Redo()      => _mutator?.TryRedo();
    public void Save()      => _ = SaveAsync();
    public void Copy()      => PART_TextPane.PART_Renderer.CopySelection();
    public void Cut()       { if (!IsReadOnly) PART_TextPane.PART_Renderer.CutSelection(); }
    public void Paste()     { if (!IsReadOnly) PART_TextPane.PART_Renderer.PasteAtCaret(); }
    public void Delete()    { if (!IsReadOnly) PART_TextPane.PART_Renderer.DeleteAtCaret(forward: true); }
    public void SelectAll() => PART_TextPane.PART_Renderer.SelectAll();
    public void Close()     { _loadCts.Cancel(); _loadCts.Dispose(); _syncService?.Dispose(); }
    public void CancelOperation() { _loadCts.Cancel(); }

    public async Task SaveAsync(CancellationToken ct = default)
    {
        if (_vm?.Model is null) return;

        var savers = _ideContext?.ExtensionRegistry
                         .GetExtensions<IDocumentSaver>()
                     ?? [];

        var saver = savers.FirstOrDefault(s => s.CanSave(_vm.Model.FilePath));
        if (saver is null)
        {
            StatusMessage?.Invoke(this, DocumentEditorResources.DocEditorHost_NoSaverStatus);
            return;
        }

        var tmp    = _vm.Model.FilePath + ".tmp";
        var backup = _vm.Model.FilePath + ".bak";
        try
        {
            IsBusy = true;
            await using (var fs = File.Create(tmp))
                await saver.SaveAsync(_vm.Model, fs, ct);

            File.Replace(tmp, _vm.Model.FilePath, backup);
            _vm.Model.UndoEngine.MarkSaved();
            _hexHighlightMgr?.Clear();

            var fileName = System.IO.Path.GetFileName(_vm.Model.FilePath);
            StatusMessage?.Invoke(this, string.Format(DocumentEditorResources.DocEditorHost_SavedStatus, fileName));
            TitleChanged?.Invoke(this, Title);
        }
        catch (Exception ex)
        {
            StatusMessage?.Invoke(this, string.Format(DocumentEditorResources.DocEditorHost_SaveFailedStatus, ex.Message));
            if (File.Exists(tmp)) File.Delete(tmp);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task SaveAsAsync(string filePath, CancellationToken ct = default)
    {
        if (_vm?.Model is null) return;
        var origPath = _vm.Model.FilePath;
        _vm.Model.FilePath = filePath;
        await SaveAsync(ct);
        if (!File.Exists(filePath)) _vm.Model.FilePath = origPath; // revert on failure
    }

    // ── IOpenableDocument ────────────────────────────────────────────────────

    public async Task OpenAsync(string filePath, CancellationToken ct = default)
    {
        _loadCts.Cancel();
        _loadCts.Dispose();
        _loadCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var linked = _loadCts.Token;

        _currentFileExtension = System.IO.Path.GetExtension(filePath);
        TitleChanged?.Invoke(this, System.IO.Path.GetFileName(filePath) + " …");
        await Dispatcher.InvokeAsync(() => PART_TextPane.ShowLoading());

        IsBusy = true;
        OperationStarted?.Invoke(this, new DocumentOperationEventArgs { Title = DocumentEditorResources.DocEditorHost_LoadingMessage });

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
                    PART_TextPane.ShowLoading(DocumentEditorResources.DocEditorHost_WaitingForIDE));
                return;
            }

            // If no loader is available yet, publish FileOpenedEvent to trigger lazy plugin activation
            // (PluginActivationService subscribes to this event and activates plugins with matching
            // fileExtension triggers). FileOpenedEvent may not have been published yet when OpenAsync
            // starts because MainWindow publishes it after the editor tab is created.
            if (_ideContext is not null && loaders.FirstOrDefault(l => l.CanLoad(filePath)) is null)
            {
                var ext = System.IO.Path.GetExtension(filePath);
                OutputMessage?.Invoke(this, $"[DocEditor] no loader for '{ext}' — publishing FileOpenedEvent to trigger lazy activation");
                _ideContext.IDEEvents.Publish(new FileOpenedEvent
                {
                    Source        = "DocumentEditorHost",
                    FilePath      = filePath,
                    FileExtension = ext,
                    FileSize      = File.Exists(filePath) ? new FileInfo(filePath).Length : 0L,
                });

                // Poll up to 3 s in 200 ms steps — plugin activation is async (Task.Run).
                const int maxWaitMs = 3000;
                const int stepMs    = 200;
                int waited = 0;
                while (waited < maxWaitMs)
                {
                    await Task.Delay(stepMs, linked).ConfigureAwait(false);
                    waited += stepMs;
                    loaders = _ideContext.ExtensionRegistry.GetExtensions<IDocumentLoader>();
                    if (loaders.FirstOrDefault(l => l.CanLoad(filePath)) is not null)
                    {
                        OutputMessage?.Invoke(this, $"[DocEditor] loader appeared after {waited} ms");
                        break;
                    }
                }
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
            StatusMessage?.Invoke(this, string.Format(DocumentEditorResources.DocEditorHost_LoadErrorMessage, ex.Message));
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
        // Exit focus mode first if switching away
        if (_isFocusMode && mode != DocumentViewMode.Focus)
            ExitFocusMode();

        switch (mode)
        {
            case DocumentViewMode.Full:
                SetPaneVisibility(text: true, structure: true, hex: true);
                break;
            case DocumentViewMode.Focus:
                EnterFocusMode();
                break;
            default:
                SetPaneVisibility(
                    text:      mode is DocumentViewMode.TextOnly or DocumentViewMode.Split or DocumentViewMode.Structure,
                    structure: mode == DocumentViewMode.Structure,
                    hex:       mode is DocumentViewMode.Split or DocumentViewMode.HexOnly);
                break;
        }

        if (PART_TextModeBtn   is not null) PART_TextModeBtn.IsChecked   = mode == DocumentViewMode.TextOnly;
        if (PART_SplitModeBtn  is not null) PART_SplitModeBtn.IsChecked  = mode == DocumentViewMode.Split;
        if (PART_HexModeBtn    is not null) PART_HexModeBtn.IsChecked    = mode == DocumentViewMode.HexOnly;
        if (PART_StructModeBtn is not null) PART_StructModeBtn.IsChecked = mode == DocumentViewMode.Structure;
        if (PART_FullModeBtn   is not null) PART_FullModeBtn.IsChecked   = mode == DocumentViewMode.Full;
        if (PART_FocusModeBtn  is not null) PART_FocusModeBtn.IsChecked  = mode == DocumentViewMode.Focus;

        var readOnlySuffix = IsReadOnly ? DocumentEditorResources.DocEditorHost_ReadOnlySuffix : string.Empty;
        PART_StatusBar.ViewModeText = mode switch
        {
            DocumentViewMode.TextOnly  => DocumentEditorResources.DocEditorHost_ViewModeText      + readOnlySuffix,
            DocumentViewMode.Split     => DocumentEditorResources.DocEditorHost_ViewModeSplit      + readOnlySuffix,
            DocumentViewMode.HexOnly   => DocumentEditorResources.DocEditorHost_ViewModeHex       + readOnlySuffix,
            DocumentViewMode.Structure => DocumentEditorResources.DocEditorHost_ViewModeStructure + readOnlySuffix,
            DocumentViewMode.Full      => DocumentEditorResources.DocEditorHost_ViewModeFull      + readOnlySuffix,
            DocumentViewMode.Focus     => DocumentEditorResources.DocEditorHost_ViewModeFocus     + readOnlySuffix,
            _                          => DocumentEditorResources.DocEditorHost_ViewModeSplit     + readOnlySuffix
        };
    }

    private void EnterFocusMode()
    {
        _isFocusMode = true;
        SetPaneVisibility(text: true, structure: false, hex: false);
        // Hide chrome rows: toolbar (row 0), breadcrumb (row 1), minimap (row 3), statusbar (row 4)
        // These are accessed via RowDefinitions on the root Grid
        if (FindName("PART_ToolbarRow") is RowDefinition toolbar)
            toolbar.Height = new GridLength(0);
        if (FindName("PART_BreadcrumbRow") is RowDefinition breadcrumb)
            breadcrumb.Height = new GridLength(0);
        if (FindName("PART_MiniMapRow") is RowDefinition minimap)
            minimap.Height = new GridLength(0);
        if (FindName("PART_StatusBarRow") is RowDefinition statusbar)
            statusbar.Height = new GridLength(0);
        PART_TextPane.Margin = new Thickness(80, 40, 80, 40);
    }

    private void ExitFocusMode()
    {
        _isFocusMode = false;
        PART_TextPane.Margin = new Thickness(0);
        if (FindName("PART_ToolbarRow") is RowDefinition toolbar)
            toolbar.Height = GridLength.Auto;
        if (FindName("PART_BreadcrumbRow") is RowDefinition breadcrumb)
            breadcrumb.Height = GridLength.Auto;
        if (FindName("PART_MiniMapRow") is RowDefinition minimap)
            minimap.Height = GridLength.Auto;
        if (FindName("PART_StatusBarRow") is RowDefinition statusbar)
            statusbar.Height = GridLength.Auto;
    }

    private static void OnIsReadOnlyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DocumentEditorHost host)
            host.UpdateReadOnlyVisuals((bool)e.NewValue);
    }

    private void UpdateReadOnlyVisuals(bool readOnly)
    {
        PART_TextPane.PART_Renderer.IsReadOnly = readOnly;
        if (PART_LockIcon is not null)
            PART_LockIcon.Visibility = readOnly ? Visibility.Visible : Visibility.Collapsed;
        // Re-apply current view mode to refresh status bar suffix
        ApplyViewMode(ViewMode);
    }

    private static void OnRenderModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DocumentEditorHost host)
            host.ApplyRenderMode((DocumentRenderMode)e.NewValue);
    }

    private void ApplyRenderMode(DocumentRenderMode mode)
    {
        var renderer = PART_TextPane.PART_Renderer;

        // Always push the mode to the renderer first (triggers RebuildLayout)
        renderer.SetRenderMode(mode);

        switch (mode)
        {
            case DocumentRenderMode.Page:
                renderer.ShowPageShadows = true;
                renderer.PageMargin      = new Thickness(40);
                // Restore text pane if coming from Outline
                SetPaneVisibility(text: true, structure: false, hex: ViewMode == DocumentViewMode.Split);
                break;

            case DocumentRenderMode.Draft:
                renderer.ShowPageShadows = false;
                renderer.PageMargin      = new Thickness(8, 4, 8, 4);
                // Restore text pane if coming from Outline
                SetPaneVisibility(text: true, structure: false, hex: ViewMode == DocumentViewMode.Split);
                break;

            case DocumentRenderMode.Outline:
                // Show text pane (renderer draws outline mode inline, structure pane is optional)
                SetPaneVisibility(text: true, structure: false, hex: false);
                break;
        }

        if (PART_PageModeBtn    is not null) PART_PageModeBtn.IsChecked    = mode == DocumentRenderMode.Page;
        if (PART_DraftModeBtn   is not null) PART_DraftModeBtn.IsChecked   = mode == DocumentRenderMode.Draft;
        if (PART_OutlineModeBtn is not null) PART_OutlineModeBtn.IsChecked = mode == DocumentRenderMode.Outline;
    }

    // ── Phase 14/15: Text + paragraph formatting toolbar handlers ─────────────

    private void OnBoldClicked(object sender, RoutedEventArgs e)          => ApplyFormat("bold");
    private void OnItalicClicked(object sender, RoutedEventArgs e)        => ApplyFormat("italic");
    private void OnUnderlineClicked(object sender, RoutedEventArgs e)     => ApplyFormat("underline");
    private void OnStrikethroughClicked(object sender, RoutedEventArgs e) => ApplyFormat("strikethrough");

    // ── Wave F: Font family ───────────────────────────────────────────────────

    private bool _suppressFontDropdown; // guard against re-entrancy when we set selection from code

    private void OnFontFamilyChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressFontDropdown) return;
        if (PART_FontFamilyDropdown?.SelectedItem is not ComboBoxItem item) return;
        var family = item.Tag?.ToString();
        if (!string.IsNullOrEmpty(family))
            ApplyRunFormat("fontFamily", family);
    }

    // ── Wave F: Font size ─────────────────────────────────────────────────────

    private void OnFontSizeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PART_FontSizeDropdown?.SelectedItem is not ComboBoxItem item) return;
        if (double.TryParse(item.Tag?.ToString(), out double sz) && sz > 0)
            ApplyRunFormat("fontSize", sz);
    }

    private void OnFontSizeCommit(object sender, RoutedEventArgs e)
    {
        // User typed a custom size in the editable ComboBox
        if (PART_FontSizeDropdown is null) return;
        var text = PART_FontSizeDropdown.Text;
        if (double.TryParse(text, out double sz) && sz is >= 4 and <= 400)
            ApplyRunFormat("fontSize", sz);
    }

    // ── Wave F: Text color picker ─────────────────────────────────────────────

    private static readonly string[] ColorSwatchHex =
    [
        "#000000", "#FFFFFF", "#FF0000", "#00B050",
        "#0070C0", "#FFC000", "#7030A0", "#FF6600",
        "#808080", "#C0C0C0", "#FF9999", "#99CC99",
        "#99BBDD", "#FFE066", "#C4A0DC", "#FFCC99"
    ];

    private bool _colorSwatchesBuilt;

    private void OnTextColorBtnClicked(object sender, RoutedEventArgs e)
    {
        EnsureColorSwatches();
        PART_ColorPickerPopup.IsOpen = true;
    }

    private void EnsureColorSwatches()
    {
        if (_colorSwatchesBuilt) return;
        _colorSwatchesBuilt = true;

        foreach (var hex in ColorSwatchHex)
        {
            var btn = new System.Windows.Controls.Button
            {
                Width           = 14,
                Height          = 14,
                Margin          = new Thickness(1),
                Background      = new System.Windows.Media.SolidColorBrush(
                                      (System.Windows.Media.Color)
                                      System.Windows.Media.ColorConverter.ConvertFromString(hex)),
                BorderThickness = new Thickness(1),
                BorderBrush     = System.Windows.Media.Brushes.Gray,
                Tag             = hex,
                ToolTip         = hex,
                Cursor          = Cursors.Hand,
            };
            btn.Click += OnSwatchClicked;
            PART_ColorSwatches.Children.Add(btn);
        }
    }

    private void OnSwatchClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button btn) return;
        var hex = btn.Tag?.ToString();
        if (string.IsNullOrEmpty(hex)) return;

        PART_ColorPickerPopup.IsOpen = false;

        // Update the color stripe under the "A"
        if (PART_ColorStripe is not null)
            PART_ColorStripe.Fill = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)
                System.Windows.Media.ColorConverter.ConvertFromString(hex));

        ApplyRunFormat("color", hex);
    }

    // ── Shared run format helper ──────────────────────────────────────────────

    private void ApplyRunFormat(string attribute, object value)
    {
        if (IsReadOnly || _mutator is null) return;
        PART_TextPane.PART_Renderer.ApplyFormatToSelection(attribute, value);
    }

    private void OnAlignLeftClicked(object sender, RoutedEventArgs e)     => ApplyBlockAttribute("align", "left");
    private void OnAlignCenterClicked(object sender, RoutedEventArgs e)   => ApplyBlockAttribute("align", "center");
    private void OnAlignRightClicked(object sender, RoutedEventArgs e)    => ApplyBlockAttribute("align", "right");

    private void OnStyleDropdownChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PART_StyleDropdown?.SelectedItem is not ComboBoxItem item) return;
        var style = item.Content?.ToString() ?? "Normal";
        if (style == "Normal")       ApplyBlockAttribute("style", null);
        else if (style.StartsWith("H") && int.TryParse(style[1..], out int lvl))
        {
            ApplyBlockAttribute("style", "heading");
            ApplyBlockAttribute("level", lvl.ToString());
        }
        else                         ApplyBlockAttribute("style", style.ToLowerInvariant());
    }

    private void ApplyBlockAttribute(string attribute, object? value)
    {
        if (IsReadOnly || _mutator is null) return;
        PART_TextPane.PART_Renderer.SetBlockAttribute(attribute, value);
    }

    public void ApplyFormat(string format)
    {
        if (IsReadOnly || _mutator is null) return;
        PART_TextPane.PART_Renderer.ApplyFormatToSelection(format, true);
    }

    // ── View mode toolbar handlers ────────────────────────────────────────────

    private void OnFullModeClicked(object sender, RoutedEventArgs e)    => ViewMode = DocumentViewMode.Full;
    private void OnFocusModeClicked(object sender, RoutedEventArgs e)   => ViewMode = DocumentViewMode.Focus;
    private void OnPageModeClicked(object sender, RoutedEventArgs e)    => RenderMode = DocumentRenderMode.Page;
    private void OnDraftModeClicked(object sender, RoutedEventArgs e)   => RenderMode = DocumentRenderMode.Draft;
    private void OnOutlineModeClicked(object sender, RoutedEventArgs e) => RenderMode = DocumentRenderMode.Outline;

    // ── Phase 18: Styles panel ────────────────────────────────────────────────

    private void OnStylesPanelStyleSelected(object? sender, string styleKey)
    {
        if (IsReadOnly || _mutator is null) return;
        PART_TextPane.PART_Renderer.SetBlockAttribute("style", styleKey);
        if (FindName("PART_StylesPopup") is System.Windows.Controls.Primitives.Popup popup)
            popup.IsOpen = false;
    }

    private void OnStylesBtnClicked(object sender, RoutedEventArgs e)
    {
        if (FindName("PART_StylesPopup") is System.Windows.Controls.Primitives.Popup popup)
            popup.IsOpen = !popup.IsOpen;
    }

    // ── Phase 19: Find & Replace (Ctrl+F / Ctrl+H) ───────────────────────────

    private void OnHostPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;

        if (e.Key == Key.F)  { OpenFindDialog(showReplace: false); e.Handled = true; }
        // Ctrl+H = find/replace in document; Ctrl+Shift+H is workspace-wide (handled by MainWindow)
        if (e.Key == Key.H && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
            { OpenFindDialog(showReplace: true); e.Handled = true; }
        if (e.Key == Key.S)  { Save(); e.Handled = true; }
    }

    private void OpenFindDialog(bool showReplace)
    {
        if (_vm?.Model is null) return;
        var searchVm = new ViewModels.DocumentSearchViewModel(_vm.Model, PART_TextPane.PART_Renderer);
        var target   = new ViewModels.DocumentSearchTarget(searchVm);
        PART_TextPane.ShowQuickSearch(target);
    }

    private void SetPaneVisibility(bool text, bool structure, bool hex)
    {
        PART_TextPane.Visibility      = text      ? Visibility.Visible   : Visibility.Collapsed;
        PART_Splitter1.Visibility     = text && (structure || hex) ? Visibility.Visible : Visibility.Collapsed;
        PART_StructurePane.Visibility = structure ? Visibility.Visible   : Visibility.Collapsed;
        PART_HexPane.Visibility       = hex       ? Visibility.Visible   : Visibility.Collapsed;
        PART_Splitter2.Visibility     = hex && structure ? Visibility.Visible : Visibility.Collapsed;

        Grid.SetColumn(PART_HexPane, 4);
        PART_TextCol.MinWidth   = text      ? 100 : 0;
        PART_StructCol.MinWidth = structure ? 100 : 0;
        PART_HexCol.MinWidth    = hex       ? 100 : 0;
        PART_TextCol.Width      = text      ? new GridLength(2, GridUnitType.Star) : new GridLength(0);
        PART_StructCol.Width    = structure ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        PART_HexCol.Width       = hex       ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        PART_Splitter1Col.Width = (text && (structure || hex)) ? new GridLength(4) : new GridLength(0);
        PART_Splitter2Col.Width = (structure && hex)           ? new GridLength(4) : new GridLength(0);
    }

    // Track pixel widths across drag deltas (ActualWidth is stale inside DragDelta)
    private double _splitter1TextPx = double.NaN;
    private double _splitter1HexPx  = double.NaN;

    private void OnSplitter1DragDelta(object sender, DragDeltaEventArgs e)
    {
        // GridSplitter is in col 1: natively resizes col 0 (text) and col 2 (struct).
        // In Split mode (struct=0) we take over: redistribute delta between text and hex,
        // and reset struct col back to 0 to undo what GridSplitter just did.
        if (PART_StructCol.Width.Value > 0) return; // three-pane: let GridSplitter handle

        // Initialise on first delta of each drag sequence
        if (double.IsNaN(_splitter1TextPx))
        {
            _splitter1TextPx = PART_TextCol.ActualWidth;
            _splitter1HexPx  = PART_HexCol.ActualWidth;
        }

        _splitter1TextPx += e.HorizontalChange;
        _splitter1HexPx  -= e.HorizontalChange;

        const double minW = 100;
        if (_splitter1TextPx < minW) { _splitter1HexPx -= (minW - _splitter1TextPx); _splitter1TextPx = minW; }
        if (_splitter1HexPx  < minW) { _splitter1TextPx -= (minW - _splitter1HexPx); _splitter1HexPx  = minW; }

        PART_TextCol.Width   = new GridLength(_splitter1TextPx, GridUnitType.Pixel);
        PART_StructCol.Width = new GridLength(0);   // undo GridSplitter's expansion of col 2
        PART_HexCol.Width    = new GridLength(_splitter1HexPx,  GridUnitType.Pixel);
        e.Handled = true;
    }

    private void OnSplitter1DragCompleted(object sender, DragCompletedEventArgs e)
    {
        _splitter1TextPx = double.NaN;
        _splitter1HexPx  = double.NaN;
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
    private void OnExportClicked(object sender, RoutedEventArgs e)
    {
        if (_vm?.Model is null) return;

        var savers = _ideContext?.ExtensionRegistry
                         .GetExtensions<IDocumentSaver>()
                     ?? [];

        // Build SaveFileDialog filter from registered savers
        var filterParts = savers
            .Select(s => $"{s.SaverName}|*{string.Join(";*", s.SupportedExtensions)}")
            .ToList();
        filterParts.Add("All Files|*.*");

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title            = DocumentEditorResources.DocEditorHost_ExportDialogTitle,
            Filter           = string.Join("|", filterParts),
            FileName         = System.IO.Path.GetFileName(_vm.Model.FilePath),
            InitialDirectory = System.IO.Path.GetDirectoryName(_vm.Model.FilePath) ?? string.Empty,
        };

        if (dlg.ShowDialog() != true) return;

        var targetPath = dlg.FileName;
        var saver      = savers.FirstOrDefault(s => s.CanSave(targetPath));
        if (saver is null)
        {
            var ext = System.IO.Path.GetExtension(targetPath);
            StatusMessage?.Invoke(this, string.Format(DocumentEditorResources.DocEditorHost_NoSaverForExport, ext));
            return;
        }

        _ = ExportToAsync(saver, targetPath);
    }

    private async Task ExportToAsync(IDocumentSaver saver, string targetPath)
    {
        if (_vm?.Model is null) return;
        try
        {
            IsBusy = true;
            var tmp = targetPath + ".tmp";
            await using (var fs = File.Create(tmp))
                await saver.SaveAsync(_vm.Model, fs);
            if (File.Exists(targetPath)) File.Delete(targetPath);
            File.Move(tmp, targetPath);
            var path = System.IO.Path.GetFileName(targetPath);
            StatusMessage?.Invoke(this, string.Format(DocumentEditorResources.DocEditorHost_ExportedStatus, path));
        }
        catch (Exception ex)
        {
            StatusMessage?.Invoke(this, string.Format(DocumentEditorResources.DocEditorHost_ExportFailedStatus, ex.Message));
            OutputMessage?.Invoke(this, $"[DocEditor] Export error: {ex}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnMetadataClicked(object sender, RoutedEventArgs e)
    {
        var meta = _vm?.Model?.Metadata;
        if (meta is null) return;

        PART_Meta_Title.Text    = string.IsNullOrEmpty(meta.Title)         ? DocumentEditorResources.DocEditorHost_MetaEmptyValue : meta.Title;
        PART_Meta_Author.Text   = string.IsNullOrEmpty(meta.Author)        ? DocumentEditorResources.DocEditorHost_MetaEmptyValue : meta.Author;
        PART_Meta_Format.Text   = string.IsNullOrEmpty(meta.FormatVersion) ? DocumentEditorResources.DocEditorHost_MetaEmptyValue : meta.FormatVersion;
        PART_Meta_Mime.Text     = string.IsNullOrEmpty(meta.MimeType)      ? DocumentEditorResources.DocEditorHost_MetaEmptyValue : meta.MimeType;
        PART_Meta_Created.Text  = meta.CreatedUtc.HasValue
            ? meta.CreatedUtc.Value.ToLocalTime().ToString("g")
            : DocumentEditorResources.DocEditorHost_MetaEmptyValue;
        PART_Meta_Modified.Text = meta.ModifiedUtc.HasValue
            ? meta.ModifiedUtc.Value.ToLocalTime().ToString("g")
            : DocumentEditorResources.DocEditorHost_MetaEmptyValue;
        PART_Meta_Macros.Text   = meta.HasMacros ? DocumentEditorResources.DocEditorHost_MetaYesValue : DocumentEditorResources.DocEditorHost_MetaNoValue;

        PART_MetadataPopup.IsOpen = true;
    }

    // ── Page Setup flyout ────────────────────────────────────────────────────

    private void OnPageSetupBtnClick(object sender, RoutedEventArgs e)
    {
        PART_PageSettingsPanel.LoadSettings(PART_TextPane.PageSettings);
        PART_PageSetupPopup.IsOpen = true;
    }

    private void OnPageSettingsApplied(object sender, DocumentPageSettings settings)
    {
        PART_TextPane.PageSettings = settings;
        PART_PageSetupPopup.IsOpen = false;
    }

    private void OnPageSettingsCancelled(object sender, EventArgs e)
        => PART_PageSetupPopup.IsOpen = false;

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

        _vm      = new DocumentEditorViewModel(model);
        _mutator = new DocumentMutator(model);
        DataContext = _vm;

        PART_TextPane.BindModel(model);
        PART_StructurePane.BindModel(model);
        PART_HexPane.BindModel(model);
        PART_MiniMap.BindModel(model);
        PART_StatusBar.BindModel(model, _currentFileExtension);

        // Pass mutator to renderer (Phase 12+)
        PART_TextPane.PART_Renderer.SetMutator(_mutator);

        // Apply page settings declared by the document (overrides A4 default)
        if (model.PageSettings is { } ps)
            PART_TextPane.PageSettings = ps;

        // Apply initial zoom and read-only state
        PART_TextPane.SetZoom(ZoomLevel);
        PART_TextPane.PART_Renderer.IsReadOnly = IsReadOnly;

        // Wire hex highlight manager
        _hexHighlightMgr = new DocumentHexHighlightManager(PART_HexPane);
        _mutator.BlockMutated += OnBlockMutated;

        _syncService = new BinaryMapSyncService(model);
        _syncService.Wire(PART_TextPane, PART_HexPane);

        ApplyViewMode(ViewMode);
        ApplyRenderMode(RenderMode);

        // Auto-detect read-only from disk
        if (File.Exists(model.FilePath))
            IsReadOnly = new FileInfo(model.FilePath).IsReadOnly;

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

    private void OnBlockMutated(object? sender, BlockMutatedArgs e)
    {
        _hexHighlightMgr?.Apply(e.Block, e.Kind);
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

    private sealed class RelayCmd(Action execute, Func<bool> canExecute) : ICommand
    {
        public bool CanExecute(object? _) => canExecute();
        public void Execute(object? _)    => execute();
        public event EventHandler? CanExecuteChanged
        {
            add    => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}
