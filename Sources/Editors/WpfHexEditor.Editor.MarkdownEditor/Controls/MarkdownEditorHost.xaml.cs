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
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.Core.Documents;
using WpfHexEditor.Editor.MarkdownEditor.Core.Services;
using WpfHexEditor.Editor.MarkdownEditor.Properties;
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
    IBufferAwareEditor,
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
    private bool          _isDark;
    private string?       _filePath;

    // Debounce timers
    private readonly DispatcherTimer _refreshTimer;
    private readonly DispatcherTimer _scrollSyncTimer;

    // Scroll sync state
    private bool _suppressEditorScrollSync;   // prevents feedback loop when preview→editor
    private bool _suppressPreviewScrollSync;  // prevents feedback loop when editor→preview

    // Cached document length — updated after each render, used by ScheduleRefresh()
    // to avoid calling GetText() on every keystroke.
    private int _cachedDocLength;

    // Open / loading guard — prevents double-render when ModifiedChanged fires during OpenAsync
    private bool _isOpening;

    // Word wrap
    private bool _wordWrap = true;

    // Splitter ratio (0.0–1.0) — persisted across sessions
    private double _splitRatio = 0.5;

    // Preview zoom (1.0 = 100 %) — persisted across sessions
    private double _previewZoom = 1.0;

    // Fullscreen state — not persisted (transient UI mode)
    private bool _isFullscreen;
    private EditorToolbarItem? _podFullscreen;

    // --- Toolbar ----------------------------------------------------------
    private ObservableCollection<EditorToolbarItem>? _toolbarItems;

    // Toolbar items that need runtime updates
    private EditorToolbarItem? _podView;
    private EditorToolbarItem? _podLayout;
    private EditorToolbarItem? _podWrap;

    // --- Status bar -------------------------------------------------------
    private readonly ObservableCollection<StatusBarItem> _statusItems = new();
    private readonly StatusBarItem _sbView        = new() { Label = MarkdownEditorResources.MdSb_ViewLabel,  Tooltip = MarkdownEditorResources.MdSb_ViewTooltip };
    private readonly StatusBarItem _sbWordCount   = new() { Label = MarkdownEditorResources.MdSb_WordsLabel, Tooltip = MarkdownEditorResources.MdSb_WordsTooltip };
    private readonly StatusBarItem _sbLineCount   = new() { Label = MarkdownEditorResources.MdSb_LinesLabel, Tooltip = MarkdownEditorResources.MdSb_LinesTooltip };
    private readonly StatusBarItem _sbReadingTime = new() { Label = MarkdownEditorResources.MdSb_ReadLabel,  Tooltip = MarkdownEditorResources.MdSb_ReadTooltip };
    private readonly StatusBarItem _sbZoom        = new() { Label = MarkdownEditorResources.MdSb_ZoomLabel,  Tooltip = MarkdownEditorResources.MdSb_ZoomTooltip };
    private readonly StatusBarItem _sbYamlTitle   = new() { Label = "YAML", Tooltip = "YAML front-matter title" };

    // --- Construction -----------------------------------------------------

    public MarkdownEditorHost()
    {
        InitializeComponent();

        // Cache interface references (TextEditor implements all of these)
        _editorDoc     = _editor;
        _editorNav     = _editor;
        _editorSearch  = _editor;
        _editorPersist = _editor;

        // Auto-refresh timer — interval is set adaptively in ScheduleRefresh()
        _refreshTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(300),
        };
        _refreshTimer.Tick += OnRefreshTimerTick;

        // Scroll-sync debounce timer
        _scrollSyncTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(200),
        };
        _scrollSyncTimer.Tick += OnScrollSyncTick;

        // Enable word wrap by default for Markdown source
        _editor.IsWordWrapEnabled = true;

        // Wire inner editor events
        _editor.ModifiedChanged  += (_, _) => { ModifiedChanged?.Invoke(this, EventArgs.Empty); ContentChanged?.Invoke(this, EventArgs.Empty); if (!_isOpening) ScheduleRefresh(); };
        _editor.CanUndoChanged   += (_, _) => CanUndoChanged?.Invoke(this, EventArgs.Empty);
        _editor.CanRedoChanged   += (_, _) => CanRedoChanged?.Invoke(this, EventArgs.Empty);
        _editor.TitleChanged     += (_, s) => TitleChanged?.Invoke(this, s);
        _editor.StatusMessage    += (_, s) => StatusMessage?.Invoke(this, s);
        _editor.OutputMessage    += (_, s) => OutputMessage?.Invoke(this, s);
        _editor.SelectionChanged += (_, _) => SelectionChanged?.Invoke(this, EventArgs.Empty);
        _editor.OperationStarted   += (_, e) => OperationStarted?.Invoke(this, e);
        _editor.OperationProgress  += (_, e) => OperationProgress?.Invoke(this, e);
        _editor.OperationCompleted += (_, e) => OperationCompleted?.Invoke(this, e);

        // Preview link clicks → open in browser
        _preview.LinkClicked += (_, href) =>
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(href) { UseShellExecute = true }); }
            catch { /* ignore */ }
        };

        // Preview pane context menu actions → delegate to host methods
        _preview.PreviewContextMenuAction += OnPreviewContextMenuAction;

        // Scroll sync — editor scrolls → sync preview; preview scrolls → sync editor
        _editor.ViewportScrollChanged += OnEditorScrollChanged;
        _preview.PreviewScrolled      += OnPreviewScrolled;

        // Source editor context menu — FORMAT + INSERT groups
        _editor.ContextMenu = BuildEditorContextMenu();

        // Intercept Ctrl+V for image paste before forwarding to the inner TextEditor.
        PreviewKeyDown  += OnPreviewKeyDown;
        PreviewTextInput += OnPreviewTextInput;

        // Drag-drop images onto the source editor pane
        AllowDrop = true;
        PreviewDragOver += OnEditorDragOver;
        PreviewDrop     += OnEditorDrop;

        // Keyboard shortcuts
        CommandBindings.Add(new CommandBinding(MdCommands.TogglePreview, (_, _) => CycleViewMode()));
        CommandBindings.Add(new CommandBinding(MdCommands.RefreshPreview, (_, _) => _ = ForceRefreshAsync()));
        CommandBindings.Add(new CommandBinding(MdCommands.SourceOnly,    (_, _) => SetViewMode(MdViewMode.SourceOnly)));
        CommandBindings.Add(new CommandBinding(MdCommands.SplitView,     (_, _) => SetViewMode(MdViewMode.Split)));
        CommandBindings.Add(new CommandBinding(MdCommands.PreviewOnly,   (_, _) => SetViewMode(MdViewMode.PreviewOnly)));
        CommandBindings.Add(new CommandBinding(MdCommands.CycleLayout,   (_, _) => CycleSplitLayout()));

        InputBindings.Add(new KeyBinding(MdCommands.TogglePreview,  new KeyGesture(Key.A,  ModifierKeys.Control | ModifierKeys.Shift)));
        InputBindings.Add(new KeyBinding(MdCommands.RefreshPreview, new KeyGesture(Key.R, ModifierKeys.Control | ModifierKeys.Alt)));
        InputBindings.Add(new KeyBinding(MdCommands.SourceOnly,     new KeyGesture(Key.D1, ModifierKeys.Control)));
        InputBindings.Add(new KeyBinding(MdCommands.SplitView,      new KeyGesture(Key.D2, ModifierKeys.Control)));
        InputBindings.Add(new KeyBinding(MdCommands.PreviewOnly,    new KeyGesture(Key.D3, ModifierKeys.Control)));
        InputBindings.Add(new KeyBinding(MdCommands.CycleLayout,    new KeyGesture(Key.L,  ModifierKeys.Control | ModifierKeys.Shift)));
        InputBindings.Add(new KeyBinding(MdCommands.ToggleWordWrap, new KeyGesture(Key.Z,  ModifierKeys.Alt)));

        CommandBindings.Add(new CommandBinding(MdCommands.ToggleWordWrap,   (_, _) => SetWordWrap(!_wordWrap)));
        CommandBindings.Add(new CommandBinding(MdCommands.ToggleFullscreen, (_, _) => ToggleFullscreen()));

        // Wire splitter drag-complete for ratio persistence
        _splitter.DragCompleted += OnSplitterDragCompleted;

        // Wire Ctrl+scroll on the preview pane for zoom
        _preview.PreviewMouseWheel += OnPreviewPaneMouseWheel;

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

    // --- Markdown-specific API (used by MarkdownOutlinePanel) -------------

    /// <summary>Returns the raw Markdown text currently loaded in the editor.</summary>
    public string GetText() => _editor.GetText() ?? string.Empty;

    /// <summary>Raised by ModifiedChanged — re-exposed so the outline panel can subscribe.</summary>
    public event EventHandler? ContentChanged;

    // --- IBufferAwareEditor -----------------------------------------------

    /// <inheritdoc/>
    public void AttachBuffer(IDocumentBuffer buffer)
        => (_editor as IBufferAwareEditor)?.AttachBuffer(buffer);

    /// <inheritdoc/>
    public void DetachBuffer()
        => (_editor as IBufferAwareEditor)?.DetachBuffer();

    // --- IOpenableDocument ------------------------------------------------

    public async Task OpenAsync(string filePath, CancellationToken ct = default)
    {
        _filePath  = filePath;
        _isOpening = true;
        try
        {
            await _editor.OpenAsync(filePath, ct);
        }
        finally
        {
            _isOpening = false;
        }
        await ForceRefreshAsync();  // single render — ModifiedChanged during open is suppressed
    }

    // --- INavigableDocument -----------------------------------------------

    public void NavigateTo(int line, int column) => _editorNav.NavigateTo(line, column);

    // --- IEditorPersistable -----------------------------------------------

    public EditorConfigDto GetEditorConfig()
    {
        var dto = _editorPersist.GetEditorConfig();
        dto.Extra ??= new Dictionary<string, string>();
        dto.Extra["md.viewMode"]    = _viewMode.ToString();
        dto.Extra["md.layout"]      = _layout.ToString();
        dto.Extra["md.wordWrap"]    = _wordWrap.ToString();
        dto.Extra["md.splitRatio"]  = _splitRatio.ToString(System.Globalization.CultureInfo.InvariantCulture);
        dto.Extra["md.previewZoom"] = _previewZoom.ToString(System.Globalization.CultureInfo.InvariantCulture);
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

        if (config.Extra.TryGetValue("md.wordWrap", out var ww) &&
            bool.TryParse(ww, out var wwBool))
            SetWordWrap(wwBool);

        if (config.Extra.TryGetValue("md.splitRatio", out var sr) &&
            double.TryParse(sr, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var ratio))
        {
            _splitRatio = Math.Clamp(ratio, 0.1, 0.9);
            UpdateGridLayout();
        }

        if (config.Extra.TryGetValue("md.previewZoom", out var pz) &&
            double.TryParse(pz, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var zoom))
            SetPreviewZoom(zoom);
    }

    public byte[]? GetUnsavedModifications()            => _editorPersist.GetUnsavedModifications();
    public void ApplyUnsavedModifications(byte[] data)  => _editorPersist.ApplyUnsavedModifications(data);
    public ChangesetSnapshot GetChangesetSnapshot()     => _editorPersist.GetChangesetSnapshot();
    public void ApplyChangeset(ChangesetDto changeset)  => _editorPersist.ApplyChangeset(changeset);
    public void MarkChangesetSaved()                    => _editorPersist.MarkChangesetSaved();
    public IReadOnlyList<BookmarkDto>? GetBookmarks()   => _editorPersist.GetBookmarks();
    public void ApplyBookmarks(IReadOnlyList<BookmarkDto> bookmarks) => _editorPersist.ApplyBookmarks(bookmarks);

    // --- IEditorToolbarContributor ----------------------------------------

    public ObservableCollection<EditorToolbarItem> ToolbarItems
        => _toolbarItems ??= BuildToolbarItems();

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
    /// <summary>Opens (or focuses) the inner TextEditor's inline Find/Replace overlay.</summary>
    public void ShowFindBar() => _editor.ShowSearch();

    public void Find(string query, SearchTargetOptions options = default) => _editorSearch.Find(query, options);
    public void FindNext()       => _editorSearch.FindNext();
    public void FindPrevious()   => _editorSearch.FindPrevious();
    public void ClearSearch()    => _editorSearch.ClearSearch();
    public void Replace(string replacement)    => _editorSearch.Replace(replacement);
    public void ReplaceAll(string replacement) => _editorSearch.ReplaceAll(replacement);
    public UIElement? GetCustomFiltersContent() => _editorSearch.GetCustomFiltersContent();

    // --- Image paste (Ctrl+V with image data on clipboard) ----------------

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Escape exits fullscreen
        if (e.Key == Key.Escape && _isFullscreen)
        {
            e.Handled = true;
            ToggleFullscreen();
            return;
        }

        // Only intercept Ctrl+V and only when the clipboard contains image data.
        if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) != 0 && Clipboard.ContainsImage())
        {
            e.Handled = true;
            _ = PasteImageAsync();
            return;
        }

        // List continuation on Enter
        if (e.Key == Key.Return && Keyboard.Modifiers == ModifierKeys.None && _viewMode != MdViewMode.PreviewOnly)
        {
            if (TryHandleListContinuation())
                e.Handled = true;
        }
    }

    // ── Auto-pair Markdown characters ─────────────────────────────────────────

    private static readonly (char Trigger, string Open, string Close)[] _autoPairs =
    [
        ('*',  "**",  "**"),   // **|**
        ('`',  "`",   "`"),    // `|`
        ('[',  "[",   "]()"),  // [|]()
        ('(',  "(",   ")"),    // (|)
    ];

    private void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (_viewMode == MdViewMode.PreviewOnly) return;
        if (e.Text.Length != 1) return;

        var ch   = e.Text[0];
        var pair = _autoPairs.FirstOrDefault(p => p.Trigger == ch);
        if (pair == default) return;

        // If there is a selection, wrap it
        var selected = _editor.GetSelectedText();
        if (!string.IsNullOrEmpty(selected))
        {
            WrapSelection(pair.Open, pair.Close, selected);
            e.Handled = true;
            return;
        }

        // No selection — insert full pair and position caret inside
        _editor.InsertText(pair.Open + pair.Close);

        // Move caret back inside the pair (before closing delimiter)
        var cfg    = _editorPersist.GetEditorConfig();
        var newCol = Math.Max(1, cfg.CaretColumn - pair.Close.Length);
        _editorNav.NavigateTo(cfg.CaretLine, newCol);

        e.Handled = true;
    }

    // ── List continuation ─────────────────────────────────────────────────────

    private static readonly Regex _listPrefixRegex =
        new(@"^(\s*)([-*+]|\d+\.)\s", RegexOptions.Compiled);

    private bool TryHandleListContinuation()
    {
        var text  = _editor.GetText();
        if (text is null) return false;

        var cfg       = _editorPersist.GetEditorConfig();
        var caretLine = cfg.CaretLine - 1; // convert to 0-based
        var lines     = text.Split('\n');
        if (caretLine < 0 || caretLine >= lines.Length) return false;

        var line = lines[caretLine].TrimEnd('\r');
        var m    = _listPrefixRegex.Match(line);
        if (!m.Success) return false;

        var indent = m.Groups[1].Value;
        var marker = m.Groups[2].Value;

        // If the list item body is empty → end the list (remove marker)
        var fullPrefix = m.Value;
        if (line.Length == fullPrefix.Length)
        {
            // Replace current line with empty line (delete the marker)
            _editor.InsertText("\n");
            // Simulate Home + Shift+End + Delete to clear the marker on previous line is complex;
            // instead just insert newline and the outer editor handles the rest via normal Enter.
            return false; // let the editor process Enter normally to remove the empty marker
        }

        // Continue the list: compute the next marker
        string nextMarker;
        if (int.TryParse(marker.TrimEnd('.'), out var num))
            nextMarker = $"{num + 1}.";
        else
            nextMarker = marker;

        _editor.InsertText($"\n{indent}{nextMarker} ");
        return true;
    }

    private static readonly string[] _imageExtensions = [".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".svg"];

    private void OnEditorDragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            var files = e.Data.GetData(System.Windows.DataFormats.FileDrop) as string[];
            if (files?.Any(f => _imageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant())) == true)
            {
                e.Effects = System.Windows.DragDropEffects.Copy;
                e.Handled = true;
                return;
            }
        }
        e.Effects = System.Windows.DragDropEffects.None;
    }

    private async void OnEditorDrop(object sender, System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)) return;

        var files = e.Data.GetData(System.Windows.DataFormats.FileDrop) as string[];
        if (files is null) return;

        foreach (var file in files.Where(f => _imageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant())))
            await DropImageFileAsync(file);

        e.Handled = true;
    }

    private async Task DropImageFileAsync(string sourcePath)
    {
        try
        {
            string relativePath;

            if (!string.IsNullOrEmpty(_filePath))
            {
                var dir       = Path.GetDirectoryName(_filePath)!;
                var assetsDir = Path.Combine(dir, "assets");
                Directory.CreateDirectory(assetsDir);
                var fileName = Path.GetFileName(sourcePath);
                var destPath = Path.Combine(assetsDir, fileName);

                await Task.Run(() => File.Copy(sourcePath, destPath, overwrite: true));
                relativePath = $"assets/{fileName}";
            }
            else
            {
                var image  = new BitmapImage(new Uri(sourcePath));
                var base64 = await Task.Run(() => ImageToBase64(image));
                relativePath = $"data:image/png;base64,{base64}";
            }

            InsertSnippet($"![{Path.GetFileNameWithoutExtension(sourcePath)}]({relativePath})");
        }
        catch (Exception ex)
        {
            OutputMessage?.Invoke(this, $"[MarkdownEditor] Image drop failed: {ex.Message}");
        }
    }

    private async Task PasteImageAsync()
    {
        try
        {
            var image = Clipboard.GetImage();
            if (image is null) return;

            string relativePath;

            if (!string.IsNullOrEmpty(_filePath))
            {
                // Save next to the markdown file in an "assets" sub-folder.
                var dir       = Path.GetDirectoryName(_filePath)!;
                var assetsDir = Path.Combine(dir, "assets");
                Directory.CreateDirectory(assetsDir);
                var fileName = $"image-{DateTime.Now:yyyyMMdd-HHmmss}.png";
                var fullPath = Path.Combine(assetsDir, fileName);

                await Task.Run(() => SavePng(image, fullPath));
                relativePath = $"assets/{fileName}";
            }
            else
            {
                // No backing file — embed as data URI (base64 PNG).
                var base64 = await Task.Run(() => ImageToBase64(image));
                relativePath = $"data:image/png;base64,{base64}";
            }

            InsertSnippet($"![pasted image]({relativePath})");
        }
        catch (Exception ex)
        {
            OutputMessage?.Invoke(this, $"[MarkdownEditor] Image paste failed: {ex.Message}");
        }
    }

    private static void SavePng(BitmapSource image, string path)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static string ImageToBase64(BitmapSource image)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return Convert.ToBase64String(ms.ToArray());
    }

    // --- Insert / Format helpers ------------------------------------------

    /// <summary>
    /// Inserts a Markdown snippet at the current caret position.
    /// Focuses the source editor first so the insertion lands in the right place.
    /// </summary>
    private void InsertSnippet(string snippet)
    {
        if (_viewMode == MdViewMode.PreviewOnly)
            SetViewMode(MdViewMode.Split);
        _editor.Focus();
        _editor.InsertText(snippet);
    }

    /// <summary>
    /// Wraps the current selection with <paramref name="before"/> / <paramref name="after"/> markers.
    /// If nothing is selected, inserts the markers with a placeholder between them.
    /// </summary>
    private void WrapSelection(string before, string after, string placeholder = "text")
    {
        if (_viewMode == MdViewMode.PreviewOnly)
            SetViewMode(MdViewMode.Split);
        _editor.Focus();
        var sel = _editor.GetSelectedText();
        var inner = string.IsNullOrEmpty(sel) ? placeholder : sel;
        _editor.InsertText($"{before}{inner}{after}");
    }

    // --- Context menu (preview pane) --------------------------------------

    private void OnPreviewContextMenuAction(object? sender, MdPreviewContextAction action)
    {
        switch (action)
        {
            case MdPreviewContextAction.SourceOnly:       SetViewMode(MdViewMode.SourceOnly);   break;
            case MdPreviewContextAction.SplitView:        SetViewMode(MdViewMode.Split);         break;
            case MdPreviewContextAction.PreviewOnly:      SetViewMode(MdViewMode.PreviewOnly);   break;
            case MdPreviewContextAction.Refresh:          _ = ForceRefreshAsync();               break;
            case MdPreviewContextAction.CycleLayout:      CycleSplitLayout();                    break;
            case MdPreviewContextAction.ToggleFullscreen: ToggleFullscreen();                    break;
            case MdPreviewContextAction.ZoomIn:           SetPreviewZoom(_previewZoom + 0.1);    break;
            case MdPreviewContextAction.ZoomOut:          SetPreviewZoom(_previewZoom - 0.1);    break;
            case MdPreviewContextAction.ZoomReset:        SetPreviewZoom(1.0);                   break;
            case MdPreviewContextAction.CopyText:         _ = CopyPreviewTextAsync();            break;
        }
    }

    private async Task CopyPreviewTextAsync()
    {
        if (!_preview.IsWebViewReady) return;
        var text = await _preview.ExecuteScriptAndGetStringAsync("document.body.innerText");
        if (!string.IsNullOrEmpty(text))
            Clipboard.SetText(text);
    }

    // --- Context menu (source editor) -------------------------------------

    private ContextMenu BuildEditorContextMenu()
    {
        var menu = new ContextMenu();
        menu.SetResourceReference(StyleProperty, "MD_ContextMenuStyle");

        // FORMAT group
        var fmtHeader = new MenuItem { Header = MarkdownEditorResources.MdCtx_GroupFormat };
        fmtHeader.SetResourceReference(StyleProperty, "MD_GroupHeaderStyle");
        menu.Items.Add(fmtHeader);

        menu.Items.Add(MakeEditorMenuItem(MarkdownEditorResources.MdCtx_Bold,          "Ctrl+B", () => WrapSelection("**", "**",  "bold text")));
        menu.Items.Add(MakeEditorMenuItem(MarkdownEditorResources.MdCtx_Italic,        "Ctrl+I", () => WrapSelection("_",  "_",   "italic text")));
        menu.Items.Add(MakeEditorMenuItem(MarkdownEditorResources.MdCtx_Strikethrough, "",       () => WrapSelection("~~", "~~",  "strikethrough")));
        menu.Items.Add(MakeEditorMenuItem(MarkdownEditorResources.MdCtx_InlineCode,    "",       () => WrapSelection("`",  "`",   "code")));

        var sep1 = new Separator();
        sep1.SetResourceReference(StyleProperty, "MD_GroupSeparatorStyle");
        menu.Items.Add(sep1);

        // INSERT group
        var insHeader = new MenuItem { Header = MarkdownEditorResources.MdCtx_GroupInsert };
        insHeader.SetResourceReference(StyleProperty, "MD_GroupHeaderStyle");
        menu.Items.Add(insHeader);

        menu.Items.Add(MakeEditorMenuItem(MarkdownEditorResources.MdCtx_Table,          "", () => InsertSnippet(
            "\n| Header 1 | Header 2 | Header 3 |\n| --- | --- | --- |\n| Cell 1 | Cell 2 | Cell 3 |\n")));
        menu.Items.Add(MakeEditorMenuItem(MarkdownEditorResources.MdCtx_CodeBlock,     "", () => InsertSnippet("\n```\n\n```\n")));
        menu.Items.Add(MakeEditorMenuItem(MarkdownEditorResources.MdCtx_Link,          "", () => WrapSelection("[", "](url)", "link text")));
        menu.Items.Add(MakeEditorMenuItem(MarkdownEditorResources.MdCtx_Image,         "", () => InsertSnippet("![alt text](image.png)")));
        menu.Items.Add(MakeEditorMenuItem(MarkdownEditorResources.MdCtx_HorizontalRule,"", () => InsertSnippet("\n---\n")));

        var sep2 = new Separator();
        sep2.SetResourceReference(StyleProperty, "MD_GroupSeparatorStyle");
        menu.Items.Add(sep2);

        // EXPORT group
        var sep3 = new Separator();
        sep3.SetResourceReference(StyleProperty, "MD_GroupSeparatorStyle");
        menu.Items.Add(sep3);

        var exportHeader = new MenuItem { Header = MarkdownEditorResources.MdCtx_GroupExport };
        exportHeader.SetResourceReference(StyleProperty, "MD_GroupHeaderStyle");
        menu.Items.Add(exportHeader);

        menu.Items.Add(MakeEditorMenuItem(MarkdownEditorResources.MdCtx_ExportHtml, "", async () => await ExportHtmlAsync()));
        menu.Items.Add(MakeEditorMenuItem(MarkdownEditorResources.MdCtx_ExportPdf,  "", async () => await ExportPdfAsync()));

        var sep4 = new Separator();
        sep4.SetResourceReference(StyleProperty, "MD_GroupSeparatorStyle");
        menu.Items.Add(sep4);

        // VIEW group
        var viewHeader = new MenuItem { Header = MarkdownEditorResources.MdCtx_GroupView };
        viewHeader.SetResourceReference(StyleProperty, "MD_GroupHeaderStyle");
        menu.Items.Add(viewHeader);

        menu.Items.Add(MakeEditorMenuItem(MarkdownEditorResources.MdCtx_WordWrap, "", () => SetWordWrap(!_wordWrap)));

        return menu;
    }

    // --- Scroll sync ---------------------------------------------------------

    private double _pendingScrollPct; // target percent for debounced sync

    private void OnEditorScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_suppressEditorScrollSync) return;
        if (_viewMode == MdViewMode.SourceOnly) return;

        var maxScroll = e.ExtentHeight - e.ViewportHeight;
        if (maxScroll <= 0) return;

        _pendingScrollPct = e.VerticalOffset / maxScroll;
        _scrollSyncTimer.Stop();
        _scrollSyncTimer.Start();
    }

    private void OnScrollSyncTick(object? sender, EventArgs e)
    {
        _scrollSyncTimer.Stop();
        _suppressPreviewScrollSync = true;
        _preview.ScrollToPercent(_pendingScrollPct);
        // Release the suppress flag after a short delay to absorb the JS scroll event
        Dispatcher.BeginInvoke(() => _suppressPreviewScrollSync = false,
            DispatcherPriority.ApplicationIdle);
    }

    private void OnPreviewScrolled(object? sender, double pct)
    {
        // Preview→editor reverse sync is intentionally a no-op.
        // The TextEditor has no public scroll-to-offset API — NavigateTo() moves the caret,
        // which fires ViewportScrollChanged and interrupts any active scrollbar drag in the editor.
        // One-way sync (editor→preview) is the correct model here.
    }

    // --- Export ---------------------------------------------------------------

    private async Task ExportHtmlAsync()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title      = MarkdownEditorResources.MdDlg_ExportHtmlTitle,
            Filter     = "HTML file (*.html)|*.html",
            DefaultExt = ".html",
            FileName   = Path.GetFileNameWithoutExtension(_filePath ?? "document"),
        };
        if (dlg.ShowDialog() != true) return;

        var text      = _editor.GetText() ?? string.Empty;
        var hasMermaid = HasMermaidDiagram(text);
        var html      = MarkdownRenderService.GetHtmlPage(text, _isDark, hasMermaid);

        await File.WriteAllTextAsync(dlg.FileName, html, System.Text.Encoding.UTF8);
    }

    private async Task ExportPdfAsync()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title      = MarkdownEditorResources.MdDlg_ExportPdfTitle,
            Filter     = "PDF file (*.pdf)|*.pdf",
            DefaultExt = ".pdf",
            FileName   = Path.GetFileNameWithoutExtension(_filePath ?? "document"),
        };
        if (dlg.ShowDialog() != true) return;

        await _preview.ExportPdfAsync(dlg.FileName);
    }

    // --- Table of Contents ---------------------------------------------------

    private static readonly Regex _headingRegex =
        new(@"^(#{1,6})\s+(.+?)(?:\s+#+)?\s*$", RegexOptions.Multiline | RegexOptions.Compiled);

    private void InsertTableOfContents()
    {
        var text = _editor.GetText();
        if (string.IsNullOrEmpty(text)) return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("## Table of Contents");

        foreach (System.Text.RegularExpressions.Match m in _headingRegex.Matches(text))
        {
            var level   = m.Groups[1].Value.Length;
            var heading = m.Groups[2].Value.Trim();
            var anchor  = ToGfmAnchor(heading);
            var indent  = new string(' ', (level - 1) * 2);
            sb.AppendLine($"{indent}- [{heading}](#{anchor})");
        }

        InsertSnippet(sb.ToString());
    }

    private static string ToGfmAnchor(string heading)
    {
        // GFM anchor: lowercase, spaces → hyphens, remove all non-alphanumeric except hyphens
        var anchor = heading.ToLowerInvariant();
        anchor = Regex.Replace(anchor, @"\s+", "-");
        anchor = Regex.Replace(anchor, @"[^\w-]", "");
        return anchor;
    }

    private static MenuItem MakeEditorMenuItem(string header, string gesture, Action onClick)
    {
        var item = new MenuItem
        {
            Header           = header,
            InputGestureText = gesture,
        };
        item.Click += (_, _) => onClick();
        return item;
    }

    // --- View mode & layout -----------------------------------------------

    private void SetViewMode(MdViewMode mode)
    {
        if (_viewMode == mode) return;
        var wasSourceOnly = _viewMode == MdViewMode.SourceOnly;
        _viewMode = mode;
        UpdateGridLayout();
        SyncViewModeToToolbar();
        UpdateStatusBar();
        // Preview was suppressed while SourceOnly — render it now that it is visible
        if (wasSourceOnly && mode != MdViewMode.SourceOnly)
            _ = ForceRefreshAsync();
    }

    private void SetSplitLayout(MdSplitLayout layout)
    {
        if (_layout == layout) return;
        _layout = layout;
        UpdateGridLayout();
        SyncLayoutToToolbar();
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
        // While fullscreen Window owns _preview, do not touch the grid layout.
        // CloseFullscreen() will call UpdateGridLayout() again after detaching _preview.
        if (_isFullscreen) return;

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

        // Force child controls to match the new layout slot — WPF arrange passes
        // do not propagate reliably to Win32 children or custom viewports after grid rebuilds.
        _preview.InvalidateWebViewSize();
        _editor.InvalidateViewportSize();
    }

    private void BuildHorizontalLayout(bool editorFirst,
        bool showEditor, bool showPreview, bool isSplit)
    {
        double r1 = _splitRatio, r2 = 1.0 - _splitRatio;
        // Col 0: first pane
        _rootGrid.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width    = isSplit ? new GridLength(r1, GridUnitType.Star) : new GridLength(1, GridUnitType.Star),
            MinWidth = 80,
        });

        if (isSplit)
        {
            // Col 1: splitter
            _rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
            // Col 2: second pane
            _rootGrid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width    = new GridLength(r2, GridUnitType.Star),
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
        double r1 = _splitRatio, r2 = 1.0 - _splitRatio;
        _rootGrid.RowDefinitions.Add(new RowDefinition
        {
            Height    = isSplit ? new GridLength(r1, GridUnitType.Star) : new GridLength(1, GridUnitType.Star),
            MinHeight = 60,
        });

        if (isSplit)
        {
            _rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(4) });
            _rootGrid.RowDefinitions.Add(new RowDefinition
            {
                Height    = new GridLength(r2, GridUnitType.Star),
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
        // Adaptive debounce using cached length — avoids O(N) GetText() on every keystroke.
        // _cachedDocLength is updated by ForceRefreshAsync() after each render.
        _refreshTimer.Interval = _cachedDocLength switch
        {
            > 50_000 => TimeSpan.FromMilliseconds(1500),
            > 10_000 => TimeSpan.FromMilliseconds(800),
            _        => TimeSpan.FromMilliseconds(300),
        };
        _refreshTimer.Start();
    }

    private async void OnRefreshTimerTick(object? sender, EventArgs e)
    {
        _refreshTimer.Stop();
        await ForceRefreshAsync();
    }

    private async Task ForceRefreshAsync()
    {
        _isDark = DetectDarkTheme();
        var text = _editor.GetText();       // single GetText() per debounce period
        _cachedDocLength = text.Length;     // update cache for next ScheduleRefresh()
        UpdateWordLineCount(text);          // reuse already-fetched text (no 2nd GetText)

        if (_viewMode == MdViewMode.SourceOnly) return;

        var hasMermaid = HasMermaidDiagram(text);
        await _preview.RenderAsync(text, _isDark, hasMermaid);
    }

    /// <summary>
    /// Returns <see langword="true"/> if the markdown source contains at least one
    /// mermaid fenced block. Used to skip the 2.9 MB mermaid.js bundle when not needed.
    /// </summary>
    private static bool HasMermaidDiagram(string? text)
        => text != null && text.Contains("```mermaid", StringComparison.OrdinalIgnoreCase);

    // --- Toolbar helpers --------------------------------------------------

    private ObservableCollection<EditorToolbarItem> BuildToolbarItems()
    {
        // View mode dropdown
        var viewItems = new ObservableCollection<EditorToolbarItem>
        {
            new() { Label = MarkdownEditorResources.MdView_SourceOnly,  Icon = "\uE8A5", Command = new RelayCmd(() => SetViewMode(MdViewMode.SourceOnly)) },
            new() { Label = MarkdownEditorResources.MdView_Split,        Icon = "\uE8A0", Command = new RelayCmd(() => SetViewMode(MdViewMode.Split)) },
            new() { Label = MarkdownEditorResources.MdView_PreviewOnly, Icon = "\uE8A1", Command = new RelayCmd(() => SetViewMode(MdViewMode.PreviewOnly)) },
        };
        _podView = new EditorToolbarItem
        {
            Icon = "\uE8A1", Label = MarkdownEditorResources.MdTb_ViewLabel, Tooltip = MarkdownEditorResources.MdTb_ViewTooltip,
            DropdownItems = viewItems,
        };

        // Layout dropdown
        var layoutItems = new ObservableCollection<EditorToolbarItem>
        {
            new() { Label = MarkdownEditorResources.MdLayout_PreviewRight,  Icon = "\uE8A0", Command = new RelayCmd(() => SetSplitLayout(MdSplitLayout.PreviewRight)) },
            new() { Label = MarkdownEditorResources.MdLayout_PreviewLeft,   Icon = "\uE8A0", Command = new RelayCmd(() => SetSplitLayout(MdSplitLayout.PreviewLeft)) },
            new() { Label = MarkdownEditorResources.MdLayout_PreviewBottom, Icon = "\uE8A0", Command = new RelayCmd(() => SetSplitLayout(MdSplitLayout.PreviewBottom)) },
            new() { Label = MarkdownEditorResources.MdLayout_PreviewTop,    Icon = "\uE8A0", Command = new RelayCmd(() => SetSplitLayout(MdSplitLayout.PreviewTop)) },
        };
        _podLayout = new EditorToolbarItem
        {
            Icon = "\uF57E", Label = MarkdownEditorResources.MdTb_LayoutLabel, Tooltip = MarkdownEditorResources.MdTb_LayoutTooltip,
            DropdownItems = layoutItems,
        };

        // Separator
        var sep = new EditorToolbarItem { IsSeparator = true };

        // Word-wrap toggle
        _podWrap = new EditorToolbarItem
        {
            Icon = "\uE8A3", Label = MarkdownEditorResources.MdTb_WrapLabel, Tooltip = MarkdownEditorResources.MdTb_WrapTooltip,
            IsToggle = true, IsChecked = _wordWrap,
            Command = new RelayCmd(() => SetWordWrap(!_wordWrap)),
        };

        // Force-refresh button
        var podRefresh = new EditorToolbarItem
        {
            Icon = "\uE72C", Label = MarkdownEditorResources.MdTb_RefreshLabel, Tooltip = MarkdownEditorResources.MdTb_RefreshTooltip,
            Command = new RelayCmd(async () => await ForceRefreshAsync()),
        };

        // Insert pod
        var insertItems = new ObservableCollection<EditorToolbarItem>
        {
            new() { Label = MarkdownEditorResources.MdInsert_Table,          Icon = "\uE8EC", Command = new RelayCmd(() => InsertSnippet("\n| Column 1 | Column 2 | Column 3 |\n|---|---|---|\n| Cell | Cell | Cell |\n")) },
            new() { Label = MarkdownEditorResources.MdInsert_CodeBlock,      Icon = "\uE943", Command = new RelayCmd(() => InsertSnippet("\n```\n\n```\n")) },
            new() { Label = MarkdownEditorResources.MdCtx_Link,              Icon = "\uE8C1", Command = new RelayCmd(() => InsertSnippet("[link text](https://example.com)")) },
            new() { Label = MarkdownEditorResources.MdInsert_Image,          Icon = "\uEB9F", Command = new RelayCmd(() => InsertSnippet("![alt text](image.png)")) },
            new() { Label = MarkdownEditorResources.MdInsert_HorizontalRule, Icon = "\uE8EF", Command = new RelayCmd(() => InsertSnippet("\n---\n")) },
            new() { Label = MarkdownEditorResources.MdInsert_TableOfContents, Icon = "\uE8FD", Command = new RelayCmd(InsertTableOfContents) },
        };
        var podInsert = new EditorToolbarItem
        {
            Icon = "\uE710", Label = MarkdownEditorResources.MdTb_InsertLabel, Tooltip = MarkdownEditorResources.MdTb_InsertTooltip,
            DropdownItems = insertItems,
        };

        // Format pod
        var formatItems = new ObservableCollection<EditorToolbarItem>
        {
            new() { Label = MarkdownEditorResources.MdFmt_Bold,          Icon = "\uE8DD", Command = new RelayCmd(() => WrapSelection("**", "**", "bold text")) },
            new() { Label = MarkdownEditorResources.MdFmt_Italic,        Icon = "\uE8DB", Command = new RelayCmd(() => WrapSelection("*", "*", "italic text")) },
            new() { Label = MarkdownEditorResources.MdFmt_Strikethrough, Icon = "\uEDE0", Command = new RelayCmd(() => WrapSelection("~~", "~~", "strikethrough")) },
            new() { Label = MarkdownEditorResources.MdFmt_InlineCode,    Icon = "\uE943", Command = new RelayCmd(() => WrapSelection("`", "`", "code")) },
        };
        var podFormat = new EditorToolbarItem
        {
            Icon = "\uE8D2", Label = MarkdownEditorResources.MdTb_FormatLabel, Tooltip = MarkdownEditorResources.MdTb_FormatTooltip,
            DropdownItems = formatItems,
        };

        // Fullscreen toggle button
        _podFullscreen = new EditorToolbarItem
        {
            Icon      = "\uE740", Label   = MarkdownEditorResources.MdTb_FullscreenLabel,
            Tooltip   = MarkdownEditorResources.MdTb_FullscreenTooltip,
            IsToggle  = true,     IsChecked = false,
            Command   = new RelayCmd(ToggleFullscreen),
        };

        var items = new ObservableCollection<EditorToolbarItem>
        {
            _podView,
            _podLayout,
            sep,
            podInsert,
            podFormat,
            new EditorToolbarItem { IsSeparator = true },
            _podWrap,
            podRefresh,
            _podFullscreen,
        };

        SyncViewModeToToolbar();
        SyncLayoutToToolbar();

        return items;
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

        _sbZoom.Value = "100 %";
        // Add preset zoom choices — clicking a choice applies that zoom level
        foreach (var (pct, factor) in new (string, double)[] {
            ("50 %", 0.5), ("75 %", 0.75), ("100 %", 1.0),
            ("125 %", 1.25), ("150 %", 1.5), ("200 %", 2.0), ("300 %", 3.0) })
        {
            var f = factor; // capture
            _sbZoom.Choices.Add(new StatusBarChoice
            {
                DisplayName = pct,
                Command     = new RelayCmd(() => SetPreviewZoom(f)),
            });
        }

        _statusItems.Add(_sbView);
        _statusItems.Add(_sbWordCount);
        _statusItems.Add(_sbLineCount);
        _statusItems.Add(_sbReadingTime);
        _statusItems.Add(_sbZoom);
        _sbYamlTitle.IsVisible = false;
        _statusItems.Add(_sbYamlTitle);

        UpdateStatusBar();
    }

    private void UpdateStatusBar()
    {
        _sbView.Value = _viewMode.ToString();
        foreach (var c in _sbView.Choices)
            c.IsActive = c.DisplayName == _viewMode.ToString();
    }

    private async void UpdateWordLineCount(string? text = null)
    {
        text ??= _editor.GetText();
        if (string.IsNullOrEmpty(text))
        {
            _sbWordCount.Value = "0";
            _sbLineCount.Value = "0";
            UpdateReadingTime(0);
            return;
        }

        // Run the counting off the UI thread to avoid blocking on large documents.
        var (words, lines) = await Task.Run(() =>
        {
            var w = text.Split([' ', '\t', '\n', '\r'],
                StringSplitOptions.RemoveEmptyEntries).Length;
            var l = text.Split('\n').Length;
            return (w, l);
        });

        _sbWordCount.Value = words.ToString("N0");
        _sbLineCount.Value = lines.ToString("N0");
        UpdateReadingTime(words);
        UpdateYamlStatusBar(text);
    }

    private void UpdateYamlStatusBar(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            _sbYamlTitle.IsVisible = false;
            return;
        }

        var (yaml, _) = MarkdownRenderService.ExtractYamlFrontmatter(text);
        if (yaml is null)
        {
            _sbYamlTitle.IsVisible = false;
            return;
        }

        // Extract "title:" key if present
        var titleMatch = Regex.Match(yaml, @"^title\s*:\s*(.+)$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        _sbYamlTitle.Value     = titleMatch.Success ? titleMatch.Groups[1].Value.Trim() : "front-matter";
        _sbYamlTitle.IsVisible = true;
    }

    private void UpdateReadingTime(int wordCount)
    {
        const int WpmAverage = 200;
        var minutes = (int)Math.Ceiling(wordCount / (double)WpmAverage);
        _sbReadingTime.Value = minutes <= 1 ? "< 1 min read" : $"~{minutes} min read";
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

    // --- Word wrap --------------------------------------------------------

    private void SetWordWrap(bool value)
    {
        _wordWrap = value;
        _editor.IsWordWrapEnabled = value;
        if (_podWrap is not null)
            _podWrap.IsChecked = value;
    }

    // --- Splitter ratio ---------------------------------------------------

    private void OnSplitterDragCompleted(object? sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        // Compute the actual ratio from the current column / row star sizes.
        bool isVertical = _layout == MdSplitLayout.PreviewRight ||
                          _layout == MdSplitLayout.PreviewLeft;
        if (isVertical && _rootGrid.ColumnDefinitions.Count == 3)
        {
            var total = _rootGrid.ColumnDefinitions[0].ActualWidth +
                        _rootGrid.ColumnDefinitions[2].ActualWidth;
            if (total > 0)
                _splitRatio = Math.Clamp(
                    _rootGrid.ColumnDefinitions[0].ActualWidth / total, 0.1, 0.9);
        }
        else if (!isVertical && _rootGrid.RowDefinitions.Count == 3)
        {
            var total = _rootGrid.RowDefinitions[0].ActualHeight +
                        _rootGrid.RowDefinitions[2].ActualHeight;
            if (total > 0)
                _splitRatio = Math.Clamp(
                    _rootGrid.RowDefinitions[0].ActualHeight / total, 0.1, 0.9);
        }
    }

    // --- Preview zoom -----------------------------------------------------

    private void OnPreviewPaneMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
        e.Handled = true;
        SetPreviewZoom(_previewZoom + (e.Delta > 0 ? 0.1 : -0.1));
    }

    private void SetPreviewZoom(double zoom)
    {
        _previewZoom  = Math.Clamp(Math.Round(zoom, 1), 0.5, 3.0);
        var label     = $"{(int)(_previewZoom * 100)} %";
        _sbZoom.Value = label;
        foreach (var c in _sbZoom.Choices)
            c.IsActive = c.DisplayName == label;
        _preview.SetZoom(_previewZoom);
    }

    // --- Fullscreen (dedicated borderless Window on active screen) -----------

    private Window? _fullscreenWindow;

    private void ToggleFullscreen()
    {
        if (_isFullscreen) CloseFullscreen();
        else               OpenFullscreen();
    }

    // Zoom level captured when entering fullscreen — restored on exit
    private double _zoomBeforeFullscreen = 1.0;

    // Win32 VK codes needed for the low-level keyboard hook
    private const int WM_KEYDOWN = 0x0100;
    private const int VK_ESCAPE  = 0x1B;

    private System.Windows.Interop.HwndSourceHook? _fullscreenHook;

    private void OpenFullscreen()
    {
        _zoomBeforeFullscreen  = _previewZoom;   // capture BEFORE setting _isFullscreen
        _isFullscreen          = true;

        // Detach _preview from the split grid before reparenting
        _rootGrid.Children.Remove(_preview);

        // Resolve the active screen — use WorkingArea so the window is on one screen only.
        // Screen.Bounds is in physical pixels; Window.Left/Top/Width/Height are WPF DIPs.
        // We must convert using the DPI of the parent window.
        var parentWin = Window.GetWindow(this);
        System.Windows.Forms.Screen screen;
        double dpiScale = 1.0;

        if (parentWin is not null)
        {
            var helper = new System.Windows.Interop.WindowInteropHelper(parentWin);
            var hwnd   = helper.Handle;
            screen     = System.Windows.Forms.Screen.FromHandle(hwnd);

            // PresentationSource gives the WPF→physical pixel transform
            var source = System.Windows.Interop.HwndSource.FromHwnd(hwnd);
            if (source?.CompositionTarget is not null)
                dpiScale = source.CompositionTarget.TransformToDevice.M11;
        }
        else
        {
            screen = System.Windows.Forms.Screen.PrimaryScreen!;
        }

        // Convert physical pixel bounds to WPF DIPs
        var b = screen.Bounds;
        _fullscreenWindow = new Window
        {
            WindowStyle        = WindowStyle.None,
            ResizeMode         = ResizeMode.NoResize,
            AllowsTransparency = false,
            Background         = System.Windows.Media.Brushes.Black,
            ShowInTaskbar      = false,
            Left               = b.Left   / dpiScale,
            Top                = b.Top    / dpiScale,
            Width              = b.Width  / dpiScale,
            Height             = b.Height / dpiScale,
            Content            = _preview,
            Topmost            = true,
        };

        // Window.KeyDown never fires when the embedded WebView2 HWND owns focus.
        // Use a low-level WndProc hook on the fullscreen Window's own HWND instead.
        _fullscreenWindow.SourceInitialized += (_, _) =>
        {
            var fsSource = (System.Windows.Interop.HwndSource)
                System.Windows.Interop.HwndSource.FromVisual(_fullscreenWindow)!;

            _fullscreenHook = (hwnd, msg, wParam, lParam, ref handled) =>
            {
                if (msg == WM_KEYDOWN && wParam.ToInt32() == VK_ESCAPE)
                {
                    handled = true;
                    Dispatcher.BeginInvoke(CloseFullscreen);
                }
                return IntPtr.Zero;
            };
            fsSource.AddHook(_fullscreenHook);
        };

        _fullscreenWindow.Closed += (_, _) =>
        {
            if (_isFullscreen) CloseFullscreen();
        };

        if (_viewMode == MdViewMode.SourceOnly)
            _ = ForceRefreshAsync();

        _fullscreenWindow.Show();
        SyncFullscreenState();
    }

    private void CloseFullscreen()
    {
        _isFullscreen = false;

        if (_fullscreenWindow is not null)
        {
            // Remove the WndProc hook before closing
            if (_fullscreenHook is not null)
            {
                var fsSource = System.Windows.Interop.HwndSource.FromVisual(_fullscreenWindow)
                    as System.Windows.Interop.HwndSource;
                fsSource?.RemoveHook(_fullscreenHook);
                _fullscreenHook = null;
            }

            // Detach _preview BEFORE closing so WPF doesn't destroy it
            _fullscreenWindow.Content = null;
            _fullscreenWindow.Close();
            _fullscreenWindow = null;
        }

        // Clear any locally-set size values retained from the fullscreen Window
        _preview.ClearValue(WidthProperty);
        _preview.ClearValue(HeightProperty);
        _preview.ClearValue(HorizontalAlignmentProperty);
        _preview.ClearValue(VerticalAlignmentProperty);

        // Rebuild the grid immediately so _preview is back in the visual tree
        UpdateGridLayout();

        // Defer the measure/layout pass: WPF needs one dispatcher cycle to fully
        // detach _preview from the closed Window before it can measure it correctly.
        var zoomToRestore = _zoomBeforeFullscreen;
        Dispatcher.BeginInvoke(() =>
        {
            _rootGrid.InvalidateMeasure();
            _rootGrid.UpdateLayout();

            _preview.SetZoom(zoomToRestore);
            _preview.InvalidateShell();
            _ = ForceRefreshAsync();
        }, System.Windows.Threading.DispatcherPriority.Loaded);

        SyncFullscreenState();
    }

    private void SyncFullscreenState()
    {
        if (_podFullscreen is not null)
            _podFullscreen.IsChecked = _isFullscreen;
        _preview.SyncFullscreenMenuItem(_isFullscreen);
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
        public static readonly RoutedCommand ToggleWordWrap    = new(nameof(ToggleWordWrap),    typeof(MarkdownEditorHost));
        public static readonly RoutedCommand ToggleFullscreen  = new(nameof(ToggleFullscreen),  typeof(MarkdownEditorHost));
    }
}
