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
using WpfHexEditor.Editor.CodeEditor.Providers;
using WpfHexEditor.Editor.CodeEditor.Services;

namespace WpfHexEditor.Editor.CodeEditor.Controls;

/// <summary>
/// A split-view host for <see cref="CodeEditor"/>.
/// Both editors share the same <see cref="CodeDocument"/>; scroll positions
/// and caret positions are independent.
/// </summary>
public sealed class CodeEditorSplitHost : Grid, IDocumentEditor, IBufferAwareEditor, IOpenableDocument, INavigableDocument, IStatusBarContributor, IRefreshTimeReporter, IDiagnosticSource, ILspAwareEditor, IEditorPersistable
{
    #region Child controls

    /// <summary>Optional logger injected by the host app (e.g. OutputLogger.Debug).</summary>
    public Action<string>? BreadcrumbLogger
    {
        get => _breadcrumbBar.Logger;
        set => _breadcrumbBar.Logger = value;
    }

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

        // IDE docking hosts call Focus() on this Grid container rather than the CodeEditor
        // child. With Focusable = true the Grid can receive keyboard focus; OnGotKeyboardFocus
        // then forwards it to _activeEditor so CodeEditor.OnGotFocus fires and the caret
        // blink timer restarts. See override below.
        Focusable        = true;
        FocusVisualStyle = null;

        // -- Forward events from primary editor (document is shared) -----------
        _primaryEditor.ModifiedChanged   += (s, e) => ModifiedChanged?.Invoke(this, e);
        // Secondary also fires ModifiedChanged independently (separate _isDirty / undo engine).
        // Wire it so DocumentModel.IsDirty stays in sync when the secondary is the active editor.
        _secondaryEditor.ModifiedChanged += (s, e) => ModifiedChanged?.Invoke(this, e);
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
        _primaryEditor.CallHierarchyDockRequested         += (s, e) => CallHierarchyDockRequested?.Invoke(this, e);
        _primaryEditor.TypeHierarchyDockRequested         += (s, e) => TypeHierarchyDockRequested?.Invoke(this, e);
        _primaryEditor.FormattingOptionsRequested          += (s, e) => FormattingOptionsRequested?.Invoke(this, e);
        _secondaryEditor.ReferenceNavigationRequested     += (s, e) => ReferenceNavigationRequested?.Invoke(this, e);
        _secondaryEditor.FindAllReferencesDockRequested   += (s, e) => FindAllReferencesDockRequested?.Invoke(this, e);
        _secondaryEditor.GoToExternalDefinitionRequested  += (s, e) => GoToExternalDefinitionRequested?.Invoke(this, e);
        _secondaryEditor.CallHierarchyDockRequested       += (s, e) => CallHierarchyDockRequested?.Invoke(this, e);
        _secondaryEditor.TypeHierarchyDockRequested       += (s, e) => TypeHierarchyDockRequested?.Invoke(this, e);
        _secondaryEditor.FormattingOptionsRequested       += (s, e) => FormattingOptionsRequested?.Invoke(this, e);

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

        // -- Minimap (right-side code overview) --------------------------------
        // Default to Visible — EditorSettingsService.Apply() will override from user settings.
        // Using Visible as default ensures minimap works even if Apply() hasn't been called yet.
        _minimap = new MinimapControl { MinimapWidth = 80, Visibility = Visibility.Visible };
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        SetColumn(_minimap, 1);
        SetRow(_minimap, 0);
        SetRowSpan(_minimap, 5);
        Children.Add(_minimap);

        // Minimap events — context menu toggle and side change
        _minimap.MinimapToggled += visible => ShowMinimap = visible;
        _minimap.SideChangeRequested += side => MinimapSide = side;

        // Coalesced minimap refresh — fires at most every 150ms on scroll/edit/highlight.
        // Uses MinimapRefreshRequested (not LayoutUpdated) to avoid a feedback loop:
        // LayoutUpdated fires after every layout pass including the minimap's own
        // InvalidateVisual, which would cause a continuous 150ms refresh cycle.
        _minimapRefreshTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(150)
        };
        _minimapRefreshTimer.Tick += (_, _) =>
        {
            _minimapRefreshTimer.Stop();
            if (ShowMinimap) _minimap.Refresh();
        };
        _primaryEditor.MinimapRefreshRequested += (_, _) => RequestMinimapRefresh();

        // Wire minimap to editor on first Loaded (ensures Document is initialized)
        Loaded += (_, _) =>
        {
            if (_minimap.Visibility == Visibility.Visible)
            {
                _minimap.SetEditor(_primaryEditor);
                _minimap.Refresh();
            }
        };
    }

    #endregion

    #region Minimap

    private readonly MinimapControl _minimap;
    private readonly System.Windows.Threading.DispatcherTimer _minimapRefreshTimer;

    private void RequestMinimapRefresh()
    {
        if (!ShowMinimap) return;
        if (!_minimapRefreshTimer.IsEnabled)
            _minimapRefreshTimer.Start();
    }

    /// <summary>Shows or hides the code overview minimap.</summary>
    public bool ShowMinimap
    {
        get => _minimap.Visibility == Visibility.Visible;
        set
        {
            _minimap.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            if (value)
            {
                _minimap.SetEditor(_primaryEditor);
                _minimap.Refresh();
            }
        }
    }

    /// <summary>Shows or hides line numbers in both editors.</summary>
    public bool ShowLineNumbers
    {
        get => _primaryEditor.ShowLineNumbers;
        set { _primaryEditor.ShowLineNumbers = value; _secondaryEditor.ShowLineNumbers = value; }
    }

    /// <summary>Render per-token colored blocks (true) or single rect per line (false).</summary>
    public bool MinimapRenderCharacters
    {
        get => _minimap.RenderCharacters;
        set => _minimap.RenderCharacters = value;
    }

    /// <summary>Minimap vertical sizing mode.</summary>
    public MinimapVerticalSize MinimapVerticalSize
    {
        get => _minimap.VerticalSize;
        set => _minimap.VerticalSize = value;
    }

    /// <summary>When the viewport slider is visible.</summary>
    public MinimapSliderMode MinimapSliderMode
    {
        get => _minimap.SliderMode;
        set => _minimap.SliderMode = value;
    }

    /// <summary>Minimap placement side. Rearranges grid columns.</summary>
    public MinimapSide MinimapSide
    {
        get => _minimap.Side;
        set
        {
            _minimap.Side = value;
            ApplyMinimapSide(value);
        }
    }

    private void ApplyMinimapSide(MinimapSide side)
    {
        if (side == MinimapSide.Left)
        {
            ColumnDefinitions[0].Width = GridLength.Auto;
            ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
            SetColumn(_minimap, 0);
            // Move all other children to column 1
            foreach (UIElement child in Children)
            {
                if (!ReferenceEquals(child, _minimap))
                    SetColumn(child, 1);
            }
        }
        else
        {
            ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
            ColumnDefinitions[1].Width = GridLength.Auto;
            SetColumn(_minimap, 1);
            foreach (UIElement child in Children)
            {
                if (!ReferenceEquals(child, _minimap))
                    SetColumn(child, 0);
            }
        }
    }

    /// <summary>Clears minimap theme brush cache — call on theme change.</summary>
    public void InvalidateMinimapThemeCache() => _minimap.InvalidateThemeCache();

    #endregion

    #region Zoom

    /// <summary>
    /// Zoom multiplier forwarded to both editor panes (0.5–4.0, 1.0 = 100%).
    /// CodeEditor uses font-size multiplication internally; snap-to-pixel in GlyphRunRenderer
    /// minimizes sub-pixel blur. LayoutTransform is not used here — it breaks internal
    /// scrollbar range calculations (ActualHeight shrinks with zoom → scroll deadzone).
    /// </summary>
    public double ZoomLevel
    {
        get => _primaryEditor.ZoomLevel;
        set
        {
            value = Math.Clamp(value, 0.5, 4.0);
            _primaryEditor.ZoomLevel   = value;
            _secondaryEditor.ZoomLevel = value;
        }
    }

    /// <summary>Raised when <see cref="ZoomLevel"/> changes (forwarded from primary editor).</summary>
    public event EventHandler<double>? ZoomLevelChanged
    {
        add    => _primaryEditor.ZoomLevelChanged += value;
        remove => _primaryEditor.ZoomLevelChanged -= value;
    }

    #endregion

    #region Focus routing

    /// <summary>
    /// Routes keyboard focus from this Grid to the active editor.
    /// Fires when the IDE docking system calls Focus() on this container.
    /// The OriginalSource guard prevents acting on events bubbling from children
    /// (those are already handled by CodeEditor.OnGotFocus).
    /// </summary>
    protected override void OnGotKeyboardFocus(System.Windows.Input.KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        if (ReferenceEquals(e.OriginalSource, this))
            _activeEditor.Focus();
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
    // Holds the per-instance EditorPluginIntegration for script-global completions.
    // Created lazily the first time a language with ScriptGlobals is set.
    private EditorPluginIntegration? _scriptGlobalsRegistry;

    public void SetLanguage(LanguageDefinition? lang)
    {
        var highlighter = lang is not null ? CodeEditorFactory.BuildHighlighter(lang) : null;
        _primaryEditor.ExternalHighlighter   = highlighter;
        _secondaryEditor.ExternalHighlighter = highlighter;
        _primaryEditor.Language   = lang;
        _secondaryEditor.Language = lang;

        // Auto-wire script globals completion when the language declares scriptGlobals.
        if (lang is not null && lang.ScriptGlobals.Count > 0)
        {
            if (_scriptGlobalsRegistry is null)
            {
                _scriptGlobalsRegistry = new EditorPluginIntegration();
                _scriptGlobalsRegistry.RegisterCompletionProvider(
                    lang.Id, new ScriptGlobalsCompletionProvider());
            }
            _primaryEditor.SetLocalCompletionRegistry(_scriptGlobalsRegistry);
            _secondaryEditor.SetLocalCompletionRegistry(_scriptGlobalsRegistry);
        }
        else
        {
            // Clear any previously set registry when switching away from a script language.
            _primaryEditor.SetLocalCompletionRegistry(null);
            _secondaryEditor.SetLocalCompletionRegistry(null);
        }
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

    /// <summary>The LSP client currently attached to this split host (from the primary editor).</summary>
    public ILspClient? LspClient => _primaryEditor.LspClient;

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

        // Force navigation bar re-parse — the Loaded event won't fire again when
        // the same editor instance is reused for a different file.
        _navBar.ForceRefresh();
    }

    #endregion

    #region IDocumentEditor — proxy to _activeEditor

    // Helper: cast to the interface so explicit implementations are accessible.
    private IDocumentEditor Active => _activeEditor;

    // IsDirty and save operations are always authoritative on the primary editor,
    // which owns the file path. The secondary shares the same CodeDocument but has
    // an independent undo engine — after a save, only the primary marks its save-point.
    public bool     IsDirty    => ((IDocumentEditor)_primaryEditor).IsDirty;
    public bool     CanUndo    => Active.CanUndo;
    public bool     CanRedo    => Active.CanRedo;
    public bool     IsReadOnly { get => Active.IsReadOnly; set { Active.IsReadOnly = value; ((IDocumentEditor)_secondaryEditor).IsReadOnly = value; } }
    public string   Title      => Active.Title;
    public bool     IsBusy     => Active.IsBusy;

    public ICommand UndoCommand      => Active.UndoCommand;
    public ICommand RedoCommand      => Active.RedoCommand;
    // Save always routes to the primary editor — it owns the file path (_currentFilePath).
    // If the secondary editor is active and Save is called via Active, SaveAsync bails out
    // because the secondary never receives OpenAsync and has _currentFilePath = null.
    public ICommand SaveCommand      => ((IDocumentEditor)_primaryEditor).SaveCommand;
    public ICommand CopyCommand      => Active.CopyCommand;
    public ICommand CutCommand       => Active.CutCommand;
    public ICommand PasteCommand     => Active.PasteCommand;
    public ICommand DeleteCommand    => Active.DeleteCommand;
    public ICommand SelectAllCommand => Active.SelectAllCommand;

    public void Undo()        => Active.Undo();
    public void Redo()        => Active.Redo();
    public void Save()        => ((IDocumentEditor)_primaryEditor).Save();
    public Task SaveAsync(CancellationToken ct = default)                     => ((IDocumentEditor)_primaryEditor).SaveAsync(ct);
    public Task SaveAsAsync(string filePath, CancellationToken ct = default)  => ((IDocumentEditor)_primaryEditor).SaveAsAsync(filePath, ct);
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
    /// Raised when the user triggers call hierarchy (Shift+Alt+H) and results are ready.
    /// Forwarded from whichever editor pane fires it.
    /// </summary>
    public event EventHandler<CallHierarchyDockRequestedEventArgs>? CallHierarchyDockRequested;

    /// <summary>
    /// Raised when the user triggers type hierarchy (Ctrl+Alt+F12) and results are ready.
    /// Forwarded from whichever editor pane fires it.
    /// </summary>
    public event EventHandler<TypeHierarchyDockRequestedEventArgs>? TypeHierarchyDockRequested;

    /// <summary>
    /// Raised when Ctrl+Click targets an external symbol (BCL / NuGet assembly).
    /// Forwarded from whichever editor pane fires it.
    /// </summary>
    public event EventHandler<GoToExternalDefinitionEventArgs>? GoToExternalDefinitionRequested;

    /// <summary>
    /// Raised when the user clicks "Options…" in the Formatting context menu.
    /// Forwarded from whichever editor pane fires it so the host can open the options page.
    /// </summary>
    public event EventHandler? FormattingOptionsRequested;

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    // IStatusBarContributor — delegates to the active (focused) editor pane
    // ═══════════════════════════════════════════════════════════════════

    /// <inheritdoc />
    public ObservableCollection<EditorStatusBarItem> StatusBarItems => _activeEditor.StatusBarItems;

    /// <inheritdoc />
    public void RefreshStatusBarItems() => _activeEditor.RefreshJsonStatusBarItems();

    // ═══════════════════════════════════════════════════════════════════
    // IRefreshTimeReporter — forwards to the active (focused) editor pane
    // ═══════════════════════════════════════════════════════════════════

    /// <inheritdoc />
    public EditorStatusBarItem? RefreshTimeStatusBarItem => _activeEditor.RefreshTimeStatusBarItem;

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
    public void AttachBuffer(IDocumentBuffer buffer)
    {
        _primaryEditor.AttachBuffer(buffer);
        _breadcrumbBar.AttachBuffer(buffer);
    }

    /// <inheritdoc/>
    public void DetachBuffer() => _primaryEditor.DetachBuffer();

    // ═══════════════════════════════════════════════════════════════════
    // IEditorPersistable — delegates to _primaryEditor + augments split state
    // ═══════════════════════════════════════════════════════════════════

    EditorConfigDto IEditorPersistable.GetEditorConfig()
    {
        var cfg = ((IEditorPersistable)_primaryEditor).GetEditorConfig();
        cfg.Extra ??= new System.Collections.Generic.Dictionary<string, string>();
        cfg.Extra["splitHost.isSplit"] = IsSplit ? "1" : "0";
        return cfg;
    }

    void IEditorPersistable.ApplyEditorConfig(EditorConfigDto config)
    {
        ((IEditorPersistable)_primaryEditor).ApplyEditorConfig(config);
        if (config.Extra?.TryGetValue("splitHost.isSplit", out var s) == true && s == "1")
            ToggleSplit();
    }

    byte[]? IEditorPersistable.GetUnsavedModifications()
        => ((IEditorPersistable)_primaryEditor).GetUnsavedModifications();

    void IEditorPersistable.ApplyUnsavedModifications(byte[] data)
        => ((IEditorPersistable)_primaryEditor).ApplyUnsavedModifications(data);

    ChangesetSnapshot IEditorPersistable.GetChangesetSnapshot()
        => ((IEditorPersistable)_primaryEditor).GetChangesetSnapshot();

    void IEditorPersistable.ApplyChangeset(ChangesetDto changeset)
        => ((IEditorPersistable)_primaryEditor).ApplyChangeset(changeset);

    void IEditorPersistable.MarkChangesetSaved()
        => ((IEditorPersistable)_primaryEditor).MarkChangesetSaved();

    IReadOnlyList<BookmarkDto>? IEditorPersistable.GetBookmarks()
        => ((IEditorPersistable)_primaryEditor).GetBookmarks();

    void IEditorPersistable.ApplyBookmarks(IReadOnlyList<BookmarkDto> bookmarks)
        => ((IEditorPersistable)_primaryEditor).ApplyBookmarks(bookmarks);
}
