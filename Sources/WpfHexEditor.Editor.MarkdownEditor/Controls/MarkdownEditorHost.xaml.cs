// ==========================================================
// Project: WpfHexEditor.Editor.MarkdownEditor
// File: Controls/MarkdownEditorHost.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     GitHub-Flavored Markdown split-host editor.
//     Wraps a TextEditor (source pane) + MarkdownPreviewPane (preview pane)
//     in a configurable split layout.
//
//     Implements the full IDocumentEditor contract by delegating to the
//     inner TextEditor instance, plus:
//       - IEditorToolbarContributor  (View / Layout / Sync / Refresh pods)
//       - IStatusBarContributor      (View mode / Word count / Line count)
//       - IEditorPersistable         (layout + view mode persisted via Extra dict)
//       - INavigableDocument         (delegates to inner TextEditor)
//       - IOpenableDocument          (loads file, triggers initial render)
//       - ISearchTarget              (delegates to inner TextEditor)
//
// Architecture Notes:
//     Pattern: Adapter/Proxy + Composite
//     UpdateGridLayout() is the single source-of-truth for all grid geometry.
//     All layout changes go through SetViewMode() / SetSplitLayout() which
//     both delegate to UpdateGridLayout() — identical to XamlDesignerSplitHost.
//     Auto-refresh uses a 500 ms debounce timer.
//     Sync-scroll uses a 200 ms debounce timer.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.MarkdownEditor.Core.Services;
using TextEditorControl = WpfHexEditor.Editor.TextEditor.Controls.TextEditor;

namespace WpfHexEditor.Editor.MarkdownEditor.Controls;

// ─── View mode & layout enums ─────────────────────────────────────────────────

/// <summary>Whether to show the source editor, the preview pane, or both.</summary>
public enum MdViewMode { SourceOnly, Split, PreviewOnly }

/// <summary>Which side the preview occupies in Split mode.</summary>
public enum MdSplitLayout { PreviewRight, PreviewLeft, PreviewBottom, PreviewTop }

// ─── Main class ───────────────────────────────────────────────────────────────

/// <summary>
/// Split-pane host for GitHub-Flavored Markdown editing + live preview.
/// </summary>
public sealed partial class MarkdownEditorHost : UserControl,
    IDocumentEditor,
    IOpenableDocument,
    IEditorPersistable,
    INavigableDocument,
    IStatusBarContributor,
    IEditorToolbarContributor,
    ISearchTarget
{
    // --- Child controls ---------------------------------------------------
    private readonly TextEditorControl   _editor  = new();
    private readonly MarkdownPreviewPane _preview = new();

    // Typed interface references to TextEditor (avoids repeated casts)
    private readonly IDocumentEditor   _editorDoc;
    private readonly INavigableDocument _editorNav;
    private readonly ISearchTarget     _editorSearch;
    private readonly IEditorPersistable _editorPersist;

    // Grid children (created once, repositioned by UpdateGridLayout)
    private readonly GridSplitter _splitter = new()
    {
        Background      = System.Windows.Media.Brushes.Transparent,
        ShowsPreview    = false,
    };

    // --- State ------------------------------------------------------------
    private MdViewMode   _viewMode    = MdViewMode.Split;
    private MdSplitLayout _layout     = MdSplitLayout.PreviewRight;
    private bool          _syncScroll = true;
    private bool          _isDark;
    private string?       _filePath;

    // Debounce timers
    private readonly DispatcherTimer _refreshTimer;
    private readonly DispatcherTimer _syncScrollTimer;
    private double  _pendingScrollPct;

    // --- Toolbar ----------------------------------------------------------
    private readonly ObservableCollection<EditorToolbarItem> _toolbarItems = new();

    // Toolbar items that need runtime updates
    private EditorToolbarItem? _podView;
    private EditorToolbarItem? _podLayout;
    private EditorToolbarItem? _podSync;

    // --- Status bar -------------------------------------------------------
    private readonly ObservableCollection<StatusBarItem> _statusItems = new();
    private readonly StatusBarItem _sbView      = new() { Label = "View",  Tooltip = "Current view mode" };
    private readonly StatusBarItem _sbWordCount = new() { Label = "Words", Tooltip = "Approximate word count" };
    private readonly StatusBarItem _sbLineCount = new() { Label = "Lines", Tooltip = "Total line count" };

    // --- Construction -----------------------------------------------------

    public MarkdownEditorHost()
    {
        InitializeComponent();

        // Cache interface references (TextEditor implements all of these)
        _editorDoc     = _editor;
        _editorNav     = _editor;
        _editorSearch  = _editor;
        _editorPersist = _editor;

        // Auto-refresh timer (500 ms debounce)
        _refreshTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(500),
        };
        _refreshTimer.Tick += OnRefreshTimerTick;

        // Sync-scroll timer (200 ms debounce)
        _syncScrollTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(200),
        };
        _syncScrollTimer.Tick += OnSyncScrollTimerTick;

        // Wire inner editor events
        _editor.ModifiedChanged  += (_, _) => { ModifiedChanged?.Invoke(this, EventArgs.Empty); ScheduleRefresh(); UpdateWordLineCount(); };
        _editor.CanUndoChanged   += (_, _) => CanUndoChanged?.Invoke(this, EventArgs.Empty);
        _editor.CanRedoChanged   += (_, _) => CanRedoChanged?.Invoke(this, EventArgs.Empty);
        _editor.TitleChanged     += (_, s) => TitleChanged?.Invoke(this, s);
        _editor.StatusMessage    += (_, s) => StatusMessage?.Invoke(this, s);
        _editor.OutputMessage    += (_, s) => OutputMessage?.Invoke(this, s);
        _editor.SelectionChanged += (_, _) => SelectionChanged?.Invoke(this, EventArgs.Empty);
        _editor.OperationStarted   += (_, e) => OperationStarted?.Invoke(this, e);
        _editor.OperationProgress  += (_, e) => OperationProgress?.Invoke(this, e);
        _editor.OperationCompleted += (_, e) => OperationCompleted?.Invoke(this, e);

        // Forward scroll percent from TextEditor viewport (for sync-scroll)
        _editor.ViewportScrollChanged += OnEditorScrollChanged;

        // Preview link clicks → open in browser
        _preview.LinkClicked += (_, href) =>
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(href) { UseShellExecute = true }); }
            catch { /* ignore */ }
        };

        // Keyboard shortcuts
        CommandBindings.Add(new CommandBinding(MdCommands.TogglePreview, (_, _) => CycleViewMode()));
        CommandBindings.Add(new CommandBinding(MdCommands.RefreshPreview, (_, _) => _ = ForceRefreshAsync()));
        CommandBindings.Add(new CommandBinding(MdCommands.SourceOnly,    (_, _) => SetViewMode(MdViewMode.SourceOnly)));
        CommandBindings.Add(new CommandBinding(MdCommands.SplitView,     (_, _) => SetViewMode(MdViewMode.Split)));
        CommandBindings.Add(new CommandBinding(MdCommands.PreviewOnly,   (_, _) => SetViewMode(MdViewMode.PreviewOnly)));
        CommandBindings.Add(new CommandBinding(MdCommands.CycleLayout,   (_, _) => CycleSplitLayout()));

        InputBindings.Add(new KeyBinding(MdCommands.TogglePreview,  new KeyGesture(Key.P,  ModifierKeys.Control | ModifierKeys.Shift)));
        InputBindings.Add(new KeyBinding(MdCommands.RefreshPreview, new KeyGesture(Key.F9)));
        InputBindings.Add(new KeyBinding(MdCommands.SourceOnly,     new KeyGesture(Key.D1, ModifierKeys.Control)));
        InputBindings.Add(new KeyBinding(MdCommands.SplitView,      new KeyGesture(Key.D2, ModifierKeys.Control)));
        InputBindings.Add(new KeyBinding(MdCommands.PreviewOnly,    new KeyGesture(Key.D3, ModifierKeys.Control)));
        InputBindings.Add(new KeyBinding(MdCommands.CycleLayout,    new KeyGesture(Key.L,  ModifierKeys.Control | ModifierKeys.Shift)));

        BuildToolbarItems();
        BuildStatusBar();
        UpdateGridLayout();

        Loaded += async (_, _) =>
        {
            _isDark = DetectDarkTheme();
            await _preview.InitializeAsync();
        };
    }

    // --- IDocumentEditor (delegated to inner TextEditor) ------------------

    public bool IsDirty    => _editor.IsDirty;
    public bool CanUndo    => _editor.CanUndo;
    public bool CanRedo    => _editor.CanRedo;
    public bool IsBusy     => _editor.IsBusy;
    public bool IsReadOnly
    {
        get => _editor.IsReadOnly;
        set => _editor.IsReadOnly = value;
    }
    public int  UndoCount  => _editorDoc.UndoCount;
    public int  RedoCount  => _editorDoc.RedoCount;
    public string Title    => _editor.Title;

    public ICommand? UndoCommand      => _editor.UndoCommand;
    public ICommand? RedoCommand      => _editor.RedoCommand;
    public ICommand? SaveCommand      => _editor.SaveCommand;
    public ICommand? CopyCommand      => _editor.CopyCommand;
    public ICommand? CutCommand       => _editor.CutCommand;
    public ICommand? PasteCommand     => _editor.PasteCommand;
    public ICommand? DeleteCommand    => _editor.DeleteCommand;
    public ICommand? SelectAllCommand => _editor.SelectAllCommand;

    public void Undo()        => _editor.Undo();
    public void Redo()        => _editor.Redo();
    public void Save()        => _editor.Save();
    public Task SaveAsync(CancellationToken ct = default) => _editor.SaveAsync(ct);
    public Task SaveAsAsync(string filePath, CancellationToken ct = default) => _editor.SaveAsAsync(filePath, ct);
    public void Copy()        => _editor.Copy();
    public void Cut()         => _editor.Cut();
    public void Paste()       => _editor.Paste();
    public void Delete()      => _editor.Delete();
    public void SelectAll()   => _editor.SelectAll();
    public void CancelOperation() => _editor.CancelOperation();
    public void Close()
    {
        _refreshTimer.Stop();
        _syncScrollTimer.Stop();
        _editor.Close();
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

    // --- IOpenableDocument ------------------------------------------------

    public async Task OpenAsync(string filePath, CancellationToken ct = default)
    {
        _filePath = filePath;
        await _editor.OpenAsync(filePath, ct);
        UpdateWordLineCount();
        await ForceRefreshAsync();
    }

    // --- INavigableDocument -----------------------------------------------

    public void NavigateTo(int line, int column) => _editorNav.NavigateTo(line, column);

    // --- IEditorPersistable -----------------------------------------------

    public EditorConfigDto GetEditorConfig()
    {
        var dto = _editorPersist.GetEditorConfig();
        dto.Extra ??= new Dictionary<string, string>();
        dto.Extra["md.viewMode"] = _viewMode.ToString();
        dto.Extra["md.layout"]   = _layout.ToString();
        dto.Extra["md.sync"]     = _syncScroll.ToString();
        return dto;
    }

    public void ApplyEditorConfig(EditorConfigDto config)
    {
        _editorPersist.ApplyEditorConfig(config);

        if (config?.Extra is null) return;

        if (config.Extra.TryGetValue("md.viewMode", out var vm) &&
            Enum.TryParse<MdViewMode>(vm, out var viewMode))
            SetViewMode(viewMode);

        if (config.Extra.TryGetValue("md.layout", out var lt) &&
            Enum.TryParse<MdSplitLayout>(lt, out var layout))
            SetSplitLayout(layout);

        if (config.Extra.TryGetValue("md.sync", out var sync) &&
            bool.TryParse(sync, out var syncBool))
            SetSyncScroll(syncBool);
    }

    public byte[]? GetUnsavedModifications()            => _editorPersist.GetUnsavedModifications();
    public void ApplyUnsavedModifications(byte[] data)  => _editorPersist.ApplyUnsavedModifications(data);
    public ChangesetSnapshot GetChangesetSnapshot()     => _editorPersist.GetChangesetSnapshot();
    public void ApplyChangeset(ChangesetDto changeset)  => _editorPersist.ApplyChangeset(changeset);
    public void MarkChangesetSaved()                    => _editorPersist.MarkChangesetSaved();
    public IReadOnlyList<BookmarkDto>? GetBookmarks()   => _editorPersist.GetBookmarks();
    public void ApplyBookmarks(IReadOnlyList<BookmarkDto> bookmarks) => _editorPersist.ApplyBookmarks(bookmarks);

    // --- IEditorToolbarContributor ----------------------------------------

    public ObservableCollection<EditorToolbarItem> ToolbarItems => _toolbarItems;

    // --- IStatusBarContributor --------------------------------------------

    public ObservableCollection<StatusBarItem> StatusBarItems => _statusItems;

    public void RefreshStatusBarItems() => UpdateStatusBar();

    // --- ISearchTarget (delegated to inner TextEditor) --------------------

    public SearchBarCapabilities Capabilities   => _editorSearch.Capabilities;
    public int MatchCount                        => _editorSearch.MatchCount;
    public int CurrentMatchIndex                 => _editorSearch.CurrentMatchIndex;
    public event EventHandler? SearchResultsChanged
    {
        add    => _editorSearch.SearchResultsChanged += value;
        remove => _editorSearch.SearchResultsChanged -= value;
    }
    public void Find(string query, SearchTargetOptions options = default) => _editorSearch.Find(query, options);
    public void FindNext()       => _editorSearch.FindNext();
    public void FindPrevious()   => _editorSearch.FindPrevious();
    public void ClearSearch()    => _editorSearch.ClearSearch();
    public void Replace(string replacement)    => _editorSearch.Replace(replacement);
    public void ReplaceAll(string replacement) => _editorSearch.ReplaceAll(replacement);
    public UIElement? GetCustomFiltersContent() => _editorSearch.GetCustomFiltersContent();

    // --- View mode & layout -----------------------------------------------

    private void SetViewMode(MdViewMode mode)
    {
        if (_viewMode == mode) return;
        _viewMode = mode;
        UpdateGridLayout();
        SyncViewModeToToolbar();
        UpdateStatusBar();
    }

    private void SetSplitLayout(MdSplitLayout layout)
    {
        if (_layout == layout) return;
        _layout = layout;
        UpdateGridLayout();
        SyncLayoutToToolbar();
    }

    private void SetSyncScroll(bool value)
    {
        _syncScroll = value;
        if (_podSync is not null)
            _podSync.IsChecked = value;
    }

    private void CycleViewMode()
    {
        SetViewMode(_viewMode switch
        {
            MdViewMode.SourceOnly  => MdViewMode.Split,
            MdViewMode.Split       => MdViewMode.PreviewOnly,
            _                      => MdViewMode.SourceOnly,
        });
    }

    private void CycleSplitLayout()
    {
        SetSplitLayout(_layout switch
        {
            MdSplitLayout.PreviewRight  => MdSplitLayout.PreviewLeft,
            MdSplitLayout.PreviewLeft   => MdSplitLayout.PreviewBottom,
            MdSplitLayout.PreviewBottom => MdSplitLayout.PreviewTop,
            _                           => MdSplitLayout.PreviewRight,
        });
    }

    // --- Grid layout (single source-of-truth) -----------------------------

    /// <summary>
    /// Rebuilds the root grid to reflect the current <see cref="_viewMode"/>
    /// and <see cref="_layout"/>.  Called every time either value changes.
    /// </summary>
    private void UpdateGridLayout()
    {
        _rootGrid.Children.Clear();
        _rootGrid.ColumnDefinitions.Clear();
        _rootGrid.RowDefinitions.Clear();

        bool showEditor  = _viewMode != MdViewMode.PreviewOnly;
        bool showPreview = _viewMode != MdViewMode.SourceOnly;
        bool isSplit     = _viewMode == MdViewMode.Split;

        // Apply splitter styling
        bool isVertical = _layout == MdSplitLayout.PreviewRight ||
                          _layout == MdSplitLayout.PreviewLeft;
        if (isVertical)
        {
            _splitter.Width           = 4;
            _splitter.Height          = double.NaN;
            _splitter.ResizeDirection = GridResizeDirection.Columns;
            _splitter.VerticalAlignment = VerticalAlignment.Stretch;
            _splitter.HorizontalAlignment = HorizontalAlignment.Stretch;
        }
        else
        {
            _splitter.Width           = double.NaN;
            _splitter.Height          = 4;
            _splitter.ResizeDirection = GridResizeDirection.Rows;
            _splitter.VerticalAlignment = VerticalAlignment.Stretch;
            _splitter.HorizontalAlignment = HorizontalAlignment.Stretch;
        }

        switch (_layout)
        {
            case MdSplitLayout.PreviewRight:
                BuildHorizontalLayout(editorFirst: true,
                    showEditor, showPreview, isSplit);
                break;
            case MdSplitLayout.PreviewLeft:
                BuildHorizontalLayout(editorFirst: false,
                    showEditor, showPreview, isSplit);
                break;
            case MdSplitLayout.PreviewBottom:
                BuildVerticalLayout(editorFirst: true,
                    showEditor, showPreview, isSplit);
                break;
            case MdSplitLayout.PreviewTop:
                BuildVerticalLayout(editorFirst: false,
                    showEditor, showPreview, isSplit);
                break;
        }
    }

    private void BuildHorizontalLayout(bool editorFirst,
        bool showEditor, bool showPreview, bool isSplit)
    {
        // Col 0: first pane
        _rootGrid.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = isSplit ? new GridLength(1, GridUnitType.Star) : GridLength.Auto,
            MinWidth = isSplit ? 80 : 0,
        });

        if (isSplit)
        {
            // Col 1: splitter
            _rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
            // Col 2: second pane
            _rootGrid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(1, GridUnitType.Star),
                MinWidth = 80,
            });
        }

        var firstCtrl  = editorFirst ? (UIElement)_editor  : _preview;
        var secondCtrl = editorFirst ? (UIElement)_preview : _editor;

        if (showEditor || showPreview)
        {
            Grid.SetColumn(firstCtrl, 0);
            firstCtrl.Visibility = (editorFirst ? showEditor : showPreview)
                ? Visibility.Visible : Visibility.Collapsed;
            _rootGrid.Children.Add(firstCtrl);
        }

        if (isSplit)
        {
            Grid.SetColumn(_splitter, 1);
            _rootGrid.Children.Add(_splitter);

            Grid.SetColumn(secondCtrl, 2);
            secondCtrl.Visibility = (editorFirst ? showPreview : showEditor)
                ? Visibility.Visible : Visibility.Collapsed;
            _rootGrid.Children.Add(secondCtrl);
        }
        else if (!editorFirst ? showEditor : showPreview)
        {
            // Non-split: add second control hidden or as only visible one
            Grid.SetColumn(secondCtrl, 0);
            _rootGrid.Children.Add(secondCtrl);
        }
    }

    private void BuildVerticalLayout(bool editorFirst,
        bool showEditor, bool showPreview, bool isSplit)
    {
        _rootGrid.RowDefinitions.Add(new RowDefinition
        {
            Height = isSplit ? new GridLength(1, GridUnitType.Star) : GridLength.Auto,
            MinHeight = isSplit ? 60 : 0,
        });

        if (isSplit)
        {
            _rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(4) });
            _rootGrid.RowDefinitions.Add(new RowDefinition
            {
                Height = new GridLength(1, GridUnitType.Star),
                MinHeight = 60,
            });
        }

        var firstCtrl  = editorFirst ? (UIElement)_editor  : _preview;
        var secondCtrl = editorFirst ? (UIElement)_preview : _editor;

        Grid.SetRow(firstCtrl, 0);
        firstCtrl.Visibility = (editorFirst ? showEditor : showPreview)
            ? Visibility.Visible : Visibility.Collapsed;
        _rootGrid.Children.Add(firstCtrl);

        if (isSplit)
        {
            Grid.SetRow(_splitter, 1);
            _rootGrid.Children.Add(_splitter);

            Grid.SetRow(secondCtrl, 2);
            secondCtrl.Visibility = (editorFirst ? showPreview : showEditor)
                ? Visibility.Visible : Visibility.Collapsed;
            _rootGrid.Children.Add(secondCtrl);
        }
    }

    // --- Refresh ----------------------------------------------------------

    private void ScheduleRefresh()
    {
        _refreshTimer.Stop();
        _refreshTimer.Start();
    }

    private async void OnRefreshTimerTick(object? sender, EventArgs e)
    {
        _refreshTimer.Stop();
        await ForceRefreshAsync();
    }

    private async Task ForceRefreshAsync()
    {
        if (_viewMode == MdViewMode.SourceOnly) return;

        _isDark = DetectDarkTheme();
        var text = _editor.GetText();
        await _preview.RenderAsync(text, _isDark);
    }

    // --- Sync scroll ------------------------------------------------------

    private void OnEditorScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (!_syncScroll || _viewMode != MdViewMode.Split) return;
        if (e.ExtentHeight <= 0) return;

        _pendingScrollPct = e.VerticalOffset / e.ExtentHeight;
        _syncScrollTimer.Stop();
        _syncScrollTimer.Start();
    }

    private void OnSyncScrollTimerTick(object? sender, EventArgs e)
    {
        _syncScrollTimer.Stop();
        _preview.ScrollToPercent(_pendingScrollPct);
    }

    // --- Toolbar helpers --------------------------------------------------

    private void BuildToolbarItems()
    {
        // View mode dropdown
        var viewItems = new ObservableCollection<EditorToolbarItem>
        {
            new() { Label = "Source Only",  Icon = "\uE8A5", Command = new RelayCmd(() => SetViewMode(MdViewMode.SourceOnly)) },
            new() { Label = "Split",        Icon = "\uE8A0", Command = new RelayCmd(() => SetViewMode(MdViewMode.Split)) },
            new() { Label = "Preview Only", Icon = "\uE8A1", Command = new RelayCmd(() => SetViewMode(MdViewMode.PreviewOnly)) },
        };
        _podView = new EditorToolbarItem
        {
            Icon = "\uE8A1", Label = "View", Tooltip = "View mode (Ctrl+1/2/3)",
            DropdownItems = viewItems,
        };

        // Layout dropdown
        var layoutItems = new ObservableCollection<EditorToolbarItem>
        {
            new() { Label = "Preview Right",  Icon = "\uE8A0", Command = new RelayCmd(() => SetSplitLayout(MdSplitLayout.PreviewRight)) },
            new() { Label = "Preview Left",   Icon = "\uE8A0", Command = new RelayCmd(() => SetSplitLayout(MdSplitLayout.PreviewLeft)) },
            new() { Label = "Preview Bottom", Icon = "\uE8A0", Command = new RelayCmd(() => SetSplitLayout(MdSplitLayout.PreviewBottom)) },
            new() { Label = "Preview Top",    Icon = "\uE8A0", Command = new RelayCmd(() => SetSplitLayout(MdSplitLayout.PreviewTop)) },
        };
        _podLayout = new EditorToolbarItem
        {
            Icon = "\uF57E", Label = "Layout", Tooltip = "Split layout (Ctrl+Shift+L)",
            DropdownItems = layoutItems,
        };

        // Separator
        var sep = new EditorToolbarItem { IsSeparator = true };

        // Sync-scroll toggle
        _podSync = new EditorToolbarItem
        {
            Icon = "\uE8CB", Label = "Sync", Tooltip = "Sync scroll",
            IsToggle = true, IsChecked = _syncScroll,
            Command = new RelayCmd(() => SetSyncScroll(!_syncScroll)),
        };

        // Force-refresh button
        var podRefresh = new EditorToolbarItem
        {
            Icon = "\uE72C", Label = "Refresh", Tooltip = "Refresh preview (F9)",
            Command = new RelayCmd(async () => await ForceRefreshAsync()),
        };

        _toolbarItems.Add(_podView);
        _toolbarItems.Add(_podLayout);
        _toolbarItems.Add(sep);
        _toolbarItems.Add(_podSync);
        _toolbarItems.Add(podRefresh);

        SyncViewModeToToolbar();
        SyncLayoutToToolbar();
    }

    private void SyncViewModeToToolbar()
    {
        if (_podView?.DropdownItems is null) return;
        for (int i = 0; i < _podView.DropdownItems.Count; i++)
            _podView.DropdownItems[i].IsChecked = (i == (int)_viewMode);
    }

    private void SyncLayoutToToolbar()
    {
        if (_podLayout?.DropdownItems is null) return;
        for (int i = 0; i < _podLayout.DropdownItems.Count; i++)
            _podLayout.DropdownItems[i].IsChecked = (i == (int)_layout);
    }

    // --- Status bar helpers -----------------------------------------------

    private void BuildStatusBar()
    {
        // View mode choices
        foreach (MdViewMode m in Enum.GetValues<MdViewMode>())
        {
            var mode = m; // capture
            _sbView.Choices.Add(new StatusBarChoice
            {
                DisplayName = mode.ToString(),
                Command     = new RelayCmd(() => SetViewMode(mode)),
            });
        }

        _statusItems.Add(_sbView);
        _statusItems.Add(_sbWordCount);
        _statusItems.Add(_sbLineCount);

        UpdateStatusBar();
    }

    private void UpdateStatusBar()
    {
        _sbView.Value = _viewMode.ToString();
        foreach (var c in _sbView.Choices)
            c.IsActive = c.DisplayName == _viewMode.ToString();
    }

    private void UpdateWordLineCount()
    {
        var text = _editor.GetText();
        if (string.IsNullOrEmpty(text))
        {
            _sbWordCount.Value = "0";
            _sbLineCount.Value = "0";
            return;
        }
        var words = text.Split(new[] { ' ', '\t', '\n', '\r' },
            StringSplitOptions.RemoveEmptyEntries).Length;
        var lines = text.Split('\n').Length;
        _sbWordCount.Value = words.ToString("N0");
        _sbLineCount.Value = lines.ToString("N0");
    }

    // --- Helpers ----------------------------------------------------------

    private static bool DetectDarkTheme()
    {
        // Heuristic: inspect the current application-level DockBackground brush.
        // Dark themes use a near-black colour; light themes use near-white.
        try
        {
            if (System.Windows.Application.Current?.Resources["DockBackgroundColor"]
                is System.Windows.Media.Color bg)
            {
                // Perceived luminance: dark if below midpoint
                double lum = 0.299 * bg.R + 0.587 * bg.G + 0.114 * bg.B;
                return lum < 128.0;
            }
        }
        catch { /* ignore */ }

        return true; // default to dark
    }

    // ─── Minimal RelayCommand (avoids dependency on full RelayCommand impl) ──
    private sealed class RelayCmd : ICommand
    {
        private readonly Func<Task>? _asyncExec;
        private readonly Action?     _syncExec;

        public RelayCmd(Action execute)       => _syncExec  = execute;
        public RelayCmd(Func<Task> execute)   => _asyncExec = execute;

        public bool CanExecute(object? _) => true;
        public void Execute(object? _)
        {
            _syncExec?.Invoke();
            _asyncExec?.Invoke();
        }
        public event EventHandler? CanExecuteChanged;
    }

    // ─── Routed commands (keyboard shortcuts) ─────────────────────────────
    private static class MdCommands
    {
        public static readonly RoutedCommand TogglePreview  = new(nameof(TogglePreview),  typeof(MarkdownEditorHost));
        public static readonly RoutedCommand RefreshPreview = new(nameof(RefreshPreview), typeof(MarkdownEditorHost));
        public static readonly RoutedCommand SourceOnly     = new(nameof(SourceOnly),     typeof(MarkdownEditorHost));
        public static readonly RoutedCommand SplitView      = new(nameof(SplitView),      typeof(MarkdownEditorHost));
        public static readonly RoutedCommand PreviewOnly    = new(nameof(PreviewOnly),    typeof(MarkdownEditorHost));
        public static readonly RoutedCommand CycleLayout    = new(nameof(CycleLayout),    typeof(MarkdownEditorHost));
    }
}
