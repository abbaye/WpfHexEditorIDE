//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Core;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.TextEditor.Highlighting;
using WpfHexEditor.Editor.TextEditor.Models;
using WpfHexEditor.Editor.TextEditor.Services;
using WpfHexEditor.Editor.TextEditor.ViewModels;

namespace WpfHexEditor.Editor.TextEditor.Controls;

/// <summary>
/// Full-featured text editor with syntax highlighting.
/// Implements <see cref="IDocumentEditor"/>, <see cref="IOpenableDocument"/>,
/// <see cref="IEditorPersistable"/>, and <see cref="INavigableDocument"/> so the project system
/// can save/restore per-file state and the Error List can navigate to a specific line.
/// </summary>
public sealed partial class TextEditor : UserControl, IDocumentEditor, IOpenableDocument, IEditorPersistable, INavigableDocument, IStatusBarContributor, ISearchTarget
{
    // -----------------------------------------------------------------------
    // Fields
    // -----------------------------------------------------------------------

    private readonly TextEditorViewModel _vm = new();
    private CancellationTokenSource? _cts         = null; // reserved for future async operations
    private TextLinkAdorner?         _linkAdorner = null;

    // -- Context menu dynamic headers ---
    private MenuItem? _undoMenuItem;
    private MenuItem? _redoMenuItem;

    // -- ISearchTarget -------------------------------------------------------
    private readonly List<(int Line, int Col)> _searchMatches = new();
    private int    _searchCurrentIndex = -1;
    private int    _searchMatchLength  = 0;
    public  event  EventHandler? SearchResultsChanged;

    // -----------------------------------------------------------------------
    // Constructor
    // -----------------------------------------------------------------------

    /// <summary>
    /// Creates a new <see cref="TextEditor"/>.
    /// </summary>
    public TextEditor()
    {
        InitializeComponent();

        // Wire ViewModel
        Viewport.Attach(_vm);
        _vm.PropertyChanged += OnVmPropertyChanged;

        // Commands
        UndoCommand      = new RelayCommand(() => Undo(),      () => CanUndo);
        RedoCommand      = new RelayCommand(() => Redo(),      () => CanRedo);
        SaveCommand      = new RelayCommand(() => Save(),      () => IsDirty && !string.IsNullOrEmpty(_vm.FilePath));
        CopyCommand      = new RelayCommand(() => Copy(),      () => _vm.HasSelection);
        CutCommand       = new RelayCommand(() => Cut(),       () => _vm.HasSelection && !IsReadOnly);
        PasteCommand     = new RelayCommand(() => Paste(),     () => !IsReadOnly);
        DeleteCommand    = new RelayCommand(() => Delete(),    () => _vm.HasSelection && !IsReadOnly);
        SelectAllCommand = new RelayCommand(() => SelectAll(), () => true);

        Loaded   += (_, _) => Viewport.Focus();
        Unloaded += (_, _) => Viewport.StopCursorBlink();

        InitializeContextMenu();
    }

    // -----------------------------------------------------------------------
    // Context menu
    // -----------------------------------------------------------------------

    private static TextBlock MakeMenuIcon(string glyph)
    {
        var tb = new TextBlock
        {
            Text                = glyph,
            FontFamily          = new FontFamily("Segoe MDL2 Assets"),
            FontSize            = 13,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
        };
        tb.SetResourceReference(TextElement.ForegroundProperty, "DockMenuForegroundBrush");
        return tb;
    }

    private void InitializeContextMenu()
    {
        var cm = new ContextMenu();

        cm.Items.Add(new MenuItem { Header = "Cu_t",        InputGestureText = "Ctrl+X", Command = ApplicationCommands.Cut,       CommandTarget = Viewport, Icon = MakeMenuIcon("\uE74E") });
        cm.Items.Add(new MenuItem { Header = "_Copy",       InputGestureText = "Ctrl+C", Command = ApplicationCommands.Copy,      CommandTarget = Viewport, Icon = MakeMenuIcon("\uE8C8") });
        cm.Items.Add(new MenuItem { Header = "_Paste",      InputGestureText = "Ctrl+V", Command = ApplicationCommands.Paste,     CommandTarget = Viewport, Icon = MakeMenuIcon("\uE9F5") });
        cm.Items.Add(new Separator());
        _undoMenuItem = new MenuItem { Header = "_Undo", InputGestureText = "Ctrl+Z", Command = ApplicationCommands.Undo, CommandTarget = Viewport, Icon = MakeMenuIcon("\uE7A7") };
        _redoMenuItem = new MenuItem { Header = "_Redo", InputGestureText = "Ctrl+Y/Ctrl+Shift+Z", Command = ApplicationCommands.Redo, CommandTarget = Viewport, Icon = MakeMenuIcon("\uE7A6") };
        cm.Items.Add(_undoMenuItem);
        cm.Items.Add(_redoMenuItem);
        cm.Items.Add(new Separator());
        cm.Items.Add(new MenuItem { Header = "Select _All", InputGestureText = "Ctrl+A", Command = ApplicationCommands.SelectAll, CommandTarget = Viewport, Icon = MakeMenuIcon("\uE8B3") });
        cm.Items.Add(new MenuItem { Header = "_Delete",     InputGestureText = "Del",    Command = ApplicationCommands.Delete,    CommandTarget = Viewport, Icon = MakeMenuIcon("\uE74D") });

        // Update undo/redo headers dynamically with step count when menu opens.
        cm.Opened += (_, _) =>
        {
            int undoCount = _vm.UndoCount;
            int redoCount = _vm.RedoCount;
            _undoMenuItem.Header = undoCount > 0 ? $"_Undo ({undoCount})" : "_Undo";
            _redoMenuItem.Header = redoCount > 0 ? $"_Redo ({redoCount})" : "_Redo";
        };

        Viewport.ContextMenu = cm;

        // Ctrl+Shift+Z → Redo (VS-standard alternative shortcut).
        Viewport.InputBindings.Add(new KeyBinding(ApplicationCommands.Redo,
            new KeyGesture(Key.Z, ModifierKeys.Control | ModifierKeys.Shift)));

        // Cut — enabled when normal or rectangular selection is active.
        Viewport.CommandBindings.Add(new CommandBinding(ApplicationCommands.Cut,
            (_, _) => { if (!Viewport.RectSelection.IsEmpty) Viewport.CutRectSelection(); else Cut(); },
            (_, e) => e.CanExecute = !IsReadOnly && (_vm.HasSelection || !Viewport.RectSelection.IsEmpty)));

        // Copy — enabled when normal or rectangular selection is active.
        Viewport.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy,
            (_, _) => { if (!Viewport.RectSelection.IsEmpty) Viewport.CopyRectSelection(); else Copy(); },
            (_, e) => e.CanExecute = _vm.HasSelection || !Viewport.RectSelection.IsEmpty));

        // Paste — disabled when a rectangular selection is active (no block-paste support).
        Viewport.CommandBindings.Add(new CommandBinding(ApplicationCommands.Paste,
            (_, _) => Paste(),
            (_, e) => e.CanExecute = !IsReadOnly && Clipboard.ContainsText() && Viewport.RectSelection.IsEmpty));

        Viewport.CommandBindings.Add(new CommandBinding(ApplicationCommands.Undo,
            (_, _) => Undo(),
            (_, e) => e.CanExecute = CanUndo));

        Viewport.CommandBindings.Add(new CommandBinding(ApplicationCommands.Redo,
            (_, _) => Redo(),
            (_, e) => e.CanExecute = CanRedo));

        Viewport.CommandBindings.Add(new CommandBinding(ApplicationCommands.SelectAll,
            (_, _) => SelectAll()));

        Viewport.CommandBindings.Add(new CommandBinding(ApplicationCommands.Delete,
            (_, _) => Delete(),
            (_, e) => e.CanExecute = !IsReadOnly));
    }

    // -----------------------------------------------------------------------
    // IDocumentEditor — State
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public bool IsDirty    => _vm.IsDirty;

    /// <inheritdoc/>
    public bool CanUndo    => _vm.CanUndo;

    /// <inheritdoc/>
    public bool CanRedo    => _vm.CanRedo;

    /// <inheritdoc/>
    public bool IsBusy     { get; private set; }

    /// <inheritdoc/>
    public string Title    => _vm.Title;

    /// <inheritdoc/>
    public bool IsReadOnly
    {
        get => _vm.IsReadOnly;
        set => _vm.IsReadOnly = value;
    }

    /// <summary>
    /// Lines scrolled per mouse-wheel notch.
    /// Forwarded to <see cref="TextViewport.MouseWheelSpeed"/>.
    /// </summary>
    public MouseWheelSpeed MouseWheelSpeed
    {
        get => Viewport.MouseWheelSpeed;
        set => Viewport.MouseWheelSpeed = value;
    }

    /// <summary>
    /// Kept for API compatibility — use <see cref="MouseWheelSpeed"/> instead.
    /// Forwarded to <see cref="TextViewport.ScrollSpeedMultiplier"/>.
    /// </summary>
    public double ScrollSpeedMultiplier
    {
        get => Viewport.ScrollSpeedMultiplier;
        set => Viewport.ScrollSpeedMultiplier = value;
    }

    /// <summary>
    /// Text zoom level (0.5–4.0, 1.0 = 100 %).
    /// Forwarded to <see cref="TextViewport.ZoomLevel"/>.
    /// Ctrl+Wheel, Ctrl+=, Ctrl+-, Ctrl+0 adjust this value interactively.
    /// </summary>
    public double ZoomLevel
    {
        get => Viewport.ZoomLevel;
        set => Viewport.ZoomLevel = value;
    }

    /// <summary>Raised when <see cref="ZoomLevel"/> changes.</summary>
    public event EventHandler<double>? ZoomLevelChanged
    {
        add    => Viewport.ZoomLevelChanged += value;
        remove => Viewport.ZoomLevelChanged -= value;
    }

    // -----------------------------------------------------------------------
    // IDocumentEditor — Commands
    // -----------------------------------------------------------------------

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

    // -----------------------------------------------------------------------
    // IDocumentEditor — Events
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public event EventHandler? ModifiedChanged;

    /// <inheritdoc/>
    public event EventHandler? CanUndoChanged;

    /// <inheritdoc/>
    public event EventHandler? CanRedoChanged;

    /// <inheritdoc/>
    public event EventHandler<string>? TitleChanged;

    /// <inheritdoc/>
    public event EventHandler<string>? StatusMessage;
    /// <inheritdoc/>
    public event EventHandler<string>? OutputMessage;

    /// <inheritdoc/>
    public event EventHandler? SelectionChanged;

    /// <inheritdoc/>
    public event EventHandler<DocumentOperationEventArgs>? OperationStarted;

    /// <inheritdoc/>
    public event EventHandler<DocumentOperationEventArgs>? OperationProgress;

    /// <inheritdoc/>
    public event EventHandler<DocumentOperationCompletedEventArgs>? OperationCompleted;

    // -----------------------------------------------------------------------
    // IDocumentEditor — Methods
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public void Undo()
    {
        _vm.Undo();
        RefreshCommands();
    }

    /// <inheritdoc/>
    public void Redo()
    {
        _vm.Redo();
        RefreshCommands();
    }

    /// <inheritdoc/>
    public void Save()
    {
        if (string.IsNullOrEmpty(_vm.FilePath)) return;
        _vm.SaveFileAsync(_vm.FilePath).GetAwaiter().GetResult();
        StatusMessage?.Invoke(this, $"Saved: {Path.GetFileName(_vm.FilePath)}");
    }

    /// <inheritdoc/>
    public async Task SaveAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_vm.FilePath)) return;
        await _vm.SaveFileAsync(_vm.FilePath, ct);
        StatusMessage?.Invoke(this, $"Saved: {Path.GetFileName(_vm.FilePath)}");
    }

    /// <inheritdoc/>
    public async Task SaveAsAsync(string filePath, CancellationToken ct = default)
    {
        await _vm.SaveFileAsync(filePath, ct);
        StatusMessage?.Invoke(this, $"Saved: {Path.GetFileName(filePath)}");
    }

    /// <inheritdoc/>
    public void Copy()
    {
        var text = _vm.GetSelectedText();
        if (!string.IsNullOrEmpty(text)) Clipboard.SetText(text);
    }

    /// <inheritdoc/>
    public void Cut()
    {
        Copy();
        if (_vm.HasSelection)
        {
            using (_vm.BeginUndoTransaction("Cut"))
                _vm.DeleteSelectedText();
            RefreshCommands();
        }
    }

    /// <inheritdoc/>
    public void Paste()
    {
        if (!IsReadOnly && Clipboard.ContainsText())
            _vm.InsertText(Clipboard.GetText());
    }

    /// <inheritdoc/>
    public void Delete()
    {
        if (!IsReadOnly)
        {
            if (_vm.HasSelection) _vm.DeleteSelectedText();
            else _vm.DeleteForward();
            RefreshCommands();
        }
    }

    /// <inheritdoc/>
    public void SelectAll()
    {
        // Simple full selection: from start of doc to end.
        _vm.SelectionAnchorLine   = 0;
        _vm.SelectionAnchorColumn = 0;
        _vm.CaretLine   = Math.Max(0, _vm.LineCount - 1);
        _vm.CaretColumn = _vm.GetLine(_vm.CaretLine).Length;
    }

    /// <inheritdoc/>
    public void Close()
    {
        Viewport.Detach();
        _vm.PropertyChanged -= OnVmPropertyChanged;
        _cts?.Cancel();
        _cts?.Dispose();
    }

    /// <inheritdoc/>
    public void CancelOperation()
    {
        _cts?.Cancel();
    }

    // -----------------------------------------------------------------------
    // IOpenableDocument
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public async Task OpenAsync(string filePath, CancellationToken ct = default)
    {
        IsBusy = true;
        OperationStarted?.Invoke(this, new DocumentOperationEventArgs
        {
            Title = "Opening", Message = Path.GetFileName(filePath), IsIndeterminate = true
        });

        try
        {
            await _vm.LoadFileAsync(filePath, null, ct);
            UpdateStatusBar();
            StatusMessage?.Invoke(this, $"Opened: {Path.GetFileName(filePath)}");
            OperationCompleted?.Invoke(this, new DocumentOperationCompletedEventArgs { Success = true });
        }
        catch (OperationCanceledException)
        {
            OperationCompleted?.Invoke(this, new DocumentOperationCompletedEventArgs { WasCancelled = true });
        }
        catch (Exception ex)
        {
            OperationCompleted?.Invoke(this, new DocumentOperationCompletedEventArgs { ErrorMessage = ex.Message });
        }
        finally
        {
            IsBusy = false;
        }
    }

    // -----------------------------------------------------------------------
    // Public extras
    // -----------------------------------------------------------------------

    /// <summary>
    /// Sets the active syntax definition (overrides auto-detection).
    /// </summary>
    public void SetSyntaxDefinition(SyntaxDefinition? def)
    {
        _vm.SyntaxDefinition = def;
        LanguageText.Text = def?.Name ?? "Plain Text";
    }

    /// <summary>
    /// Sets the text encoding (UTF-8, Latin-1, Shift-JIS, …).
    /// </summary>
    public void SetEncoding(Encoding enc)
    {
        _vm.Encoding = enc;
        EncodingText.Text = enc.WebName.ToUpperInvariant();
    }

    /// <summary>
    /// Loads a raw text string into the editor.
    /// </summary>
    public void SetText(string text) => _vm.SetText(text);

    /// <summary>
    /// Returns the full document text.
    /// </summary>
    public string GetText() => _vm.GetText();

    /// <summary>
    /// Populates the editor with raw text, bypassing file I/O.
    /// Optionally applies a syntax language by name (e.g. "C#") and/or marks the
    /// document as read-only. Intended for plugin-generated content such as
    /// decompiled source or IL disassembly that has no backing file.
    /// </summary>
    /// <param name="text">Text content to display.</param>
    /// <param name="readOnly">When true the user cannot edit the content.</param>
    /// <param name="languageName">
    /// Optional syntax language name to look up in <see cref="SyntaxDefinitionCatalog"/>.
    /// Pass null for plain text.
    /// </param>
    public void SetContentDirect(string text, bool readOnly = true, string? languageName = null)
    {
        _vm.SetText(text);
        _vm.IsReadOnly = readOnly;

        if (languageName is not null)
        {
            var def = SyntaxDefinitionCatalog.Instance.FindByName(languageName);
            if (def is not null)
            {
                _vm.SyntaxDefinition = def;
                LanguageText.Text    = def.Name;
            }
        }
    }

    /// <summary>
    /// Sets content and installs a <see cref="TextLinkAdorner"/> for Ctrl+Click goto-definition
    /// navigation.  Each link renders as an underline; Ctrl+Clicking invokes <see cref="TextLink.OnClick"/>.
    /// Call <see cref="ClearLinks"/> to remove the adorner.
    /// </summary>
    public void SetContentWithLinks(
        string                    text,
        IReadOnlyList<TextLink>   links,
        bool                      readOnly     = true,
        string?                   languageName = null)
    {
        SetContentDirect(text, readOnly, languageName);
        InstallAdorner(links);
    }

    /// <summary>Removes the current text-link adorner (if any).</summary>
    public void ClearLinks()
    {
        if (_linkAdorner is null) return;

        var layer = AdornerLayer.GetAdornerLayer(Viewport);
        layer?.Remove(_linkAdorner);
        _linkAdorner = null;
    }

    // ── Adorner management ────────────────────────────────────────────────────

    private void InstallAdorner(IReadOnlyList<TextLink> links)
    {
        // Remove stale adorner if present.
        ClearLinks();

        if (links.Count == 0) return;

        var layer = AdornerLayer.GetAdornerLayer(Viewport);
        if (layer is null) return;

        _linkAdorner = new TextLinkAdorner(Viewport, _vm);
        _linkAdorner.SetLinks(links);
        layer.Add(_linkAdorner);
    }

    /// <summary>
    /// Moves the caret to the given 1-based line and column and scrolls it into view.
    /// </summary>
    public void GoToLine(int line, int column = 1)
    {
        // ViewModel uses 0-based indices; DTO / public API is 1-based
        _vm.CaretLine   = Math.Max(0, line   - 1);
        _vm.CaretColumn = Math.Max(0, column - 1);
    }

    /// <inheritdoc/>
    void INavigableDocument.NavigateTo(int line, int column) => GoToLine(line, column);

    // -----------------------------------------------------------------------
    // IEditorPersistable
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public EditorConfigDto GetEditorConfig() => new()
    {
        // Text-editor specific fields
        SyntaxLanguageId  = _vm.SyntaxDefinition?.Name,
        CaretLine         = _vm.CaretLine + 1,    // store 1-based
        CaretColumn       = _vm.CaretColumn + 1,
        FirstVisibleLine  = Viewport.FirstVisibleLine,
        // HexEditor fields are not applicable — leave at defaults
        SelectionStart    = -1,
    };

    /// <inheritdoc/>
    public void ApplyEditorConfig(EditorConfigDto config)
    {
        if (config is null) return;

        // Restore caret position (convert 1-based back to 0-based)
        if (config.CaretLine > 0)
        {
            _vm.CaretLine   = config.CaretLine - 1;
            _vm.CaretColumn = Math.Max(0, config.CaretColumn - 1);
        }

        // Restore scroll position after layout
        if (config.FirstVisibleLine > 0)
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () =>
            {
                if (Viewport.LineHeight > 0)
                    ScrollView.ScrollToVerticalOffset(config.FirstVisibleLine * Viewport.LineHeight);
            });
        }

        // Restore syntax language override
        if (!string.IsNullOrEmpty(config.SyntaxLanguageId))
        {
            var def = SyntaxDefinitionCatalog.Instance.FindByName(config.SyntaxLanguageId);
            if (def is not null) SetSyntaxDefinition(def);
        }
    }

    /// <inheritdoc/>
    /// <remarks>Text editors do not have binary unsaved modifications.</remarks>
    public byte[]? GetUnsavedModifications() => null;

    /// <inheritdoc/>
    /// <remarks>Not applicable for text editors.</remarks>
    public void ApplyUnsavedModifications(byte[] data) { }

    /// <inheritdoc/>
    /// <remarks>Text editors do not use binary bookmarks.</remarks>
    public IReadOnlyList<BookmarkDto>? GetBookmarks() => null;

    /// <inheritdoc/>
    public void ApplyBookmarks(IReadOnlyList<BookmarkDto> bookmarks) { }

    /// <inheritdoc/>
    /// <remarks>Text editors do not have binary changeset edits.</remarks>
    public ChangesetSnapshot GetChangesetSnapshot() => ChangesetSnapshot.Empty;

    /// <inheritdoc/>
    /// <remarks>Not applicable for text editors.</remarks>
    public void ApplyChangeset(ChangesetDto changeset) { }

    /// <inheritdoc/>
    public void MarkChangesetSaved() { }

    // -----------------------------------------------------------------------
    // Scroll sync
    // -----------------------------------------------------------------------

    private void ScrollView_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_vm is null || Viewport.LineHeight <= 0) return;

        int firstLine = (int)(e.VerticalOffset / Viewport.LineHeight);
        Viewport.FirstVisibleLine   = firstLine;
        Viewport.HorizontalOffset   = e.HorizontalOffset;

        // Adjust the viewport height to fill the scroll area
        Viewport.Width  = Math.Max(Viewport.EstimatedMaxWidth, ScrollView.ViewportWidth);
        Viewport.Height = Math.Max(Viewport.TotalHeight + Viewport.LineHeight, ScrollView.ViewportHeight);
    }

    // -----------------------------------------------------------------------
    // VM change handler
    // -----------------------------------------------------------------------

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        Dispatcher.InvokeAsync(() =>
        {
            switch (e.PropertyName)
            {
                case nameof(TextEditorViewModel.IsDirty):
                    ModifiedChanged?.Invoke(this, EventArgs.Empty);
                    TitleChanged?.Invoke(this, _vm.Title);
                    RefreshCommands();
                    break;
                case nameof(TextEditorViewModel.CanUndo):
                    CanUndoChanged?.Invoke(this, EventArgs.Empty);
                    RefreshCommands();
                    break;
                case nameof(TextEditorViewModel.CanRedo):
                    CanRedoChanged?.Invoke(this, EventArgs.Empty);
                    RefreshCommands();
                    break;
                case nameof(TextEditorViewModel.CaretStatus):
                    CaretText.Text = _vm.CaretStatus;
                    StatusMessage?.Invoke(this, _vm.CaretStatus);
                    RefreshTextStatusBarItems();
                    break;
                case nameof(TextEditorViewModel.Title):
                    TitleChanged?.Invoke(this, _vm.Title);
                    break;
                case nameof(TextEditorViewModel.HasSelection):
                    SelectionChanged?.Invoke(this, EventArgs.Empty);
                    RefreshCommands();
                    break;
            }
        });
    }

    private void RefreshCommands()
    {
        (UndoCommand      as RelayCommand)?.RaiseCanExecuteChanged();
        (RedoCommand      as RelayCommand)?.RaiseCanExecuteChanged();
        (SaveCommand      as RelayCommand)?.RaiseCanExecuteChanged();
        (CopyCommand      as RelayCommand)?.RaiseCanExecuteChanged();
        (CutCommand       as RelayCommand)?.RaiseCanExecuteChanged();
        (PasteCommand     as RelayCommand)?.RaiseCanExecuteChanged();
        (DeleteCommand    as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void UpdateStatusBar()
    {
        LanguageText.Text  = _vm.SyntaxDefinition?.Name ?? "Plain Text";
        CaretText.Text     = _vm.CaretStatus;
        EncodingText.Text  = _vm.Encoding.WebName.ToUpperInvariant();
    }

    // -----------------------------------------------------------------------
    // IStatusBarContributor — IDE AppStatusBar integration
    // -----------------------------------------------------------------------

    private ObservableCollection<StatusBarItem>? _teStatusBarItems;
    private StatusBarItem _sbTeLanguage = null!;
    private StatusBarItem _sbTePosition = null!;
    private StatusBarItem _sbTeZoom     = null!;
    private StatusBarItem _sbTeEncoding = null!;

    /// <inheritdoc />
    public ObservableCollection<StatusBarItem> StatusBarItems
        => _teStatusBarItems ??= BuildTextStatusBarItems();

    private ObservableCollection<StatusBarItem> BuildTextStatusBarItems()
    {
        _sbTeLanguage = new StatusBarItem { Label = "Language", Tooltip = "Active syntax language" };
        _sbTePosition = new StatusBarItem { Label = "Position", Tooltip = "Caret line and column" };
        _sbTeZoom     = new StatusBarItem { Label = "Zoom",     Tooltip = "Editor zoom level" };
        _sbTeEncoding = new StatusBarItem { Label = "Encoding", Tooltip = "File encoding" };

        // Zoom preset choices.
        foreach (var (pct, factor) in new (string, double)[] { ("50%", 0.5), ("75%", 0.75), ("100%", 1.0), ("125%", 1.25), ("150%", 1.5), ("200%", 2.0) })
        {
            var capture = factor;
            _sbTeZoom.Choices.Add(new StatusBarChoice
            {
                DisplayName = pct,
                Command     = new RelayCommand(() => ZoomLevel = capture),
            });
        }

        // Wire live-update events once.
        ZoomLevelChanged += (_, _) => RefreshTextStatusBarItems();

        RefreshTextStatusBarItems();
        return new ObservableCollection<StatusBarItem> { _sbTeLanguage, _sbTePosition, _sbTeZoom, _sbTeEncoding };
    }

    void IStatusBarContributor.RefreshStatusBarItems() => RefreshTextStatusBarItems();

    private void RefreshTextStatusBarItems()
    {
        if (_teStatusBarItems is null) return;

        _sbTeLanguage.Value = _vm.SyntaxDefinition?.Name ?? "Plain Text";
        _sbTePosition.Value = _vm.CaretStatus;
        _sbTeZoom.Value     = $"{(int)(ZoomLevel * 100)}%";
        _sbTeEncoding.Value = _vm.Encoding.WebName.ToUpperInvariant();

        // Keep zoom choice checkmarks in sync.
        string zoomLabel = _sbTeZoom.Value;
        foreach (var choice in _sbTeZoom.Choices)
            choice.IsActive = choice.DisplayName == zoomLabel;
    }

    // -----------------------------------------------------------------------
    // ISearchTarget — inline QuickSearchBar support
    // -----------------------------------------------------------------------

    SearchBarCapabilities ISearchTarget.Capabilities =>
        SearchBarCapabilities.CaseSensitive | SearchBarCapabilities.Replace;

    int ISearchTarget.MatchCount        => _searchMatches.Count;
    int ISearchTarget.CurrentMatchIndex => _searchCurrentIndex;

    UIElement? ISearchTarget.GetCustomFiltersContent() => null;

    void ISearchTarget.Find(string query, SearchTargetOptions options)
    {
        _searchMatches.Clear();
        _searchCurrentIndex = -1;
        _searchMatchLength  = 0;

        if (string.IsNullOrEmpty(query)) { SearchResultsChanged?.Invoke(this, EventArgs.Empty); return; }

        _searchMatchLength = query.Length;
        var comparison = options.HasFlag(SearchTargetOptions.CaseSensitive)
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        for (int li = 0; li < _vm.LineCount; li++)
        {
            var lineText = _vm.GetLine(li);
            int idx = 0;
            while ((idx = lineText.IndexOf(query, idx, comparison)) >= 0)
            {
                _searchMatches.Add((li, idx));
                idx += query.Length;
            }
        }

        if (_searchMatches.Count > 0)
        {
            _searchCurrentIndex = 0;
            NavigateToSearchMatch(_searchCurrentIndex);
        }

        SearchResultsChanged?.Invoke(this, EventArgs.Empty);
    }

    void ISearchTarget.FindNext()
    {
        if (_searchMatches.Count == 0) return;
        _searchCurrentIndex = (_searchCurrentIndex + 1) % _searchMatches.Count;
        NavigateToSearchMatch(_searchCurrentIndex);
        SearchResultsChanged?.Invoke(this, EventArgs.Empty);
    }

    void ISearchTarget.FindPrevious()
    {
        if (_searchMatches.Count == 0) return;
        _searchCurrentIndex = (_searchCurrentIndex - 1 + _searchMatches.Count) % _searchMatches.Count;
        NavigateToSearchMatch(_searchCurrentIndex);
        SearchResultsChanged?.Invoke(this, EventArgs.Empty);
    }

    void ISearchTarget.ClearSearch()
    {
        _searchMatches.Clear();
        _searchCurrentIndex = -1;
        _searchMatchLength  = 0;
        _vm.ClearSelection();
        SearchResultsChanged?.Invoke(this, EventArgs.Empty);
    }

    void ISearchTarget.Replace(string replacement)
    {
        if (_searchCurrentIndex < 0 || _searchCurrentIndex >= _searchMatches.Count) return;
        var (line, col) = _searchMatches[_searchCurrentIndex];

        // Set selection to the match span, then replace via ViewModel
        _vm.SelectionAnchorLine   = line;
        _vm.SelectionAnchorColumn = col;
        _vm.CaretLine   = line;
        _vm.CaretColumn = col + _searchMatchLength;
        _vm.DeleteSelectedText();
        _vm.InsertText(replacement);

        // Re-run find to refresh match list
        SearchResultsChanged?.Invoke(this, EventArgs.Empty);
    }

    void ISearchTarget.ReplaceAll(string replacement)
    {
        if (_searchMatches.Count == 0) return;

        // Replace from bottom to top so earlier offsets remain valid
        for (int i = _searchMatches.Count - 1; i >= 0; i--)
        {
            var (line, col) = _searchMatches[i];
            _vm.SelectionAnchorLine   = line;
            _vm.SelectionAnchorColumn = col;
            _vm.CaretLine   = line;
            _vm.CaretColumn = col + _searchMatchLength;
            _vm.DeleteSelectedText();
            _vm.InsertText(replacement);
        }

        _searchMatches.Clear();
        _searchCurrentIndex = -1;
        SearchResultsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void NavigateToSearchMatch(int index)
    {
        var (line, col) = _searchMatches[index];
        _vm.SelectionAnchorLine   = line;
        _vm.SelectionAnchorColumn = col;
        _vm.CaretLine   = line;
        _vm.CaretColumn = col + _searchMatchLength;

        if (Viewport.LineHeight > 0)
            ScrollView.ScrollToVerticalOffset(Math.Max(0, (line - 3) * Viewport.LineHeight));
    }

    // -----------------------------------------------------------------------
    // Quick Search Bar — show / hide + keyboard hook
    // -----------------------------------------------------------------------

    private void ShowSearch()
    {
        if (QuickSearchBarOverlay.Visibility == Visibility.Visible)
        {
            QuickSearchBarOverlay.FocusSearchInput();
            return;
        }

        QuickSearchBarOverlay.OnCloseRequested -= OnSearchBarCloseRequested;
        QuickSearchBarOverlay.OnCloseRequested += OnSearchBarCloseRequested;
        QuickSearchBarOverlay.BindToTarget(this);
        QuickSearchBarOverlay.Visibility = Visibility.Visible;
        QuickSearchBarOverlay.EnsureDefaultPosition(SearchBarCanvas);
    }

    private void HideSearch()
    {
        QuickSearchBarOverlay.Visibility = Visibility.Collapsed;
        QuickSearchBarOverlay.Detach();
        ((ISearchTarget)this).ClearSearch();
    }

    private void OnSearchBarCloseRequested(object? sender, EventArgs e) => HideSearch();

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
        { ShowSearch(); e.Handled = true; }
        else if (e.Key == Key.Escape && QuickSearchBarOverlay.Visibility == Visibility.Visible)
        { HideSearch(); e.Handled = true; }
    }
}

// -----------------------------------------------------------------------
// Minimal RelayCommand (avoids external dependency)
// -----------------------------------------------------------------------

internal sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute    = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
    public void Execute(object? parameter)    => _execute();

    public event EventHandler? CanExecuteChanged;
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
