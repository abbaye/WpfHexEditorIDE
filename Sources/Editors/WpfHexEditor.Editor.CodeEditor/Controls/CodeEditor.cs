//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Custom CodeEditor - Main Editor Control (Phase 1 - Foundation)
// Author : Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com), Claude Sonnet 4.6
// Inspired by HexViewport.cs custom rendering pattern
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Editor.CodeEditor.Folding;
using WpfHexEditor.Editor.CodeEditor.Models;
using WpfHexEditor.Editor.CodeEditor.Helpers;
using WpfHexEditor.Editor.CodeEditor.Rendering;
using WpfHexEditor.Editor.CodeEditor.Services;
using WpfHexEditor.Editor.CodeEditor.Snippets;
using WpfHexEditor.Editor.CodeEditor.NavigationBar;
using WpfHexEditor.Core;
using WpfHexEditor.Core.Settings;
using WpfHexEditor.Editor.CodeEditor.Properties;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.Core.Helpers;
using WpfHexEditor.Editor.Core.Documents;
using WpfHexEditor.Editor.Core.LSP;
using WpfHexEditor.Editor.CodeEditor.Options;
using WpfHexEditor.Core.ProjectSystem.Languages;
using WpfHexEditor.Editor.CodeEditor.Selection;
using WpfHexEditor.Editor.CodeEditor.Input;
using WpfHexEditor.Editor.CodeEditor.MultiCaret;

namespace WpfHexEditor.Editor.CodeEditor.Controls
{
    /// <summary>
    /// High-performance JSON text editor using custom rendering (FrameworkElement).
    /// Phase 1: Basic text display + keyboard input + line numbers
    /// Phase 2: Syntax highlighting with CodeSyntaxHighlighter
    /// Future phases will add: SmartComplete, validation
    /// </summary>
    public partial class CodeEditor : FrameworkElement, IDocumentEditor, IBufferAwareEditor, IUndoAwareEditor, ILspAwareEditor, IDiagnosticSource, IPropertyProviderSource, IOpenableDocument, INavigableDocument, IStatusBarContributor, IRefreshTimeReporter, ISearchTarget, IEditorPersistable
    {
        #region Fields - Document Model

        private CodeDocument _document;

        // -- IBufferAwareEditor -------------------------------------------
        private IDocumentBuffer? _buffer;
        private bool             _suppressBufferSync;
        private int _cursorLine = 0;        // Current cursor line (0-based)
        private int _cursorColumn = 0;      // Current cursor column (0-based)
        private TextSelection _selection;   // Current text selection
        private int _lastNotifiedCursorLine = -1; // Tracks last CaretMoved notification line

        // Multi-caret manager — index 0 is always the primary caret.
        private readonly CaretManager _caretManager = new();

        #endregion

        #region Fields - Syntax Highlighting (Phase 2)

        private CodeSyntaxHighlighter _highlighter;

        // Link hit-zones (URLs + emails): rebuilt on every render pass; used for cursor + Ctrl+Click.
        private readonly List<LinkHitZone> _linkHitZones = new();

        // Fold-label hit-zones: rebuilt on every render pass; used for click-to-toggle.
        private readonly List<(Rect rect, int line)> _foldLabelHitZones = new();

        // Fold peek — VS-style hover preview of collapsed region content.
        private FoldPeekPopup?   _foldPeekPopup;
        private System.Windows.Threading.DispatcherTimer? _foldPeekTimer;
        private int              _foldPeekTargetLine = -1;

        // End-of-block hint popup — shown on hover over }, #endregion, </Tag> etc.
        private EndBlockHintPopup? _endBlockHintPopup;
        private System.Windows.Threading.DispatcherTimer? _endBlockHintTimer;
        private int                _endBlockHintHoveredLine = -1;
        private FoldingRegion?     _endBlockHintActiveRegion;

        // The link zone (URL or email) currently under the mouse pointer (null = none).
        // Drives hover underline; changing it triggers InvalidateVisual().
        private LinkHitZone? _hoveredLinkZone;

        // Explicit tooltip object opened/closed in OnMouseMove.
        // Using ToolTip directly (instead of the ToolTip property) ensures the tooltip
        // appears even when the mouse is already inside the CodeEditor control.
        private ToolTip? _urlTooltip;
        private ToolTip? _hintTooltip;

        // Compiled URL regex — re-used across all render passes (thread-safe read-only after init).
        private static readonly Regex s_urlRegex = new(
            @"https?://[^\s""'<>\[\]{}|\\^`]+",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Compiled email regex — same lifetime guarantee as s_urlRegex.
        private static readonly Regex s_emailRegex = new(
            @"[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}",
            RegexOptions.Compiled);

        /// <summary>
        /// Optional external syntax highlighter (e.g. RegexBasedSyntaxHighlighter for .whlang languages).
        /// When set, overrides the built-in JSON highlighter for all text rendering.
        /// </summary>
        public static readonly DependencyProperty ExternalHighlighterProperty =
            DependencyProperty.Register(nameof(ExternalHighlighter), typeof(ISyntaxHighlighter),
                typeof(CodeEditor), new FrameworkPropertyMetadata(null,
                    FrameworkPropertyMetadataOptions.AffectsRender,
                    OnExternalHighlighterChanged));

        public ISyntaxHighlighter? ExternalHighlighter
        {
            get => (ISyntaxHighlighter?)GetValue(ExternalHighlighterProperty);
            set => SetValue(ExternalHighlighterProperty, value);
        }

        /// <summary>
        /// The active syntax highlighter: <see cref="ExternalHighlighter"/> if set,
        /// otherwise the built-in <see cref="CodeSyntaxHighlighter"/>.
        /// Used by the minimap for on-demand token fallback when TokensCache is not ready.
        /// </summary>
        public ISyntaxHighlighter? ActiveHighlighter
            => (ISyntaxHighlighter?)ExternalHighlighter ?? _highlighter;

        private static void OnExternalHighlighterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not CodeEditor editor) return;

            // Reset block-comment state carried over from the previous file.
            if (e.OldValue is ISyntaxHighlighter old) old.Reset();
            if (e.NewValue is ISyntaxHighlighter h)   h.Reset();

            // Invalidate all token caches so the pipeline re-highlights with the new highlighter.
            // Required for bracket colorization: new highlighter may produce different token Kinds
            // (e.g. Kind.Bracket from the synthetic bracket rule added in BuildHighlighter).
            // Without this, IsCacheDirty stays false and ColorizeLine never sees Kind.Bracket tokens.
            editor._document?.InvalidateAllCache();

            // Force a full repaint so the new (or cleared) highlighter is applied
            // to all currently visible lines immediately, without waiting for a scroll.
            editor.InvalidateVisual();
            editor.RefreshJsonStatusBarItems();
        }

        #endregion

        #region Fields - Undo/Redo

        private readonly WpfHexEditor.Editor.Core.Undo.UndoEngine _undoEngine = new();
        private bool _isInternalEdit = false; // Prevent undo recording during undo/redo
        private bool _isDirty = false;        // IDocumentEditor: unsaved changes flag
        private bool _isDocumentLoaded = false; // True once content has been fully loaded into the buffer
        private string? _currentFilePath;     // IDocumentEditor: last saved file path

        /// <inheritdoc/>
        public Action<string>? BeforeSaveCallback { get; set; }

        // Context-menu items that need dynamic headers (Undo (3) / Redo (0)).
        private System.Windows.Controls.MenuItem? _undoMenuItem;
        private System.Windows.Controls.MenuItem? _redoMenuItem;

        #endregion

        #region Fields - Mouse Selection (Phase 3)

        private bool _isSelecting = false;
        private bool _isOverwriteMode = false;
        private TextPosition _mouseDownPosition;

        // Coalesces InvalidateVisual() calls during mouse-drag selection.
        // Prevents dispatcher queue flooding at high mouse-event rates (200–1000 Hz).
        private bool _selectionRenderPending;

        // Auto-scroll during drag: fires at 50 ms intervals when the mouse is outside
        // the visible viewport while a selection drag is in progress.
        private System.Windows.Threading.DispatcherTimer _autoScrollTimer;
        private Point _lastMousePosition;

        // Feature A — Rectangular (block/column) selection (Alt+LeftClick+drag)
        private readonly RectangularSelection _rectSelection = new();
        private bool _isRectSelecting;

        // Feature B — Text drag-and-drop (move selection by dragging)
        private readonly DragDropState _dragDrop = new();
        private bool _isRectDrag; // true when the active drag originates from a rect selection block

        // Middle-click auto-scroll (pan mode)
        private PanModeController _panMode = null!;

        #endregion

        #region Fields - SmartComplete (Phase 4)

        private SmartCompletePopup _smartCompletePopup;
        private bool _enableSmartComplete = true;

        #endregion

        #region Fields - LSP Client (Phase 4 — LSP Integration)

        // Optional LSP client injected by the IDE host (via SetLspClient).
        // Null when no language server is configured for the current language.
        private WpfHexEditor.Editor.Core.LSP.ILspClient? _lspClient;

        // Signature help popup — shown on '(' keystroke when an LSP client is active.
        private SignatureHelpPopup?     _signatureHelpPopup;

        // Code action popup — shown on Ctrl+. when an LSP client is active.
        private LspCodeActionPopup?     _lspCodeActionPopup;

        // Rename popup — shown on F2 when an LSP client is active.
        private LspRenamePopup?         _lspRenamePopup;

        // Document manager injected by LspDocumentBridgeService for workspace-wide edits.
        private IDocumentManager?       _lspDocumentManager;

        // Monotonically increasing document version sent with every didChange notification.
        private int _lspDocVersion;

        // Tracks nested call depth for SignatureHelp — incremented on '(', decremented on ')'.
        // When it reaches 0 the SignatureHelp popup is dismissed.
        private int _signatureParamDepth;

        // Lightbulb gutter: -1 = no bulb; ≥0 = line index where code actions are available.
        private int _lightbulbLine = -1;
        // Debounce timer for CheckCodeActionsAtCursorAsync (600 ms after cursor movement).
        private System.Windows.Threading.DispatcherTimer? _lightbulbTimer;

        // Go-to-Symbol palette (Ctrl+T)
        private GoToSymbolPopup? _goToSymbolPopup;

        // Debounce timer for textDocument/didChange — 300 ms after last keystroke.
        private System.Windows.Threading.DispatcherTimer? _lspChangeTimer;

        // Pending incremental change for the next timer tick.
        // Null = multiple changes collapsed → use full-text DidChange fallback.
        private (int SL, int SC, int EL, int EC, int OldLen, string NewText)? _pendingLspChange;

        // Inline "Find All References" popup (lazily created on first use).
        private ReferencesPopup?           _referencesPopup;

        // Last reference results — used when the user pins the popup to a docked panel.
        private List<ReferenceGroup> _lastReferenceGroups = new();
        private string               _lastReferenceSymbol = string.Empty;

        // ── Bracket Pair Colorization (#162) ────────────────────────────────────
        private readonly Services.BracketDepthColorizer _bracketColorizer      = new();
        private          int                            _bracketDepthFirstLine = -1;

        // ── InlineHints ──────────────────────────────────────────────────────────
        private          int                            _inlineHintsSource   = 0; // 0=Auto, 1=RoslynOnly, 2=RegexAlways
        private readonly Services.InlineHintsService                                                                                              _inlineHintsService  = new();
        private readonly Layers.LspInlayHintsLayer                                                                                                 _lspInlayHintsLayer  = new();
        private readonly Layers.LspDeclarationHintsLayer                                                                                                   _lspDeclarationHintsLayer    = new();
        private readonly Layers.LspSemanticTokensLayer                                                                                                     _semanticTokensLayer         = new();
        private          IReadOnlyDictionary<int, (int Count, string Symbol, string IconGlyph, System.Windows.Media.Brush IconBrush, WpfHexEditor.Editor.Core.InlineHintsSymbolKinds Kind, bool IsRoslyn)> _hintsData = new Dictionary<int, (int, string, string, System.Windows.Media.Brush, WpfHexEditor.Editor.Core.InlineHintsSymbolKinds, bool)>();
        private          int                                                                                                                   _visibleHintsCount = 0;
        /// <summary>Cumulative hint count before each line: _hintsCumulative[i] = number of visible hints on lines 0..i-1.</summary>
        private          int[]                                                                                                                  _hintsCumulative = System.Array.Empty<int>();
        private readonly List<(Rect Zone, int LineIndex, string Symbol)>                                                                       _hintsHitZones     = new();
        private readonly List<(int LineIndex, double Y)>                                                                                       _visLinePositions = new();
        private readonly List<int>                                                                                                              _visLineSubRows   = new(); // parallel to _visLinePositions — sub-row index within the logical line (word wrap)
        private readonly Dictionary<int, double>                                                                                               _lineYLookup      = new();
        private          int                                                                                                                   _hoveredHintsLine  = -1;

        // ── Quick Info Hover ──────────────────────────────────────────────────
        private Services.HoverQuickInfoService?      _hoverQuickInfoService;
        private QuickInfoPopup?                      _quickInfoPopup;
        private Point                                _lastHoverPixel;
        private TextPosition                         _lastHoverTextPos = new(-1, -1);

        // ── Ctrl+Click Navigation ─────────────────────────────────────────────
        private Services.CtrlClickNavigationService? _ctrlClickService;
        private bool                                 _ctrlDown;
        private readonly List<SymbolHitZone>         _symbolHitZones    = new();
        private SymbolHitZone?                       _hoveredSymbolZone;

        /// <summary>
        /// Routed command for "Find All References" — default gesture Shift+F12,
        /// matching the Visual Studio keyboard binding.
        /// </summary>
        public static readonly RoutedUICommand FindAllReferencesCommand = new(
            "Find All References",
            "FindAllReferences",
            typeof(CodeEditor),
            new InputGestureCollection { new KeyGesture(Key.F12, ModifierKeys.Shift) });

        /// <summary>
        /// Routed command for "Select Next Occurrence" — Ctrl+D (VS Code behaviour).
        /// Adds the next occurrence of the word/selection as a secondary caret selection.
        /// </summary>
        public static readonly RoutedUICommand SelectNextOccurrenceCommand = new(
            "Select Next Occurrence",
            "SelectNextOccurrence",
            typeof(CodeEditor),
            new InputGestureCollection { new KeyGesture(Key.D, ModifierKeys.Control) });

        /// <summary>
        /// Routed command for "Refresh Highlights" — Ctrl+Shift+R.
        /// Forces an immediate clear and re-request of all highlight layers.
        /// </summary>
        public static readonly RoutedUICommand RefreshHighlightsCommand = new(
            "Refresh Highlights",
            "RefreshHighlights",
            typeof(CodeEditor),
            new InputGestureCollection { new KeyGesture(Key.R, ModifierKeys.Control | ModifierKeys.Shift) });

        #endregion

        #region Fields - Validation (Phase 5)

        private List<Models.ValidationError> _validationErrors = new List<Models.ValidationError>();
        // O(1) lookup for RenderValidationGlyph — rebuilt whenever _validationErrors changes (OPT-PERF-01).
        private Dictionary<int, List<Models.ValidationError>> _validationByLine = new();
        private FormatSchemaValidator _validator;
        private System.Windows.Threading.DispatcherTimer _validationTimer;

        // Cached frozen pens for squiggly-line rendering — avoids per-error Pen allocations (OPT-PERF-02).
        private static readonly Pen s_squigglyError   = MakeSquigglyPen(Colors.Red);
        private static readonly Pen s_squigglyWarning = MakeSquigglyPen(Color.FromRgb(255, 165, 0));
        private static readonly Pen s_squigglyInfo    = MakeSquigglyPen(Colors.Blue);

        private static readonly Pen s_lineNumberSeparatorPen = MakeFrozenPen(Color.FromRgb(200, 200, 200), 1.0);
        private static readonly Pen s_glyphInnerPen          = MakeFrozenPen(Colors.White, 1.5);

        // Fold-collapse inline label rendering assets.
        // Static brushes are used as fallback when theme tokens are absent.
        private static readonly Brush    s_foldLabelPenBrush  = MakeFrozenBrush(Color.FromRgb(86, 156, 214));   // VS blue border
        private static readonly Brush    s_foldLabelTextBrush = MakeFrozenBrush(Color.FromRgb(200, 200, 200));
        private static readonly Brush    s_foldLabelBgBrush   = MakeFrozenBrush(Color.FromArgb(30, 86, 156, 214)); // translucent blue
        private static readonly Typeface s_foldLabelTypeface  = new("Segoe UI");

        // Scope guide line pen — semi-transparent so it doesn't obscure text.
        private static readonly Pen s_scopeGuidePen = MakeFrozenPen(Color.FromArgb(60, 128, 128, 128), 1.0);

        // Active (cursor-containing) scope guide — thicker + more opaque than the passive one.
        private static readonly Pen s_scopeGuideActivePen = MakeFrozenPen(Color.FromArgb(140, 180, 180, 180), 1.5);

        // Word-under-caret highlight assets — resolved from theme resources (CE_WordHighlightBackground / CE_WordHighlightBorder).
        // Static fallbacks used when theme tokens are missing.
        private static readonly Brush s_wordHighlightBgFallback  = MakeFrozenBrush(Color.FromArgb(26, 86, 156, 214));
        private static readonly Pen   s_wordHighlightPenFallback = MakeFrozenPen(Color.FromArgb(180, 86, 156, 214), 1.0);
        private Brush _wordHighlightBg;
        private Pen   _wordHighlightPen;

        // Bracket matching highlight assets — fixed colors, safe as static frozen fields (OPT-PERF-03).
        private static readonly Brush s_bracketHighlightBrush = MakeFrozenBrush(Color.FromArgb(80, 0, 120, 215));
        private static readonly Pen   s_bracketBorderPen      = MakeFrozenPen(Color.FromRgb(0, 120, 215), 1.5);

        private static Pen MakeSquigglyPen(Color color) => MakeFrozenPen(color, 1.5);

        private static Pen MakeFrozenPen(Color color, double thickness)
        {
            var pen = new Pen(new SolidColorBrush(color), thickness);
            pen.Brush.Freeze();
            pen.Freeze();
            return pen;
        }

        private static Brush MakeFrozenBrush(Color color)
        {
            var b = new SolidColorBrush(color);
            b.Freeze();
            return b;
        }

        /// <summary>Creates a theme-aware Segoe MDL2 Assets icon TextBlock for context menu items.</summary>
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
            tb.SetResourceReference(System.Windows.Documents.TextElement.ForegroundProperty, "DockMenuForegroundBrush");
            return tb;
        }

        #endregion

        #region Fields - Virtual Scrolling (Phase 11)

        private VirtualizationEngine _virtualizationEngine;

        /// <summary>Exposes virtualization engine for minimap viewport tracking.</summary>
        internal VirtualizationEngine VirtualizationEngine => _virtualizationEngine;

        private double _verticalScrollOffset = 0;
        private double _horizontalScrollOffset = 0;
        private double _maxContentWidth = 0;

        #endregion

        #region Fields - Word Wrap (ADR-049)

        private int   _charsPerVisualLine;
        private int[] _wrapHeights  = Array.Empty<int>(); // visual rows per logical line
        private int[] _wrapOffsets  = Array.Empty<int>(); // first visual row of logical line i (prefix sum)
        private int   _totalVisualRows;
        private double _lastWrapArrangedWidth = -1;

        #endregion

        #region Fields - ScrollBar Children

        private System.Windows.Controls.Primitives.ScrollBar _vScrollBar;
        private System.Windows.Controls.Primitives.ScrollBar _hScrollBar;
        private VisualCollection _scrollBarChildren;
        private bool _updatingScrollBar = false;

        #endregion

        #region Fields - Smooth Scrolling

        private System.Windows.Threading.DispatcherTimer _smoothScrollTimer;
        private double _targetScrollOffset = 0;
        private double _currentScrollOffset = 0;
        private const double SmoothScrollSpeed = 0.35; // Interpolation factor (0-1) — higher = snappier

        #endregion

        #region Fields - Find/Replace

        private List<TextPosition> _findResults = new List<TextPosition>();
        private int _currentFindMatchIndex = -1;
        private int _findMatchLength = 0;
        private string? _lastFindQuery;

        #endregion

        #region Fields - Word Highlight

        private readonly List<TextPosition> _wordHighlights           = new();
        // OPT-PERF: pre-computed distinct line list reused by scroll marker panel — avoids
        // Select().Distinct().ToList() allocation on every cursor move.
        private readonly List<int>          _wordHighlightLines       = new();
        private readonly HashSet<int>       _wordHighlightLineSet     = new();
        private string                      _wordHighlightWord        = string.Empty;
        private int                         _wordHighlightLen         = 0;
        private int                         _wordHighlightTrackedLine = -1; // last cursor line seen in OnRender
        private int                         _wordHighlightTrackedCol  = -1; // last cursor column seen in OnRender
        private System.Windows.Threading.DispatcherTimer? _wordHighlightTimer;
        private CodeScrollMarkerPanel?      _codeScrollMarkerPanel;

        #endregion

        #region Fields - Rendering State

        private Typeface _typeface;
        private Typeface _boldTypeface;
        private Typeface _lineNumberTypeface;
        private double _fontSize     = 12.0; // Effective size = _baseFontSize * ZoomLevel
        private double _baseFontSize = 12.0; // EditorFontSize DP value (zoom-independent)
        private double _charWidth;          // Cached character width
        private double _charHeight;         // Cached character height
        private double _lineHeight;         // Line height with padding

        // GlyphRun renderer — recreated whenever font or DPI changes.
        private GlyphRunRenderer? _glyphRenderer;

        // Per-frame pen/brush caches — rebuilt only when the corresponding value changes (OPT-PERF-03).
        private Pen?    _cachedCaretPen;
        private Pen?    _cachedCaretSecondaryPen;
        private Color   _cachedCaretColor;
        private Pen?    _cachedCurrentLineBorderPen;
        private Color   _cachedCurrentLineBorderColor;
        private Pen?    _cachedUrlPen;
        private Brush?  _cachedUrlBrush;
        private Pen?    _cachedFoldLabelPen;
        private Pen?    _cachedFoldLabelHoverPen;
        private Brush?  _cachedFoldLabelBorderBrush;
        private Brush?  _cachedFoldLabelHoverBrush;

        // DPI value for the current render pass — set once at OnRender entry (OPT-PERF-03).
        private double _renderPixelsPerDip = 1.0;

        // Visible folding regions cache — populated in CalculateVisibleLines to avoid
        // O(total-regions) iteration in RenderScopeGuides every frame (OPT-PERF-03).
        private readonly List<FoldingRegion> _visibleRegions = new();

        // Bracket match cache — avoids O(distance) search on every render frame (OPT-PERF-03).
        private int          _cachedBracketCursorLine = -1;
        private int          _cachedBracketCursorCol  = -1;
        private int          _cachedBracketColumn     = -1;
        private TextPosition? _cachedBracketMatchResult;

        // Multi-line selection geometry cache — avoids O(n²) Geometry.Combine each frame (OPT-PERF-04).
        private Geometry?    _cachedSelectionGeometry;
        private TextPosition _cachedSelGeomStart;
        private TextPosition _cachedSelGeomEnd;
        private int          _cachedSelGeomFirstLine;
        private int          _cachedSelGeomLastLine;

        // LSP diagnostics render coalescing — avoids redundant InvalidateVisual() on rapid batches (OPT-PERF-05).
        private bool _diagnosticsRenderPending;

        // Folding support (Phase B3).
        private FoldingEngine?  _foldingEngine;
        private GutterControl?  _gutterControl;

        // Breakpoint gutter (ADR-DBG-01).
        private BreakpointGutterControl? _breakpointGutterControl;
        private BlameGutterControl?      _blameGutterControl;

        // Change-marker gutter (#166): 4px strip left of the breakpoint gutter.
        private ChangeMarkerGutterControl?                              _changeMarkerGutterControl;
        private readonly Services.GutterChangeTracker                  _changeTracker = new();
        private IReadOnlyDictionary<int, Models.LineChangeKind>        _changeMap
            = new Dictionary<int, Models.LineChangeKind>();

        // Inline peek definition host (#158 — VS2026 style)
        private InlinePeekHost? _inlinePeekHost;
        private int             _peekHostLine   = -1;
        private double          _peekHostHeight = 0.0;

        // 1-based execution line (null when no debug session is paused).
        private int? _executionLineOneBased;

        // Reusable geometry segment list — avoids per-frame allocation during execution/breakpoint rendering.
        private readonly List<Geometry> _renderSegments = new();
        // Reusable set for tracking highlighted breakpoint lines within a single render pass.
        private readonly HashSet<int> _renderHighlightedLines = new();

        // Dedicated DrawingVisual for the caret so blink ticks never trigger a full OnRender.
        // Added last to _scrollBarChildren so it composites on top of all content.
        private readonly DrawingVisual _caretVisual = new();

        // Dirty line range captured from _document.DirtyLines when TextLines flag is set.
        // (from, to) are 0-based logical line indices. Expanded by 1 line each side at render time.
        private (int From, int To) _dirtyLineRange = (0, int.MaxValue);

        // ── Dirty-region rendering (Phase 4) ────────────────────────────────────
        // Tracks which parts of the viewport need repainting; avoids full-frame
        // redraws for caret blinks, selection changes, and other partial updates.
        [Flags]
        private enum RenderDirtyFlags
        {
            None      = 0,
            Caret     = 1 << 0,   // only caret rect changed (blink tick)
            Selection = 1 << 1,   // selection overlay changed
            TextLines = 1 << 2,   // one or more line contents changed
            Overlays  = 1 << 3,   // breakpoints, execution marker, find highlights
            FullFrame = 1 << 7    // scroll, fold, theme — full repaint required
        }

        private RenderDirtyFlags _dirtyFlags = RenderDirtyFlags.FullFrame;

        /// <summary>
        /// Marks the given dirty region and schedules a WPF visual update.
        /// Prefer over <c>InvalidateVisual()</c> — accumulates flags so multiple
        /// callers in one frame are coalesced into a single render pass.
        /// </summary>
        private void InvalidateRegion(RenderDirtyFlags flags)
        {
            _dirtyFlags |= flags;
            InvalidateVisual();
        }

        // Sticky scroll header (#160).
        private StickyScrollHeader? _stickyScrollHeader;
        private bool   _stickyScrollEnabled         = true;
        private int    _stickyScrollMaxLines        = 4;
        private bool   _stickyScrollSyntaxHighlight = true;
        private bool   _stickyScrollClickToNavigate = true;
        private double _stickyScrollOpacity         = 0.95;
        private int    _stickyScrollMinScopeLines   = 5;
        // Perf: track last known sticky state to gate InvalidateArrange() (ADR-IH-PERF-02).
        private int    _stickyScrollLastEntryCount  = -1;
        private int    _lastStickyFirstLine         = -1;

        // Breakpoint placement validation + info popup (ADR-DBG-BP-01).
        private IBreakpointSource?      _bpSource;
        private BreakpointInfoPopup?    _bpInfoPopup;
        private IReadOnlyList<Regex>    _bpNonExecutableRegexes = Array.Empty<Regex>();
        private IReadOnlyList<Regex>    _bpContinuationRegexes  = Array.Empty<Regex>();
        private int                     _bpMaxScanLines         = 20;
        private bool                    _bpBlockScopeHighlight  = true;

        // True between the first Ctrl+M press and the second chord key (outlining commands).
        private bool _outlineChordPending;

        // True between the first Ctrl+K press and the second chord key (formatting commands).
        private bool _formatChordPending;

        // When true, document is formatted automatically on every save.
        private bool _formatOnSave;

        /// <summary>When true, the document is formatted on every Ctrl+S save.</summary>
        public bool FormatOnSave
        {
            get => _formatOnSave;
            set => _formatOnSave = value;
        }

        // XML / XAML attribute formatting overrides.
        private int  _xmlAttributeIndentLevels = 2;
        private bool _xmlOneAttributePerLine;

        /// <summary>
        /// Number of extra indent levels applied to attribute continuation lines in XML/XAML.
        /// Default 2 matches VS XAML formatting (double-indent).
        /// </summary>
        public int XmlAttributeIndentLevels
        {
            get => _xmlAttributeIndentLevels;
            set
            {
                _xmlAttributeIndentLevels = value;
                if (_codeEditorOptions is not null)
                    _codeEditorOptions.XmlAttributeIndentLevels = value;
            }
        }

        /// <summary>
        /// When true, each XML/XAML attribute is placed on its own line during formatting.
        /// </summary>
        public bool XmlOneAttributePerLine
        {
            get => _xmlOneAttributePerLine;
            set
            {
                _xmlOneAttributePerLine = value;
                if (_codeEditorOptions is not null)
                    _codeEditorOptions.XmlOneAttributePerLine = value;
            }
        }

        // Stored reference to CodeEditorOptions for formatting overrides.
        private Options.CodeEditorOptions? _codeEditorOptions;

        // Formatting service — LSP-first, fallback StructuralFormatter.
        private readonly Services.CodeFormattingService _codeFormattingService = new();

        // Color swatch preview (#168) — renders + tracks hit areas.
        private readonly Services.ColorSwatchRenderer _colorSwatchRenderer = new();

        // Whitespace markers — rendered as pale dots/arrows in selection or always.
        private Rendering.WhitespaceRenderer? _whitespaceRenderer;
        private Options.WhitespaceDisplayMode _whitespaceMode = Options.WhitespaceDisplayMode.Selection;

        // 500ms folding debounce timer (P1-CE-01) — prevents O(n) scan on every keystroke
        private System.Windows.Threading.DispatcherTimer? _foldingDebounceTimer;

        // Incremental max-width tracking (P1-CE-02) — O(1) on growth, O(n) only on shrink
        private int _cachedMaxLineLength;
        // Number of lines whose length equals _cachedMaxLineLength.
        // When it drops to 0 a rescan is needed; avoids LINQ allocation on every shrink.
        private int _maxLengthCount;

        // Per-line-number FormattedText cache (P1-CE-03) — eliminates 2,400 allocs/s at 60Hz
        private readonly Dictionary<int, FormattedText> _lineNumberCache = new();
        private Typeface? _cachedLineNumberTypeface;
        private double _cachedLineNumberFontSize = -1;

        // Background highlight pipeline (P1-CE-06)
        private readonly Services.HighlightPipelineService _highlightPipeline = new();
        // Last visible range that was submitted to the pipeline — avoids re-scheduling when unchanged.
        private int _lastHighlightFirst = -1;
        private int _lastHighlightLast  = -1;

        private int _firstVisibleLine = 0;  // Physical line index of first rendered line
        private int _lastVisibleLine = 0;   // Physical line index of last rendered line
        private int _firstVisibleRank = 0;  // Visible rank of _firstVisibleLine (for VE pixel math)

        // OPT-D: lineYLookup dirty flag — avoids rebuilding per-line Y positions on every
        // render frame (e.g. caret blink at 530 ms).  Rebuilt only when the visible range,
        // InlineHints data, or folding regions actually change.
        private bool _linePositionsDirty = true;

        #endregion

        #region Fields - Caret Blinking

        private System.Windows.Threading.DispatcherTimer _caretTimer;
        private bool _caretVisible = true;

        #endregion

        #region Fields - Layout Constants

        private const double TopMargin = 2;
        private const double LeftMargin = 5;
        private const double LineNumberWidth = 60;
        private const double LineNumberMargin = 5;
        private const double TextAreaLeftOffset = 70; // LineNumberWidth + margin
        private const double ScrollBarThickness = 17.0;
        private const double SelectionCornerRadius = 3.0;

        // Extra vertical space reserved at the top of each line slot for InlineHints hints.
        private const double HintLineHeight = 16.0;

        #endregion

        #region Fields - Colors (Brushes)

        #endregion

        #region Dependency Properties with [Category] Attributes

        // Properties are organized by category for auto-generated settings panel
        // Uses same pattern as HexEditor with DynamicSettingsGenerator

        public static readonly DependencyProperty ShowLineNumbersProperty =
            DependencyProperty.Register(nameof(ShowLineNumbers), typeof(bool), typeof(CodeEditor),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>
        /// Show or hide line numbers in the left gutter
        /// </summary>
        [Category("Appearance")]
        [DisplayName("Show Line Numbers")]
        [Description("Display line numbers in the left gutter")]
        public bool ShowLineNumbers
        {
            get => (bool)GetValue(ShowLineNumbersProperty);
            set => SetValue(ShowLineNumbersProperty, value);
        }

        public static readonly DependencyProperty IsWordWrapEnabledProperty =
            DependencyProperty.Register(nameof(IsWordWrapEnabled), typeof(bool), typeof(CodeEditor),
                new FrameworkPropertyMetadata(false, OnIsWordWrapEnabledChanged));

        private static void OnIsWordWrapEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not CodeEditor ed) return;
            ed._horizontalScrollOffset = 0;
            ed.RebuildWrapMap();
            ed.InvalidateMeasure();
            ed.InvalidateVisual();
        }

        /// <summary>
        /// When true, lines wrap visually at the viewport edge instead of scrolling horizontally.
        /// Disables the horizontal scrollbar. (ADR-049)
        /// </summary>
        [Category("Features")]
        [DisplayName("Word Wrap")]
        [Description("Wrap long lines at the viewport edge instead of scrolling horizontally")]
        public bool IsWordWrapEnabled
        {
            get => (bool)GetValue(IsWordWrapEnabledProperty);
            set => SetValue(IsWordWrapEnabledProperty, value);
        }

        public static readonly DependencyProperty IsFoldingEnabledProperty =
            DependencyProperty.Register(nameof(IsFoldingEnabled), typeof(bool), typeof(CodeEditor),
                new FrameworkPropertyMetadata(true, OnIsFoldingEnabledChanged));

        /// <summary>
        /// Enable or disable the code-folding gutter ([+]/[-] markers).
        /// When disabled the GutterControl is hidden and FoldingEngine analysis is skipped.
        /// </summary>
        [Category("Features")]
        [DisplayName("Enable Folding")]
        [Description("Show code-folding markers in the left gutter (brace pairs / indentation blocks)")]
        public bool IsFoldingEnabled
        {
            get => (bool)GetValue(IsFoldingEnabledProperty);
            set => SetValue(IsFoldingEnabledProperty, value);
        }

        private static void OnIsFoldingEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not CodeEditor editor) return;
            if (editor._gutterControl != null)
                editor._gutterControl.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
            if ((bool)e.NewValue && editor._document != null)
                editor._foldingEngine?.Analyze(editor._document.Lines);
            editor.InvalidateMeasure();
        }

        public static readonly DependencyProperty FoldToggleOnDoubleClickProperty =
            DependencyProperty.Register(nameof(FoldToggleOnDoubleClick), typeof(bool), typeof(CodeEditor),
                new FrameworkPropertyMetadata(true, OnFoldToggleOnDoubleClickChanged));

        /// <summary>
        /// When true, the inline collapsed block label ({…} / "Constructor") requires a
        /// double-click to expand. The gutter toggle triangle is unaffected (always single-click).
        /// </summary>
        [Category("Features")]
        [DisplayName("Fold Open on Double-Click")]
        [Description("When enabled, the inline collapsed block label requires a double-click to expand. The gutter triangle is unaffected.")]
        public bool FoldToggleOnDoubleClick
        {
            get => (bool)GetValue(FoldToggleOnDoubleClickProperty);
            set => SetValue(FoldToggleOnDoubleClickProperty, value);
        }

        private static void OnFoldToggleOnDoubleClickChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // Inline label behavior only — gutter triangle is unaffected.
        }

        public static readonly DependencyProperty ShowScopeGuidesProperty =
            DependencyProperty.Register(nameof(ShowScopeGuides), typeof(bool), typeof(CodeEditor),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>
        /// Show or hide the vertical scope guide lines that connect matching brace pairs.
        /// Toggled via the Code Editor options panel.
        /// </summary>
        public bool ShowScopeGuides
        {
            get => (bool)GetValue(ShowScopeGuidesProperty);
            set => SetValue(ShowScopeGuidesProperty, value);
        }

        public static readonly DependencyProperty EnableWordHighlightProperty =
            DependencyProperty.Register(nameof(EnableWordHighlight), typeof(bool), typeof(CodeEditor),
                new FrameworkPropertyMetadata(true, OnEnableWordHighlightChanged));

        /// <summary>
        /// When true, places a subtle highlight box on every occurrence of the word under the
        /// caret and shows proportional tick marks on the vertical scrollbar.
        /// </summary>
        public bool EnableWordHighlight
        {
            get => (bool)GetValue(EnableWordHighlightProperty);
            set => SetValue(EnableWordHighlightProperty, value);
        }

        private static void OnEnableWordHighlightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ce = (CodeEditor)d;
            if (!(bool)e.NewValue)
            {
                ce._wordHighlights.Clear();
                ce._wordHighlightLines.Clear();
                ce._wordHighlightLineSet.Clear();
                ce._codeScrollMarkerPanel?.ClearWordMarkers();
                ce.InvalidateVisual();
            }
            else
            {
                ce.ScheduleWordHighlightUpdate();
            }
        }

        /// <summary>
        /// Injectable <see cref="Snippets.SnippetManager"/> that provides language-specific snippets.
        /// When set, pressing Tab after a matching trigger word expands the snippet instead of
        /// inserting whitespace. Pass <c>null</c> to disable snippet expansion.
        /// </summary>
        public static readonly DependencyProperty SnippetManagerProperty =
            DependencyProperty.Register(nameof(SnippetManager), typeof(SnippetManager),
                typeof(CodeEditor), new System.Windows.PropertyMetadata(null));

        [Category("Features")]
        [DisplayName("Snippet Manager")]
        [Description("Provides Tab-triggered snippet expansion for the current language.")]
        public SnippetManager? SnippetManager
        {
            get => (SnippetManager?)GetValue(SnippetManagerProperty);
            set => SetValue(SnippetManagerProperty, value);
        }

        public static readonly DependencyProperty EnableSmartCompleteProperty =
            DependencyProperty.Register(nameof(EnableSmartComplete), typeof(bool), typeof(CodeEditor),
                new FrameworkPropertyMetadata(true));

        /// <summary>
        /// Enable SmartComplete context-aware autocomplete
        /// </summary>
        [Category("Features")]
        [DisplayName("Enable SmartComplete")]
        [Description("Enable context-aware autocomplete suggestions (Ctrl+Space to trigger manually)")]
        public bool EnableSmartComplete
        {
            get => (bool)GetValue(EnableSmartCompleteProperty);
            set
            {
                SetValue(EnableSmartCompleteProperty, value);
                _enableSmartComplete = value;
            }
        }

        public static readonly DependencyProperty ClickableLinksEnabledProperty =
            DependencyProperty.Register(nameof(ClickableLinksEnabled), typeof(bool), typeof(CodeEditor),
                new FrameworkPropertyMetadata(true));

        /// <summary>
        /// When <see langword="true"/>, HTTP/HTTPS URLs are detected and Ctrl+Click opens them in the default browser.
        /// </summary>
        [Category("Features")]
        [DisplayName("Clickable Links")]
        [Description("Ctrl+Click on http(s):// URLs opens them in the default browser.")]
        public bool ClickableLinksEnabled
        {
            get => (bool)GetValue(ClickableLinksEnabledProperty);
            set => SetValue(ClickableLinksEnabledProperty, value);
        }

        public static readonly DependencyProperty ClickableEmailsEnabledProperty =
            DependencyProperty.Register(nameof(ClickableEmailsEnabled), typeof(bool), typeof(CodeEditor),
                new FrameworkPropertyMetadata(true));

        /// <summary>
        /// When <see langword="true"/>, email addresses are detected and Ctrl+Click opens the default mail client.
        /// </summary>
        [Category("Features")]
        [DisplayName("Clickable Emails")]
        [Description("Ctrl+Click on email addresses opens the default mail client (mailto:).")]
        public bool ClickableEmailsEnabled
        {
            get => (bool)GetValue(ClickableEmailsEnabledProperty);
            set => SetValue(ClickableEmailsEnabledProperty, value);
        }

        public static readonly DependencyProperty EnableFindAllReferencesProperty =
            DependencyProperty.Register(nameof(EnableFindAllReferences), typeof(bool), typeof(CodeEditor),
                new FrameworkPropertyMetadata(true));

        /// <summary>
        /// When false, the Find All References command (Shift+F12) and its
        /// context-menu item are disabled regardless of LSP availability.
        /// </summary>
        [Category("Features")]
        [DisplayName("Enable Find All References")]
        [Description("Enables the Find All References command (Shift+F12) via the language server.")]
        public bool EnableFindAllReferences
        {
            get => (bool)GetValue(EnableFindAllReferencesProperty);
            set => SetValue(EnableFindAllReferencesProperty, value);
        }

        // ── Blame Gutter ──────────────────────────────────────────────────────────

        public static readonly DependencyProperty ShowBlameGutterProperty =
            DependencyProperty.Register(nameof(ShowBlameGutter), typeof(bool), typeof(CodeEditor),
                new FrameworkPropertyMetadata(false, OnShowBlameGutterChanged));

        [Category("Features")]
        [DisplayName("Show Blame Gutter")]
        [Description("Shows a 6-pixel color bar encoding commit age for each line.")]
        public bool ShowBlameGutter
        {
            get => (bool)GetValue(ShowBlameGutterProperty);
            set => SetValue(ShowBlameGutterProperty, value);
        }

        private static void OnShowBlameGutterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not CodeEditor ce || ce._blameGutterControl is null) return;
            ce._blameGutterControl.Visibility = (bool)e.NewValue
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;
            ce.InvalidateMeasure();
        }

        /// <summary>
        /// Injects blame data from the Git plugin into the blame gutter.
        /// Called by the App layer after <c>GitBlameLoadedEvent</c> arrives.
        /// </summary>
        public void SetBlame(IReadOnlyList<WpfHexEditor.Editor.Core.LSP.BlameEntry>? entries)
            => _blameGutterControl?.SetBlame(entries);

        // ── Inline Peek Definition (#158) ────────────────────────────────────

        /// <summary>Closes the inline peek panel and restores normal line layout.</summary>
        internal void CloseInlinePeek()
        {
            if (_inlinePeekHost != null)
                _scrollBarChildren.Remove(_inlinePeekHost);
            _inlinePeekHost = null;
            _peekHostLine   = -1;
            _peekHostHeight = 0.0;
            _linePositionsDirty = true;
            InvalidateMeasure();
            InvalidateVisual();
        }

        /// <summary>
        /// Scrolls the editor vertically so the peek panel is fully visible
        /// when it would otherwise extend below the viewport.
        /// </summary>
        internal void EnsurePeekVisible(int anchorLine)
        {
            if (_lineHeight <= 0) return;
            if (!_lineYLookup.TryGetValue(anchorLine, out double anchorY))
                anchorY = TopMargin + (anchorLine - _firstVisibleLine) * _lineHeight;

            double panelTop    = anchorY + _lineHeight;
            double panelBottom = panelTop + _peekHostHeight;
            double viewBottom  = ActualHeight - (_hScrollBar?.ActualHeight ?? 0);

            if (panelBottom > viewBottom)
            {
                double needed = panelBottom - viewBottom;
                _verticalScrollOffset = Math.Min(
                    _verticalScrollOffset + needed,
                    Math.Max(0, (_document?.Lines.Count ?? 0) * _lineHeight - ActualHeight));
                _linePositionsDirty = true;
                InvalidateVisual();
            }
        }

        // ── Change Marker Gutter (#166) ───────────────────────────────────────

        public static readonly DependencyProperty ShowChangeMarkersProperty =
            DependencyProperty.Register(nameof(ShowChangeMarkers), typeof(bool), typeof(CodeEditor),
                new FrameworkPropertyMetadata(true, OnShowChangeMarkersChanged));

        [Category("Features")]
        [DisplayName("Show Change Markers")]
        [Description("Shows Added / Modified / Deleted line indicators in the gutter.")]
        public bool ShowChangeMarkers
        {
            get => (bool)GetValue(ShowChangeMarkersProperty);
            set => SetValue(ShowChangeMarkersProperty, value);
        }

        private static void OnShowChangeMarkersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not CodeEditor ce || ce._changeMarkerGutterControl is null) return;
            ce._changeMarkerGutterControl.Visibility = (bool)e.NewValue
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;
            ce.InvalidateMeasure();
        }

        public static readonly DependencyProperty ShowInlineHintsProperty =
            DependencyProperty.Register(nameof(ShowInlineHints), typeof(bool), typeof(CodeEditor),
                new FrameworkPropertyMetadata(true,
                    FrameworkPropertyMetadataOptions.AffectsRender,
                    OnShowInlineHintsChanged));

        [Category("Features")]
        [DisplayName("Show Code Lens")]
        [Description("Shows inline reference counts above each declaration.")]
        public bool ShowInlineHints
        {
            get => (bool)GetValue(ShowInlineHintsProperty);
            set => SetValue(ShowInlineHintsProperty, value);
        }

        private static void OnShowInlineHintsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not CodeEditor ce) return;
            // Trigger layout/scrollbar recalculation — _lineHeight is no longer affected by InlineHints.
            // Only visible-line Y positions change (per-declaration extra space), handled in OnRender.
            ce.InvalidateMeasure();
        }

        public static readonly DependencyProperty ShowLspInlayHintsProperty =
            DependencyProperty.Register(nameof(ShowLspInlayHints), typeof(bool), typeof(CodeEditor),
                new FrameworkPropertyMetadata(true,
                    FrameworkPropertyMetadataOptions.AffectsRender,
                    (d, _) => (d as CodeEditor)?._lspInlayHintsLayer.SetContext(null, 0, 0, 0, 0)));

        [Category("Features")]
        [DisplayName("Show LSP Inlay Hints")]
        [Description("Shows parameter-name hints inline before arguments (requires LSP).")]
        public bool ShowLspInlayHints
        {
            get => (bool)GetValue(ShowLspInlayHintsProperty);
            set => SetValue(ShowLspInlayHintsProperty, value);
        }

        public static readonly DependencyProperty ShowLspDeclarationHintsProperty =
            DependencyProperty.Register(nameof(ShowLspDeclarationHints), typeof(bool), typeof(CodeEditor),
                new FrameworkPropertyMetadata(true,
                    FrameworkPropertyMetadataOptions.AffectsRender,
                    (d, _) => (d as CodeEditor)?._lspDeclarationHintsLayer.SetContext(null, 0, 0, 0, 0)));

        [Category("Features")]
        [DisplayName("Show LSP Code Lens")]
        [Description("Shows reference counts and test runner hints above declarations (requires LSP).")]
        public bool ShowLspDeclarationHints
        {
            get => (bool)GetValue(ShowLspDeclarationHintsProperty);
            set => SetValue(ShowLspDeclarationHintsProperty, value);
        }

        public static readonly DependencyProperty EnableSemanticHighlightingProperty =
            DependencyProperty.Register(nameof(EnableSemanticHighlighting), typeof(bool), typeof(CodeEditor),
                new FrameworkPropertyMetadata(false,
                    FrameworkPropertyMetadataOptions.AffectsRender,
                    (o, e) =>
                    {
                        var ce      = (CodeEditor)o;
                        var enabled = (bool)e.NewValue;
                        ce._semanticTokensLayer.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
                        ce._semanticTokensLayer.SetLspClient(enabled ? ce._lspClient : null);
                        if (!enabled) ce._semanticTokensLayer.SetContext(null, 0, 0, 0, 0);
                    }));

        [Category("Features")]
        [DisplayName("Semantic Highlighting")]
        [Description("Overlays LSP semantic token colors above syntactic highlighting (requires Roslyn LSP). whfmt coloring is unchanged when disabled or when no LSP is active.")]
        public bool EnableSemanticHighlighting
        {
            get => (bool)GetValue(EnableSemanticHighlightingProperty);
            set => SetValue(EnableSemanticHighlightingProperty, value);
        }

        public static readonly DependencyProperty InlineHintsVisibleKindsProperty =
            DependencyProperty.Register(
                nameof(InlineHintsVisibleKinds),
                typeof(WpfHexEditor.Editor.Core.InlineHintsSymbolKinds),
                typeof(CodeEditor),
                new FrameworkPropertyMetadata(
                    WpfHexEditor.Editor.Core.InlineHintsSymbolKinds.All,
                    FrameworkPropertyMetadataOptions.AffectsRender,
                    OnInlineHintsVisibleKindsChanged));

        [Category("Features")]
        [DisplayName("Code Lens Visible Kinds")]
        [Description("Bitmask of symbol kinds for which inline reference-count hints are displayed.")]
        public WpfHexEditor.Editor.Core.InlineHintsSymbolKinds InlineHintsVisibleKinds
        {
            get => (WpfHexEditor.Editor.Core.InlineHintsSymbolKinds)GetValue(InlineHintsVisibleKindsProperty);
            set => SetValue(InlineHintsVisibleKindsProperty, value);
        }

        private static void OnInlineHintsVisibleKindsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not CodeEditor ce) return;
            ce.RebuildVisibleHintsCount();
            ce.InvalidateMeasure();
        }

        /// <summary>
        /// Reference-count source strategy: 0=Auto, 1=RoslynOnly, 2=RegexAlways.
        /// Setting this re-wires the InlineHints provider immediately.
        /// </summary>
        [Category("Features")]
        [DisplayName("Inline Hints Source")]
        [Description("0=Auto (Roslyn when available), 1=RoslynOnly, 2=RegexAlways.")]
        public int InlineHintsSource
        {
            get => _inlineHintsSource;
            set
            {
                if (_inlineHintsSource == value) return;
                _inlineHintsSource = value;
                _inlineHintsService.SetReferenceCountProvider(
                    _lspClient as WpfHexEditor.Editor.Core.LSP.IReferenceCountProvider,
                    value);
            }
        }

        private bool _showVarTypeHints = true;
        public bool ShowVarTypeHints
        {
            get => _showVarTypeHints;
            set
            {
                if (_showVarTypeHints == value) return;
                _showVarTypeHints = value;
                NotifyRoslynInlineHintsOptions();
            }
        }

        private bool _showLambdaReturnTypeHints = true;
        public bool ShowLambdaReturnTypeHints
        {
            get => _showLambdaReturnTypeHints;
            set
            {
                if (_showLambdaReturnTypeHints == value) return;
                _showLambdaReturnTypeHints = value;
                NotifyRoslynInlineHintsOptions();
            }
        }

        private void NotifyRoslynInlineHintsOptions()
        {
            if (_lspClient is WpfHexEditor.Editor.Core.LSP.IInlineHintsOptionsClient hintsClient)
                hintsClient.SetInlineHintsOptions(_showVarTypeHints, _showLambdaReturnTypeHints);
        }

        // ── Quick Info DPs ─────────────────────────────────────────────────────

        public static readonly DependencyProperty ShowQuickInfoProperty =
            DependencyProperty.Register(nameof(ShowQuickInfo), typeof(bool), typeof(CodeEditor),
                new FrameworkPropertyMetadata(true, OnShowQuickInfoChanged));

        [Category("Features")]
        [DisplayName("Show Quick Info")]
        [Description("Shows an interactive VS-like hover tooltip with symbol information (Quick Info).")]
        public bool ShowQuickInfo
        {
            get => (bool)GetValue(ShowQuickInfoProperty);
            set => SetValue(ShowQuickInfoProperty, value);
        }

        private static void OnShowQuickInfoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not CodeEditor ce || (bool)e.NewValue) return;
            ce._quickInfoPopup?.Hide();
            ce._hoverQuickInfoService?.Cancel();
        }

        public static readonly DependencyProperty QuickInfoDelayMsProperty =
            DependencyProperty.Register(nameof(QuickInfoDelayMs), typeof(int), typeof(CodeEditor),
                new FrameworkPropertyMetadata(400, OnQuickInfoDelayChanged));

        [Category("Features")]
        [DisplayName("Quick Info Delay (ms)")]
        [Description("Hover dwell time in milliseconds before Quick Info appears (200–1000 ms).")]
        public int QuickInfoDelayMs
        {
            get => (int)GetValue(QuickInfoDelayMsProperty);
            set => SetValue(QuickInfoDelayMsProperty, value);
        }

        private static void OnQuickInfoDelayChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not CodeEditor ce || ce._hoverQuickInfoService is null) return;
            ce._hoverQuickInfoService.SetDebounceInterval(
                TimeSpan.FromMilliseconds(Math.Max(200, (int)e.NewValue)));
        }

        // ── Breakpoint Line Highlight DP ──────────────────────────────────────

        public static readonly DependencyProperty ShowBreakpointLineHighlightProperty =
            DependencyProperty.Register(nameof(ShowBreakpointLineHighlight), typeof(bool), typeof(CodeEditor),
                new FrameworkPropertyMetadata(true, (d, _) => (d as CodeEditor)?.InvalidateVisual()));

        [Category("Features")]
        [DisplayName("Highlight Breakpoint Lines")]
        [Description("When true, breakpoint lines are highlighted with a semi-transparent background tint.")]
        public bool ShowBreakpointLineHighlight
        {
            get => (bool)GetValue(ShowBreakpointLineHighlightProperty);
            set => SetValue(ShowBreakpointLineHighlightProperty, value);
        }

        // ── End-of-Block Hint DPs ─────────────────────────────────────────────

        public static readonly DependencyProperty ShowEndOfBlockHintProperty =
            DependencyProperty.Register(nameof(ShowEndOfBlockHint), typeof(bool), typeof(CodeEditor),
                new FrameworkPropertyMetadata(true, OnShowEndOfBlockHintChanged));

        [Category("Features")]
        [DisplayName("Show End-of-Block Hint")]
        [Description("When true, hovering over }, #endregion, </Tag> shows the matching opening line(s).")]
        public bool ShowEndOfBlockHint
        {
            get => (bool)GetValue(ShowEndOfBlockHintProperty);
            set => SetValue(ShowEndOfBlockHintProperty, value);
        }

        private static void OnShowEndOfBlockHintChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not CodeEditor ce || (bool)e.NewValue) return;
            ce.DismissEndBlockHint();
        }

        public static readonly DependencyProperty EndOfBlockHintDelayMsProperty =
            DependencyProperty.Register(nameof(EndOfBlockHintDelayMs), typeof(int), typeof(CodeEditor),
                new FrameworkPropertyMetadata(600, OnEndOfBlockHintDelayChanged));

        [Category("Features")]
        [DisplayName("End-of-Block Hint Delay (ms)")]
        [Description("Hover dwell time in milliseconds before the end-of-block hint popup appears (100–2000 ms).")]
        public int EndOfBlockHintDelayMs
        {
            get => (int)GetValue(EndOfBlockHintDelayMsProperty);
            set => SetValue(EndOfBlockHintDelayMsProperty, value);
        }

        private static void OnEndOfBlockHintDelayChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not CodeEditor ce || ce._endBlockHintTimer is null) return;
            ce._endBlockHintTimer.Interval = TimeSpan.FromMilliseconds(
                Math.Clamp((int)e.NewValue, 100, 2000));
        }

        // ── Language Definition DP ─────────────────────────────────────────────

        public static readonly DependencyProperty LanguageProperty =
            DependencyProperty.Register(nameof(Language), typeof(LanguageDefinition), typeof(CodeEditor),
                new FrameworkPropertyMetadata(null,
                    FrameworkPropertyMetadataOptions.AffectsRender,
                    OnLanguageChanged));

        [Category("Features")]
        [DisplayName("Language Definition")]
        [Description("Active language definition controlling InlineHints and Ctrl+Click navigation per language.")]
        public LanguageDefinition? Language
        {
            get => (LanguageDefinition?)GetValue(LanguageProperty);
            set => SetValue(LanguageProperty, value);
        }

        private static void OnLanguageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not CodeEditor editor) return;
            var newLang = e.NewValue as LanguageDefinition;

            // Update column rulers from the language definition; null clears rulers.
            editor.ColumnRulers = newLang?.ColumnRulers;

            // Update bracket pair colorizer — drives CE_Bracket_1/2/3/4 depth colors.
            editor._bracketColorizer.SetPairs(newLang?.BracketPairs);
            editor._bracketDepthFirstLine = -1; // force depth rescan on next render

            // Re-evaluate InlineHints service attachment: only attach when the language declares support.
            if (newLang?.EnableInlineHints == true)
                editor._inlineHintsService.Attach(editor._document, editor._currentFilePath);
            else
                editor._inlineHintsService.Detach();

            // Rebuild folding strategy from language-specific FoldingRules.
            var foldingStrategy = Folding.LanguageFoldingStrategyBuilder.Build(newLang?.FoldingRules, newLang?.LineCommentPrefix);
            if (foldingStrategy is not null && editor._foldingEngine is not null)
            {
                editor._foldingEngine.ReplaceStrategy(foldingStrategy);
                editor._foldingEngine.Analyze(editor._document.Lines);
                editor.InvalidateVisual();
            }

            // Compile breakpoint placement validation regexes from BreakpointRules.
            editor.RebuildBreakpointValidation(newLang);

            // Keep SmartComplete popup aware of the current language (for local completions).
            if (editor._smartCompletePopup is not null)
                editor._smartCompletePopup.CurrentLanguage = newLang;
        }

        private void RebuildBreakpointValidation(LanguageDefinition? lang)
        {
            var patterns = lang?.BreakpointRules?.NonExecutablePatterns ?? Array.Empty<string>();
            var compiled = new List<Regex>(patterns.Count);
            foreach (var p in patterns)
            {
                try   { compiled.Add(new Regex(p, RegexOptions.Compiled)); }
                catch { /* malformed pattern — skip silently */              }
            }
            _bpNonExecutableRegexes = compiled;

            // Compile statement continuation regexes for multi-line highlight.
            var contPatterns = lang?.BreakpointRules?.StatementContinuationPatterns ?? Array.Empty<string>();
            var contCompiled = new List<Regex>(contPatterns.Count);
            foreach (var cp in contPatterns)
            {
                try   { contCompiled.Add(new Regex(cp, RegexOptions.Compiled)); }
                catch { /* malformed pattern — skip silently */                   }
            }
            _bpContinuationRegexes = contCompiled;
            _bpMaxScanLines        = lang?.BreakpointRules?.MaxStatementScanLines ?? 20;
            _bpBlockScopeHighlight = lang?.BreakpointRules?.BlockScopeHighlight ?? true;

            if (_breakpointGutterControl is null) return;
            _breakpointGutterControl.ValidateLine = compiled.Count == 0
                ? (Func<int, bool>?)null
                : line =>
                {
                    string text = (line >= 1 && line <= _document?.Lines.Count)
                        ? (_document.Lines[line - 1].Text ?? string.Empty)
                        : string.Empty;
                    foreach (var r in _bpNonExecutableRegexes)
                        if (r.IsMatch(text)) return false;
                    return true;
                };

            // Statement-span enforcement: 1 instruction = 1 breakpoint.
            // The BP is always anchored to the FIRST line of the statement,
            // regardless of which line the user clicked.
            _breakpointGutterControl.ResolveBreakpointLine = contCompiled.Count == 0
                ? null
                : clickedLine1 =>
                {
                    int line0 = clickedLine1 - 1;
                    var (start0, end0) = ResolveStatementSpan(line0);

                    int firstLine1 = start0 + 1; // 1-based first line of the statement

                    var existingBps = _bpSource?.GetBreakpointLines(_currentFilePath ?? string.Empty)
                                      ?? (IReadOnlyList<int>)Array.Empty<int>();

                    int? existingInSpan = null;
                    foreach (int bp1 in existingBps)
                    {
                        int bp0 = bp1 - 1;
                        if (bp0 >= start0 && bp0 <= end0)
                        {
                            existingInSpan = bp1;
                            break;
                        }
                    }

                    // No existing BP in this statement → place on the first line.
                    if (existingInSpan is null)
                        return firstLine1;

                    // BP already on the first line → toggle it off.
                    if (existingInSpan.Value == firstLine1)
                        return firstLine1;

                    // BP on a non-first line (anomalous state): move it to the first line.
                    _bpSource!.Delete(_currentFilePath!, existingInSpan.Value);
                    return firstLine1;
                };
        }

        // ── Column Rulers DP (#165) ───────────────────────────────────────────

        public static readonly DependencyProperty ColumnRulersProperty =
            DependencyProperty.Register(nameof(ColumnRulers), typeof(IReadOnlyList<int>), typeof(CodeEditor),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>
        /// Column ruler positions driven by whfmt <c>"columnRulers"</c> (e.g. [80, 120]).
        /// Set automatically from <see cref="Language"/> when the active language changes.
        /// Null = no rulers.
        /// </summary>
        [Category("Features")]
        [DisplayName("Column Rulers")]
        [Description("Character columns at which vertical guide lines are drawn. Driven by whfmt 'columnRulers'.")]
        public IReadOnlyList<int>? ColumnRulers
        {
            get => (IReadOnlyList<int>?)GetValue(ColumnRulersProperty);
            set => SetValue(ColumnRulersProperty, value);
        }

        public static readonly DependencyProperty ShowColumnRulersProperty =
            DependencyProperty.Register(nameof(ShowColumnRulers), typeof(bool), typeof(CodeEditor),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>
        /// Show or hide the vertical column ruler lines defined by the active language's whfmt.
        /// Toggled via the CodeEditor context menu.
        /// </summary>
        public bool ShowColumnRulers
        {
            get => (bool)GetValue(ShowColumnRulersProperty);
            set => SetValue(ShowColumnRulersProperty, value);
        }

        public static readonly DependencyProperty EnableValidationProperty =
            DependencyProperty.Register(nameof(EnableValidation), typeof(bool), typeof(CodeEditor),
                new FrameworkPropertyMetadata(true));

        /// <summary>
        /// Enable real-time format definition validation
        /// </summary>
        [Category("Features")]
        [DisplayName("Enable Validation")]
        [Description("Enable real-time validation with visual feedback (squiggly lines under errors)")]
        public bool EnableValidation
        {
            get => (bool)GetValue(EnableValidationProperty);
            set
            {
                SetValue(EnableValidationProperty, value);
                if (value)
                    TriggerValidation();
                else
                {
                    _validationErrors.Clear();
                    _validationByLine.Clear();
                    DiagnosticsChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        // ===== APPEARANCE - FONTS =====

        public static readonly DependencyProperty EditorFontFamilyProperty =
            DependencyProperty.Register(nameof(EditorFontFamily), typeof(FontFamily), typeof(CodeEditor),
                new FrameworkPropertyMetadata(new FontFamily("Consolas"), FrameworkPropertyMetadataOptions.AffectsRender,
                    OnFontChanged));

        [Category("Appearance.Fonts")]
        [DisplayName("Editor Font Family")]
        [Description("Font family for editor text (monospace recommended)")]
        public FontFamily EditorFontFamily
        {
            get => (FontFamily)GetValue(EditorFontFamilyProperty);
            set => SetValue(EditorFontFamilyProperty, value);
        }

        public static readonly DependencyProperty EditorFontSizeProperty =
            DependencyProperty.Register(nameof(EditorFontSize), typeof(double), typeof(CodeEditor),
                new FrameworkPropertyMetadata(12.0, FrameworkPropertyMetadataOptions.AffectsRender,
                    OnFontChanged));

        [Category("Appearance.Fonts")]
        [DisplayName("Editor Font Size")]
        [Description("Font size for editor text (points)")]
        [Range(8, 72)]
        public double EditorFontSize
        {
            get => (double)GetValue(EditorFontSizeProperty);
            set => SetValue(EditorFontSizeProperty, value);
        }

        // -- ZoomLevel (B6) -----------------------------------------------------

        public static readonly DependencyProperty ZoomLevelProperty =
            DependencyProperty.Register(nameof(ZoomLevel), typeof(double), typeof(CodeEditor),
                new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender,
                    OnZoomLevelChanged),
                ValidateZoomLevel);

        private static bool ValidateZoomLevel(object value)
            => value is double d && d >= 0.5 && d <= 4.0;

        private static void OnZoomLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not CodeEditor editor) return;
            editor._fontSize = editor._baseFontSize * (double)e.NewValue;
            editor.CalculateCharacterDimensions();
            editor.InvalidateMeasure();
            editor.ZoomLevelChanged?.Invoke(editor, (double)e.NewValue);
        }

        /// <summary>
        /// Current zoom multiplier applied on top of <see cref="EditorFontSize"/>.
        /// Range: 0.5 (50%) to 4.0 (400%), default 1.0 (100%).
        /// Ctrl+MouseWheel adjusts this value in 0.1 increments.
        /// </summary>
        [Category("Appearance.Fonts")]
        [DisplayName("Zoom Level")]
        [Description("Zoom multiplier applied to the base font size (0.5–4.0). Ctrl+wheel to adjust.")]
        public double ZoomLevel
        {
            get => (double)GetValue(ZoomLevelProperty);
            set => SetValue(ZoomLevelProperty, Math.Clamp(value, 0.5, 4.0));
        }

        /// <summary>Raised when <see cref="ZoomLevel"/> changes. Arg is the new zoom value.</summary>
        public event EventHandler<double>? ZoomLevelChanged;

        /// <summary>
        /// Raised when the minimap should refresh: scroll position changed, text changed,
        /// syntax highlights completed, or folding regions changed.
        /// Subscribe to this instead of <see cref="FrameworkElement.LayoutUpdated"/> to
        /// avoid a feedback loop where minimap InvalidateVisual re-triggers LayoutUpdated.
        /// </summary>
        public event EventHandler? MinimapRefreshRequested;

        public static readonly DependencyProperty LineNumberFontSizeProperty =
            DependencyProperty.Register(nameof(LineNumberFontSize), typeof(double), typeof(CodeEditor),
                new FrameworkPropertyMetadata(10.0, FrameworkPropertyMetadataOptions.AffectsRender));

        [Category("Appearance.Fonts")]
        [DisplayName("Line Number Font Size")]
        [Description("Font size for line numbers (points)")]
        [Range(6, 24)]
        public double LineNumberFontSize
        {
            get => (double)GetValue(LineNumberFontSizeProperty);
            set => SetValue(LineNumberFontSizeProperty, value);
        }

        public static readonly DependencyProperty EditorFontWeightProperty =
            DependencyProperty.Register(nameof(EditorFontWeight), typeof(FontWeight), typeof(CodeEditor),
                new FrameworkPropertyMetadata(FontWeights.Normal, FrameworkPropertyMetadataOptions.AffectsRender,
                    OnFontChanged));

        [Category("Appearance.Fonts")]
        [DisplayName("Editor Font Weight")]
        [Description("Font weight for editor text (Normal, Bold, etc.)")]
        public FontWeight EditorFontWeight
        {
            get => (FontWeight)GetValue(EditorFontWeightProperty);
            set => SetValue(EditorFontWeightProperty, value);
        }

        public static readonly DependencyProperty LineNumberFontFamilyProperty =
            DependencyProperty.Register(nameof(LineNumberFontFamily), typeof(FontFamily), typeof(CodeEditor),
                new FrameworkPropertyMetadata(new FontFamily("Consolas"), FrameworkPropertyMetadataOptions.AffectsRender));

        [Category("Appearance.Fonts")]
        [DisplayName("Line Number Font Family")]
        [Description("Font family for line numbers")]
        public FontFamily LineNumberFontFamily
        {
            get => (FontFamily)GetValue(LineNumberFontFamilyProperty);
            set => SetValue(LineNumberFontFamilyProperty, value);
        }

        public static readonly DependencyProperty LineHeightMultiplierProperty =
            DependencyProperty.Register(nameof(LineHeightMultiplier), typeof(double), typeof(CodeEditor),
                new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender,
                    OnFontChanged));

        [Category("Appearance.Fonts")]
        [DisplayName("Line Height Multiplier")]
        [Description("Line height as a multiple of font size (1.0-3.0)")]
        [Range(1.0, 3.0)]
        public double LineHeightMultiplier
        {
            get => (double)GetValue(LineHeightMultiplierProperty);
            set => SetValue(LineHeightMultiplierProperty, Math.Max(1.0, Math.Min(3.0, value)));
        }

        public static readonly DependencyProperty BoldKeywordsProperty =
            DependencyProperty.Register(nameof(BoldKeywords), typeof(bool), typeof(CodeEditor),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        [Category("Appearance.Fonts")]
        [DisplayName("Bold Keywords")]
        [Description("Render keywords (signature, field, etc.) in bold font")]
        public bool BoldKeywords
        {
            get => (bool)GetValue(BoldKeywordsProperty);
            set => SetValue(BoldKeywordsProperty, value);
        }

        public static readonly DependencyProperty ItalicCommentsProperty =
            DependencyProperty.Register(nameof(ItalicComments), typeof(bool), typeof(CodeEditor),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        [Category("Appearance.Fonts")]
        [DisplayName("Italic Comments")]
        [Description("Render comments in italic font")]
        public bool ItalicComments
        {
            get => (bool)GetValue(ItalicCommentsProperty);
            set => SetValue(ItalicCommentsProperty, value);
        }

        // ===== APPEARANCE - COLORS =====

        public static readonly DependencyProperty EditorBackgroundProperty =
            DependencyProperty.Register(nameof(EditorBackground), typeof(Brush), typeof(CodeEditor),
                new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

        [Category("Appearance.Colors")]
        [DisplayName("Editor Background")]
        [Description("Background color of the editor")]
        public Brush EditorBackground
        {
            get => (Brush)GetValue(EditorBackgroundProperty);
            set => SetValue(EditorBackgroundProperty, value);
        }

        public static readonly DependencyProperty EditorForegroundProperty =
            DependencyProperty.Register(nameof(EditorForeground), typeof(Brush), typeof(CodeEditor),
                new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender));

        [Category("Appearance.Colors")]
        [DisplayName("Editor Foreground")]
        [Description("Default text color")]
        public Brush EditorForeground
        {
            get => (Brush)GetValue(EditorForegroundProperty);
            set => SetValue(EditorForegroundProperty, value);
        }

        public static readonly DependencyProperty LineNumberBackgroundProperty =
            DependencyProperty.Register(nameof(LineNumberBackground), typeof(Brush), typeof(CodeEditor),
                new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                    FrameworkPropertyMetadataOptions.AffectsRender));

        [Category("Appearance.Colors")]
        [DisplayName("Line Number Background")]
        [Description("Background color of line number gutter")]
        public Brush LineNumberBackground
        {
            get => (Brush)GetValue(LineNumberBackgroundProperty);
            set => SetValue(LineNumberBackgroundProperty, value);
        }

        public static readonly DependencyProperty LineNumberForegroundProperty =
            DependencyProperty.Register(nameof(LineNumberForeground), typeof(Brush), typeof(CodeEditor),
                new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(128, 128, 128)),
                    FrameworkPropertyMetadataOptions.AffectsRender));

        [Category("Appearance.Colors")]
        [DisplayName("Line Number Foreground")]
        [Description("Text color of line numbers")]
        public Brush LineNumberForeground
        {
            get => (Brush)GetValue(LineNumberForegroundProperty);
            set => SetValue(LineNumberForegroundProperty, value);
        }

        public static readonly DependencyProperty CurrentLineBackgroundProperty =
            DependencyProperty.Register(nameof(CurrentLineBackground), typeof(Brush), typeof(CodeEditor),
                new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromArgb(30, 0, 120, 215)),
                    FrameworkPropertyMetadataOptions.AffectsRender));

        [Category("Appearance.Colors")]
        [DisplayName("Current Line Background")]
        [Description("Highlight color for current line")]
        public Brush CurrentLineBackground
        {
            get => (Brush)GetValue(CurrentLineBackgroundProperty);
            set => SetValue(CurrentLineBackgroundProperty, value);
        }

        public static readonly DependencyProperty SelectionBackgroundProperty =
            DependencyProperty.Register(nameof(SelectionBackground), typeof(Brush), typeof(CodeEditor),
                new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(173, 214, 255)),
                    FrameworkPropertyMetadataOptions.AffectsRender));

        [Category("Appearance.Colors")]
        [DisplayName("Selection Background")]
        [Description("Background color for selected text")]
        public Brush SelectionBackground
        {
            get => (Brush)GetValue(SelectionBackgroundProperty);
            set => SetValue(SelectionBackgroundProperty, value);
        }

        // ===== APPEARANCE - EDITOR COLORS (Advanced) =====

        public static readonly DependencyProperty CaretColorProperty =
            DependencyProperty.Register(nameof(CaretColor), typeof(Color), typeof(CodeEditor),
                new FrameworkPropertyMetadata(Colors.Black, FrameworkPropertyMetadataOptions.AffectsRender));

        [Category("Appearance.Colors")]
        [DisplayName("Caret Color")]
        [Description("Color of the text cursor")]
        public Color CaretColor
        {
            get => (Color)GetValue(CaretColorProperty);
            set => SetValue(CaretColorProperty, value);
        }

        public static readonly DependencyProperty CurrentLineBorderColorProperty =
            DependencyProperty.Register(nameof(CurrentLineBorderColor), typeof(Color), typeof(CodeEditor),
                new FrameworkPropertyMetadata(Color.FromArgb(80, 0, 120, 215), FrameworkPropertyMetadataOptions.AffectsRender));

        [Category("Appearance.Colors")]
        [DisplayName("Current Line Border Color")]
        [Description("Border color around the current line (when ShowCurrentLineBorder is enabled)")]
        public Color CurrentLineBorderColor
        {
            get => (Color)GetValue(CurrentLineBorderColorProperty);
            set => SetValue(CurrentLineBorderColorProperty, value);
        }

        public static readonly DependencyProperty InactiveSelectionBackgroundProperty =
            DependencyProperty.Register(nameof(InactiveSelectionBackground), typeof(Brush), typeof(CodeEditor),
                new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(62, 62, 66)),
                    FrameworkPropertyMetadataOptions.AffectsRender));

        [Category("Appearance.Colors")]
        [DisplayName("Inactive Selection Background")]
        [Description("Selection background color when editor loses focus (VS-like grayed selection)")]
        public Brush InactiveSelectionBackground
        {
            get => (Brush)GetValue(InactiveSelectionBackgroundProperty);
            set => SetValue(InactiveSelectionBackgroundProperty, value);
        }

        public static readonly DependencyProperty ValidationErrorGlyphColorProperty =
            DependencyProperty.Register(nameof(ValidationErrorGlyphColor), typeof(Color), typeof(CodeEditor),
                new FrameworkPropertyMetadata(Colors.Red, FrameworkPropertyMetadataOptions.AffectsRender));

        [Category("Appearance.Colors")]
        [DisplayName("Validation Error Glyph")]
        [Description("Color for error icons in gutter")]
        public Color ValidationErrorGlyphColor
        {
            get => (Color)GetValue(ValidationErrorGlyphColorProperty);
            set => SetValue(ValidationErrorGlyphColorProperty, value);
        }

        public static readonly DependencyProperty ValidationWarningGlyphColorProperty =
            DependencyProperty.Register(nameof(ValidationWarningGlyphColor), typeof(Color), typeof(CodeEditor),
                new FrameworkPropertyMetadata(Color.FromRgb(255, 165, 0), FrameworkPropertyMetadataOptions.AffectsRender));

        [Category("Appearance.Colors")]
        [DisplayName("Validation Warning Glyph")]
        [Description("Color for warning icons in gutter")]
        public Color ValidationWarningGlyphColor
        {
            get => (Color)GetValue(ValidationWarningGlyphColorProperty);
            set => SetValue(ValidationWarningGlyphColorProperty, value);
        }

        // ===== BEHAVIOR =====

        public static readonly DependencyProperty IndentSizeProperty =
            DependencyProperty.Register(nameof(IndentSize), typeof(int), typeof(CodeEditor),
                new FrameworkPropertyMetadata(2, OnIndentSizeChanged));

        [Category("Behavior")]
        [DisplayName("Indent Size")]
        [Description("Number of spaces per indentation level")]
        public int IndentSize
        {
            get => (int)GetValue(IndentSizeProperty);
            set => SetValue(IndentSizeProperty, value);
        }

        public static readonly DependencyProperty AutoIndentModeProperty =
            DependencyProperty.Register(nameof(AutoIndentMode), typeof(AutoIndentMode), typeof(CodeEditor),
                new FrameworkPropertyMetadata(AutoIndentMode.KeepIndent));

        [Category("Behavior")]
        [DisplayName("Auto-Indent Mode")]
        [Description("Controls automatic indentation when Enter is pressed: None, KeepIndent, or Smart.")]
        public AutoIndentMode AutoIndentMode
        {
            get => (AutoIndentMode)GetValue(AutoIndentModeProperty);
            set => SetValue(AutoIndentModeProperty, value);
        }

        public static readonly DependencyProperty SmartCompleteDelayProperty =
            DependencyProperty.Register(nameof(SmartCompleteDelay), typeof(int), typeof(CodeEditor),
                new FrameworkPropertyMetadata(300));

        [Category("Behavior")]
        [DisplayName("SmartComplete Delay (ms)")]
        [Description("Delay before showing SmartComplete popup (milliseconds)")]
        public int SmartCompleteDelay
        {
            get => (int)GetValue(SmartCompleteDelayProperty);
            set => SetValue(SmartCompleteDelayProperty, value);
        }

        public static readonly DependencyProperty ValidationDelayProperty =
            DependencyProperty.Register(nameof(ValidationDelay), typeof(int), typeof(CodeEditor),
                new FrameworkPropertyMetadata(1000));

        [Category("Behavior")]
        [DisplayName("Validation Delay (ms)")]
        [Description("Delay before running validation after text change (milliseconds)")]
        public int ValidationDelay
        {
            get => (int)GetValue(ValidationDelayProperty);
            set => SetValue(ValidationDelayProperty, value);
        }

        // ===== BEHAVIOR - SELECTION & CURSOR =====

        public static readonly DependencyProperty ShowCurrentLineHighlightProperty =
            DependencyProperty.Register(nameof(ShowCurrentLineHighlight), typeof(bool), typeof(CodeEditor),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        [Category("Behavior.Selection")]
        [DisplayName("Show Current Line Highlight")]
        [Description("Highlight the line where the cursor is located")]
        public bool ShowCurrentLineHighlight
        {
            get => (bool)GetValue(ShowCurrentLineHighlightProperty);
            set => SetValue(ShowCurrentLineHighlightProperty, value);
        }

        public static readonly DependencyProperty ShowCurrentLineBorderProperty =
            DependencyProperty.Register(nameof(ShowCurrentLineBorder), typeof(bool), typeof(CodeEditor),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

        [Category("Behavior.Selection")]
        [DisplayName("Show Current Line Border")]
        [Description("Show border around the current line")]
        public bool ShowCurrentLineBorder
        {
            get => (bool)GetValue(ShowCurrentLineBorderProperty);
            set => SetValue(ShowCurrentLineBorderProperty, value);
        }

        public static readonly DependencyProperty CaretBlinkRateProperty =
            DependencyProperty.Register(nameof(CaretBlinkRate), typeof(int), typeof(CodeEditor),
                new FrameworkPropertyMetadata(500, OnCaretBlinkRateChanged));

        [Category("Behavior.Selection")]
        [DisplayName("Caret Blink Rate (ms)")]
        [Description("Cursor blink speed in milliseconds (0 = no blink)")]
        public int CaretBlinkRate
        {
            get => (int)GetValue(CaretBlinkRateProperty);
            set => SetValue(CaretBlinkRateProperty, Math.Max(0, Math.Min(2000, value)));
        }

        private static void OnCaretBlinkRateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CodeEditor editor)
            {
                editor.UpdateCaretBlinkTimer();
            }
        }

        public static readonly DependencyProperty CaretWidthProperty =
            DependencyProperty.Register(nameof(CaretWidth), typeof(double), typeof(CodeEditor),
                new FrameworkPropertyMetadata(2.0, FrameworkPropertyMetadataOptions.AffectsRender));

        [Category("Behavior.Selection")]
        [DisplayName("Caret Width")]
        [Description("Width of the text cursor in pixels")]
        public double CaretWidth
        {
            get => (double)GetValue(CaretWidthProperty);
            set => SetValue(CaretWidthProperty, Math.Max(1.0, Math.Min(5.0, value)));
        }

        public static readonly DependencyProperty SmartBackspaceProperty =
            DependencyProperty.Register(nameof(SmartBackspace), typeof(bool), typeof(CodeEditor),
                new FrameworkPropertyMetadata(true));

        [Category("Behavior.Selection")]
        [DisplayName("Smart Backspace")]
        [Description("Backspace removes full indent when at start of indented line")]
        public bool SmartBackspace
        {
            get => (bool)GetValue(SmartBackspaceProperty);
            set => SetValue(SmartBackspaceProperty, value);
        }

        // ===== BEHAVIOR - ADVANCED FEATURES =====

        public static readonly DependencyProperty BracketPairColorizationEnabledProperty =
            DependencyProperty.Register(nameof(BracketPairColorizationEnabled), typeof(bool), typeof(CodeEditor),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>
        /// When true and the active language defines <c>bracketPairs</c> in its whfmt,
        /// brackets are colored with CE_Bracket_1/2/3/4 based on nesting depth.
        /// False = all brackets use the single <c>CE_Bracket</c> token.
        /// </summary>
        [Category("Behavior.Advanced")]
        [DisplayName("Bracket Pair Colorization")]
        [Description("Color brackets with CE_Bracket_1/2/3/4 based on nesting depth (requires bracketPairs in whfmt).")]
        public bool BracketPairColorizationEnabled
        {
            get => (bool)GetValue(BracketPairColorizationEnabledProperty);
            set => SetValue(BracketPairColorizationEnabledProperty, value);
        }

        // -- Rainbow Scope Guides -------------------------------------------------

        public static readonly DependencyProperty RainbowScopeGuidesEnabledProperty =
            DependencyProperty.Register(nameof(RainbowScopeGuidesEnabled), typeof(bool), typeof(CodeEditor),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>
        /// When true and bracket pair colorization is enabled, scope guide lines
        /// are colored with CE_Bracket_1/2/3/4 based on nesting depth.
        /// </summary>
        [Category("Behavior.Advanced")]
        [DisplayName("Rainbow Scope Guides")]
        [Description("Color scope guide lines by bracket depth using CE_Bracket_1/2/3/4.")]
        public bool RainbowScopeGuidesEnabled
        {
            get => (bool)GetValue(RainbowScopeGuidesEnabledProperty);
            set => SetValue(RainbowScopeGuidesEnabledProperty, value);
        }

        // Rainbow scope guide pen cache — instance fields because they depend on theme resources.
        private Pen[]? _rainbowGuidePens;
        private Pen[]? _rainbowGuideActivePens;

        /// <summary>
        /// Lazily creates 4 inactive + 4 active frozen pens from the CE_Bracket_1..4 theme resources.
        /// </summary>
        private void EnsureRainbowGuidePens()
        {
            if (_rainbowGuidePens != null) return;

            _rainbowGuidePens = new Pen[4];
            _rainbowGuideActivePens = new Pen[4];

            for (int i = 0; i < 4; i++)
            {
                var res = TryFindResource($"CE_Bracket_{i + 1}");
                Color color = res is SolidColorBrush scb ? scb.Color
                            : res is Color c             ? c
                            : Colors.Gray;

                _rainbowGuidePens[i] = MakeFrozenPen(Color.FromArgb(120, color.R, color.G, color.B), 1.0);
                _rainbowGuideActivePens[i] = MakeFrozenPen(Color.FromArgb(220, color.R, color.G, color.B), 1.5);
            }
        }

        // -- Color Swatch Preview (#168) -----------------------------------------

        public static readonly DependencyProperty ColorSwatchPreviewEnabledProperty =
            DependencyProperty.Register(nameof(ColorSwatchPreviewEnabled), typeof(bool), typeof(CodeEditor),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>
        /// When true and the active language has <c>colorLiteralPatterns</c> in its whfmt definition,
        /// a 12×12 colour preview swatch is rendered to the left of each colour literal.
        /// Click a swatch to raise <see cref="ColorSwatchClicked"/>.
        /// </summary>
        public bool ColorSwatchPreviewEnabled
        {
            get => (bool)GetValue(ColorSwatchPreviewEnabledProperty);
            set => SetValue(ColorSwatchPreviewEnabledProperty, value);
        }

        /// <summary>
        /// Raised when the user clicks a color swatch.
        /// The host can open a color picker and apply the edited color back into the document.
        /// </summary>
        public event EventHandler<ColorSwatchClickedEventArgs>? ColorSwatchClicked;

        /// <summary>
        /// Raised when the user clicks "Options…" in the Formatting context menu.
        /// The host should open the Code Editor options page.
        /// </summary>
        public event EventHandler? FormattingOptionsRequested;

        public static readonly DependencyProperty EnableBracketMatchingProperty =
            DependencyProperty.Register(nameof(EnableBracketMatching), typeof(bool), typeof(CodeEditor),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        [Category("Behavior.Advanced")]
        [DisplayName("Enable Bracket Matching")]
        [Description("Highlight matching brackets/braces when cursor is on one")]
        public bool EnableBracketMatching
        {
            get => (bool)GetValue(EnableBracketMatchingProperty);
            set => SetValue(EnableBracketMatchingProperty, value);
        }

        public static readonly DependencyProperty EnableAutoClosingBracketsProperty =
            DependencyProperty.Register(nameof(EnableAutoClosingBrackets), typeof(bool), typeof(CodeEditor),
                new FrameworkPropertyMetadata(true));

        [Category("Behavior.Advanced")]
        [DisplayName("Auto-Close Brackets")]
        [Description("Automatically insert closing bracket/brace when typing opening one")]
        public bool EnableAutoClosingBrackets
        {
            get => (bool)GetValue(EnableAutoClosingBracketsProperty);
            set => SetValue(EnableAutoClosingBracketsProperty, value);
        }

        public static readonly DependencyProperty EnableAutoClosingQuotesProperty =
            DependencyProperty.Register(nameof(EnableAutoClosingQuotes), typeof(bool), typeof(CodeEditor),
                new FrameworkPropertyMetadata(true));

        [Category("Behavior.Advanced")]
        [DisplayName("Auto-Close Quotes")]
        [Description("Automatically insert closing quote when typing opening quote")]
        public bool EnableAutoClosingQuotes
        {
            get => (bool)GetValue(EnableAutoClosingQuotesProperty);
            set => SetValue(EnableAutoClosingQuotesProperty, value);
        }

        public static readonly DependencyProperty SkipOverClosingCharProperty =
            DependencyProperty.Register(nameof(SkipOverClosingChar), typeof(bool), typeof(CodeEditor),
                new FrameworkPropertyMetadata(true));

        [Category("Behavior.Advanced")]
        [DisplayName("Skip Over Closing Char")]
        [Description("When a closing bracket/quote already exists at cursor, advance over it instead of inserting a duplicate.")]
        public bool SkipOverClosingChar
        {
            get => (bool)GetValue(SkipOverClosingCharProperty);
            set => SetValue(SkipOverClosingCharProperty, value);
        }

        public static readonly DependencyProperty WrapSelectionInPairsProperty =
            DependencyProperty.Register(nameof(WrapSelectionInPairs), typeof(bool), typeof(CodeEditor),
                new FrameworkPropertyMetadata(true));

        [Category("Behavior.Advanced")]
        [DisplayName("Wrap Selection In Pairs")]
        [Description("When text is selected and an opening bracket/quote is typed, surround the selection with the matching pair.")]
        public bool WrapSelectionInPairs
        {
            get => (bool)GetValue(WrapSelectionInPairsProperty);
            set => SetValue(WrapSelectionInPairsProperty, value);
        }

        // ===== BEHAVIOR - SCROLLING & PERFORMANCE =====

        public static readonly DependencyProperty EnableVirtualScrollingProperty =
            DependencyProperty.Register(nameof(EnableVirtualScrolling), typeof(bool), typeof(CodeEditor),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        [Category("Behavior.Scrolling")]
        [DisplayName("Enable Virtual Scrolling")]
        [Description("Use virtualization to handle large documents (100K+ lines) efficiently")]
        public bool EnableVirtualScrolling
        {
            get => (bool)GetValue(EnableVirtualScrollingProperty);
            set => SetValue(EnableVirtualScrollingProperty, value);
        }

        public static readonly DependencyProperty SmoothScrollingProperty =
            DependencyProperty.Register(nameof(SmoothScrolling), typeof(bool), typeof(CodeEditor),
                new FrameworkPropertyMetadata(false)); // Off by default — instant scroll matches HexEditor/MouseWheelSpeed DP.

        [Category("Behavior.Scrolling")]
        [DisplayName("Smooth Scrolling")]
        [Description("Enable smooth animated scrolling")]
        public bool SmoothScrolling
        {
            get => (bool)GetValue(SmoothScrollingProperty);
            set => SetValue(SmoothScrollingProperty, value);
        }

        public static readonly DependencyProperty ScrollSpeedMultiplierProperty =
            DependencyProperty.Register(nameof(ScrollSpeedMultiplier), typeof(double), typeof(CodeEditor),
                new FrameworkPropertyMetadata(1.0));

        [Category("Behavior.Scrolling")]
        [DisplayName("Scroll Speed Multiplier")]
        [Description("Multiplier for scroll speed (0.5 = slower, 3.0 = faster)")]
        [Range(0.5, 3.0, Step = 0.1)]
        public double ScrollSpeedMultiplier
        {
            get => (double)GetValue(ScrollSpeedMultiplierProperty);
            set => SetValue(ScrollSpeedMultiplierProperty, Math.Max(0.5, Math.Min(3.0, value)));
        }

        public static readonly DependencyProperty MouseWheelSpeedProperty =
            DependencyProperty.Register(nameof(MouseWheelSpeed), typeof(MouseWheelSpeed), typeof(CodeEditor),
                new FrameworkPropertyMetadata(MouseWheelSpeed.System));

        [Category("Behavior.Scrolling")]
        [DisplayName("Mouse Wheel Speed")]
        [Description("Lines scrolled per wheel notch. System = Windows setting (typically 3). VerySlow=1, Slow=3, Normal=5, Fast=7, VeryFast=9.")]
        public MouseWheelSpeed MouseWheelSpeed
        {
            get => (MouseWheelSpeed)GetValue(MouseWheelSpeedProperty);
            set => SetValue(MouseWheelSpeedProperty, value);
        }

        public static readonly DependencyProperty HorizontalScrollSensitivityProperty =
            DependencyProperty.Register(nameof(HorizontalScrollSensitivity), typeof(double), typeof(CodeEditor),
                new FrameworkPropertyMetadata(1.0));

        [Category("Behavior.Scrolling")]
        [DisplayName("Horizontal Scroll Sensitivity")]
        [Description("Sensitivity for horizontal scrolling (0.5 = less sensitive, 3.0 = more sensitive)")]
        [Range(0.5, 3.0, Step = 0.1)]
        public double HorizontalScrollSensitivity
        {
            get => (double)GetValue(HorizontalScrollSensitivityProperty);
            set => SetValue(HorizontalScrollSensitivityProperty, Math.Max(0.5, Math.Min(3.0, value)));
        }

        public static readonly DependencyProperty ScrollBarVisibilityModeProperty =
            DependencyProperty.Register(nameof(ScrollBarVisibilityMode), typeof(ScrollBarVisibility), typeof(CodeEditor),
                new FrameworkPropertyMetadata(ScrollBarVisibility.Auto));

        [Category("Behavior.Scrolling")]
        [DisplayName("Scroll Bar Visibility")]
        [Description("When to show scroll bars (Auto, Visible, Hidden, Disabled)")]
        public ScrollBarVisibility ScrollBarVisibilityMode
        {
            get => (ScrollBarVisibility)GetValue(ScrollBarVisibilityModeProperty);
            set => SetValue(ScrollBarVisibilityModeProperty, value);
        }

        public static readonly DependencyProperty RenderBufferProperty =
            DependencyProperty.Register(nameof(RenderBuffer), typeof(int), typeof(CodeEditor),
                new FrameworkPropertyMetadata(10, FrameworkPropertyMetadataOptions.AffectsRender));

        [Category("Behavior.Scrolling")]
        [DisplayName("Render Buffer (extra lines)")]
        [Description("Number of extra lines to render above/below viewport for smooth scrolling")]
        [Range(5, 50)]
        public int RenderBuffer
        {
            get => (int)GetValue(RenderBufferProperty);
            set => SetValue(RenderBufferProperty, Math.Max(5, Math.Min(50, value)));
        }

        public static readonly DependencyProperty MaxCachedLinesProperty =
            DependencyProperty.Register(nameof(MaxCachedLines), typeof(int), typeof(CodeEditor),
                new FrameworkPropertyMetadata(1000));

        [Category("Behavior.Scrolling")]
        [DisplayName("Max Cached Lines")]
        [Description("Maximum number of tokenized lines to keep in cache")]
        [Range(100, 10000)]
        public int MaxCachedLines
        {
            get => (int)GetValue(MaxCachedLinesProperty);
            set => SetValue(MaxCachedLinesProperty, Math.Max(100, Math.Min(10000, value)));
        }

        public static readonly DependencyProperty UseHardwareAccelerationProperty =
            DependencyProperty.Register(nameof(UseHardwareAcceleration), typeof(bool), typeof(CodeEditor),
                new FrameworkPropertyMetadata(true, OnUseHardwareAccelerationChanged));

        [Category("Behavior.Scrolling")]
        [DisplayName("Use Hardware Acceleration")]
        [Description("Enable GPU acceleration for rendering (recommended)")]
        public bool UseHardwareAcceleration
        {
            get => (bool)GetValue(UseHardwareAccelerationProperty);
            set => SetValue(UseHardwareAccelerationProperty, value);
        }

        // ===== SYNTAX HIGHLIGHTING COLORS =====

        public static readonly DependencyProperty SyntaxBraceColorProperty =
            DependencyProperty.Register(nameof(SyntaxBraceColor), typeof(Brush), typeof(CodeEditor),
                new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender,
                    OnSyntaxColorChanged));

        [Category("Syntax Highlighting")]
        [DisplayName("Brace Color { }")]
        [Description("Color for curly braces")]
        public Brush SyntaxBraceColor
        {
            get => (Brush)GetValue(SyntaxBraceColorProperty);
            set => SetValue(SyntaxBraceColorProperty, value);
        }

        public static readonly DependencyProperty SyntaxBracketColorProperty =
            DependencyProperty.Register(nameof(SyntaxBracketColor), typeof(Brush), typeof(CodeEditor),
                new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender,
                    OnSyntaxColorChanged));

        [Category("Syntax Highlighting")]
        [DisplayName("Bracket Color [ ]")]
        [Description("Color for square brackets")]
        public Brush SyntaxBracketColor
        {
            get => (Brush)GetValue(SyntaxBracketColorProperty);
            set => SetValue(SyntaxBracketColorProperty, value);
        }

        public static readonly DependencyProperty SyntaxKeyColorProperty =
            DependencyProperty.Register(nameof(SyntaxKeyColor), typeof(Brush), typeof(CodeEditor),
                new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(0, 0, 255)),
                    FrameworkPropertyMetadataOptions.AffectsRender, OnSyntaxColorChanged));

        [Category("Syntax Highlighting")]
        [DisplayName("Key Color")]
        [Description("Color for JSON property keys")]
        public Brush SyntaxKeyColor
        {
            get => (Brush)GetValue(SyntaxKeyColorProperty);
            set => SetValue(SyntaxKeyColorProperty, value);
        }

        public static readonly DependencyProperty SyntaxStringValueColorProperty =
            DependencyProperty.Register(nameof(SyntaxStringValueColor), typeof(Brush), typeof(CodeEditor),
                new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(163, 21, 21)),
                    FrameworkPropertyMetadataOptions.AffectsRender, OnSyntaxColorChanged));

        [Category("Syntax Highlighting")]
        [DisplayName("String Value Color")]
        [Description("Color for string values")]
        public Brush SyntaxStringValueColor
        {
            get => (Brush)GetValue(SyntaxStringValueColorProperty);
            set => SetValue(SyntaxStringValueColorProperty, value);
        }

        public static readonly DependencyProperty SyntaxNumberColorProperty =
            DependencyProperty.Register(nameof(SyntaxNumberColor), typeof(Brush), typeof(CodeEditor),
                new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(9, 134, 88)),
                    FrameworkPropertyMetadataOptions.AffectsRender, OnSyntaxColorChanged));

        [Category("Syntax Highlighting")]
        [DisplayName("Number Color")]
        [Description("Color for numeric values")]
        public Brush SyntaxNumberColor
        {
            get => (Brush)GetValue(SyntaxNumberColorProperty);
            set => SetValue(SyntaxNumberColorProperty, value);
        }

        public static readonly DependencyProperty SyntaxBooleanColorProperty =
            DependencyProperty.Register(nameof(SyntaxBooleanColor), typeof(Brush), typeof(CodeEditor),
                new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(0, 0, 255)),
                    FrameworkPropertyMetadataOptions.AffectsRender, OnSyntaxColorChanged));

        [Category("Syntax Highlighting")]
        [DisplayName("Boolean Color")]
        [Description("Color for true/false values")]
        public Brush SyntaxBooleanColor
        {
            get => (Brush)GetValue(SyntaxBooleanColorProperty);
            set => SetValue(SyntaxBooleanColorProperty, value);
        }

        public static readonly DependencyProperty SyntaxNullColorProperty =
            DependencyProperty.Register(nameof(SyntaxNullColor), typeof(Brush), typeof(CodeEditor),
                new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender,
                    OnSyntaxColorChanged));

        [Category("Syntax Highlighting")]
        [DisplayName("Null Color")]
        [Description("Color for null values")]
        public Brush SyntaxNullColor
        {
            get => (Brush)GetValue(SyntaxNullColorProperty);
            set => SetValue(SyntaxNullColorProperty, value);
        }

        public static readonly DependencyProperty SyntaxCommentColorProperty =
            DependencyProperty.Register(nameof(SyntaxCommentColor), typeof(Brush), typeof(CodeEditor),
                new FrameworkPropertyMetadata(Brushes.Green, FrameworkPropertyMetadataOptions.AffectsRender,
                    OnSyntaxColorChanged));

        [Category("Syntax Highlighting")]
        [DisplayName("Comment Color")]
        [Description("Color for // and /* */ comments")]
        public Brush SyntaxCommentColor
        {
            get => (Brush)GetValue(SyntaxCommentColorProperty);
            set => SetValue(SyntaxCommentColorProperty, value);
        }

        public static readonly DependencyProperty SyntaxKeywordColorProperty =
            DependencyProperty.Register(nameof(SyntaxKeywordColor), typeof(Brush), typeof(CodeEditor),
                new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(0, 0, 255)),
                    FrameworkPropertyMetadataOptions.AffectsRender, OnSyntaxColorChanged));

        [Category("Syntax Highlighting")]
        [DisplayName("Keyword Color")]
        [Description("Color for keywords (signature, field, conditional, loop, action)")]
        public Brush SyntaxKeywordColor
        {
            get => (Brush)GetValue(SyntaxKeywordColorProperty);
            set => SetValue(SyntaxKeywordColorProperty, value);
        }

        public static readonly DependencyProperty SyntaxValueTypeColorProperty =
            DependencyProperty.Register(nameof(SyntaxValueTypeColor), typeof(Brush), typeof(CodeEditor),
                new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(43, 145, 175)),
                    FrameworkPropertyMetadataOptions.AffectsRender, OnSyntaxColorChanged));

        [Category("Syntax Highlighting")]
        [DisplayName("Value Type Color")]
        [Description("Color for value types (uint8, int32, string, etc.)")]
        public Brush SyntaxValueTypeColor
        {
            get => (Brush)GetValue(SyntaxValueTypeColorProperty);
            set => SetValue(SyntaxValueTypeColorProperty, value);
        }

        public static readonly DependencyProperty SyntaxCalcExpressionColorProperty =
            DependencyProperty.Register(nameof(SyntaxCalcExpressionColor), typeof(Brush), typeof(CodeEditor),
                new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(128, 0, 128)),
                    FrameworkPropertyMetadataOptions.AffectsRender, OnSyntaxColorChanged));

        [Category("Syntax Highlighting")]
        [DisplayName("Calc Expression Color")]
        [Description("Color for calc: expressions")]
        public Brush SyntaxCalcExpressionColor
        {
            get => (Brush)GetValue(SyntaxCalcExpressionColorProperty);
            set => SetValue(SyntaxCalcExpressionColorProperty, value);
        }

        public static readonly DependencyProperty SyntaxVariableReferenceColorProperty =
            DependencyProperty.Register(nameof(SyntaxVariableReferenceColor), typeof(Brush), typeof(CodeEditor),
                new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(128, 0, 128)),
                    FrameworkPropertyMetadataOptions.AffectsRender, OnSyntaxColorChanged));

        [Category("Syntax Highlighting")]
        [DisplayName("Variable Reference Color")]
        [Description("Color for var: references")]
        public Brush SyntaxVariableReferenceColor
        {
            get => (Brush)GetValue(SyntaxVariableReferenceColorProperty);
            set => SetValue(SyntaxVariableReferenceColorProperty, value);
        }

        public static readonly DependencyProperty SyntaxErrorColorProperty =
            DependencyProperty.Register(nameof(SyntaxErrorColor), typeof(Brush), typeof(CodeEditor),
                new FrameworkPropertyMetadata(Brushes.Red, FrameworkPropertyMetadataOptions.AffectsRender,
                    OnSyntaxColorChanged));

        [Category("Syntax Highlighting")]
        [DisplayName("Error Color")]
        [Description("Color for syntax errors")]
        public Brush SyntaxErrorColor
        {
            get => (Brush)GetValue(SyntaxErrorColorProperty);
            set => SetValue(SyntaxErrorColorProperty, value);
        }

        public static readonly DependencyProperty SyntaxCommaColorProperty =
            DependencyProperty.Register(nameof(SyntaxCommaColor), typeof(Brush), typeof(CodeEditor),
                new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender,
                    OnSyntaxColorChanged));

        [Category("Syntax Highlighting")]
        [DisplayName("Comma Color")]
        [Description("Color for commas in JSON")]
        public Brush SyntaxCommaColor
        {
            get => (Brush)GetValue(SyntaxCommaColorProperty);
            set => SetValue(SyntaxCommaColorProperty, value);
        }

        public static readonly DependencyProperty SyntaxColonColorProperty =
            DependencyProperty.Register(nameof(SyntaxColonColor), typeof(Brush), typeof(CodeEditor),
                new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender,
                    OnSyntaxColorChanged));

        [Category("Syntax Highlighting")]
        [DisplayName("Colon Color")]
        [Description("Color for colons in JSON (key:value separator)")]
        public Brush SyntaxColonColor
        {
            get => (Brush)GetValue(SyntaxColonColorProperty);
            set => SetValue(SyntaxColonColorProperty, value);
        }

        public static readonly DependencyProperty SyntaxEscapeSequenceColorProperty =
            DependencyProperty.Register(nameof(SyntaxEscapeSequenceColor), typeof(Brush), typeof(CodeEditor),
                new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(215, 186, 125)),
                    FrameworkPropertyMetadataOptions.AffectsRender, OnSyntaxColorChanged));

        [Category("Syntax Highlighting")]
        [DisplayName("Escape Sequence Color")]
        [Description("Color for escape sequences in strings (\\n, \\t, \\u0000)")]
        public Brush SyntaxEscapeSequenceColor
        {
            get => (Brush)GetValue(SyntaxEscapeSequenceColorProperty);
            set => SetValue(SyntaxEscapeSequenceColorProperty, value);
        }

        public static readonly DependencyProperty SyntaxUrlColorProperty =
            DependencyProperty.Register(nameof(SyntaxUrlColor), typeof(Brush), typeof(CodeEditor),
                new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(0, 102, 204)),
                    FrameworkPropertyMetadataOptions.AffectsRender, OnSyntaxColorChanged));

        [Category("Syntax Highlighting")]
        [DisplayName("URL Color")]
        [Description("Color for URLs detected in string values")]
        public Brush SyntaxUrlColor
        {
            get => (Brush)GetValue(SyntaxUrlColorProperty);
            set => SetValue(SyntaxUrlColorProperty, value);
        }

        public static readonly DependencyProperty SyntaxDeprecatedColorProperty =
            DependencyProperty.Register(nameof(SyntaxDeprecatedColor), typeof(Brush), typeof(CodeEditor),
                new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(128, 128, 128)),
                    FrameworkPropertyMetadataOptions.AffectsRender, OnSyntaxColorChanged));

        [Category("Syntax Highlighting")]
        [DisplayName("Deprecated Color")]
        [Description("Color for deprecated properties or values (strikethrough)")]
        public Brush SyntaxDeprecatedColor
        {
            get => (Brush)GetValue(SyntaxDeprecatedColorProperty);
            set => SetValue(SyntaxDeprecatedColorProperty, value);
        }

        public static readonly DependencyProperty HighlightMatchColorProperty =
            DependencyProperty.Register(nameof(HighlightMatchColor), typeof(Brush), typeof(CodeEditor),
                new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromArgb(80, 255, 255, 0)),
                    FrameworkPropertyMetadataOptions.AffectsRender));

        [Category("Syntax Highlighting")]
        [DisplayName("Highlight Match Color")]
        [Description("Background color for matching brackets/words highlight")]
        public Brush HighlightMatchColor
        {
            get => (Brush)GetValue(HighlightMatchColorProperty);
            set => SetValue(HighlightMatchColorProperty, value);
        }

        public static readonly DependencyProperty FindResultColorProperty =
            DependencyProperty.Register(nameof(FindResultColor), typeof(Brush), typeof(CodeEditor),
                new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromArgb(100, 255, 165, 0)),
                    FrameworkPropertyMetadataOptions.AffectsRender));

        [Category("Syntax Highlighting")]
        [DisplayName("Find Result Color")]
        [Description("Background color for search/find results")]
        public Brush FindResultColor
        {
            get => (Brush)GetValue(FindResultColorProperty);
            set => SetValue(FindResultColorProperty, value);
        }

        #endregion

        #region Property Changed Callbacks

        private static void OnFontChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CodeEditor editor)
            {
                // Update typefaces
                var fontFamily = editor.EditorFontFamily;
                editor._typeface = new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
                editor._boldTypeface = new Typeface(fontFamily, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
                editor._baseFontSize = editor.EditorFontSize;
                editor._fontSize     = editor._baseFontSize * editor.ZoomLevel;

                // Recalculate character dimensions
                editor.CalculateCharacterDimensions();
                // Full layout pass: _maxContentWidth and scrollbar ranges depend on _charWidth / _lineHeight
                editor.InvalidateMeasure();
            }
        }

        private static void OnIndentSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CodeEditor editor && editor._document != null)
            {
                editor._document.IndentSize = (int)e.NewValue;
            }
        }

        private static void OnSyntaxColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CodeEditor editor && editor._highlighter != null)
            {
                // Update highlighter colors from DPs
                editor.UpdateSyntaxHighlighterColors();

                // Invalidate all line caches to force re-highlighting
                if (editor._document != null)
                {
                    editor._document.InvalidateAllCache();
                }

                editor.InvalidateVisual();
            }
        }

        private static void OnUseHardwareAccelerationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CodeEditor editor)
            {
                // Update RenderOptions hints for hardware acceleration
                bool useAcceleration = (bool)e.NewValue;

                if (useAcceleration)
                {
                    // Enable hardware acceleration hints
                    RenderOptions.SetBitmapScalingMode(editor, BitmapScalingMode.HighQuality);
                    RenderOptions.SetCachingHint(editor, CachingHint.Cache);
                }
                else
                {
                    // Disable caching hints (forces software rendering)
                    RenderOptions.SetBitmapScalingMode(editor, BitmapScalingMode.Linear);
                    RenderOptions.SetCachingHint(editor, CachingHint.Unspecified);
                }

                editor.InvalidateVisual();
            }
        }

        #endregion

        #region Constructor

        public CodeEditor()
        {
            // Prevent child visuals (LSP layers, caret DrawingVisual) from rendering
            // outside the control bounds when scroll / InlineHints push Y values past
            // the viewport.  The docking host does not clip its content presenter.
            ClipToBounds = true;

            // Initialize document
            _document = new CodeDocument();
            _selection = new TextSelection();

            // Subscribe to document changes
            _document.TextChanged += Document_TextChanged;

            // Initialize typefaces (will use DP values)
            UpdateTypefacesFromDPs();

            // Calculate character dimensions
            CalculateCharacterDimensions();

            // Initialize syntax highlighter (Phase 2)
            _highlighter = new CodeSyntaxHighlighter();
            UpdateSyntaxHighlighterColors();

            // Initialize undo engine — size from options (or default 500).
            _undoEngine.MaxHistorySize = 500; // updated via ApplyOptions when options are provided
            _undoEngine.MarkSaved();   // Initial state is clean.
            _undoEngine.StateChanged  += OnUndoEngineStateChanged;

            // Initialize SmartComplete popup (Phase 4)
            _smartCompletePopup = new SmartCompletePopup(this);

            // Initialize LSP Signature Help popup
            _signatureHelpPopup     = new SignatureHelpPopup(this);

            // Initialize Go-to-Symbol palette (Ctrl+T)
            _goToSymbolPopup = new GoToSymbolPopup();
            _goToSymbolPopup.NavigationRequested += OnGoToSymbolNavigation;

            // Initialize LSP Code Action popup (Ctrl+.) and Rename popup (F2)
            _lspCodeActionPopup     = new LspCodeActionPopup(this);
            _lspRenamePopup         = new LspRenamePopup(this);

            // Initialize validator (Phase 5)
            _validator = new FormatSchemaValidator();
            _validationTimer = new System.Windows.Threading.DispatcherTimer();
            _validationTimer.Interval = TimeSpan.FromMilliseconds(ValidationDelay);
            _validationTimer.Tick += ValidationTimer_Tick;

            // Folding debounce (P1-CE-01) — 500 ms after last keystroke
            _foldingDebounceTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _foldingDebounceTimer.Tick += (_, _) =>
            {
                _foldingDebounceTimer!.Stop();
                if (IsFoldingEnabled && _foldingEngine != null)
                    _foldingEngine.Analyze(_document.Lines);
            };

            // Background highlight pipeline (P1-CE-06) — invalidate & re-render on completion.
            // Suppressed while smooth-scroll is animating to avoid a double-render per frame:
            // timer-tick → InvalidateVisual → OnRender would already redraw; we only need
            // the highlights render once the viewport has settled.
            _highlightPipeline.HighlightsComputed += (_, _) =>
            {
                if (!_smoothScrollTimer.IsEnabled)
                    InvalidateVisual();
                MinimapRefreshRequested?.Invoke(this, EventArgs.Empty);
            };

            // Resolve theme-aware word highlight brushes (fallback to static defaults).
            _wordHighlightBg  = s_wordHighlightBgFallback;
            _wordHighlightPen = s_wordHighlightPenFallback;

            // Make focusable for keyboard input
            Focusable = true;
            FocusVisualStyle = null; // No focus rectangle

            // Set minimum size
            MinWidth = 200;
            MinHeight = 100;

            // Initialize Virtual Scrolling (Phase 11)
            InitializeVirtualScrolling();

            // Initialize Context Menu (Phase C)
            InitializeContextMenu();

            // Initialize caret blink timer
            // Render priority (7) keeps the blink tick above Background LSP diagnostic work (4)
            // so the caret fires reliably even during Roslyn workspace init bursts.
            _caretTimer = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Render);
            _caretTimer.Tick += CaretTimer_Tick;
            UpdateCaretBlinkTimer();

            // Multi-caret: any change → redraw so secondary carets appear immediately.
            _caretManager.CaretsChanged += (_, _) => InvalidateVisual();

            // Diagnostics → refresh scroll-marker panel tick marks whenever validation runs.
            DiagnosticsChanged += (_, _) => UpdateDiagnosticScrollMarkers();

            // Initialize smooth scroll timer
            _smoothScrollTimer = new System.Windows.Threading.DispatcherTimer();
            _smoothScrollTimer.Interval = TimeSpan.FromMilliseconds(16); // ~60 FPS
            _smoothScrollTimer.Tick += SmoothScrollTimer_Tick;

            // Auto-scroll timer: scrolls the viewport while the mouse is held outside
            // bounds during a drag-selection (50 ms → ~20 scroll steps/s).
            _autoScrollTimer = new System.Windows.Threading.DispatcherTimer();
            _autoScrollTimer.Interval = TimeSpan.FromMilliseconds(50);
            _autoScrollTimer.Tick += AutoScrollTimer_Tick;

            // Middle-click pan mode controller (shared with all scroll-capable editors)
            _panMode = new PanModeController(this,
                (dx, dy) => { ScrollVertical(dy); ScrollHorizontal(dx); });

            // Initialize ScrollBar visual children (vertical + horizontal)
            _scrollBarChildren = new VisualCollection(this);
            _vScrollBar = new System.Windows.Controls.Primitives.ScrollBar
            {
                Orientation = Orientation.Vertical,
                SmallChange  = _lineHeight,
                LargeChange  = 100,
                Minimum      = 0,
                Maximum      = 0,
                Value        = 0,
                Cursor       = Cursors.Arrow  // override parent IBeam
            };
            _hScrollBar = new System.Windows.Controls.Primitives.ScrollBar
            {
                Orientation = Orientation.Horizontal,
                SmallChange  = _charWidth * 3,
                LargeChange  = 100,
                Minimum      = 0,
                Maximum      = 0,
                Value        = 0,
                Cursor       = Cursors.Arrow  // override parent IBeam
            };
            _vScrollBar.ValueChanged += VScrollBar_ValueChanged;
            _hScrollBar.ValueChanged += HScrollBar_ValueChanged;
            _scrollBarChildren.Add(_vScrollBar);
            _scrollBarChildren.Add(_hScrollBar);

            // Initialize folding subsystem (B3).
            // CompositeFoldingStrategy combines brace folding ({}) with #region/#endregion directive folding.
            _foldingEngine = new FoldingEngine(
                new CompositeFoldingStrategy(
                    new BraceFoldingStrategy(),
                    new RegionDirectiveFoldingStrategy()));
            _gutterControl = new GutterControl();
            _gutterControl.SetEngine(_foldingEngine);
            // Fold state change: re-arrange (→ UpdateScrollBars corrects the range) and re-render content.
            // Gutter re-renders internally via its own RegionsChanged handler.
            _foldingEngine.RegionsChanged += (_, _) => { _linePositionsDirty = true; InvalidateMeasure(); InvalidateVisual(); MinimapRefreshRequested?.Invoke(this, EventArgs.Empty); UpdateDiagnosticScrollMarkers(); SyncScrollMarkerCaretAndSelection(); };
            _scrollBarChildren.Add(_gutterControl);

            // Breakpoint gutter (ADR-DBG-01): positioned to the left of fold markers.
            _breakpointGutterControl = new BreakpointGutterControl();
            _breakpointGutterControl.RightClickRequested += OnBreakpointRightClick;
            _breakpointGutterControl.HoverBreakpointRequested += OnGutterBreakpointHover;
            _scrollBarChildren.Add(_breakpointGutterControl);

            // Blame gutter (Phase 2C): 6px color bar left of the breakpoint gutter.
            _blameGutterControl = new BlameGutterControl { Visibility = System.Windows.Visibility.Collapsed };
            _scrollBarChildren.Add(_blameGutterControl);

            // Change-marker gutter (#166): 4px strip between blame and breakpoint gutters.
            _changeMarkerGutterControl = new ChangeMarkerGutterControl();
            _scrollBarChildren.Add(_changeMarkerGutterControl);
            _changeTracker.Changed += (_, map) =>
            {
                _changeMap = map;
                _changeMarkerGutterControl?.Update(
                    _lineHeight, _firstVisibleLine, _lastVisibleLine,
                    TopMargin, _lineYLookup, _changeMap);
            };

            // Initialize word-highlight scroll marker overlay.
            _codeScrollMarkerPanel = new CodeScrollMarkerPanel();
            _scrollBarChildren.Add(_codeScrollMarkerPanel); // renders on top of _vScrollBar

            // Sticky scroll header (#160) — floats above the text area.
            _stickyScrollHeader          = new StickyScrollHeader();
            _stickyScrollHeader.Opacity  = _stickyScrollOpacity;
            _stickyScrollHeader.ScopeClicked += OnStickyScrollScopeClicked;
            _scrollBarChildren.Add(_stickyScrollHeader);

            // LSP overlay layers: inlay hints (parameter names) and declaration hints (test runner hints).
            // Added before caret so caret composites on top.
            _scrollBarChildren.Add(_lspInlayHintsLayer);
            _scrollBarChildren.Add(_lspDeclarationHintsLayer);
            _scrollBarChildren.Add(_semanticTokensLayer);

            // Caret visual is always last so it composites on top of all content.
            _scrollBarChildren.Add(_caretVisual);

            // Debounce timer: update word highlights 250 ms after the caret stops moving.
            // Input priority (5) keeps the tick above Background LSP diagnostic dispatches (4)
            // so frequent diagnostic-driven renders don't starve the word highlight update.
            _wordHighlightTimer = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(250) };
            _wordHighlightTimer.Tick += (_, _) => { _wordHighlightTimer.Stop(); UpdateWordHighlights(); };

            // Attach InlineHints service to the initial document.
            _inlineHintsService.HintsDataRefreshed += OnLensDataRefreshed;
            _inlineHintsService.Attach(_document, _currentFilePath);

            // Initialize Quick Info and Ctrl+Click services.
            _hoverQuickInfoService                    = new Services.HoverQuickInfoService();
            _hoverQuickInfoService.QuickInfoResolved  += OnQuickInfoResolved;
            _ctrlClickService                         = new Services.CtrlClickNavigationService();
            _ctrlClickService.TargetResolved          += OnCtrlClickTargetResolved;

            // Fold peek timer — fires 1.5 s after the mouse settles over a fold label.
            _foldPeekTimer          = new System.Windows.Threading.DispatcherTimer
                                      { Interval = TimeSpan.FromMilliseconds(600) };
            _foldPeekTimer.Tick    += OnFoldPeekTimerTick;

            // End-of-block hint timer — fires after hover dwell on a closing token.
            _endBlockHintTimer       = new System.Windows.Threading.DispatcherTimer
                                       { Interval = TimeSpan.FromMilliseconds(600) }; // default; updated via EndOfBlockHintDelayMs DP
            _endBlockHintTimer.Tick += OnEndBlockHintTimerTick;

            // Apply theme resource bindings when connected to the visual tree
            Loaded += (_, _) =>
            {
                ApplyThemeResourceBindings();

                // Wire window-level mouse handlers so auto-scroll continues when the mouse
                // leaves the CodeEditor boundary (e.g. into docking tabs or the title bar).
                // Mouse.AddPreviewMouseMoveHandler uses tunneling — fires even when another
                // element has mouse capture, which is more reliable than relying solely on
                // CaptureMouse() across docking/WindowChrome containers.
                var window = Window.GetWindow(this);
                if (window != null)
                {
                    Mouse.AddPreviewMouseMoveHandler(window, OnWindowPreviewMouseMove);
                    Mouse.AddPreviewMouseUpHandler(window, OnWindowPreviewMouseUp);
                }
            };

            Unloaded += (_, _) =>
            {
                var window = Window.GetWindow(this);
                if (window != null)
                {
                    Mouse.RemovePreviewMouseMoveHandler(window, OnWindowPreviewMouseMove);
                    Mouse.RemovePreviewMouseUpHandler(window, OnWindowPreviewMouseUp);
                }
            };
        }

        private void OnLensDataRefreshed(object? sender, EventArgs e)
        {
            _hintsData           = _inlineHintsService.HintsData;
            RebuildVisibleHintsCount();
            _linePositionsDirty = true; // OPT-D: InlineHints data affects per-line Y offsets
            InvalidateVisual();
        }

        /// <summary>
        /// Returns true when <paramref name="lineIndex"/> has a InlineHints entry whose
        /// <see cref="WpfHexEditor.Editor.Core.InlineHintsSymbolKinds"/> flag matches the
        /// current <see cref="InlineHintsVisibleKinds"/> filter.
        /// </summary>
        private bool IsHintEntryVisible(int lineIndex)
        {
            if (!_hintsData.TryGetValue(lineIndex, out var entry)) return false;
            return (entry.Kind & InlineHintsVisibleKinds) != 0;
        }

        /// <summary>
        /// Recounts filtered InlineHints entries and caches the result in <see cref="_visibleHintsCount"/>.
        /// Must be called whenever <see cref="_hintsData"/> or <see cref="InlineHintsVisibleKinds"/> changes
        /// so that scrollbar height calculations use the correct filtered count.
        /// </summary>
        private void RebuildVisibleHintsCount()
        {
            var kinds = InlineHintsVisibleKinds;
            int count = 0;
            foreach (var entry in _hintsData.Values)
            {
                if ((entry.Kind & kinds) != 0) count++;
            }
            _visibleHintsCount = count;

            // Rebuild cumulative hint array for O(1) scroll-offset → line conversion.
            int lineCount = _document?.Lines.Count ?? 0;
            if (_hintsCumulative.Length != lineCount + 1)
                _hintsCumulative = new int[lineCount + 1];
            int cum = 0;
            for (int i = 0; i < lineCount; i++)
            {
                _hintsCumulative[i] = cum;
                if (_hintsData.TryGetValue(i, out var entry) && (entry.Kind & kinds) != 0)
                    cum++;
            }
            _hintsCumulative[lineCount] = cum;
        }

        /// <summary>
        /// Binds all color DPs to the active theme's CE_* / TE_* resource keys via DynamicResource.
        /// Called on Loaded so the element is connected to the application resource tree.
        /// Safe to call multiple times — subsequent calls just re-register the same keys.
        /// CE_* keys provide Code Editor–specific palette (VS Dark/Light per theme).
        /// TE_* keys are shared with TextEditor for tokens that have no CE_* equivalent.
        /// </summary>
        private void ApplyThemeResourceBindings()
        {
            // Viewport — use CE_* dedicated keys so the code editor tracks its own palette
            SetResourceReference(EditorBackgroundProperty,      "CE_Background");
            SetResourceReference(EditorForegroundProperty,      "CE_Foreground");
            SetResourceReference(LineNumberBackgroundProperty,  "CE_LineNumBg");
            SetResourceReference(LineNumberForegroundProperty,  "CE_LineNumFg");
            SetResourceReference(CurrentLineBackgroundProperty, "CE_CurrentLine");
            SetResourceReference(SelectionBackgroundProperty,          "CE_Selection");
            SetResourceReference(InactiveSelectionBackgroundProperty,  "CE_SelectionInactive");
            SetResourceReference(CaretColorProperty,                   "TE_CaretColor");

            // Syntax highlighting — CE_* for semantically named token colors
            SetResourceReference(SyntaxKeyColorProperty,              "CE_Identifier");
            SetResourceReference(SyntaxKeywordColorProperty,          "CE_Keyword");
            SetResourceReference(SyntaxBooleanColorProperty,          "CE_Keyword");
            SetResourceReference(SyntaxStringValueColorProperty,      "CE_String");
            SetResourceReference(SyntaxNumberColorProperty,           "CE_Number");
            SetResourceReference(SyntaxCommentColorProperty,          "CE_Comment");
            SetResourceReference(SyntaxValueTypeColorProperty,        "CE_Type");
            SetResourceReference(SyntaxCalcExpressionColorProperty,   "TE_Directive");
            SetResourceReference(SyntaxVariableReferenceColorProperty,"CE_Identifier");
            SetResourceReference(SyntaxEscapeSequenceColorProperty,   "CE_String");
            SetResourceReference(SyntaxUrlColorProperty,              "CE_Attribute");
            SetResourceReference(SyntaxBraceColorProperty,            "CE_Brace");
            SetResourceReference(SyntaxBracketColorProperty,          "CE_Bracket");
            SetResourceReference(SyntaxCommaColorProperty,            "CE_Operator");
            SetResourceReference(SyntaxColonColorProperty,            "CE_Operator");
            SetResourceReference(SyntaxNullColorProperty,             "CE_Keyword");
            SetResourceReference(SyntaxDeprecatedColorProperty,       "CE_Operator");
            SetResourceReference(SyntaxErrorColorProperty,            "CE_Error");

            // Apply themed scrollbar style — CodeEditor creates scrollbars in C# before being
            // attached to the visual tree, so the implicit ScrollBar style from the theme
            // ResourceDictionary is not resolved at construction time. Apply it explicitly here,
            // after the element is loaded and the App resource tree is accessible.
            if (TryFindResource(typeof(System.Windows.Controls.Primitives.ScrollBar)) is Style sbStyle)
            {
                if (_vScrollBar != null) _vScrollBar.Style = sbStyle;
                if (_hScrollBar != null) _hScrollBar.Style = sbStyle;
            }

            // Invalidate rainbow scope guide pen cache so it picks up new theme colors.
            _rainbowGuidePens = null;
            _rainbowGuideActivePens = null;

            // Resolve theme-aware word highlight brushes.
            RefreshWordHighlightBrushes();
        }

        /// <summary>
        /// Resolves word highlight brushes from theme resources (CE_WordHighlightBackground,
        /// CE_WordHighlightBorder). Falls back to static defaults when tokens are missing.
        /// </summary>
        private void RefreshWordHighlightBrushes()
        {
            _wordHighlightBg = TryFindResource("CE_WordHighlightBackground") as Brush
                               ?? s_wordHighlightBgFallback;

            var borderBrush = TryFindResource("CE_WordHighlightBorder") as Brush;
            if (borderBrush is not null)
            {
                var pen = new Pen(borderBrush, 1.0);
                if (pen.CanFreeze) pen.Freeze();
                _wordHighlightPen = pen;
            }
            else
            {
                _wordHighlightPen = s_wordHighlightPenFallback;
            }
        }

        /// <summary>
        /// Applies settings from <paramref name="options"/> to this editor instance.
        /// Font, display, and feature settings are applied immediately.
        /// Per-token color overrides in <see cref="CodeEditorOptions.SyntaxColorOverrides"/>
        /// take precedence over the active theme's CE_* resources for as long as the
        /// override is set; clearing an override reverts to the theme resource.
        /// </summary>
        public void ApplyOptions(CodeEditorOptions options)
        {
            if (options is null) return;
            _codeEditorOptions = options;

            // Font / display
            if (!string.IsNullOrEmpty(options.FontFamily))
                EditorFontFamily = new FontFamily(options.FontFamily);

            if (options.FontSize is > 6 and < 72)
                EditorFontSize = options.FontSize;

            ShowLineNumbers          = options.ShowLineNumbers;
            ShowScopeGuides          = options.ShowScopeGuides;
            FoldToggleOnDoubleClick  = options.FoldToggleOnDoubleClick;
            IsWordWrapEnabled        = options.WordWrap;
            EnableFindAllReferences  = options.EnableFindAllReferences;
            ClickableLinksEnabled    = options.ClickableLinksEnabled;
            ClickableEmailsEnabled   = options.ClickableEmailsEnabled;
            ShowInlineHints             = options.ShowInlineHints;
            InlineHintsVisibleKinds     = options.InlineHintsVisibleKinds;
            InlineHintsSource           = options.InlineHintsSource;
            ShowVarTypeHints            = options.ShowVarTypeHints;
            ShowLambdaReturnTypeHints   = options.ShowLambdaReturnTypeHints;
            ShowLspInlayHints           = options.ShowLspInlayHints;
            ShowLspDeclarationHints             = options.ShowLspDeclarationHints;
            EnableWordHighlight      = options.EnableWordHighlight;
            ShowEndOfBlockHint      = options.ShowEndOfBlockHint;
            EndOfBlockHintDelayMs   = options.EndOfBlockHintDelayMs;

            // Auto-indent
            AutoIndentMode = options.AutoIndentMode;

            // Auto-close / smart editing
            EnableAutoClosingBrackets = options.AutoClosingBrackets;
            EnableAutoClosingQuotes   = options.AutoClosingQuotes;
            SkipOverClosingChar       = options.SkipOverClosingChar;
            WrapSelectionInPairs      = options.WrapSelectionInPairs;

            // Bracket pair depth colorization (#162)
            BracketPairColorizationEnabled = options.BracketPairColorization;

            // Rainbow scope guides (bracket-colored folding lines)
            RainbowScopeGuidesEnabled = options.RainbowScopeGuides;

            // Color swatch preview (#168)
            ColorSwatchPreviewEnabled = options.ColorSwatchPreview;

            // Code formatting (#159)
            _formatOnSave              = options.FormatOnSave;
            _xmlAttributeIndentLevels  = options.XmlAttributeIndentLevels ?? 2;
            _xmlOneAttributePerLine    = options.XmlOneAttributePerLine   ?? false;

            // Whitespace markers
            _whitespaceMode = options.WhitespaceMode;

            // Sticky scroll (#160)
            _stickyScrollEnabled         = options.StickyScrollEnabled;
            _stickyScrollMaxLines        = options.StickyScrollMaxLines;
            _stickyScrollSyntaxHighlight = options.StickyScrollSyntaxHighlight;
            _stickyScrollClickToNavigate = options.StickyScrollClickToNavigate;
            _stickyScrollOpacity         = options.StickyScrollOpacity;
            _stickyScrollMinScopeLines   = options.StickyScrollMinScopeLines;
            if (_stickyScrollHeader != null)
            {
                _stickyScrollHeader.Opacity = _stickyScrollOpacity;
                UpdateStickyScrollHeader();
                InvalidateMeasure();
            }

            // Syntax color overrides — set local value to override the DynamicResource binding.
            // A null override clears the local value so DynamicResource (CE_*) takes effect again.
            foreach (var kind in Enum.GetValues<SyntaxTokenKind>())
            {
                var dp    = SyntaxTokenKindToColorProperty(kind);
                var color = options.GetOverride(kind);

                if (dp is null) continue;

                if (color.HasValue)
                    SetValue(dp, new SolidColorBrush(color.Value));
                else
                    ClearValue(dp);  // revert to DynamicResource (CE_*) binding
            }

            // Re-establish DynamicResource bindings for all syntax color DPs.
            // ClearValue() above removes any SetResourceReference binding that
            // ApplyThemeResourceBindings() previously set. Calling it again is
            // idempotent and ensures CE_* theme resources are live for all
            // non-overridden token kinds (overridden ones keep their SetValue).
            ApplyThemeResourceBindings();
        }

        /// <summary>
        /// Applies or removes a per-kind syntax colour override on this editor instance.
        /// Calling with a non-null <paramref name="color"/> sets the DP local value to the
        /// specified brush, which wins over the active theme resource.
        /// Calling with null clears the local value and re-binds the DP to the CE_* theme
        /// resource so the editor tracks theme changes again.
        /// </summary>
        public void SetSyntaxColorOverride(SyntaxTokenKind kind, Color? color)
        {
            var dp = SyntaxTokenKindToColorProperty(kind);
            if (dp is null) return;

            if (color.HasValue)
            {
                SetValue(dp, new SolidColorBrush(color.Value));
            }
            else
            {
                ClearValue(dp);
                // Re-bind to the CE_* theme resource so colour tracks theme changes.
                if (s_kindToResourceKey.TryGetValue(kind, out var key))
                    SetResourceReference(dp, key);
            }
        }

        /// <summary>
        /// Maps a <see cref="SyntaxTokenKind"/> to the corresponding color <see cref="DependencyProperty"/>.
        /// Returns null for token kinds that have no direct color DP.
        /// </summary>
        private static DependencyProperty? SyntaxTokenKindToColorProperty(SyntaxTokenKind kind) => kind switch
        {
            SyntaxTokenKind.Keyword    => SyntaxKeywordColorProperty,
            SyntaxTokenKind.String     => SyntaxStringValueColorProperty,
            SyntaxTokenKind.Number     => SyntaxNumberColorProperty,
            SyntaxTokenKind.Comment    => SyntaxCommentColorProperty,
            SyntaxTokenKind.Type       => SyntaxValueTypeColorProperty,
            SyntaxTokenKind.Identifier => SyntaxKeyColorProperty,
            SyntaxTokenKind.Operator   => SyntaxColonColorProperty,   // maps to operator-class DP
            SyntaxTokenKind.Bracket    => SyntaxBracketColorProperty,
            SyntaxTokenKind.Attribute  => SyntaxUrlColorProperty,      // attribute/annotation DP
            _                          => null
        };

        /// <summary>
        /// Maps each <see cref="SyntaxTokenKind"/> directly to the CE_* theme resource key
        /// used in <see cref="ResolveBrushForKind"/>.
        /// Using TryFindResource bypasses the DP local-value layer (SetValue/ClearValue),
        /// so ApplyOptions() overrides or ClearValue calls never corrupt syntax colors.
        /// </summary>
        private static readonly Dictionary<SyntaxTokenKind, string> s_kindToResourceKey =
            new()
            {
                { SyntaxTokenKind.Keyword,    "CE_Keyword"    },
                { SyntaxTokenKind.String,     "CE_String"     },
                { SyntaxTokenKind.Number,     "CE_Number"     },
                { SyntaxTokenKind.Comment,    "CE_Comment"    },
                { SyntaxTokenKind.Type,       "CE_Type"       },
                { SyntaxTokenKind.Identifier, "CE_Identifier" },
                { SyntaxTokenKind.Operator,   "CE_Operator"   },
                { SyntaxTokenKind.Bracket,    "CE_Bracket"    },
                { SyntaxTokenKind.Attribute,   "CE_Attribute"   },
                { SyntaxTokenKind.ControlFlow, "CE_ControlFlow" },
            };

        /// <summary>
        /// Resolves the live theme brush for a <see cref="SyntaxTokenKind"/> by calling
        /// <see cref="FrameworkElement.TryFindResource"/> with the matching CE_* key.
        /// This walks the logical tree (CodeEditor → Window → Application), guaranteeing
        /// the current theme color is returned regardless of any DP local-value interference
        /// (e.g. from <see cref="ApplyOptions"/> calling ClearValue or SetValue).
        /// Returns <see langword="null"/> for <see cref="SyntaxTokenKind.Default"/>,
        /// letting callers fall back to any baked-in brush stored on the token itself.
        /// </summary>
        private Brush? ResolveBrushForKind(SyntaxTokenKind kind)
            => s_kindToResourceKey.TryGetValue(kind, out var key)
                ? TryFindResource(key) as Brush
                : null;

        private void UpdateTypefacesFromDPs()
        {
            _typeface           = new Typeface(EditorFontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            _boldTypeface       = new Typeface(EditorFontFamily, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
            _lineNumberTypeface = new Typeface(LineNumberFontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            _baseFontSize       = EditorFontSize;
            _fontSize           = _baseFontSize * ZoomLevel;
        }

        #region Visual Children (ScrollBars)

        protected override int VisualChildrenCount => _scrollBarChildren?.Count ?? 0;

        protected override Visual GetVisualChild(int index) => _scrollBarChildren[index];

        #endregion

        private void UpdateSyntaxHighlighterColors()
        {
            if (_highlighter == null) return;

            _highlighter.DefaultColor = EditorForeground; // Default text color
            _highlighter.BraceColor = SyntaxBraceColor;
            _highlighter.BracketColor = SyntaxBracketColor;
            _highlighter.KeyColor = SyntaxKeyColor;
            _highlighter.StringValueColor = SyntaxStringValueColor;
            _highlighter.NumberColor = SyntaxNumberColor;
            _highlighter.BooleanColor = SyntaxBooleanColor;
            _highlighter.NullColor = SyntaxNullColor;
            _highlighter.CommentColor = SyntaxCommentColor;
            _highlighter.KeywordColor = SyntaxKeywordColor;
            _highlighter.ValueTypeColor = SyntaxValueTypeColor;
            _highlighter.CalcExpressionColor = SyntaxCalcExpressionColor;
            _highlighter.VariableReferenceColor = SyntaxVariableReferenceColor;
            _highlighter.ErrorColor = SyntaxErrorColor;

            // Phase 10.5: Additional syntax colors
            _highlighter.CommaColor = SyntaxCommaColor;
            _highlighter.ColonColor = SyntaxColonColor;

            // Phase 100%: Escape sequences, URLs, and deprecated keywords
            _highlighter.EscapeSequenceColor = SyntaxEscapeSequenceColor;
            _highlighter.UrlColor = SyntaxUrlColor;
            _highlighter.DeprecatedColor = SyntaxDeprecatedColor;
        }

        /// <summary>
        /// Update caret blink timer based on CaretBlinkRate DP
        /// </summary>
        private void UpdateCaretBlinkTimer()
        {
            if (_caretTimer == null) return;

            int blinkRate = CaretBlinkRate;

            if (blinkRate <= 0)
            {
                // No blinking - always visible
                _caretTimer.Stop();
                _caretVisible = true;
                InvalidateVisual();
            }
            else
            {
                _caretTimer.Interval = TimeSpan.FromMilliseconds(blinkRate);
                _caretTimer.Start();
                _caretVisible = true;
                InvalidateVisual();
            }
        }

        /// <summary>
        /// Redraws the caret DrawingVisual only — zero cost to the main OnRender pipeline.
        /// Must be called after every caret state change (blink toggle, position change, focus change).
        /// Also called at the end of OnRender to sync the caret over freshly rendered content.
        /// </summary>
        private void RenderCaretVisual()
        {
            if (_caretVisual is null || !IsLoaded) return;
            if (ActualWidth <= 0 || ActualHeight <= 0) return;

            using var dc = _caretVisual.RenderOpen();

            if (_document == null || _document.Lines.Count == 0)
                return;

            bool hasVBar = _vScrollBar?.Visibility == Visibility.Visible;
            bool hasHBar = _hScrollBar?.Visibility == Visibility.Visible;
            double contentW = Math.Max(0, ActualWidth  - (hasVBar ? ScrollBarThickness : 0));
            double contentH = Math.Max(0, ActualHeight - (hasHBar ? ScrollBarThickness : 0));
            double textLeft = ShowLineNumbers ? TextAreaLeftOffset : LeftMargin;

            // Mirror the exact clip + transform stack active when RenderCursor is called in OnRender.
            dc.PushClip(new System.Windows.Media.RectangleGeometry(
                new Rect(textLeft, 0, Math.Max(0, contentW - textLeft), contentH)));
            dc.PushTransform(new System.Windows.Media.TranslateTransform(-_horizontalScrollOffset, 0));

            RenderCursor(dc);

            dc.Pop(); // H-scroll transform
            dc.Pop(); // text-area clip
        }

        /// <summary>
        /// Caret blink timer tick handler — redraws only the caret DrawingVisual.
        /// Does NOT trigger InvalidateVisual on the main FrameworkElement.
        /// </summary>
        private void CaretTimer_Tick(object sender, EventArgs e)
        {
            _caretVisible = !_caretVisible;
            RenderCaretVisual();
        }

        /// <summary>
        /// Reset caret to visible and restart blink timer
        /// Called when user types or moves cursor
        /// </summary>
        private void ResetCaretBlink()
        {
            _caretVisible = true;
            if (_caretTimer != null && _caretTimer.IsEnabled)
            {
                _caretTimer.Stop();
                _caretTimer.Start();
            }
            RenderCaretVisual();
            SyncScrollMarkerCaretAndSelection();
        }

        /// <summary>
        /// Called from all cursor-movement sites (MoveCursor, mouse clicks) that do not
        /// trigger a full OnRender. Schedules the word-highlight debounce timer so the
        /// highlight updates without waiting for the next render frame.
        /// </summary>
        private void NotifyCursorMoved() => ScheduleWordHighlightUpdate();

        /// <summary>
        /// Pushes caret + selection positions to the scroll marker panel.
        /// Called when either changes — not from OnRender — to avoid per-frame panel redraws.
        /// </summary>
        private void SyncScrollMarkerCaretAndSelection()
        {
            if (_codeScrollMarkerPanel == null || _document == null) return;
            // BUG3-FIX: use visible (fold-compressed) indices so the caret and selection
            // markers on the scroll panel align with the scrollbar, which operates in
            // visible-line space (hidden lines are excluded from the pixel budget).
            bool hasSelection = !_selection.IsEmpty && _selection.NormalizedStart.Line != _selection.NormalizedEnd.Line;
            _codeScrollMarkerPanel.UpdateCaretAndSelection(
                PhysicalToVisibleLineIndex(_cursorLine),
                hasSelection ? PhysicalToVisibleLineIndex(_selection.NormalizedStart.Line) : -1,
                hasSelection ? PhysicalToVisibleLineIndex(_selection.NormalizedEnd.Line)   : -1,
                VisibleLineCount);
        }

        /// <summary>
        /// Smooth scroll timer tick handler - interpolates scroll position
        /// </summary>
        private void SmoothScrollTimer_Tick(object sender, EventArgs e)
        {
            if (!SmoothScrolling)
            {
                _smoothScrollTimer.Stop();
                return;
            }

            // Interpolate between current and target offset
            double diff = _targetScrollOffset - _currentScrollOffset;

            if (Math.Abs(diff) < 0.5)
            {
                // Close enough - snap to target and stop.
                _currentScrollOffset  = _targetScrollOffset;
                _verticalScrollOffset = _targetScrollOffset;
                _smoothScrollTimer.Stop();

                // Reset tracking so the next OnRender schedules a highlight pass for
                // the newly settled visible range (highlighting was suppressed during scroll).
                _lastHighlightFirst = -1;
                _lastHighlightLast  = -1;
            }
            else
            {
                // Interpolate (ease out)
                _currentScrollOffset += diff * SmoothScrollSpeed;
                _verticalScrollOffset = _currentScrollOffset;
            }

            // Update virtualization engine and repaint
            if (_virtualizationEngine != null)
            {
                _virtualizationEngine.ScrollOffset = _verticalScrollOffset;
                _virtualizationEngine.CalculateVisibleRange();
            }

            SyncVScrollBar();
            InvalidateVisual();
        }

        /// <summary>
        /// Auto-scroll timer tick handler.
        /// Fires at 50 ms intervals while the mouse is held outside the viewport
        /// during a drag-selection. Scrolls the viewport and extends the selection
        /// to the clamped text position matching the mouse location.
        /// </summary>
        private void AutoScrollTimer_Tick(object sender, EventArgs e)
        {
            if (!_isSelecting && !_isRectSelecting)
            {
                _autoScrollTimer.Stop();
                return;
            }

            double mouseY = _lastMousePosition.Y;

            // Scroll speed uses the same MouseWheelSpeed DP as the wheel handler so
            // the auto-scroll rate is consistent with the user's wheel preference.
            int    speedLines = MouseWheelSpeed == MouseWheelSpeed.System
                ? SystemParameters.WheelScrollLines
                : (int)MouseWheelSpeed;
            double maxDelta   = speedLines * _lineHeight;

            // Guarantee at least 1 line per tick then accelerate up to maxDelta over 80 px
            // of overshoot. The previous proportional formula (overshoot/height * max) produced
            // near-zero deltas when the mouse was just barely outside the viewport.
            double delta = 0;
            if (mouseY < 0)
            {
                double lines = Math.Clamp(1.0 + (-mouseY) / 80.0, 1.0, speedLines);
                delta = -lines * _lineHeight;
            }
            else if (mouseY > ActualHeight)
            {
                double lines = Math.Clamp(1.0 + (mouseY - ActualHeight) / 80.0, 1.0, speedLines);
                delta = lines * _lineHeight;
            }

            if (delta != 0)
                ScrollVertical(delta);

            // Extend selection to the clamped text position under the mouse.
            double clampedY = Math.Max(0, Math.Min(ActualHeight - 1, mouseY));
            var    textPos  = PixelToTextPosition(new Point(_lastMousePosition.X, clampedY));

            if (textPos != _selection.End)
            {
                _selection.End = textPos;
                _cursorLine    = textPos.Line;
                _cursorColumn  = textPos.Column;
                InvalidateVisual();
                NotifyCaretMovedIfChanged();
            }
        }

        #endregion

        #region Character Dimension Calculation

        /// <summary>
        /// Calculates character dimensions and (re)creates the <see cref="GlyphRunRenderer"/>
        /// for the current font, size and display DPI.
        /// </summary>
        private void CalculateCharacterDimensions()
        {
            double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            // Rebuild GlyphRunRenderer — this also caches char width / height
            // from the GlyphTypeface (fast path) or FormattedText (fallback).
            _glyphRenderer = new GlyphRunRenderer(
                _typeface, _boldTypeface, _fontSize, pixelsPerDip);

            _charWidth  = _glyphRenderer.CharWidth;
            _charHeight = _glyphRenderer.CharHeight;
            _lineHeight = (_charHeight + 2) * LineHeightMultiplier;

            // Font changed — line-number cache and GlyphRun cache are stale (P1-CE-03/05)
            _lineNumberCache.Clear();
            if (_document != null)
                foreach (var line in _document.Lines)
                { line.GlyphRunCache = null; line.IsGlyphCacheDirty = true; }
        }

        /// <summary>
        /// O(n) full rebuild of <see cref="_cachedMaxLineLength"/>. Called only at load time
        /// or document swap — not on every keystroke (P1-CE-02).
        /// </summary>
        private void RebuildMaxLineLength()
        {
            _cachedMaxLineLength = _document?.Lines.Count > 0
                ? _document.Lines.Max(l => l.Text.Length)
                : 0;
        }


        #endregion

        #region Context Menu (Phase C)

        /// <summary>
        /// Initialize context menu with standard editing commands
        /// </summary>
        private void InitializeContextMenu()
        {
            var contextMenu = new ContextMenu();

            // Cut
            var cutMenuItem = new MenuItem
            {
                Header           = "Cu_t",
                InputGestureText = "Ctrl+X",
                Command          = ApplicationCommands.Cut,
                CommandTarget    = this,
                Icon             = MakeMenuIcon("\uE74E")
            };
            contextMenu.Items.Add(cutMenuItem);

            // Copy
            var copyMenuItem = new MenuItem
            {
                Header           = "_Copy",
                InputGestureText = "Ctrl+C",
                Command          = ApplicationCommands.Copy,
                CommandTarget    = this,
                Icon             = MakeMenuIcon("\uE8C8")
            };
            contextMenu.Items.Add(copyMenuItem);

            // Paste
            var pasteMenuItem = new MenuItem
            {
                Header           = "_Paste",
                InputGestureText = "Ctrl+V",
                Command          = ApplicationCommands.Paste,
                CommandTarget    = this,
                Icon             = MakeMenuIcon("\uE9F5")
            };
            contextMenu.Items.Add(pasteMenuItem);

            // Separator
            contextMenu.Items.Add(new Separator());

            // Undo — stored as field so the Opened handler can update the header dynamically.
            _undoMenuItem = new MenuItem
            {
                Header           = "_Undo",
                InputGestureText = "Ctrl+Z",
                Command          = ApplicationCommands.Undo,
                CommandTarget    = this,
                Icon             = MakeMenuIcon("\uE7A7")
            };
            contextMenu.Items.Add(_undoMenuItem);

            // Redo
            _redoMenuItem = new MenuItem
            {
                Header           = "_Redo",
                InputGestureText = "Ctrl+Y / Ctrl+Shift+Z",
                Command          = ApplicationCommands.Redo,
                CommandTarget    = this,
                Icon             = MakeMenuIcon("\uE7A6")
            };
            contextMenu.Items.Add(_redoMenuItem);

            // Update dynamic headers just before the menu appears.
            contextMenu.Opened += (_, _) =>
            {
                if (_undoMenuItem != null)
                    _undoMenuItem.Header = CanUndo
                        ? $"_Undo ({(_sharedUndoEngine?.UndoCount ?? _undoEngine.UndoCount)})"
                        : "_Undo";
                if (_redoMenuItem != null)
                    _redoMenuItem.Header = CanRedo
                        ? $"_Redo ({(_sharedUndoEngine?.RedoCount ?? _undoEngine.RedoCount)})"
                        : "_Redo";
            };

            // Separator
            contextMenu.Items.Add(new Separator());

            // Select All
            var selectAllMenuItem = new MenuItem
            {
                Header           = CodeEditorResources.CodeEditor_ContextMenuSelectAll,
                InputGestureText = "Ctrl+A",
                Command          = ApplicationCommands.SelectAll,
                CommandTarget    = this,
                Icon             = MakeMenuIcon("\uE8B3")
            };
            contextMenu.Items.Add(selectAllMenuItem);

            // Delete
            var deleteMenuItem = new MenuItem
            {
                Header           = CodeEditorResources.CodeEditor_ContextMenuDelete,
                InputGestureText = "Del",
                Command          = ApplicationCommands.Delete,
                CommandTarget    = this,
                Icon             = MakeMenuIcon("\uE74D")
            };
            contextMenu.Items.Add(deleteMenuItem);

            // Separator
            contextMenu.Items.Add(new Separator());

            // Find
            var findMenuItem = new MenuItem
            {
                Header           = CodeEditorResources.CodeEditor_ContextMenuFind,
                InputGestureText = "Ctrl+F",
                Command          = ApplicationCommands.Find,
                CommandTarget    = this,
                Icon             = MakeMenuIcon("\uE721")
            };
            contextMenu.Items.Add(findMenuItem);

            // Replace
            var replaceMenuItem = new MenuItem
            {
                Header           = CodeEditorResources.CodeEditor_ContextMenuReplace,
                InputGestureText = "Ctrl+H",
                Command          = ApplicationCommands.Replace,
                CommandTarget    = this,
                Icon             = MakeMenuIcon("\uE8AB")
            };
            contextMenu.Items.Add(replaceMenuItem);

            // Separator
            contextMenu.Items.Add(new Separator());

            // Find All References (LSP)
            var findRefsMenuItem = new MenuItem
            {
                Header           = CodeEditorResources.CodeEditor_ContextMenuFindAllRefs,
                InputGestureText = "Shift+F12",
                Command          = FindAllReferencesCommand,
                CommandTarget    = this,
                Icon             = MakeMenuIcon("\uE8FD")
            };
            contextMenu.Items.Add(findRefsMenuItem);

            // Dynamically enable/disable the item when the menu opens.
            contextMenu.Opened += (_, _) =>
                findRefsMenuItem.IsEnabled = EnableFindAllReferences
                                             && _document is not null;

            // Quick Fix (LSP Code Actions)
            var quickFixMenuItem = new MenuItem
            {
                Header           = CodeEditorResources.CodeEditor_ContextMenuQuickFix,
                InputGestureText = "Ctrl+.",
                Icon             = MakeMenuIcon("\uE73E"),
            };
            quickFixMenuItem.Click += (_, _) => _ = ShowCodeActionsAsync();
            contextMenu.Items.Add(quickFixMenuItem);

            // Rename Symbol (LSP Rename)
            var renameMenuItem = new MenuItem
            {
                Header           = CodeEditorResources.CodeEditor_ContextMenuRenameSymbol,
                InputGestureText = "F2",
                Icon             = MakeMenuIcon("\uE70F"),
            };
            renameMenuItem.Click += (_, _) => _ = StartRenameAsync();
            contextMenu.Items.Add(renameMenuItem);

            // Go to Definition (F12)
            var goToDefMenuItem = new MenuItem
            {
                Header           = CodeEditorResources.CodeEditor_ContextMenuGoToDefinition,
                InputGestureText = "F12",
                Icon             = MakeMenuIcon("\uE8A9"),
            };
            goToDefMenuItem.Click += (_, _) => _ = GoToDefinitionAtCaretAsync();
            contextMenu.Items.Add(goToDefMenuItem);

            // Go to Implementation (Ctrl+F12)
            var goToImplMenuItem = new MenuItem
            {
                Header           = CodeEditorResources.CodeEditor_ContextMenuGoToImplementation,
                InputGestureText = "Ctrl+F12",
                Icon             = MakeMenuIcon("\uE8A9"),
            };
            goToImplMenuItem.Click += (_, _) => _ = GoToImplementationAtCaretAsync();
            contextMenu.Items.Add(goToImplMenuItem);

            // Peek Definition (Alt+F12)
            var peekDefMenuItem = new MenuItem
            {
                Header           = CodeEditorResources.CodeEditor_ContextMenuPeekDefinition,
                InputGestureText = "Alt+F12",
                Icon             = MakeMenuIcon("\uE7C3"),
            };
            peekDefMenuItem.Click += (_, _) => _ = ShowPeekDefinitionAsync();
            contextMenu.Items.Add(peekDefMenuItem);

            // Show Call Hierarchy (Shift+Alt+H)
            var callHierarchyMenuItem = new MenuItem
            {
                Header           = CodeEditorResources.CodeEditor_ContextMenuShowCallHierarchy,
                InputGestureText = "Shift+Alt+H",
                Icon             = MakeMenuIcon("\uE81E"),
            };
            callHierarchyMenuItem.Click += (_, _) => _ = PrepareCallHierarchyAtCaretAsync();
            contextMenu.Items.Add(callHierarchyMenuItem);

            // Show Type Hierarchy (Ctrl+Alt+F12)
            var typeHierarchyMenuItem = new MenuItem
            {
                Header           = CodeEditorResources.CodeEditor_ContextMenuShowTypeHierarchy,
                InputGestureText = "Ctrl+Alt+F12",
                Icon             = MakeMenuIcon("\uE8A9"),
            };
            typeHierarchyMenuItem.Click += (_, _) => _ = PrepareTypeHierarchyAtCaretAsync();
            contextMenu.Items.Add(typeHierarchyMenuItem);

            // Enable/disable LSP items based on whether a client is active.
            contextMenu.Opened += (_, _) =>
            {
                var lspActive = _lspClient is not null;
                quickFixMenuItem.IsEnabled       = lspActive;
                renameMenuItem.IsEnabled         = lspActive;
                goToDefMenuItem.IsEnabled        = lspActive;
                goToImplMenuItem.IsEnabled       = lspActive;
                peekDefMenuItem.IsEnabled        = lspActive;
                callHierarchyMenuItem.IsEnabled  = lspActive;
                typeHierarchyMenuItem.IsEnabled  = lspActive;
            };

            // Separator
            contextMenu.Items.Add(new Separator());

            // ── Formatting submenu ──────────────────────────────────────────────
            var formattingMenu = new MenuItem { Header = CodeEditorResources.CodeEditor_ContextMenuFormatting, Icon = MakeMenuIcon("\uE8E3") };

            // Format Document (Ctrl+K, Ctrl+D)
            var formatDocMenuItem = new MenuItem
            {
                Header           = CodeEditorResources.CodeEditor_ContextMenuFormatDocument,
                InputGestureText = "Ctrl+K, Ctrl+D",
                Icon             = MakeMenuIcon("\uE8E3")
            };
            formatDocMenuItem.Click += (_, _) => _ = FormatDocumentAsync();

            // Format Selection (Ctrl+K, Ctrl+F)
            var formatSelMenuItem = new MenuItem
            {
                Header           = CodeEditorResources.CodeEditor_ContextMenuFormatSelection,
                InputGestureText = "Ctrl+K, Ctrl+F",
                Icon             = MakeMenuIcon("\uE762")
            };
            formatSelMenuItem.Click += (_, _) => _ = FormatSelectionAsync();

            formattingMenu.Items.Add(formatDocMenuItem);
            formattingMenu.Items.Add(formatSelMenuItem);
            formattingMenu.Items.Add(new Separator());

            // Format JSON
            var formatJsonMenuItem = new MenuItem
            {
                Header           = CodeEditorResources.CodeEditor_ContextMenuFormatJson,
                InputGestureText = "Ctrl+Shift+F",
                Icon             = MakeMenuIcon("\uE70F")
            };
            formatJsonMenuItem.Click += FormatJsonMenuItem_Click;

            // Validate JSON
            var validateMenuItem = new MenuItem
            {
                Header           = CodeEditorResources.CodeEditor_ContextMenuValidateJson,
                InputGestureText = "F5",
                Icon             = MakeMenuIcon("\uE73E")
            };
            validateMenuItem.Click += ValidateMenuItem_Click;

            formattingMenu.Items.Add(formatJsonMenuItem);
            formattingMenu.Items.Add(validateMenuItem);
            formattingMenu.Items.Add(new Separator());

            // Options...
            var formattingOptionsMenuItem = new MenuItem
            {
                Header = CodeEditorResources.CodeEditor_ContextMenuFormattingOptions,
                Icon   = MakeMenuIcon("\uE713")   // Settings gear
            };
            formattingOptionsMenuItem.Click += (_, _) =>
                FormattingOptionsRequested?.Invoke(this, EventArgs.Empty);
            formattingMenu.Items.Add(formattingOptionsMenuItem);

            // Enable/disable formatting items based on edit state and selection.
            contextMenu.Opened += (_, _) =>
            {
                formatDocMenuItem.IsEnabled = !IsReadOnly;
                formatSelMenuItem.IsEnabled = !IsReadOnly && !_selection.IsEmpty;
            };

            contextMenu.Items.Add(formattingMenu);

            // Separator
            contextMenu.Items.Add(new Separator());

            // ── Outlining submenu — mirrors Visual Studio outlining menu ──
            var outlineMenu = new MenuItem { Header = CodeEditorResources.CodeEditor_ContextMenuOutlining };

            var miToggleCurrent = new MenuItem
            {
                Header           = CodeEditorResources.CodeEditor_ContextMenuToggleOutlining,
                InputGestureText = "Ctrl+M, Ctrl+M",
                Icon             = MakeMenuIcon("\uE8A0")
            };
            miToggleCurrent.Click += (_, _) => OutlineToggleCurrent();

            var miToggleAll = new MenuItem
            {
                Header           = CodeEditorResources.CodeEditor_ContextMenuToggleAllOutlining,
                InputGestureText = "Ctrl+M, Ctrl+L",
                Icon             = MakeMenuIcon("\uE8B7")
            };
            miToggleAll.Click += (_, _) => OutlineToggleAll();

            var miStop = new MenuItem
            {
                Header           = CodeEditorResources.CodeEditor_ContextMenuStopOutlining,
                InputGestureText = "Ctrl+M, Ctrl+P",
                Icon             = MakeMenuIcon("\uE711")
            };
            miStop.Click += (_, _) => OutlineStop();

            var miStopHiding = new MenuItem
            {
                Header           = CodeEditorResources.CodeEditor_ContextMenuStopHidingCurrent,
                InputGestureText = "Ctrl+M, Ctrl+U",
                Icon             = MakeMenuIcon("\uE7B3")
            };
            miStopHiding.Click += (_, _) => OutlineStopHidingCurrent();

            var miCollapseDefs = new MenuItem
            {
                Header           = CodeEditorResources.CodeEditor_ContextMenuCollapseToDefs,
                InputGestureText = "Ctrl+M, Ctrl+O",
                Icon             = MakeMenuIcon("\uE8C4")
            };
            miCollapseDefs.Click += (_, _) => OutlineCollapseToDefinitions();

            outlineMenu.Items.Add(miToggleCurrent);
            outlineMenu.Items.Add(miToggleAll);
            outlineMenu.Items.Add(new Separator());
            outlineMenu.Items.Add(miStop);
            outlineMenu.Items.Add(miStopHiding);
            outlineMenu.Items.Add(new Separator());
            outlineMenu.Items.Add(miCollapseDefs);

            // Enable the submenu only when folding is active.
            contextMenu.Opened += (_, _) => outlineMenu.IsEnabled = IsFoldingEnabled;

            contextMenu.Items.Add(outlineMenu);
            // ─────────────────────────────────────────────────────────────────────────

            // Word Wrap toggle
            contextMenu.Items.Add(new Separator());
            var miWordWrap = new MenuItem
            {
                Header           = CodeEditorResources.CodeEditor_ContextMenuWordWrap,
                IsCheckable      = true,
                InputGestureText = "Alt+Z",
                Icon             = MakeMenuIcon("\uE751")
            };
            miWordWrap.SetBinding(MenuItem.IsCheckedProperty,
                new System.Windows.Data.Binding(nameof(IsWordWrapEnabled)) { Source = this, Mode = System.Windows.Data.BindingMode.TwoWay });
            contextMenu.Items.Add(miWordWrap);

            // Column Rulers toggle
            var miColumnRulers = new MenuItem
            {
                Header      = CodeEditorResources.CodeEditor_ContextMenuColumnRulers,
                IsCheckable = true,
                Icon        = MakeMenuIcon("\uE745")
            };
            miColumnRulers.SetBinding(MenuItem.IsCheckedProperty,
                new System.Windows.Data.Binding(nameof(ShowColumnRulers)) { Source = this, Mode = System.Windows.Data.BindingMode.TwoWay });
            contextMenu.Items.Add(miColumnRulers);

            // Show Whitespace submenu (radio-style: None / Selection / Always)
            var wsMenu = new MenuItem { Header = CodeEditorResources.CodeEditor_ContextMenuShowWhitespace, Icon = MakeMenuIcon("\uE7C5") };
            var wsNone = new MenuItem { Header = CodeEditorResources.CodeEditor_ContextMenuWhitespaceNone,      IsCheckable = true };
            var wsSel  = new MenuItem { Header = CodeEditorResources.CodeEditor_ContextMenuWhitespaceSelection, IsCheckable = true };
            var wsAll  = new MenuItem { Header = CodeEditorResources.CodeEditor_ContextMenuWhitespaceAlways,    IsCheckable = true };

            wsNone.Click += (_, _) => { _whitespaceMode = Options.WhitespaceDisplayMode.None;      InvalidateVisual(); };
            wsSel.Click  += (_, _) => { _whitespaceMode = Options.WhitespaceDisplayMode.Selection;  InvalidateVisual(); };
            wsAll.Click  += (_, _) => { _whitespaceMode = Options.WhitespaceDisplayMode.Always;     InvalidateVisual(); };

            wsMenu.Items.Add(wsNone);
            wsMenu.Items.Add(wsSel);
            wsMenu.Items.Add(wsAll);

            contextMenu.Opened += (_, _) =>
            {
                wsNone.IsChecked = _whitespaceMode == Options.WhitespaceDisplayMode.None;
                wsSel.IsChecked  = _whitespaceMode == Options.WhitespaceDisplayMode.Selection;
                wsAll.IsChecked  = _whitespaceMode == Options.WhitespaceDisplayMode.Always;
            };
            contextMenu.Items.Add(wsMenu);

            // Refresh Highlights — Ctrl+Shift+R
            contextMenu.Items.Add(new Separator());
            var miRefreshHighlights = new MenuItem
            {
                Header           = CodeEditorResources.CodeEditor_ContextMenuRefreshHighlights,
                InputGestureText = "Ctrl+Shift+R",
                Icon             = MakeMenuIcon("\uE72C")
            };
            miRefreshHighlights.Click += (_, _) => RefreshHighlights();
            // RoutedUICommand.CanExecute cannot route back to the editor once the ContextMenu
            // takes focus. Update IsEnabled directly when the menu opens instead.
            contextMenu.Opened += (_, _) => miRefreshHighlights.IsEnabled = _currentFilePath is not null;
            contextMenu.Items.Add(miRefreshHighlights);

            // Re-analyze Folding
            var miReanalyzeFolding = new MenuItem
            {
                Header = CodeEditorResources.CodeEditor_ContextMenuReanalyzeFolding,
                Icon   = MakeMenuIcon("\uE8A0")
            };
            miReanalyzeFolding.Click += (_, _) => ReanalyzeFolding();
            contextMenu.Opened += (_, _) => miReanalyzeFolding.IsEnabled = IsFoldingEnabled && _currentFilePath is not null;
            contextMenu.Items.Add(miReanalyzeFolding);

            // Toggle Word Highlight
            var miWordHighlight = new MenuItem
            {
                Header    = CodeEditorResources.CodeEditor_ContextMenuWordHighlight,
                Icon      = MakeMenuIcon("\uE7C1"),  // Highlight glyph
                IsCheckable = true,
            };
            miWordHighlight.Click += (_, _) => EnableWordHighlight = miWordHighlight.IsChecked;
            contextMenu.Opened += (_, _) => miWordHighlight.IsChecked = EnableWordHighlight;
            contextMenu.Items.Add(miWordHighlight);

            // Set context menu
            ContextMenu = contextMenu;

            // Register command bindings
            RegisterContextMenuCommands();
        }

        /// <summary>
        /// Register command bindings for context menu commands
        /// </summary>
        private void RegisterContextMenuCommands()
        {
            // Cut — enabled for both normal and rectangular selection.
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Cut,
                (sender, e) => CutToClipboard(),
                (sender, e) => e.CanExecute = !IsReadOnly && (!_selection.IsEmpty || !_rectSelection.IsEmpty)));

            // Copy — enabled for both normal and rectangular selection.
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy,
                (sender, e) => CopyToClipboard(),
                (sender, e) => e.CanExecute = !_selection.IsEmpty || !_rectSelection.IsEmpty));

            // Paste — disabled when a rectangular selection is active (no block-paste support).
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Paste,
                (sender, e) => PasteFromClipboard(),
                (sender, e) => e.CanExecute = Clipboard.ContainsText() && _rectSelection.IsEmpty));

            // Undo
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Undo,
                (sender, e) => Undo(),
                (sender, e) => e.CanExecute = CanUndo));

            // Redo
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Redo,
                (sender, e) => Redo(),
                (sender, e) => e.CanExecute = CanRedo));

            // Select All
            CommandBindings.Add(new CommandBinding(ApplicationCommands.SelectAll,
                (sender, e) => SelectAll()));

            // Delete
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Delete,
                (sender, e) => { if (!_selection.IsEmpty) DeleteSelection(); else DeleteCharAfter(); },
                (sender, e) => e.CanExecute = !IsReadOnly));

            // Find (Ctrl+F) and Replace (Ctrl+H) are handled by CodeEditorSplitHost
            // via PreviewKeyDown → ShowSearch(), which binds the shared QuickSearchBar
            // to this editor through ISearchTarget. No modeless Window is needed here.

            // Find All References (Shift+F12) — works with or without an LSP client.
            CommandBindings.Add(new CommandBinding(
                FindAllReferencesCommand,
                async (_, _) => await FindAllReferencesAsync(),
                (_, e) => e.CanExecute = EnableFindAllReferences
                                         && _document is not null));

            // Select Next Occurrence (Ctrl+D) — multi-caret word selection.
            CommandBindings.Add(new CommandBinding(
                SelectNextOccurrenceCommand,
                (_, _) => SelectNextOccurrence(),
                (_, e) => e.CanExecute = _document is not null));

            // Refresh Highlights (Ctrl+Shift+R) — force clear + re-request all highlight layers.
            CommandBindings.Add(new CommandBinding(
                RefreshHighlightsCommand,
                (_, _) => RefreshHighlights(),
                (_, e) => e.CanExecute = _currentFilePath is not null));
        }

        /// <summary>
        /// Selects the next occurrence of the word at the caret (or the current selection text)
        /// and adds a secondary caret at that position. VS Code Ctrl+D behaviour.
        /// </summary>
        private void SelectNextOccurrence()
        {
            if (_document == null) return;

            string word = _selection.IsEmpty
                ? GetWordAtCursor()
                : _document.GetText(_selection.NormalizedStart, _selection.NormalizedEnd);

            if (string.IsNullOrEmpty(word)) return;

            // Start search from the current caret position (or end of the last caret).
            int startLine = _caretManager.IsMultiCaret
                ? _caretManager.Carets[^1].Line
                : _cursorLine;
            int startCol  = _caretManager.IsMultiCaret
                ? _caretManager.Carets[^1].Column + 1
                : _cursorColumn + 1;

            int totalLines = _document.Lines.Count;
            for (int pass = 0; pass < 2; pass++) // allow wrap-around once
            {
                for (int li = (pass == 0 ? startLine : 0);
                     li < totalLines;
                     li++)
                {
                    string lineText = _document.Lines[li].Text;
                    int searchFrom  = (pass == 0 && li == startLine) ? Math.Min(startCol, lineText.Length) : 0;
                    int idx         = lineText.IndexOf(word, searchFrom, StringComparison.Ordinal);

                    if (idx >= 0)
                    {
                        _caretManager.AddCaret(li, idx + word.Length);
                        // Also update primary caret to the new position.
                        _cursorLine   = li;
                        _cursorColumn = idx + word.Length;
                        EnsureCursorVisible();
                        NotifyCaretMovedIfChanged();
                        InvalidateVisual();
                        return;
                    }
                }
                // Wrap: restart from top on second pass.
                startLine = 0;
                startCol  = 0;
            }
        }

        private void FormatJsonMenuItem_Click(object sender, RoutedEventArgs e)
        {
            FormatJson();
        }

        private void ValidateMenuItem_Click(object sender, RoutedEventArgs e)
        {
            RunValidation();
        }

        private void ExecuteFind(string query)
        {
            _findResults.Clear();
            _findMatchLength = 0;
            if (string.IsNullOrEmpty(query) || _document == null)
            { InvalidateVisual(); return; }

            _findMatchLength = query.Length;
            for (int line = 0; line < _document.Lines.Count; line++)
            {
                var lineText = _document.Lines[line].Text;
                int col = 0;
                while (true)
                {
                    int idx = lineText.IndexOf(query, col, StringComparison.OrdinalIgnoreCase);
                    if (idx < 0) break;
                    _findResults.Add(new Models.TextPosition(line, idx));
                    col = idx + 1;
                }
            }
            InvalidateVisual();
        }

        /// <summary>
        /// Navigates to the next find result (wraps around).
        /// </summary>
        public void FindNext()
        {
            if (_findResults.Count == 0 && !string.IsNullOrEmpty(_lastFindQuery))
                ExecuteFind(_lastFindQuery);
            if (_findResults.Count == 0) return;
            _currentFindMatchIndex = (_currentFindMatchIndex + 1) % _findResults.Count;
            NavigateToFindMatch();
            SearchResultsChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Navigates to the previous find result (wraps around).
        /// </summary>
        public void FindPrevious()
        {
            if (_findResults.Count == 0 && !string.IsNullOrEmpty(_lastFindQuery))
                ExecuteFind(_lastFindQuery);
            if (_findResults.Count == 0) return;
            _currentFindMatchIndex = (_currentFindMatchIndex - 1 + _findResults.Count) % _findResults.Count;
            NavigateToFindMatch();
            SearchResultsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void NavigateToFindMatch()
        {
            if (_currentFindMatchIndex < 0 || _currentFindMatchIndex >= _findResults.Count) return;
            var match = _findResults[_currentFindMatchIndex];
            _cursorLine   = match.Line;
            _cursorColumn = match.Column + _findMatchLength;
            EnsureCursorVisible();
            InvalidateVisual();
        }

        private void ClearFind()
        {
            _findResults.Clear();
            _currentFindMatchIndex = -1;
            _findMatchLength = 0;
            InvalidateVisual();
        }

        #region ISearchTarget

        public event EventHandler? SearchResultsChanged;

        SearchBarCapabilities ISearchTarget.Capabilities =>
            SearchBarCapabilities.CaseSensitive | SearchBarCapabilities.Replace;

        int ISearchTarget.MatchCount        => _findResults.Count;
        int ISearchTarget.CurrentMatchIndex => _currentFindMatchIndex;

        void ISearchTarget.Find(string query, SearchTargetOptions options)
        {
            _lastFindQuery = query;
            ExecuteFind(query);
            if (_findResults.Count > 0)
            {
                _currentFindMatchIndex = 0;
                NavigateToFindMatch();
            }
            else
            {
                _currentFindMatchIndex = -1;
                InvalidateVisual();
            }
            SearchResultsChanged?.Invoke(this, EventArgs.Empty);
        }

        void ISearchTarget.FindNext()     => FindNext();
        void ISearchTarget.FindPrevious() => FindPrevious();

        void ISearchTarget.ClearSearch()
        {
            ClearFind();
            SearchResultsChanged?.Invoke(this, EventArgs.Empty);
        }

        void ISearchTarget.Replace(string replacement)
        {
            // Replace the current find match with the replacement text
            if (_currentFindMatchIndex < 0 || _currentFindMatchIndex >= _findResults.Count) return;
            var match = _findResults[_currentFindMatchIndex];
            _selection.Start = match;
            _selection.End   = new Models.TextPosition(match.Line, match.Column + _findMatchLength);
            DeleteSelection();
            foreach (var ch in replacement) InsertChar(ch);
            // Re-run find and advance to next match
            ExecuteFind(_lastFindQuery ?? string.Empty);
            if (_findResults.Count > 0)
            {
                _currentFindMatchIndex = Math.Min(_currentFindMatchIndex, _findResults.Count - 1);
                NavigateToFindMatch();
            }
            SearchResultsChanged?.Invoke(this, EventArgs.Empty);
        }

        void ISearchTarget.ReplaceAll(string replacement)
        {
            if (string.IsNullOrEmpty(_lastFindQuery)) return;
            ExecuteFind(_lastFindQuery);
            if (_findResults.Count == 0) return;

            // Iterate results in reverse to preserve column / line offsets
            for (int i = _findResults.Count - 1; i >= 0; i--)
            {
                var match = _findResults[i];
                _selection.Start = match;
                _selection.End   = new Models.TextPosition(match.Line, match.Column + _findMatchLength);
                DeleteSelection();
                _isInternalEdit = true; // suppress undo coalescing inside loop
                try { foreach (var ch in replacement) InsertChar(ch); }
                finally { _isInternalEdit = false; }
            }
            ClearFind();
            _isDirty = true;
            ModifiedChanged?.Invoke(this, EventArgs.Empty);
            SearchResultsChanged?.Invoke(this, EventArgs.Empty);
        }

        UIElement? ISearchTarget.GetCustomFiltersContent() => null;

        #endregion

        // -- Format JSON --------------------------------------------------

        private void FormatJson()
        {
            var text = GetText();
            try
            {
                using var jdoc = System.Text.Json.JsonDocument.Parse(text,
                    new System.Text.Json.JsonDocumentOptions { AllowTrailingCommas = true });
                var formatted = System.Text.Json.JsonSerializer.Serialize(
                    jdoc.RootElement,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

                if (formatted != text)
                {
                    LoadText(formatted);
                    _isDirty = true;
                    ModifiedChanged?.Invoke(this, EventArgs.Empty);
                }
                StatusMessage?.Invoke(this, "JSON formatted.");
            }
            catch (System.Text.Json.JsonException ex)
            {
                MessageBox.Show(string.Format(CodeEditorResources.CodeEditor_FormatJsonError, ex.Message),
                    CodeEditorResources.CodeEditor_FormatJsonErrorTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // -- Validate JSON ------------------------------------------------

        private void RunValidation()
        {
            var text = GetText();
            try
            {
                using var _ = System.Text.Json.JsonDocument.Parse(text,
                    new System.Text.Json.JsonDocumentOptions { AllowTrailingCommas = true });
                StatusMessage?.Invoke(this, CodeEditorResources.CodeEditor_ValidateJsonSuccess);
                MessageBox.Show(CodeEditorResources.CodeEditor_ValidateJsonSuccess, CodeEditorResources.CodeEditor_ValidateJsonTitle,
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (System.Text.Json.JsonException ex)
            {
                var msg = string.Format(CodeEditorResources.CodeEditor_ValidateJsonError, ex.Message);
                StatusMessage?.Invoke(this, msg);
                MessageBox.Show(msg, CodeEditorResources.CodeEditor_ValidateJsonErrorTitle,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        #endregion

        #region Virtual Scrolling (Phase 11)

        /// <summary>
        /// Initialize virtual scrolling engine
        /// </summary>
        private void InitializeVirtualScrolling()
        {
            _virtualizationEngine = new VirtualizationEngine
            {
                TotalLines = _document?.Lines.Count ?? 0,
                ViewportHeight = ActualHeight,
                LineHeight = _lineHeight,
                ScrollOffset = 0,
                RenderBuffer = RenderBuffer
            };

            // Subscribe to size changed for viewport updates
            SizeChanged += CodeEditor_SizeChanged;
        }

        private void CodeEditor_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_virtualizationEngine != null)
            {
                bool hasHBar = _hScrollBar?.Visibility == Visibility.Visible;
                // Refresh TotalLines in case this editor shares a document that was populated
                // after SetDocument() was called (e.g. secondary pane in split view where
                // OpenAsync runs after Loaded and only updates the primary editor).
                _virtualizationEngine.TotalLines = _document?.Lines.Count ?? 0;
                _virtualizationEngine.ViewportHeight = ActualHeight - (hasHBar ? ScrollBarThickness : 0);
                _virtualizationEngine.CalculateVisibleRange();
            }
            // Word wrap: rebuild map when size changes (viewport width affects wrap width).
            if (IsWordWrapEnabled) RebuildWrapMap();
            // Scrollbar ranges depend on viewport size — trigger layout pass.
            // InvalidateVisual() is required here: InvalidateArrange() alone does NOT call
            // OnRender, so after the first real SizeChanged (ActualHeight goes from 0 to N),
            // the visible-line range is recalculated but the canvas stays blank until the
            // next user interaction forces a repaint. (ADR-002)
            InvalidateArrange();
            InvalidateVisual();
        }

        /// <summary>
        /// Update virtualization engine when document changes
        /// </summary>
        private void UpdateVirtualization()
        {
            if (_virtualizationEngine == null || _document == null)
                return;

            _virtualizationEngine.TotalLines = _document.Lines.Count;
            _virtualizationEngine.LineHeight = _lineHeight;
            _virtualizationEngine.RenderBuffer = RenderBuffer;
            _virtualizationEngine.CalculateVisibleRange();

            // Word wrap: rebuild map after document content changes.
            if (IsWordWrapEnabled) RebuildWrapMap();
        }

        /// <summary>
        /// Scroll viewport vertically by pixel amount
        /// </summary>
        public void ScrollVertical(double delta)
        {
            if (_virtualizationEngine == null || !EnableVirtualScrolling)
                return;

            // delta is already speed * _lineHeight from the caller (MouseWheelSpeed controls line count).
            // Clamp against the scrollbar maximum (which includes VS-style padding)
            // instead of the VE's own TotalHeight (which does not).
            double maxV = _vScrollBar?.Maximum ?? double.MaxValue;
            double newOffset = Math.Max(0, Math.Min(_verticalScrollOffset + delta, maxV));

            if (SmoothScrolling)
            {
                // Smooth scrolling - animate to target
                _targetScrollOffset = newOffset;

                // Initialize current offset if first scroll
                if (_currentScrollOffset == 0 && _verticalScrollOffset == 0)
                    _currentScrollOffset = _verticalScrollOffset;

                // Start animation timer
                if (!_smoothScrollTimer.IsEnabled)
                    _smoothScrollTimer.Start();
            }
            else
            {
                // Instant scrolling - jump directly
                _verticalScrollOffset = newOffset;
                _currentScrollOffset = newOffset;
                _targetScrollOffset = newOffset;
                _virtualizationEngine.ScrollOffset = newOffset;
                _virtualizationEngine.CalculateVisibleRange();
                SyncVScrollBar();
                InvalidateVisual();
            }
        }

        /// <summary>
        /// Scrolls the viewport so the given 0-based line is at the top.
        /// Does NOT move the caret or clear selection.
        /// Used by the minimap for scroll-only interaction.
        /// </summary>
        public void ScrollViewToLine(int lineIndex)
        {
            if (_virtualizationEngine == null) return;
            var newOffset = _virtualizationEngine.ScrollToLine(lineIndex);
            _verticalScrollOffset = newOffset;
            _currentScrollOffset = newOffset;
            _targetScrollOffset = newOffset;
            _virtualizationEngine.ScrollOffset = newOffset;
            _virtualizationEngine.CalculateVisibleRange();
            SyncVScrollBar();
            InvalidateVisual();
            MinimapRefreshRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Maximum scroll offset in pixels. Matches <c>_vScrollBar.Maximum</c> which
        /// accounts for TopMargin, folded lines, and inline hints.
        /// Used by the minimap to match the scrollbar range exactly.
        /// </summary>
        public double MaxScrollOffset
            => _vScrollBar?.Maximum
               ?? Math.Max(0, _virtualizationEngine?.TotalHeight - _virtualizationEngine?.ViewportHeight ?? 0);

        /// <summary>
        /// Scrolls the viewport to an exact pixel offset.
        /// Does NOT move the caret. No line-boundary quantization.
        /// Used by the minimap for sub-line-precision scrolling.
        /// </summary>
        public void ScrollViewToOffset(double pixelOffset)
        {
            if (_virtualizationEngine == null) return;
            double maxOffset = MaxScrollOffset;
            var newOffset = Math.Clamp(pixelOffset, 0, maxOffset);
            _verticalScrollOffset = newOffset;
            _currentScrollOffset = newOffset;
            _targetScrollOffset = newOffset;
            _virtualizationEngine.ScrollOffset = newOffset;
            _virtualizationEngine.CalculateVisibleRange();
            SyncVScrollBar();
            InvalidateVisual();
            MinimapRefreshRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Scroll viewport horizontally by pixel amount (used by pan mode and Shift+Wheel).
        /// </summary>
        private void ScrollHorizontal(double delta)
        {
            if (_hScrollBar == null || IsWordWrapEnabled) return;

            double maxH = Math.Max(0, _maxContentWidth - (ActualWidth - TextAreaLeftOffset));
            _horizontalScrollOffset = Math.Max(0, Math.Min(_horizontalScrollOffset + delta, maxH));
            SyncHScrollBar();
            InvalidateVisual();
        }

        /// <summary>
        /// Ensure cursor line is visible in viewport
        /// </summary>
        private void EnsureCursorVisible()
        {
            // Horizontal scroll must always run, regardless of virtual scrolling mode.
            EnsureCursorColumnVisible();

            // Word wrap: scroll to the visual row of the caret rather than the logical line.
            if (IsWordWrapEnabled && _wrapOffsets.Length > _cursorLine && _charsPerVisualLine > 0)
            {
                int caretVisRow = _wrapOffsets[_cursorLine] + _cursorColumn / _charsPerVisualLine;
                double caretY   = caretVisRow * _lineHeight;
                bool hasHBar    = _hScrollBar?.Visibility == Visibility.Visible;
                double viewportH = ActualHeight - TopMargin - (hasHBar ? ScrollBarThickness : 0);
                if (caretY < _verticalScrollOffset)
                {
                    _verticalScrollOffset = Math.Max(0, caretY);
                    SyncVScrollBar();
                    InvalidateVisual();
                }
                else if (caretY + _lineHeight > _verticalScrollOffset + viewportH)
                {
                    _verticalScrollOffset = caretY + _lineHeight - viewportH;
                    SyncVScrollBar();
                    InvalidateVisual();
                }
                NotifyCaretMovedIfChanged();
                return;
            }

            if (_virtualizationEngine == null || !EnableVirtualScrolling)
            {
                NotifyCaretMovedIfChanged();
                return;
            }

            // BUG3-FIX: The VirtualizationEngine maps scroll offsets to lines via
            // (offset / lineHeight), treating every line as equidistant.  When folding
            // is active the scroll space is compressed: hidden lines are removed from the
            // pixel budget, so the visible-pixel position of physical line N is
            // (N - hiddenLinesBefore(N)) * lineHeight, NOT (N * lineHeight).
            // Passing the raw _cursorLine index to VE.EnsureLineVisible() therefore
            // produces an offset that is too large by (hiddenBefore * lineHeight), which
            // either freezes the viewport or scrolls past the actual cursor position.
            //
            // Fix: count visible lines before _cursorLine to compute the correct pixel Y,
            // then use that pixel directly for the viewport boundary check.
            double caretPixelY = ComputeVisiblePixelY(_cursorLine);
            bool hasHBarEcv = _hScrollBar?.Visibility == Visibility.Visible;
            double viewportHEcv = ActualHeight - TopMargin - (hasHBarEcv ? ScrollBarThickness : 0);

            double newOffset;
            if (caretPixelY < _verticalScrollOffset)
                newOffset = Math.Max(0, caretPixelY);
            else if (caretPixelY + _lineHeight > _verticalScrollOffset + viewportHEcv)
                newOffset = caretPixelY + _lineHeight - viewportHEcv;
            else
                newOffset = _verticalScrollOffset; // already visible — no scroll needed

            double maxV = _vScrollBar?.Maximum ?? double.MaxValue;
            newOffset = Math.Min(newOffset, maxV);

            if (Math.Abs(newOffset - _verticalScrollOffset) > 0.1)
            {
                _verticalScrollOffset = newOffset;
                _virtualizationEngine.ScrollOffset = newOffset;
                _virtualizationEngine.CalculateVisibleRange();
                SyncVScrollBar();
                InvalidateVisual();
            }

            NotifyCaretMovedIfChanged();
        }

        /// <summary>
        /// Returns the pixel Y position (from document top, in scroll-offset space) of
        /// <paramref name="physicalLine"/>, accounting for hidden (folded) lines.
        /// Each visible line contributes exactly <c>_lineHeight</c> pixels; hidden lines
        /// contribute nothing, matching the compressed scroll space used by the scrollbar.
        /// O(physicalLine) — only called from <see cref="EnsureCursorVisible"/>.
        /// </summary>
        private double ComputeVisiblePixelY(int physicalLine)
        {
            if (_foldingEngine == null || _foldingEngine.TotalHiddenLineCount == 0)
                return physicalLine * _lineHeight;

            int visibleBefore = 0;
            for (int i = 0; i < physicalLine && i < (_document?.Lines.Count ?? 0); i++)
            {
                if (!_foldingEngine.IsLineHidden(i))
                    visibleBefore++;
            }
            return visibleBefore * _lineHeight;
        }

        /// <summary>
        /// Converts a physical (document) line index to its visible (fold-compressed) line index.
        /// Hidden lines are skipped; the result is the 0-based rank of <paramref name="physicalLine"/>
        /// among non-hidden lines.  Returns the same value as the input when no folding is active.
        /// Used to align scroll-marker panel positions with the compressed scrollbar space.
        /// O(physicalLine).
        /// </summary>
        internal int PhysicalToVisibleLineIndex(int physicalLine)
        {
            if (_foldingEngine == null || _foldingEngine.TotalHiddenLineCount == 0)
                return physicalLine;

            int vis = 0;
            int count = Math.Min(physicalLine, _document?.Lines.Count - 1 ?? 0);
            for (int i = 0; i < count; i++)
            {
                if (!_foldingEngine.IsLineHidden(i))
                    vis++;
            }
            return vis;
        }

        /// <summary>
        /// Total number of visible (non-hidden) lines in the current document.
        /// Matches the scroll space denominator used by the scrollbar.
        /// </summary>
        internal int VisibleLineCount
            => Math.Max(1, (_document?.Lines.Count ?? 1) - (_foldingEngine?.TotalHiddenLineCount ?? 0));

        /// <summary>
        /// Returns the true Y pixel position of a line from the document top,
        /// accounting for InlineHints extra height. O(1) via <see cref="_hintsCumulative"/>.
        /// Does not account for folded lines (fold adjustments happen in CalculateVisibleLines).
        /// </summary>
        private double GetTrueLineY(int lineIndex)
        {
            int hintsAbove = (ShowInlineHints && lineIndex < _hintsCumulative.Length)
                ? _hintsCumulative[lineIndex]
                : 0;
            return TopMargin + lineIndex * _lineHeight + hintsAbove * HintLineHeight;
        }

        /// <summary>
        /// Inverse of <see cref="GetTrueLineY"/>: converts a scroll pixel offset
        /// to a line index using binary search on the cumulative hint array. O(log n).
        /// </summary>
        private int ScrollOffsetToLine(double offset)
        {
            int lineCount = _document?.Lines.Count ?? 0;
            if (lineCount == 0) return 0;
            int lo = 0, hi = lineCount - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (GetTrueLineY(mid) - TopMargin <= offset)
                    lo = mid;
                else
                    hi = mid - 1;
            }
            return lo;
        }

        /// <summary>
        /// Fires <see cref="CaretMoved"/> when the caret has moved to a different line.
        /// Call after every operation that may change <c>_cursorLine</c>.
        /// </summary>
        private void NotifyCaretMovedIfChanged()
        {
            if (_cursorLine != _lastNotifiedCursorLine)
            {
                _lastNotifiedCursorLine = _cursorLine;
                CaretMoved?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Scrolls to <paramref name="line"/> and places the caret at column 0.
        /// Used by the navigation bar ComboBox selection.
        /// </summary>
        public void NavigateToLine(int line)
        {
            if (_document == null || line < 0 || line >= _document.Lines.Count) return;
            _cursorLine   = line;
            _cursorColumn = 0;
            _selection.Clear();
            EnsureCursorVisible();
            InvalidateVisual();
        }

        /// <summary>
        /// <see cref="INavigableDocument"/> implementation.
        /// Accepts 1-based line/column (IDE convention) and converts to 0-based internal coords.
        /// Used by the host when navigating from the References popup or the Error List.
        /// </summary>
        void INavigableDocument.NavigateTo(int line, int column)
        {
            int zeroLine = Math.Max(0, line - 1);
            int zeroCol  = Math.Max(0, column - 1);
            if (_document == null || zeroLine >= _document.Lines.Count) return;

            _cursorLine   = zeroLine;
            _cursorColumn = Math.Min(zeroCol, _document.Lines[zeroLine].Length);
            _selection.Clear();
            EnsureCursorVisible();
            InvalidateVisual();
        }

        #endregion

        // ── Code Formatting public API (#159) ─────────────────────────────────────

        /// <summary>
        /// Formats the full document (Ctrl+K, Ctrl+D).
        /// LSP textDocument/formatting is tried first; falls back to StructuralFormatter.
        /// The change is applied as a single undoable transaction.
        /// </summary>
        public async System.Threading.Tasks.Task FormatDocumentAsync(
            System.Threading.CancellationToken ct = default)
        {
            if (IsReadOnly || _document is null) return;

            string original = GetText();

            // Build merged rules: whfmt base → user overrides
            var baseRules   = Language?.FormattingRules ?? new FormattingRules();
            var mergedRules = baseRules.WithOverrides(_codeEditorOptions?.BuildOverrides());
            bool insertSpaces = !mergedRules.UseTabs;
            int  tabSize      = mergedRules.IndentSize;

            string formatted = await _codeFormattingService
                .FormatDocumentAsync(
                    _currentFilePath ?? string.Empty,
                    original,
                    mergedRules,
                    _lspClient,
                    tabSize,
                    insertSpaces,
                    ct)
                .ConfigureAwait(true); // resume on UI thread

            if (formatted == original) return;

            using (_undoEngine.BeginTransaction(CodeEditorResources.CodeEditor_FormatDocumentTransaction))
            {
                SelectAll();
                DeleteSelection();
                _document.InsertText(new Models.TextPosition(0, 0), formatted);
            }

            _selection.Clear();
            _cursorLine   = 0;
            _cursorColumn = 0;
            EnsureCursorVisible();

            // Formatting is a bulk replace — skip the 500 ms debounce and re-analyse
            // folding immediately so toggle arrows and fold lines reflect the new structure.
            if (IsFoldingEnabled && _foldingEngine != null)
            {
                _foldingDebounceTimer?.Stop();
                _foldingEngine.Analyze(_document.Lines);
            }

            InvalidateMeasure();
            InvalidateVisual();
        }

        /// <summary>
        /// Formats only the current selection (Ctrl+K, Ctrl+F).
        /// Falls back to FormatDocumentAsync when there is no active selection.
        /// </summary>
        public async System.Threading.Tasks.Task FormatSelectionAsync(
            System.Threading.CancellationToken ct = default)
        {
            if (IsReadOnly || _document is null) return;

            if (_selection.IsEmpty)
            {
                await FormatDocumentAsync(ct).ConfigureAwait(true);
                return;
            }

            string original    = GetText();
            var    baseRules   = Language?.FormattingRules ?? new FormattingRules();
            var    mergedRules = baseRules.WithOverrides(_codeEditorOptions?.BuildOverrides());
            bool   insertSpaces = !mergedRules.UseTabs;
            int    tabSize     = mergedRules.IndentSize;
            var    start       = _selection.NormalizedStart;
            var    end         = _selection.NormalizedEnd;

            string formatted = await _codeFormattingService
                .FormatSelectionAsync(
                    _currentFilePath ?? string.Empty,
                    original,
                    start.Line, start.Column,
                    end.Line,   end.Column,
                    mergedRules,
                    _lspClient,
                    tabSize,
                    insertSpaces,
                    ct)
                .ConfigureAwait(true); // resume on UI thread

            if (formatted == original) return;

            using (_undoEngine.BeginTransaction(CodeEditorResources.CodeEditor_FormatSelectionTransaction))
            {
                // Replace full text; the service has already scoped the change.
                SelectAll();
                DeleteSelection();
                _document.InsertText(new Models.TextPosition(0, 0), formatted);
            }

            _selection.Clear();

            // Formatting is a bulk replace — skip the 500 ms debounce and re-analyse
            // folding immediately so toggle arrows and fold lines reflect the new structure.
            if (IsFoldingEnabled && _foldingEngine != null)
            {
                _foldingDebounceTimer?.Stop();
                _foldingEngine.Analyze(_document.Lines);
            }

            InvalidateMeasure();
            InvalidateVisual();
        }

        // ── Sticky Scroll public API ───────────────────────────────────────────

        /// <summary>
        /// Applies sticky-scroll settings from the host (MainWindow/options page).
        /// </summary>
        public void ApplyStickyScrollSettings(
            bool enabled, int maxLines, bool syntaxHighlight,
            bool clickToNavigate, double opacity, int minScopeLines)
        {
            _stickyScrollEnabled         = enabled;
            _stickyScrollMaxLines        = Math.Clamp(maxLines, 1, 10);
            _stickyScrollSyntaxHighlight = syntaxHighlight;
            _stickyScrollClickToNavigate = clickToNavigate;
            _stickyScrollOpacity         = Math.Clamp(opacity, 0.5, 1.0);
            _stickyScrollMinScopeLines   = Math.Clamp(minScopeLines, 2, 20);

            if (_stickyScrollHeader != null)
            {
                _stickyScrollHeader.Opacity = _stickyScrollOpacity;
                UpdateStickyScrollHeader();
                InvalidateMeasure();
            }
        }

        private void OnStickyScrollScopeClicked(object? sender, int startLine)
            => NavigateToLine(startLine);

    }

    /// <summary>
    /// Event arguments raised when the user clicks a colour swatch in the editor.
    /// </summary>
    public sealed class ColorSwatchClickedEventArgs : EventArgs
    {
        /// <summary>The colour value shown by the swatch that was clicked.</summary>
        public System.Windows.Media.Color Color { get; }

        /// <summary>0-based document line index of the colour literal.</summary>
        public int Line { get; }

        internal ColorSwatchClickedEventArgs(System.Windows.Media.Color color, int line)
        {
            Color = color;
            Line  = line;
        }
    }
}
