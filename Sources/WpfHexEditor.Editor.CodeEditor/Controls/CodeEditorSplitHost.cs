// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: CodeEditorSplitHost.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-05
// Description:
//     Host container that wraps two CodeEditor instances sharing the same
//     CodeDocument. A toggle button lets the user split the view horizontally.
//     Implements IDocumentEditor by delegating to the last-focused editor
//     so the host (docking, menu) interacts transparently.
//     Implements IOpenableDocument by delegating to _primaryEditor.LoadFromFile
//     (both editors share the same CodeDocument, so the secondary view is
//     automatically updated).
//     Implements INavigableDocument.NavigateTo so the Error List can jump to
//     a specific line in the active editor pane.
//
// Architecture Notes:
//     Proxy / Delegate Pattern — IDocumentEditor forwarded to _activeEditor.
//     Composite — wraps two CodeEditor children sharing one CodeDocument.
//     Observer  — GotFocus on each CodeEditor updates _activeEditor reference.
// ==========================================================

using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Editor.CodeEditor.Models;
using WpfHexEditor.Editor.CodeEditor.NavigationBar;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.Core.Documents;
using WpfHexEditor.Editor.Core.LSP;
using WpfHexEditor.Editor.Core.Views;
using EditorStatusBarItem = WpfHexEditor.Editor.Core.StatusBarItem;
using WpfHexEditor.Core.ProjectSystem.Languages;

namespace WpfHexEditor.Editor.CodeEditor.Controls;

/// <summary>
/// A split-view host for <see cref="CodeEditor"/>.
/// Both editors share the same <see cref="CodeDocument"/>; scroll positions
/// and caret positions are independent.
/// </summary>
public sealed class CodeEditorSplitHost : Grid, IDocumentEditor, IBufferAwareEditor, IOpenableDocument, INavigableDocument, IStatusBarContributor, IDiagnosticSource, ILspAwareEditor
{
    #region Child controls

    private readonly CodeEditor              _primaryEditor;
    private readonly CodeEditor              _secondaryEditor;
    private readonly GridSplitter            _splitter;
    private readonly ToggleButton            _splitToggle;
    private readonly CodeEditorNavigationBar _navBar;

    private readonly RowDefinition     _navBarRow;
    private readonly RowDefinition     _breadcrumbRow;
    private readonly RowDefinition     _primaryRow;
    private readonly RowDefinition     _splitterRow;
    private readonly RowDefinition     _secondaryRow;
    private readonly LspBreadcrumbBar  _breadcrumbBar;

    // The editor that most recently received focus — commands delegate to this one.
    private CodeEditor _activeEditor;

    // -- QuickSearch overlay -----------------------------------------------
    private readonly Canvas         _searchBarCanvas;
    private readonly QuickSearchBar _searchBarOverlay;

    #endregion

    #region Constructor

    public CodeEditorSplitHost()
    {
        // -- Row layout ------------------------------------------------------
        // Row 0 = navigation bar (fixed 22 px)
        // Row 1 = LSP breadcrumb bar (0 px when LSP inactive, 22 px when active)
        // Row 2 = primary editor  (star)
        // Row 3 = GridSplitter    (auto, hidden when not split)
        // Row 4 = secondary editor (0 → star when split)
        _navBarRow      = new RowDefinition { Height = new GridLength(22) };
        _breadcrumbRow  = new RowDefinition { Height = new GridLength(0) };   // hidden until LSP active
        _primaryRow     = new RowDefinition { Height = new GridLength(1, GridUnitType.Star) };
        _splitterRow    = new RowDefinition { Height = GridLength.Auto };
        _secondaryRow   = new RowDefinition { Height = new GridLength(0) };

        RowDefinitions.Add(_navBarRow);
        RowDefinitions.Add(_breadcrumbRow);
        RowDefinitions.Add(_primaryRow);
        RowDefinitions.Add(_splitterRow);
        RowDefinitions.Add(_secondaryRow);

        // -- LSP breadcrumb bar (Row 1, collapsed until LSP is set) ------------
        _breadcrumbBar = new LspBreadcrumbBar();
        SetRow(_breadcrumbBar, 1);
        Children.Add(_breadcrumbBar);

        // -- Primary editor --------------------------------------------------
        _primaryEditor = new CodeEditor();
        SetRow(_primaryEditor, 2);
        Children.Add(_primaryEditor);

        // -- Splitter (hidden while not split) -------------------------------
        _splitter = new GridSplitter
        {
            Height              = 4,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Visibility          = Visibility.Collapsed,
            Background          = SystemColors.ControlDarkBrush,
        };
        SetRow(_splitter, 3);
        Children.Add(_splitter);

        // -- Secondary editor (shares the same document) ---------------------
        _secondaryEditor = new CodeEditor();
        SetRow(_secondaryEditor, 4);
        Children.Add(_secondaryEditor);

        // -- Split toggle (flat VS2022-style) --------------------------------
        _splitToggle = new ToggleButton
        {
            Content             = "\uE8A5",  // Segoe MDL2 "SplitView" glyph
            FontFamily          = new FontFamily("Segoe MDL2 Assets"),
            FontSize            = 10,
            Width               = 16,
            Height              = 16,
            Padding             = new Thickness(0),
            ToolTip             = "Split Editor",
            Style               = BuildFlatToggleButtonStyle(),
        };
        _splitToggle.Checked   += OnSplitToggleChecked;
        _splitToggle.Unchecked += OnSplitToggleUnchecked;
        // Note: _splitToggle is NOT added to this Grid directly; it is handed
        // to the navigation bar which places it in its rightmost column.

        // -- Navigation bar (Row 0) ------------------------------------------
        _navBar = new CodeEditorNavigationBar();
        _navBar.AddSplitToggle(_splitToggle);
        _navBar.Attach(_primaryEditor);
        SetRow(_navBar, 0);
        Children.Add(_navBar);

        // -- Initialise active editor and wire focus tracking -----------------
        _activeEditor = _primaryEditor;

        _primaryEditor.GotFocus   += (_, _) => { _activeEditor = _primaryEditor;   _activeEditor.RefreshJsonStatusBarItems(); };
        _secondaryEditor.GotFocus += (_, _) => { _activeEditor = _secondaryEditor; _activeEditor.RefreshJsonStatusBarItems(); };

        // -- Forward events from primary editor (document is shared) -----------
        _primaryEditor.ModifiedChanged  += (s, e) => ModifiedChanged?.Invoke(this, e);
        _primaryEditor.CanUndoChanged   += (s, e) => CanUndoChanged?.Invoke(this, e);
        _primaryEditor.CanRedoChanged   += (s, e) => CanRedoChanged?.Invoke(this, e);
        _primaryEditor.TitleChanged     += (s, e) => TitleChanged?.Invoke(this, e);
        _primaryEditor.StatusMessage    += (s, e) => StatusMessage?.Invoke(this, e);
        _primaryEditor.OutputMessage    += (s, e) => OutputMessage?.Invoke(this, e);
        _primaryEditor.SelectionChanged += (s, e) => SelectionChanged?.Invoke(this, e);
        _primaryEditor.OperationStarted   += (s, e) => OperationStarted?.Invoke(this, e);
        _primaryEditor.OperationProgress  += (s, e) => OperationProgress?.Invoke(this, e);
        _primaryEditor.OperationCompleted += (s, e) => OperationCompleted?.Invoke(this, e);

        // -- Forward InlineHints reference events from both editors ---------------
        // Either editor can show the references popup (both share the same document).
        _primaryEditor.ReferenceNavigationRequested       += (s, e) => ReferenceNavigationRequested?.Invoke(this, e);
        _primaryEditor.FindAllReferencesDockRequested     += (s, e) => FindAllReferencesDockRequested?.Invoke(this, e);
        _primaryEditor.GoToExternalDefinitionRequested    += (s, e) => GoToExternalDefinitionRequested?.Invoke(this, e);
        _secondaryEditor.ReferenceNavigationRequested     += (s, e) => ReferenceNavigationRequested?.Invoke(this, e);
        _secondaryEditor.FindAllReferencesDockRequested   += (s, e) => FindAllReferencesDockRequested?.Invoke(this, e);
        _secondaryEditor.GoToExternalDefinitionRequested  += (s, e) => GoToExternalDefinitionRequested?.Invoke(this, e);

        // Attach breadcrumb bar to primary editor so it tracks caret position.
        _breadcrumbBar.Attach(_primaryEditor, filePath: null);

        // Connect the secondary editor to the same document after primary is loaded.
        Loaded += OnHostLoaded;

        // -- QuickSearch overlay (Canvas floats above all rows) ---------------
        _searchBarCanvas = new Canvas();
        SetRow(_searchBarCanvas, 0);
        SetRowSpan(_searchBarCanvas, 5);        // navBar + breadcrumb + primary + splitter + secondary
        Panel.SetZIndex(_searchBarCanvas, 10);
        _searchBarOverlay = new QuickSearchBar { Width = 520, Visibility = Visibility.Collapsed };
        _searchBarOverlay.OnCloseRequested += (_, _) => HideSearch();
        _searchBarCanvas.Children.Add(_searchBarOverlay);
        Children.Add(_searchBarCanvas);

        PreviewKeyDown += OnHostPreviewKeyDown;
    }

    #endregion

    #region Public API — document access

    /// <summary>The primary (top) code editor instance.</summary>
    public CodeEditor PrimaryEditor => _primaryEditor;

    /// <summary>The secondary (bottom) code editor instance — only visible when split.</summary>
    public CodeEditor SecondaryEditor => _secondaryEditor;

    /// <summary>Whether the split view is currently active.</summary>
    public bool IsSplit => _splitToggle.IsChecked == true;

    /// <summary>
    /// Programmatically toggles the split view.
    /// </summary>
    public void ToggleSplit() => _splitToggle.IsChecked = !_splitToggle.IsChecked;

    #endregion

    #region Quick Search

    /// <summary>
    /// Shows the inline unified search bar and binds it to the currently active editor.
    /// If already visible, just re-focuses the search input.
    /// </summary>
    public void ShowSearch()
    {
        if (_searchBarOverlay.Visibility == Visibility.Visible)
        {
            _searchBarOverlay.FocusSearchInput();
            return;
        }

        _searchBarOverlay.BindToTarget(_activeEditor);
        _searchBarOverlay.Visibility = Visibility.Visible;
        _searchBarOverlay.EnsureDefaultPosition(_searchBarCanvas);
    }

    /// <summary>Hides the inline search bar and clears its bound state.</summary>
    public void HideSearch()
    {
        _searchBarOverlay.Visibility = Visibility.Collapsed;
        _searchBarOverlay.Detach();
    }

    /// <summary>
    /// Shows the inline search bar with the Replace section pre-expanded.
    /// If already visible, just expands Replace and re-focuses the search input.
    /// </summary>
    public void ShowSearchAndReplace()
    {
        ShowSearch();
        _searchBarOverlay.ExpandReplace();
    }

    #endregion

    #region Language

    /// <summary>
    /// Applies a <see cref="LanguageDefinition"/> to both editor panes.
    /// Builds the appropriate <see cref="ISyntaxHighlighter"/> and sets
    /// <see cref="CodeEditor.ExternalHighlighter"/> and <see cref="CodeEditor.Language"/>
    /// on both the primary and secondary editors.
    /// Pass <see langword="null"/> to clear syntax highlighting (Plain Text mode).
    /// </summary>
    /// <param name="lang">The language to activate, or <see langword="null"/> for none.</param>
    public void SetLanguage(LanguageDefinition? lang)
    {
        var highlighter = lang is not null ? CodeEditorFactory.BuildHighlighter(lang) : null;
        _primaryEditor.ExternalHighlighter   = highlighter;
        _secondaryEditor.ExternalHighlighter = highlighter;
        _primaryEditor.Language   = lang;
        _secondaryEditor.Language = lang;
    }

    #endregion

    #region Quick Search — keyboard handler

    private void OnHostPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            ShowSearch();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && _searchBarOverlay.Visibility == Visibility.Visible)
        {
            HideSearch();
            e.Handled = true;
        }
    }

    #endregion

    #region ILspAwareEditor

    /// <inheritdoc/>
    public void SetLspClient(ILspClient? client)
    {
        // Forward to both code editors so hover, code actions, rename all work in split view.
        _primaryEditor.SetLspClient(client);
        _secondaryEditor.SetLspClient(client);

        // Show/hide the breadcrumb bar and update its client.
        _breadcrumbBar.SetLspClient(client);
        _breadcrumbRow.Height = client is not null
            ? new GridLength(22)
            : new GridLength(0);
    }

    /// <inheritdoc/>
    public void SetDocumentManager(WpfHexEditor.Editor.Core.Documents.IDocumentManager manager)
    {
        // Forward to primary editor; secondary shares the same document so only one call needed.
        (_primaryEditor as ILspAwareEditor)?.SetDocumentManager(manager);
    }

    #endregion

    #region Split button style

    /// <summary>
    /// Builds a flat, borderless VS2022-like ToggleButton style.
    /// Uses dynamic resource references so the button respects the active theme.
    /// States: Normal=transparent | Hover=CE_Selection@30% | Checked=CE_Selection@60%
    /// </summary>
    private static Style BuildFlatToggleButtonStyle()
    {
        // Reusable transparent + 1px transparent border template
        var template = new ControlTemplate(typeof(ToggleButton));

        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(2));
        border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        border.SetValue(Border.BorderBrushProperty, Brushes.Transparent);

        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        content.SetValue(ContentPresenter.VerticalAlignmentProperty,   VerticalAlignment.Center);
        border.AppendChild(content);

        template.VisualTree = border;

        // Hover trigger — light theme-aware fill
        var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(
            Border.BackgroundProperty,
            new SolidColorBrush(Color.FromArgb(50, 100, 100, 100)),
            "border"));
        hoverTrigger.Setters.Add(new Setter(
            Border.BorderBrushProperty,
            new SolidColorBrush(Color.FromArgb(80, 150, 150, 150)),
            "border"));
        template.Triggers.Add(hoverTrigger);

        // Checked trigger — more prominent fill
        var checkedTrigger = new Trigger { Property = ToggleButton.IsCheckedProperty, Value = true };
        checkedTrigger.Setters.Add(new Setter(
            Border.BackgroundProperty,
            new SolidColorBrush(Color.FromArgb(90, 100, 140, 200)),
            "border"));
        checkedTrigger.Setters.Add(new Setter(
            Border.BorderBrushProperty,
            new SolidColorBrush(Color.FromArgb(120, 100, 150, 220)),
            "border"));
        template.Triggers.Add(checkedTrigger);

        // Name the border so ControlTemplate Trigger Setters can target it by name.
        border.Name = "border";

        var style = new Style(typeof(ToggleButton));
        style.Setters.Add(new Setter(Control.TemplateProperty, template));
        style.Setters.Add(new Setter(Control.ForegroundProperty, new SolidColorBrush(Color.FromRgb(180, 180, 180))));
        style.Setters.Add(new Setter(Control.CursorProperty, Cursors.Hand));
        style.Seal();
        return style;
    }

    #endregion

    #region Split toggle handlers

    private void OnSplitToggleChecked(object sender, RoutedEventArgs e)
    {
        // Expand the secondary row to half the current height.
        double currentHeight = _primaryRow.ActualHeight;
        double half = Math.Max(50, currentHeight / 2);
        _primaryRow.Height   = new GridLength(half, GridUnitType.Star);
        _secondaryRow.Height = new GridLength(half, GridUnitType.Star);
        _splitter.Visibility = Visibility.Visible;
        _secondaryEditor.Focus();
    }

    private void OnSplitToggleUnchecked(object sender, RoutedEventArgs e)
    {
        _secondaryRow.Height = new GridLength(0);
        _splitter.Visibility = Visibility.Collapsed;
        _primaryEditor.Focus();
    }

    #endregion

    #region Loaded — share document between editors

    private void OnHostLoaded(object sender, RoutedEventArgs e)
    {
        // Share the primary editor's document with the secondary editor.
        // Both will render and edit the same CodeDocument instance.
        var doc = _primaryEditor.Document;
        if (doc != null)
            _secondaryEditor.SetDocument(doc);
    }

    #endregion

    #region INavigableDocument — delegates to active editor

    /// <inheritdoc/>
    void INavigableDocument.NavigateTo(int line, int column)
    {
        // CodeEditor.NavigateToLine is 0-based; INavigableDocument contract is 1-based.
        // Navigates the active (last-focused) editor so the visible pane follows the caret.
        _activeEditor.NavigateToLine(Math.Max(0, line - 1));
    }

    #endregion

    #region IOpenableDocument — delegates to primary editor

    async Task IOpenableDocument.OpenAsync(string filePath, CancellationToken ct)
    {
        // Resolve and apply the language for EVERY file open — not just the first.
        // The previous "lazy / null-guard" approach caused syntax highlighting to
        // persist from the prior file when switching between files of different types.
        var language = LanguageRegistry.Instance.GetLanguageForFile(filePath);

        if (language is not null)
        {
            var highlighter = CodeEditorFactory.BuildHighlighter(language);
            _primaryEditor.ExternalHighlighter   = highlighter;
            _secondaryEditor.ExternalHighlighter = highlighter;

            // Propagate the LanguageDefinition so per-language feature gates
            // (InlineHints, Ctrl+Click navigation) apply even when the editor is
            // created outside CodeEditorFactory (e.g. XamlDesignerSplitHost).
            _primaryEditor.Language   = language;
            _secondaryEditor.Language = language;
        }
        else
        {
            // Unknown / plain-text file — clear any previous highlighter so the
            // editor does not keep colouring the new content with stale rules.
            _primaryEditor.ExternalHighlighter   = null;
            _secondaryEditor.ExternalHighlighter = null;
            _primaryEditor.Language   = null;
            _secondaryEditor.Language = null;
        }

        // Delegate to the primary editor's async open (file I/O runs off the UI thread).
        // Both editors share the same CodeDocument, so the secondary view updates automatically.
        await ((IOpenableDocument)_primaryEditor).OpenAsync(filePath, ct);

        // Sync breadcrumb bar file path so DocumentSymbolsAsync uses the correct URI.
        _breadcrumbBar.SetFilePath(filePath);
    }

    #endregion

    #region IDocumentEditor — proxy to _activeEditor

    // Helper: cast to the interface so explicit implementations are accessible.
    private IDocumentEditor Active => _activeEditor;

    public bool     IsDirty    => Active.IsDirty;
    public bool     CanUndo    => Active.CanUndo;
    public bool     CanRedo    => Active.CanRedo;
    public bool     IsReadOnly { get => Active.IsReadOnly; set { Active.IsReadOnly = value; ((IDocumentEditor)_secondaryEditor).IsReadOnly = value; } }
    public string   Title      => Active.Title;
    public bool     IsBusy     => Active.IsBusy;

    public ICommand UndoCommand      => Active.UndoCommand;
    public ICommand RedoCommand      => Active.RedoCommand;
    public ICommand SaveCommand      => Active.SaveCommand;
    public ICommand CopyCommand      => Active.CopyCommand;
    public ICommand CutCommand       => Active.CutCommand;
    public ICommand PasteCommand     => Active.PasteCommand;
    public ICommand DeleteCommand    => Active.DeleteCommand;
    public ICommand SelectAllCommand => Active.SelectAllCommand;

    public void Undo()        => Active.Undo();
    public void Redo()        => Active.Redo();
    public void Save()        => Active.Save();
    public Task SaveAsync(CancellationToken ct = default)                     => Active.SaveAsync(ct);
    public Task SaveAsAsync(string filePath, CancellationToken ct = default)  => Active.SaveAsAsync(filePath, ct);
    public void Copy()        => Active.Copy();
    public void Cut()         => Active.Cut();
    public void Paste()       => Active.Paste();
    public void Delete()      => Active.Delete();
    public void SelectAll()   => Active.SelectAll();
    public void Close()       { ((IDocumentEditor)_primaryEditor).Close(); ((IDocumentEditor)_secondaryEditor).Close(); }
    public void CancelOperation() => Active.CancelOperation();

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

    /// <summary>
    /// Raised when the user navigates to a reference in a different file via the
    /// Find All References popup. Forwarded from whichever editor pane fires it.
    /// </summary>
    public event EventHandler<ReferencesNavigationEventArgs>? ReferenceNavigationRequested;

    /// <summary>
    /// Raised when the user pins the References popup into a docked panel.
    /// Forwarded from whichever editor pane fires it.
    /// </summary>
    public event EventHandler<FindAllReferencesDockEventArgs>? FindAllReferencesDockRequested;

    /// <summary>
    /// Raised when Ctrl+Click targets an external symbol (BCL / NuGet assembly).
    /// Forwarded from whichever editor pane fires it.
    /// </summary>
    public event EventHandler<GoToExternalDefinitionEventArgs>? GoToExternalDefinitionRequested;

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    // IStatusBarContributor — delegates to the active (focused) editor pane
    // ═══════════════════════════════════════════════════════════════════

    /// <inheritdoc />
    public ObservableCollection<EditorStatusBarItem> StatusBarItems => _activeEditor.StatusBarItems;

    /// <inheritdoc />
    public void RefreshStatusBarItems() => _activeEditor.RefreshJsonStatusBarItems();

    // ═══════════════════════════════════════════════════════════════════
    // IDiagnosticSource — forwards to _primaryEditor
    // ═══════════════════════════════════════════════════════════════════

    /// <inheritdoc />
    public string SourceLabel => ((IDiagnosticSource)_primaryEditor).SourceLabel;

    /// <inheritdoc />
    public System.Collections.Generic.IReadOnlyList<DiagnosticEntry> GetDiagnostics()
        => ((IDiagnosticSource)_primaryEditor).GetDiagnostics();

    /// <inheritdoc />
    public event EventHandler? DiagnosticsChanged
    {
        add    => ((IDiagnosticSource)_primaryEditor).DiagnosticsChanged += value;
        remove => ((IDiagnosticSource)_primaryEditor).DiagnosticsChanged -= value;
    }

    // ═══════════════════════════════════════════════════════════════════
    // IBufferAwareEditor — delegates to _primaryEditor (CodeEditor)
    // ═══════════════════════════════════════════════════════════════════

    /// <inheritdoc/>
    public void AttachBuffer(IDocumentBuffer buffer) => _primaryEditor.AttachBuffer(buffer);

    /// <inheritdoc/>
    public void DetachBuffer() => _primaryEditor.DetachBuffer();
}
