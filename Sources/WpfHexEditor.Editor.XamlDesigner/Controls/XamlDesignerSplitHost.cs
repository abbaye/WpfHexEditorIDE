// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: XamlDesignerSplitHost.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// Description:
//     Main document editor for .xaml files.
//     Hosts a CodeEditorSplitHost (code pane) and a DesignCanvas (design pane)
//     side-by-side with a split ratio controlled by a GridSplitter.
//     Provides Code / Split / Design view mode toggle buttons in a toolbar strip.
//     Auto-preview debounces code changes and re-renders the canvas.
//
// Architecture Notes:
//     Proxy / Delegate Pattern — IDocumentEditor forwarded to the CodeEditorSplitHost.
//     Composite — wraps CodeEditorSplitHost + DesignCanvas.
//     Observer — DesignCanvas.RenderError drives the error banner visibility.
//     View mode state machine: CodeOnly / Split / DesignOnly.
// ==========================================================

using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WpfHexEditor.Editor.CodeEditor;
using WpfHexEditor.Editor.CodeEditor.Controls;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.XamlDesigner.Models;
using WpfHexEditor.ProjectSystem.Languages;

namespace WpfHexEditor.Editor.XamlDesigner.Controls;

/// <summary>
/// Split-pane XAML designer document editor.
/// </summary>
public sealed class XamlDesignerSplitHost : Grid,
    IDocumentEditor,
    IOpenableDocument,
    IEditorPersistable,
    IStatusBarContributor
{
    // ── View mode ─────────────────────────────────────────────────────────────

    private enum ViewMode { CodeOnly, Split, DesignOnly }
    private ViewMode _viewMode = ViewMode.Split;

    // ── Child controls ────────────────────────────────────────────────────────

    private readonly CodeEditorSplitHost _codeHost;
    private readonly DesignCanvas        _designCanvas;
    private readonly GridSplitter        _splitter;

    private readonly ColumnDefinition    _codeColumn;
    private readonly ColumnDefinition    _splitterColumn;
    private readonly ColumnDefinition    _designColumn;

    // Toolbar strip controls
    private readonly ToggleButton _btnCodeOnly;
    private readonly ToggleButton _btnSplit;
    private readonly ToggleButton _btnDesignOnly;
    private readonly ToggleButton _btnAutoPreview;
    private readonly Border       _errorBanner;
    private readonly TextBlock    _errorText;

    // ── Auto-preview debounce ─────────────────────────────────────────────────

    private readonly DispatcherTimer _previewTimer;
    private bool _autoPreviewEnabled = true;

    // ── Document state ────────────────────────────────────────────────────────

    private readonly XamlDocument _document = new();
    private string?  _filePath;

    // ── Status bar ─────────────────────────────────────────────────────────────

    private readonly StatusBarItem _sbElement      = new() { Label = "XAML", Value = "" };
    private readonly StatusBarItem _sbCoordinates  = new() { Label = "Pos",  Value = "" };

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Fired when the selected element changes (used by the plugin to sync panels).</summary>
    public event EventHandler? SelectedElementChanged;

    // ── Constructor ───────────────────────────────────────────────────────────

    public XamlDesignerSplitHost()
    {
        // -- Column layout ---------------------------------------------------
        _codeColumn    = new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) };
        _splitterColumn= new ColumnDefinition { Width = new GridLength(4) };
        _designColumn  = new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) };

        ColumnDefinitions.Add(_codeColumn);
        ColumnDefinitions.Add(_splitterColumn);
        ColumnDefinitions.Add(_designColumn);

        // -- Row layout ------------------------------------------------------
        var toolbarRow = new RowDefinition { Height = GridLength.Auto };
        var contentRow = new RowDefinition { Height = new GridLength(1, GridUnitType.Star) };

        RowDefinitions.Add(toolbarRow);
        RowDefinitions.Add(contentRow);

        // -- Toolbar strip ---------------------------------------------------
        var toolbar = BuildToolbar(out _btnCodeOnly, out _btnSplit, out _btnDesignOnly,
                                   out _btnAutoPreview, out _errorBanner, out _errorText);
        SetRow(toolbar, 0);
        SetColumnSpan(toolbar, 3);
        Children.Add(toolbar);

        // -- Code pane -------------------------------------------------------
        _codeHost = new CodeEditorSplitHost();
        SetRow(_codeHost, 1);
        SetColumn(_codeHost, 0);
        Children.Add(_codeHost);

        // -- GridSplitter ----------------------------------------------------
        _splitter = new GridSplitter
        {
            Width               = 4,
            VerticalAlignment   = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ResizeDirection     = GridResizeDirection.Columns,
            ResizeBehavior      = GridResizeBehavior.PreviousAndNext
        };
        _splitter.SetResourceReference(BackgroundProperty, "DockSplitterBrush");
        SetRow(_splitter, 1);
        SetColumn(_splitter, 1);
        Children.Add(_splitter);

        // -- Design canvas ---------------------------------------------------
        _designCanvas = new DesignCanvas();
        SetRow(_designCanvas, 1);
        SetColumn(_designCanvas, 2);
        Children.Add(_designCanvas);

        // -- Auto-preview timer ----------------------------------------------
        _previewTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _previewTimer.Tick += OnPreviewTimerTick;

        // -- Wire events -----------------------------------------------------
        _codeHost.PrimaryEditor.ModifiedChanged += OnCodeModified;
        _designCanvas.RenderError               += OnRenderError;
        _designCanvas.SelectedElementChanged    += OnDesignSelectionChanged;

        // -- Forward IDocumentEditor events from code pane ------------------
        _codeHost.ModifiedChanged  += (s, e) => ModifiedChanged?.Invoke(this, e);
        _codeHost.CanUndoChanged   += (s, e) => CanUndoChanged?.Invoke(this, e);
        _codeHost.CanRedoChanged   += (s, e) => CanRedoChanged?.Invoke(this, e);
        _codeHost.TitleChanged     += (s, e) => TitleChanged?.Invoke(this, e);
        _codeHost.StatusMessage    += (s, e) => StatusMessage?.Invoke(this, e);
        _codeHost.OutputMessage    += (s, e) => OutputMessage?.Invoke(this, e);
        _codeHost.SelectionChanged += (s, e) => SelectionChanged?.Invoke(this, e);
        _codeHost.OperationStarted   += (s, e) => OperationStarted?.Invoke(this, e);
        _codeHost.OperationProgress  += (s, e) => OperationProgress?.Invoke(this, e);
        _codeHost.OperationCompleted += (s, e) => OperationCompleted?.Invoke(this, e);

        // Apply initial view mode.
        ApplyViewMode(ViewMode.Split);
    }

    // ── Public helpers ────────────────────────────────────────────────────────

    /// <summary>Exposes the design canvas for plugin wiring (selection events, etc.).</summary>
    public DesignCanvas Canvas => _designCanvas;

    // ── IOpenableDocument ─────────────────────────────────────────────────────

    async Task IOpenableDocument.OpenAsync(string filePath, CancellationToken ct)
    {
        _filePath = filePath;

        // Inject XAML language highlighter.
        if (_codeHost.PrimaryEditor.ExternalHighlighter is null)
        {
            var language = LanguageRegistry.Instance.GetLanguageForFile(filePath);
            if (language is not null)
            {
                var highlighter = CodeEditorFactory.BuildHighlighter(language);
                _codeHost.PrimaryEditor.ExternalHighlighter   = highlighter;
                _codeHost.SecondaryEditor.ExternalHighlighter = highlighter;
            }
        }

        await ((IOpenableDocument)_codeHost).OpenAsync(filePath, ct);

        // Trigger initial render after the file is loaded.
        if (_autoPreviewEnabled)
            TriggerPreview();
    }

    // ── IDocumentEditor (proxy to _codeHost) ─────────────────────────────────

    private IDocumentEditor Active => _codeHost;

    public bool     IsDirty    => Active.IsDirty;
    public bool     CanUndo    => Active.CanUndo;
    public bool     CanRedo    => Active.CanRedo;
    public bool     IsReadOnly { get => Active.IsReadOnly; set { Active.IsReadOnly = value; } }
    public string   Title      => Active.Title;
    public bool     IsBusy     => Active.IsBusy;

    public ICommand? UndoCommand      => Active.UndoCommand;
    public ICommand? RedoCommand      => Active.RedoCommand;
    public ICommand? SaveCommand      => Active.SaveCommand;
    public ICommand? CopyCommand      => Active.CopyCommand;
    public ICommand? CutCommand       => Active.CutCommand;
    public ICommand? PasteCommand     => Active.PasteCommand;
    public ICommand? DeleteCommand    => Active.DeleteCommand;
    public ICommand? SelectAllCommand => Active.SelectAllCommand;

    public void Undo()          => Active.Undo();
    public void Redo()          => Active.Redo();
    public void Save()          => Active.Save();
    public Task SaveAsync(CancellationToken ct = default)                    => Active.SaveAsync(ct);
    public Task SaveAsAsync(string filePath, CancellationToken ct = default) => Active.SaveAsAsync(filePath, ct);
    public void Copy()          => Active.Copy();
    public void Cut()           => Active.Cut();
    public void Paste()         => Active.Paste();
    public void Delete()        => Active.Delete();
    public void SelectAll()     => Active.SelectAll();
    public void CancelOperation() => Active.CancelOperation();
    public void Close()
    {
        _previewTimer.Stop();
        ((IDocumentEditor)_codeHost).Close();
    }

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

    // ── IEditorPersistable ────────────────────────────────────────────────────

    public EditorConfigDto GetEditorConfig()
    {
        var dto = (_codeHost is IEditorPersistable p)
            ? p.GetEditorConfig()
            : new EditorConfigDto();

        // Persist XAML designer-specific state in the Extra dictionary.
        dto.Extra ??= new Dictionary<string, string>();
        dto.Extra["xd.view"]    = _viewMode.ToString();
        dto.Extra["xd.ratio"]   = GetSplitRatio().ToString("F4", System.Globalization.CultureInfo.InvariantCulture);
        dto.Extra["xd.selPath"] = _designCanvas.SelectedElement is null
            ? string.Empty
            : GetElementPath(_designCanvas.SelectedElement);

        return dto;
    }

    public void ApplyEditorConfig(EditorConfigDto config)
    {
        if (_codeHost is IEditorPersistable p)
            p.ApplyEditorConfig(config);

        if (config.Extra is not null)
        {
            if (config.Extra.TryGetValue("xd.view", out var viewStr)
                && Enum.TryParse<ViewMode>(viewStr, out var mode))
                ApplyViewMode(mode);

            if (config.Extra.TryGetValue("xd.ratio", out var ratioStr)
                && double.TryParse(ratioStr, System.Globalization.NumberStyles.Float,
                                   System.Globalization.CultureInfo.InvariantCulture, out var ratio))
                SetSplitRatio(ratio);
        }
    }

    public byte[]? GetUnsavedModifications()
        => (_codeHost as IEditorPersistable)?.GetUnsavedModifications();

    public void ApplyUnsavedModifications(byte[] data)
        => (_codeHost as IEditorPersistable)?.ApplyUnsavedModifications(data);

    public ChangesetSnapshot GetChangesetSnapshot()
        => (_codeHost as IEditorPersistable)?.GetChangesetSnapshot() ?? ChangesetSnapshot.Empty;

    public void ApplyChangeset(ChangesetDto changeset)
        => (_codeHost as IEditorPersistable)?.ApplyChangeset(changeset);

    public void MarkChangesetSaved()
        => (_codeHost as IEditorPersistable)?.MarkChangesetSaved();

    public IReadOnlyList<BookmarkDto>? GetBookmarks()
        => (_codeHost as IEditorPersistable)?.GetBookmarks();

    public void ApplyBookmarks(IReadOnlyList<BookmarkDto> bookmarks)
        => (_codeHost as IEditorPersistable)?.ApplyBookmarks(bookmarks);

    // ── IStatusBarContributor ─────────────────────────────────────────────────

    public ObservableCollection<StatusBarItem> StatusBarItems { get; } = new();

    public void RefreshStatusBarItems()
    {
        if (StatusBarItems.Count == 0)
        {
            StatusBarItems.Add(_sbElement);
            StatusBarItems.Add(_sbCoordinates);
        }

        var el = _designCanvas.SelectedElement;
        _sbElement.Value     = el?.GetType().Name ?? string.Empty;
        _sbCoordinates.Value = el is System.Windows.FrameworkElement fe
            ? $"{fe.ActualWidth:F0} × {fe.ActualHeight:F0}"
            : string.Empty;
    }

    // ── Auto-preview ──────────────────────────────────────────────────────────

    private void OnCodeModified(object? sender, EventArgs e)
    {
        if (!_autoPreviewEnabled) return;
        _previewTimer.Stop();
        _previewTimer.Start();
    }

    private void OnPreviewTimerTick(object? sender, EventArgs e)
    {
        _previewTimer.Stop();
        TriggerPreview();
    }

    private void TriggerPreview()
    {
        var text = _codeHost.PrimaryEditor.Document?.GetText() ?? string.Empty;
        _document.SetXaml(text);
        _designCanvas.XamlSource = text;
    }

    // ── Render error ──────────────────────────────────────────────────────────

    private void OnRenderError(object? sender, string? message)
    {
        _errorBanner.Visibility = message is not null ? Visibility.Visible : Visibility.Collapsed;
        _errorText.Text         = message ?? string.Empty;
    }

    // ── Design canvas selection ───────────────────────────────────────────────

    private void OnDesignSelectionChanged(object? sender, EventArgs e)
    {
        RefreshStatusBarItems();
        SelectedElementChanged?.Invoke(this, EventArgs.Empty);
    }

    // ── View mode ─────────────────────────────────────────────────────────────

    private void ApplyViewMode(ViewMode mode)
    {
        _viewMode = mode;

        // Update column widths.
        switch (mode)
        {
            case ViewMode.CodeOnly:
                _codeColumn.Width    = new GridLength(1, GridUnitType.Star);
                _splitterColumn.Width= new GridLength(0);
                _designColumn.Width  = new GridLength(0);
                _splitter.Visibility = Visibility.Collapsed;
                break;

            case ViewMode.Split:
                _codeColumn.Width    = new GridLength(1, GridUnitType.Star);
                _splitterColumn.Width= new GridLength(4);
                _designColumn.Width  = new GridLength(1, GridUnitType.Star);
                _splitter.Visibility = Visibility.Visible;
                break;

            case ViewMode.DesignOnly:
                _codeColumn.Width    = new GridLength(0);
                _splitterColumn.Width= new GridLength(0);
                _designColumn.Width  = new GridLength(1, GridUnitType.Star);
                _splitter.Visibility = Visibility.Collapsed;
                break;
        }

        // Sync toggle button states without re-entrancy.
        _btnCodeOnly.IsChecked   = mode == ViewMode.CodeOnly;
        _btnSplit.IsChecked      = mode == ViewMode.Split;
        _btnDesignOnly.IsChecked = mode == ViewMode.DesignOnly;
    }

    // ── Persistence helpers ───────────────────────────────────────────────────

    private double GetSplitRatio()
    {
        double total = _codeColumn.ActualWidth + _designColumn.ActualWidth;
        return total > 0 ? _codeColumn.ActualWidth / total : 0.5;
    }

    private void SetSplitRatio(double ratio)
    {
        if (_viewMode != ViewMode.Split) return;
        _codeColumn.Width   = new GridLength(ratio,       GridUnitType.Star);
        _designColumn.Width = new GridLength(1.0 - ratio, GridUnitType.Star);
    }

    private static string GetElementPath(UIElement el)
        => el.GetType().Name; // Simplified — full XPath resolution is Phase 3.

    // ── Toolbar builder ───────────────────────────────────────────────────────

    private Border BuildToolbar(
        out ToggleButton btnCode, out ToggleButton btnSplit, out ToggleButton btnDesign,
        out ToggleButton btnAuto, out Border errorBanner, out TextBlock errorText)
    {
        // Toggle button factory.
        ToggleButton MakeToggle(string glyph, string tooltip)
        {
            var btn = new ToggleButton
            {
                Content             = glyph,
                FontFamily          = new FontFamily("Segoe MDL2 Assets"),
                FontSize            = 11,
                Width               = 26,
                Height              = 22,
                Padding             = new Thickness(0),
                Margin              = new Thickness(1, 0, 0, 0),
                VerticalAlignment   = VerticalAlignment.Center,
                ToolTip             = tooltip,
                Background          = Brushes.Transparent,
                BorderThickness     = new Thickness(0)
            };
            btn.SetResourceReference(ForegroundProperty, "DockMenuForegroundBrush");
            return btn;
        }

        btnCode   = MakeToggle("\uE8A5", "Code only");
        btnSplit  = MakeToggle("\uE70D", "Split view");
        btnDesign = MakeToggle("\uE769", "Design only");
        btnAuto   = MakeToggle("\uE8EA", "Auto-preview on / off");
        btnAuto.IsChecked = true;

        // Wire toggle clicks.
        btnCode.Click   += (_, _) => { if (btnCode.IsChecked == true)   ApplyViewMode(ViewMode.CodeOnly); };
        btnSplit.Click  += (_, _) => { if (btnSplit.IsChecked == true)  ApplyViewMode(ViewMode.Split); };
        btnDesign.Click += (_, _) => { if (btnDesign.IsChecked == true) ApplyViewMode(ViewMode.DesignOnly); };
        btnAuto.Checked   += (_, _) => _autoPreviewEnabled = true;
        btnAuto.Unchecked += (_, _) => { _autoPreviewEnabled = false; _previewTimer.Stop(); };

        // Error banner (hidden by default).
        errorText = new TextBlock
        {
            FontSize  = 11,
            Margin    = new Thickness(6, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming      = TextTrimming.CharacterEllipsis
        };
        errorText.SetResourceReference(ForegroundProperty, "XD_ErrorBannerForeground");

        errorBanner = new Border
        {
            Visibility      = Visibility.Collapsed,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding         = new Thickness(4, 2, 4, 2),
            Child           = errorText
        };
        errorBanner.SetResourceReference(BackgroundProperty,   "XD_ErrorBannerBackground");
        errorBanner.SetResourceReference(BorderBrushProperty,  "XD_ErrorBannerBorder");

        // Assemble toolbar DockPanel.
        var dp = new DockPanel { LastChildFill = true };
        dp.SetResourceReference(BackgroundProperty, "XD_PanelToolbarBrush");

        var leftStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 2, 0, 2) };
        leftStack.Children.Add(btnCode);
        leftStack.Children.Add(btnSplit);
        leftStack.Children.Add(btnDesign);

        var sep = new Separator { Width = 1, Margin = new Thickness(4, 2, 4, 2) };
        sep.SetResourceReference(BackgroundProperty, "DockBorderBrush");
        leftStack.Children.Add(sep);
        leftStack.Children.Add(btnAuto);

        DockPanel.SetDock(leftStack, Dock.Left);
        dp.Children.Add(leftStack);
        dp.Children.Add(errorBanner);

        var container = new Border
        {
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child           = dp
        };
        container.SetResourceReference(BorderBrushProperty, "XD_PanelToolbarBorderBrush");

        return container;
    }
}
