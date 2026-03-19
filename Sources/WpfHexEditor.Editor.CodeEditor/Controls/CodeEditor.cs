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
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.CodeEditor.Options;
using WpfHexEditor.ProjectSystem.Languages;
using WpfHexEditor.Editor.CodeEditor.Selection;
using WpfHexEditor.Editor.CodeEditor.Input;

namespace WpfHexEditor.Editor.CodeEditor.Controls
{
    /// <summary>
    /// High-performance JSON text editor using custom rendering (FrameworkElement).
    /// Phase 1: Basic text display + keyboard input + line numbers
    /// Phase 2: Syntax highlighting with CodeSyntaxHighlighter
    /// Future phases will add: IntelliSense, validation
    /// </summary>
    public class CodeEditor : FrameworkElement, IDocumentEditor, IDiagnosticSource, IPropertyProviderSource, IOpenableDocument, INavigableDocument, IStatusBarContributor, ISearchTarget, IEditorPersistable
    {
        #region Fields - Document Model

        private CodeDocument _document;
        private int _cursorLine = 0;        // Current cursor line (0-based)
        private int _cursorColumn = 0;      // Current cursor column (0-based)
        private TextSelection _selection;   // Current text selection
        private int _lastNotifiedCursorLine = -1; // Tracks last CaretMoved notification line

        #endregion

        #region Fields - Syntax Highlighting (Phase 2)

        private CodeSyntaxHighlighter _highlighter;

        // URL hit-zones: rebuilt on every render pass; used for cursor + Ctrl+Click.
        private readonly List<UrlHitZone> _urlHitZones = new();

        // Fold-label hit-zones: rebuilt on every render pass; used for click-to-toggle.
        private readonly List<(Rect rect, int line)> _foldLabelHitZones = new();

        // Fold peek — VS-style hover preview of collapsed region content.
        private FoldPeekPopup?   _foldPeekPopup;
        private System.Windows.Threading.DispatcherTimer? _foldPeekTimer;
        private int              _foldPeekTargetLine = -1;

        // The URL zone currently under the mouse pointer (null = none).
        // Drives hover underline; changing it triggers InvalidateVisual().
        private UrlHitZone? _hoveredUrlZone;

        // Explicit tooltip object opened/closed in OnMouseMove.
        // Using ToolTip directly (instead of the ToolTip property) ensures the tooltip
        // appears even when the mouse is already inside the CodeEditor control.
        private ToolTip? _urlTooltip;

        // Compiled URL regex — re-used across all render passes (thread-safe read-only after init).
        private static readonly Regex s_urlRegex = new(
            @"https?://[^\s""'<>\[\]{}|\\^`]+",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

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

        private static void OnExternalHighlighterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not CodeEditor editor) return;

            // Reset block-comment state carried over from the previous file.
            if (e.OldValue is ISyntaxHighlighter old) old.Reset();
            if (e.NewValue is ISyntaxHighlighter h)   h.Reset();

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
        private string? _currentFilePath;     // IDocumentEditor: last saved file path

        // Context-menu items that need dynamic headers (Undo (3) / Redo (0)).
        private System.Windows.Controls.MenuItem? _undoMenuItem;
        private System.Windows.Controls.MenuItem? _redoMenuItem;

        #endregion

        #region Fields - Mouse Selection (Phase 3)

        private bool _isSelecting = false;
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

        #endregion

        #region Fields - IntelliSense (Phase 4)

        private IntelliSensePopup _intelliSensePopup;
        private bool _enableIntelliSense = true;

        #endregion

        #region Fields - LSP Client (Phase 4 — LSP Integration)

        // Optional LSP client injected by the IDE host (via SetLspClient).
        // Null when no language server is configured for the current language.
        private WpfHexEditor.Editor.Core.LSP.ILspClient? _lspClient;

        // Monotonically increasing document version sent with every didChange notification.
        private int _lspDocVersion;

        // Debounce timer for textDocument/didChange — 300 ms after last keystroke.
        private System.Windows.Threading.DispatcherTimer? _lspChangeTimer;

        // Inline "Find All References" popup (lazily created on first use).
        private ReferencesPopup?           _referencesPopup;

        // Last reference results — used when the user pins the popup to a docked panel.
        private List<ReferenceGroup> _lastReferenceGroups = new();
        private string               _lastReferenceSymbol = string.Empty;

        // ── CodeLens ──────────────────────────────────────────────────────────
        private readonly Services.CodeLensService                                                                                              _codeLensService  = new();
        private          IReadOnlyDictionary<int, (int Count, string Symbol, string IconGlyph, System.Windows.Media.Brush IconBrush, WpfHexEditor.Editor.Core.CodeLensSymbolKinds Kind)> _lensData = new Dictionary<int, (int, string, string, System.Windows.Media.Brush, WpfHexEditor.Editor.Core.CodeLensSymbolKinds)>();
        private          int                                                                                                                   _visibleLensCount = 0;
        private readonly List<(Rect Zone, int LineIndex, string Symbol)>                                                                       _lensHitZones     = new();
        private readonly List<(int LineIndex, double Y)>                                                                                       _visLinePositions = new();
        private readonly Dictionary<int, double>                                                                                               _lineYLookup      = new();
        private          int                                                                                                                   _hoveredLensLine  = -1;

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

        // Word-under-caret highlight assets (VS Code "read highlight" style).
        private static readonly Brush s_wordHighlightBg  = MakeFrozenBrush(Color.FromArgb(26, 86, 156, 214));
        private static readonly Pen   s_wordHighlightPen = MakeFrozenPen(Color.FromArgb(180, 86, 156, 214), 1.0);

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
        private double _verticalScrollOffset = 0;
        private double _horizontalScrollOffset = 0;
        private double _maxContentWidth = 0;

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

        // Folding support (Phase B3).
        private FoldingEngine?  _foldingEngine;
        private GutterControl?  _gutterControl;

        // True between the first Ctrl+M press and the second chord key (outlining commands).
        private bool _outlineChordPending;

        // 500ms folding debounce timer (P1-CE-01) — prevents O(n) scan on every keystroke
        private System.Windows.Threading.DispatcherTimer? _foldingDebounceTimer;

        // Incremental max-width tracking (P1-CE-02) — O(1) on growth, O(n) only on shrink
        private int _cachedMaxLineLength;

        // Per-line-number FormattedText cache (P1-CE-03) — eliminates 2,400 allocs/s at 60Hz
        private readonly Dictionary<int, FormattedText> _lineNumberCache = new();
        private Typeface? _cachedLineNumberTypeface;
        private double _cachedLineNumberFontSize = -1;

        // Background highlight pipeline (P1-CE-06)
        private readonly Services.HighlightPipelineService _highlightPipeline = new();
        // Last visible range that was submitted to the pipeline — avoids re-scheduling when unchanged.
        private int _lastHighlightFirst = -1;
        private int _lastHighlightLast  = -1;

        private int _firstVisibleLine = 0;  // Scrolling support (Phase 1: always 0)
        private int _lastVisibleLine = 0;   // Will be calculated in Phase 1

        // OPT-D: lineYLookup dirty flag — avoids rebuilding per-line Y positions on every
        // render frame (e.g. caret blink at 530 ms).  Rebuilt only when the visible range,
        // CodeLens data, or folding regions actually change.
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
        private const double ScrollBarThickness = 12.0;
        private const double SelectionCornerRadius = 3.0;

        // Extra vertical space reserved at the top of each line slot for CodeLens hints.
        private const double LensLineHeight = 16.0;

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

        public static readonly DependencyProperty EnableIntelliSenseProperty =
            DependencyProperty.Register(nameof(EnableIntelliSense), typeof(bool), typeof(CodeEditor),
                new FrameworkPropertyMetadata(true));

        /// <summary>
        /// Enable IntelliSense context-aware autocomplete
        /// </summary>
        [Category("Features")]
        [DisplayName("Enable IntelliSense")]
        [Description("Enable context-aware autocomplete suggestions (Ctrl+Space to trigger manually)")]
        public bool EnableIntelliSense
        {
            get => (bool)GetValue(EnableIntelliSenseProperty);
            set
            {
                SetValue(EnableIntelliSenseProperty, value);
                _enableIntelliSense = value;
            }
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

        public static readonly DependencyProperty ShowCodeLensProperty =
            DependencyProperty.Register(nameof(ShowCodeLens), typeof(bool), typeof(CodeEditor),
                new FrameworkPropertyMetadata(true,
                    FrameworkPropertyMetadataOptions.AffectsRender,
                    OnShowCodeLensChanged));

        [Category("Features")]
        [DisplayName("Show Code Lens")]
        [Description("Shows inline reference counts above each declaration.")]
        public bool ShowCodeLens
        {
            get => (bool)GetValue(ShowCodeLensProperty);
            set => SetValue(ShowCodeLensProperty, value);
        }

        private static void OnShowCodeLensChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not CodeEditor ce) return;
            // Trigger layout/scrollbar recalculation — _lineHeight is no longer affected by CodeLens.
            // Only visible-line Y positions change (per-declaration extra space), handled in OnRender.
            ce.InvalidateMeasure();
        }

        public static readonly DependencyProperty CodeLensVisibleKindsProperty =
            DependencyProperty.Register(
                nameof(CodeLensVisibleKinds),
                typeof(WpfHexEditor.Editor.Core.CodeLensSymbolKinds),
                typeof(CodeEditor),
                new FrameworkPropertyMetadata(
                    WpfHexEditor.Editor.Core.CodeLensSymbolKinds.All,
                    FrameworkPropertyMetadataOptions.AffectsRender,
                    OnCodeLensVisibleKindsChanged));

        [Category("Features")]
        [DisplayName("Code Lens Visible Kinds")]
        [Description("Bitmask of symbol kinds for which inline reference-count hints are displayed.")]
        public WpfHexEditor.Editor.Core.CodeLensSymbolKinds CodeLensVisibleKinds
        {
            get => (WpfHexEditor.Editor.Core.CodeLensSymbolKinds)GetValue(CodeLensVisibleKindsProperty);
            set => SetValue(CodeLensVisibleKindsProperty, value);
        }

        private static void OnCodeLensVisibleKindsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not CodeEditor ce) return;
            ce.RebuildVisibleLensCount();
            ce.InvalidateMeasure();
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

        // ── Language Definition DP ─────────────────────────────────────────────

        public static readonly DependencyProperty LanguageProperty =
            DependencyProperty.Register(nameof(Language), typeof(LanguageDefinition), typeof(CodeEditor),
                new FrameworkPropertyMetadata(null,
                    FrameworkPropertyMetadataOptions.AffectsRender,
                    OnLanguageChanged));

        [Category("Features")]
        [DisplayName("Language Definition")]
        [Description("Active language definition controlling CodeLens and Ctrl+Click navigation per language.")]
        public LanguageDefinition? Language
        {
            get => (LanguageDefinition?)GetValue(LanguageProperty);
            set => SetValue(LanguageProperty, value);
        }

        private static void OnLanguageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not CodeEditor ce) return;
            var lang = e.NewValue as LanguageDefinition;
            // Re-evaluate CodeLens service attachment: only attach when the language declares support.
            if (lang?.EnableCodeLens == true)
                ce._codeLensService.Attach(ce._document, ce._currentFilePath);
            else
                ce._codeLensService.Detach();
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

        public static readonly DependencyProperty IntelliSenseDelayProperty =
            DependencyProperty.Register(nameof(IntelliSenseDelay), typeof(int), typeof(CodeEditor),
                new FrameworkPropertyMetadata(300));

        [Category("Behavior")]
        [DisplayName("IntelliSense Delay (ms)")]
        [Description("Delay before showing IntelliSense popup (milliseconds)")]
        public int IntelliSenseDelay
        {
            get => (int)GetValue(IntelliSenseDelayProperty);
            set => SetValue(IntelliSenseDelayProperty, value);
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

            // Initialize IntelliSense popup (Phase 4)
            _intelliSensePopup = new IntelliSensePopup(this);

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
            };

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
            _caretTimer = new System.Windows.Threading.DispatcherTimer();
            _caretTimer.Tick += CaretTimer_Tick;
            UpdateCaretBlinkTimer();

            // Initialize smooth scroll timer
            _smoothScrollTimer = new System.Windows.Threading.DispatcherTimer();
            _smoothScrollTimer.Interval = TimeSpan.FromMilliseconds(16); // ~60 FPS
            _smoothScrollTimer.Tick += SmoothScrollTimer_Tick;

            // Auto-scroll timer: scrolls the viewport while the mouse is held outside
            // bounds during a drag-selection (50 ms → ~20 scroll steps/s).
            _autoScrollTimer = new System.Windows.Threading.DispatcherTimer();
            _autoScrollTimer.Interval = TimeSpan.FromMilliseconds(50);
            _autoScrollTimer.Tick += AutoScrollTimer_Tick;

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
            _foldingEngine.RegionsChanged += (_, _) => { _linePositionsDirty = true; InvalidateMeasure(); InvalidateVisual(); };
            _scrollBarChildren.Add(_gutterControl);

            // Initialize word-highlight scroll marker overlay.
            _codeScrollMarkerPanel = new CodeScrollMarkerPanel();
            _scrollBarChildren.Add(_codeScrollMarkerPanel); // renders on top of _vScrollBar

            // Debounce timer: update word highlights 250 ms after the caret stops moving.
            _wordHighlightTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _wordHighlightTimer.Tick += (_, _) => { _wordHighlightTimer.Stop(); UpdateWordHighlights(); };

            // Attach CodeLens service to the initial document.
            _codeLensService.LensDataRefreshed += OnLensDataRefreshed;
            _codeLensService.Attach(_document, _currentFilePath);

            // Initialize Quick Info and Ctrl+Click services.
            _hoverQuickInfoService                    = new Services.HoverQuickInfoService();
            _hoverQuickInfoService.QuickInfoResolved  += OnQuickInfoResolved;
            _ctrlClickService                         = new Services.CtrlClickNavigationService();
            _ctrlClickService.TargetResolved          += OnCtrlClickTargetResolved;

            // Fold peek timer — fires 1.5 s after the mouse settles over a fold label.
            _foldPeekTimer          = new System.Windows.Threading.DispatcherTimer
                                      { Interval = TimeSpan.FromMilliseconds(600) };
            _foldPeekTimer.Tick    += OnFoldPeekTimerTick;

            // Apply theme resource bindings when connected to the visual tree
            Loaded += (_, _) => ApplyThemeResourceBindings();
        }

        private void OnLensDataRefreshed(object? sender, EventArgs e)
        {
            _lensData           = _codeLensService.LensData;
            RebuildVisibleLensCount();
            _linePositionsDirty = true; // OPT-D: CodeLens data affects per-line Y offsets
            InvalidateVisual();
        }

        /// <summary>
        /// Returns true when <paramref name="lineIndex"/> has a CodeLens entry whose
        /// <see cref="WpfHexEditor.Editor.Core.CodeLensSymbolKinds"/> flag matches the
        /// current <see cref="CodeLensVisibleKinds"/> filter.
        /// </summary>
        private bool IsLensEntryVisible(int lineIndex)
        {
            if (!_lensData.TryGetValue(lineIndex, out var entry)) return false;
            return (entry.Kind & CodeLensVisibleKinds) != 0;
        }

        /// <summary>
        /// Recounts filtered CodeLens entries and caches the result in <see cref="_visibleLensCount"/>.
        /// Must be called whenever <see cref="_lensData"/> or <see cref="CodeLensVisibleKinds"/> changes
        /// so that scrollbar height calculations use the correct filtered count.
        /// </summary>
        private void RebuildVisibleLensCount()
        {
            var kinds = CodeLensVisibleKinds;
            int count = 0;
            foreach (var entry in _lensData.Values)
            {
                if ((entry.Kind & kinds) != 0) count++;
            }
            _visibleLensCount = count;
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

            // Font / display
            if (!string.IsNullOrEmpty(options.FontFamily))
                EditorFontFamily = new FontFamily(options.FontFamily);

            if (options.FontSize is > 6 and < 72)
                EditorFontSize = options.FontSize;

            ShowLineNumbers          = options.ShowLineNumbers;
            ShowScopeGuides          = options.ShowScopeGuides;
            FoldToggleOnDoubleClick  = options.FoldToggleOnDoubleClick;
            EnableFindAllReferences  = options.EnableFindAllReferences;
            ShowCodeLens             = options.ShowCodeLens;
            CodeLensVisibleKinds     = options.CodeLensVisibleKinds;
            EnableWordHighlight      = options.EnableWordHighlight;

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
                { SyntaxTokenKind.Attribute,  "CE_Attribute"  },
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
        /// Caret blink timer tick handler - toggles visibility
        /// </summary>
        private void CaretTimer_Tick(object sender, EventArgs e)
        {
            _caretVisible = !_caretVisible;
            InvalidateVisual();
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
            if (!_isSelecting)
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
                    _undoMenuItem.Header = _undoEngine.CanUndo
                        ? $"_Undo ({_undoEngine.UndoCount})"
                        : "_Undo";
                if (_redoMenuItem != null)
                    _redoMenuItem.Header = _undoEngine.CanRedo
                        ? $"_Redo ({_undoEngine.RedoCount})"
                        : "_Redo";
            };

            // Separator
            contextMenu.Items.Add(new Separator());

            // Select All
            var selectAllMenuItem = new MenuItem
            {
                Header           = "Select _All",
                InputGestureText = "Ctrl+A",
                Command          = ApplicationCommands.SelectAll,
                CommandTarget    = this,
                Icon             = MakeMenuIcon("\uE8B3")
            };
            contextMenu.Items.Add(selectAllMenuItem);

            // Delete
            var deleteMenuItem = new MenuItem
            {
                Header           = "_Delete",
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
                Header           = "_Find...",
                InputGestureText = "Ctrl+F",
                Command          = ApplicationCommands.Find,
                CommandTarget    = this,
                Icon             = MakeMenuIcon("\uE721")
            };
            contextMenu.Items.Add(findMenuItem);

            // Replace
            var replaceMenuItem = new MenuItem
            {
                Header           = "_Replace...",
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
                Header           = "Find All _References",
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

            // Separator
            contextMenu.Items.Add(new Separator());

            // Format JSON
            var formatJsonMenuItem = new MenuItem
            {
                Header           = "F_ormat JSON",
                InputGestureText = "Ctrl+Shift+F",
                Icon             = MakeMenuIcon("\uE70F")
            };
            formatJsonMenuItem.Click += FormatJsonMenuItem_Click;
            contextMenu.Items.Add(formatJsonMenuItem);

            // Validate
            var validateMenuItem = new MenuItem
            {
                Header           = "_Validate JSON",
                InputGestureText = "F5",
                Icon             = MakeMenuIcon("\uE73E")
            };
            validateMenuItem.Click += ValidateMenuItem_Click;
            contextMenu.Items.Add(validateMenuItem);

            // Separator
            contextMenu.Items.Add(new Separator());

            // ── Outlining submenu — mirrors Visual Studio outlining menu ──
            var outlineMenu = new MenuItem { Header = "_Outlining" };

            var miToggleCurrent = new MenuItem
            {
                Header           = "Toggle _Outlining",
                InputGestureText = "Ctrl+M, Ctrl+M",
                Icon             = MakeMenuIcon("\uE8A0")
            };
            miToggleCurrent.Click += (_, _) => OutlineToggleCurrent();

            var miToggleAll = new MenuItem
            {
                Header           = "Toggle _All Outlining",
                InputGestureText = "Ctrl+M, Ctrl+L",
                Icon             = MakeMenuIcon("\uE8B7")
            };
            miToggleAll.Click += (_, _) => OutlineToggleAll();

            var miStop = new MenuItem
            {
                Header           = "_Stop Outlining",
                InputGestureText = "Ctrl+M, Ctrl+P",
                Icon             = MakeMenuIcon("\uE711")
            };
            miStop.Click += (_, _) => OutlineStop();

            var miStopHiding = new MenuItem
            {
                Header           = "Stop _Hiding Current",
                InputGestureText = "Ctrl+M, Ctrl+U",
                Icon             = MakeMenuIcon("\uE7B3")
            };
            miStopHiding.Click += (_, _) => OutlineStopHidingCurrent();

            var miCollapseDefs = new MenuItem
            {
                Header           = "_Collapse to Definitions",
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
                (sender, e) => e.CanExecute = _undoEngine.CanUndo));

            // Redo
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Redo,
                (sender, e) => Redo(),
                (sender, e) => e.CanExecute = _undoEngine.CanRedo));

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
                MessageBox.Show($"Cannot format — invalid JSON:\n{ex.Message}",
                    "Format Error", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                StatusMessage?.Invoke(this, "JSON is valid.");
                MessageBox.Show("JSON is valid.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (System.Text.Json.JsonException ex)
            {
                var msg = $"Invalid JSON: {ex.Message}";
                StatusMessage?.Invoke(this, msg);
                MessageBox.Show(msg, "Validation Error",
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
            // Scrollbar ranges depend on viewport size — trigger layout pass
            InvalidateArrange();
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
        }

        /// <summary>
        /// Scroll viewport vertically by pixel amount
        /// </summary>
        public void ScrollVertical(double delta)
        {
            if (_virtualizationEngine == null || !EnableVirtualScrolling)
                return;

            // delta is already speed * _lineHeight from the caller (MouseWheelSpeed controls line count).
            // No additional multiplier — ScrollSpeedMultiplier is kept for API compatibility only.
            double newOffset = _virtualizationEngine.ScrollByPixels(delta);

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
        /// Ensure cursor line is visible in viewport
        /// </summary>
        private void EnsureCursorVisible()
        {
            if (_virtualizationEngine == null || !EnableVirtualScrolling)
                return;

            double newOffset = _virtualizationEngine.EnsureLineVisible(_cursorLine);
            if (Math.Abs(newOffset - _verticalScrollOffset) > 0.1)
            {
                _verticalScrollOffset = newOffset;
                _virtualizationEngine.ScrollOffset = newOffset;
                _virtualizationEngine.CalculateVisibleRange();
                SyncVScrollBar();
                InvalidateVisual();
            }

            EnsureCursorColumnVisible();

            NotifyCaretMovedIfChanged();
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

        #region Rendering - OnRender Override

        /// <summary>
        /// Returns the Y pixel position (relative to the control top) for the n-th visible
        /// (non-hidden) line in the current viewport.  Preserves the sub-pixel smooth-scroll
        /// fraction from the virtualization engine so lines align correctly during smooth scroll.
        /// </summary>
        /// <param name="visIdx">0-based index among non-hidden visible lines.</param>
        #region Scope Guide Lines

        /// <summary>
        /// Draws vertical scope guide lines for each non-collapsed fold region.
        /// Each line is placed at the indentation column of the body content,
        /// running from the bottom of the opening brace line to the top of the closing brace line
        /// (VS Code style — the guide covers only the body, without touching either brace).
        /// </summary>
        private void RenderScopeGuides(DrawingContext dc)
        {
            if (!ShowScopeGuides || _foldingEngine == null || _document == null || _lineHeight <= 0)
                return;

            double textX = ShowLineNumbers ? TextAreaLeftOffset : LeftMargin;

            // Determine the innermost block containing the cursor for active-guide highlight.
            var activeRegion = FindInnermostContainingRegion(_cursorLine);

            foreach (var region in _foldingEngine.Regions)
            {
                if (region.IsCollapsed) continue; // body is hidden — no guide needed
                if (region.Kind == FoldingRegionKind.Directive) continue; // no guide for #region/#endregion

                // Skip regions entirely outside the visible range.
                if (region.EndLine < _firstVisibleLine || region.StartLine + 1 > _lastVisibleLine) continue;

                double guideX = ComputeScopeGuideX(textX, region);
                if (guideX <= textX) continue; // block not indented — skip

                // For Allman-style code the BraceFoldingStrategy sets StartLine = method-header line,
                // so StartLine+1 is the standalone { line — skip it to start the guide after the {.
                int bodyStart = region.StartLine + 1;
                if (bodyStart < _document!.Lines.Count && _document.Lines[bodyStart].Text.Trim() == "{")
                    bodyStart++;

                // Draw from bottom of { (first real body line) to top of } — VS Code exact behavior.
                double yTop    = ScopeLineIndexToY(bodyStart);
                double yBottom = ScopeLineIndexToY(region.EndLine);

                // Use the active pen for the innermost block containing the cursor (VS Code style).
                var pen = (activeRegion != null && ReferenceEquals(region, activeRegion))
                    ? s_scopeGuideActivePen
                    : s_scopeGuidePen;

                dc.DrawLine(pen, new Point(guideX, yTop), new Point(guideX, yBottom));
            }
        }

        /// <summary>
        /// Returns the X position for the scope guide of <paramref name="region"/> by
        /// finding the leading whitespace of the first non-empty line inside the block.
        /// </summary>
        /// <summary>
        /// Returns the innermost non-collapsed <see cref="FoldingRegion"/> whose body contains
        /// <paramref name="cursorLine"/>, or <c>null</c> if none does.
        /// "Innermost" is defined as the region with the largest <c>StartLine</c> (most nested block).
        /// </summary>
        private FoldingRegion? FindInnermostContainingRegion(int cursorLine)
        {
            FoldingRegion? best = null;
            foreach (var r in _foldingEngine!.Regions)
            {
                if (r.IsCollapsed) continue;
                if (cursorLine > r.StartLine && cursorLine <= r.EndLine)
                    if (best == null || r.StartLine > best.StartLine)
                        best = r;
            }
            return best;
        }

        private double ComputeScopeGuideX(double textX, FoldingRegion region)
        {
            for (int i = region.StartLine + 1; i < region.EndLine && i < _document!.Lines.Count; i++)
            {
                var text = _document.Lines[i].Text;
                if (string.IsNullOrWhiteSpace(text)) continue;
                int spaces = 0;
                foreach (char c in text)
                {
                    if      (c == ' ')  spaces++;
                    else if (c == '\t') spaces += IndentSize;
                    else break;
                }
                return spaces > 0 ? textX + spaces * _charWidth : textX;
            }
            return textX;
        }

        /// <summary>
        /// Converts a document line index to its Y pixel position in the viewport,
        /// accounting for fold-collapsed lines (mirrors <see cref="GetFoldAwareLineY"/>
        /// but takes an absolute line index instead of a visible-line counter).
        /// </summary>
        private double ScopeLineIndexToY(int lineIndex)
        {
            int visIdx = 0;
            for (int i = _firstVisibleLine; i < lineIndex && i < _document!.Lines.Count; i++)
                if (_foldingEngine == null || !_foldingEngine.IsLineHidden(i)) visIdx++;
            return GetFoldAwareLineY(visIdx);
        }

        #endregion

        #region Outlining Commands (Ctrl+M chord)

        /// <summary>Toggle the fold region that starts on the cursor line (Ctrl+M, Ctrl+M).</summary>
        private void OutlineToggleCurrent()
        {
            if (_foldingEngine == null || !IsFoldingEnabled) return;
            _foldingEngine.ToggleRegion(_cursorLine);
            InvalidateVisual();
        }

        /// <summary>
        /// Collapse all regions if none are collapsed; otherwise expand all (Ctrl+M, Ctrl+L).
        /// </summary>
        private void OutlineToggleAll()
        {
            if (_foldingEngine == null || !IsFoldingEnabled) return;
            bool anyCollapsed = _foldingEngine.Regions.Any(r => r.IsCollapsed);
            if (anyCollapsed) _foldingEngine.ExpandAll();
            else              _foldingEngine.CollapseAll();
            InvalidateVisual();
        }

        /// <summary>Expand all regions and disable outlining (Ctrl+M, Ctrl+P).</summary>
        private void OutlineStop()
        {
            _foldingEngine?.ExpandAll();
            IsFoldingEnabled = false;
            InvalidateVisual();
        }

        /// <summary>
        /// Expand the innermost collapsed region that contains the cursor (Ctrl+M, Ctrl+U).
        /// </summary>
        private void OutlineStopHidingCurrent()
        {
            if (_foldingEngine == null || !IsFoldingEnabled) return;
            // Find the innermost collapsed region containing the cursor.
            FoldingRegion? innermost = null;
            foreach (var r in _foldingEngine.Regions)
            {
                if (!r.IsCollapsed) continue;
                if (_cursorLine < r.StartLine || _cursorLine > r.EndLine) continue;
                if (innermost == null || (r.EndLine - r.StartLine) < (innermost.EndLine - innermost.StartLine))
                    innermost = r;
            }
            if (innermost != null)
                _foldingEngine.ToggleRegion(innermost.StartLine);
            InvalidateVisual();
        }

        /// <summary>Collapse all regions (Ctrl+M, Ctrl+O).</summary>
        private void OutlineCollapseToDefinitions()
        {
            if (_foldingEngine == null || !IsFoldingEnabled) return;
            _foldingEngine.CollapseAll();
            InvalidateVisual();
        }

        #endregion

        /// <summary>
        /// Returns the Y coordinate where the code text for visible-index
        /// <paramref name="visIdx"/> should be drawn.
        /// For declaration lines with CodeLens active the Y is pushed down by
        /// <see cref="LensLineHeight"/>; for all other lines it sits at the slot top.
        /// Falls back to the uniform formula when the precomputed list is unavailable.
        /// </summary>
        private double GetFoldAwareLineY(int visIdx)
        {
            if (visIdx < _visLinePositions.Count)
                return _visLinePositions[visIdx].Y;

            // Fallback: uniform layout (no CodeLens offset).
            double scrollFraction = (EnableVirtualScrolling && _virtualizationEngine != null)
                ? _virtualizationEngine.GetLineYPosition(_firstVisibleLine)
                : 0.0;
            return TopMargin + scrollFraction + visIdx * _lineHeight;
        }

        /// <summary>
        /// Returns the top Y of the lens hint zone for visible-index <paramref name="visIdx"/>
        /// (the LensLineHeight zone immediately above the code text).
        /// </summary>
        private double GetLensZoneY(int visIdx) => GetFoldAwareLineY(visIdx) - LensLineHeight;

        /// <summary>
        /// Precomputes per-visible-line Y positions, adding <see cref="LensLineHeight"/>
        /// only for lines that have a CodeLens entry.  Must be called in OnRender immediately
        /// after <see cref="CalculateVisibleLines"/>.
        /// </summary>
        private void ComputeVisibleLinePositions()
        {
            _visLinePositions.Clear();
            _lineYLookup.Clear();

            double scrollFraction = (EnableVirtualScrolling && _virtualizationEngine != null)
                ? _virtualizationEngine.GetLineYPosition(_firstVisibleLine)
                : 0.0;
            double y = TopMargin + scrollFraction;

            for (int i = _firstVisibleLine; i <= _lastVisibleLine; i++)
            {
                if (_foldingEngine?.IsLineHidden(i) == true) continue;

                if (ShowCodeLens && IsLensEntryVisible(i))
                {
                    // Code text sits below the hint zone.
                    double codeY = y + LensLineHeight;
                    _visLinePositions.Add((i, codeY));
                    _lineYLookup[i] = codeY;
                    y += _lineHeight + LensLineHeight;
                }
                else
                {
                    _visLinePositions.Add((i, y));
                    _lineYLookup[i] = y;
                    y += _lineHeight;
                }
            }
        }

        /// <summary>
        /// Draws "N références" hints in the lens zone (top <see cref="LensLineHeight"/> px of
        /// each line slot) for lines that have declaration items in <see cref="_lensData"/>.
        /// Hit zones are stored in <see cref="_lensHitZones"/> for mouse interaction.
        /// Called from OnRender after the text-area clip but before the H-scroll transform,
        /// so hints are clipped to the text column yet not scrolled horizontally.
        /// </summary>
        private void RenderCodeLensHints(DrawingContext dc)
        {
            if (!ShowCodeLens || _visibleLensCount == 0 || _document == null) return;

            _lensHitZones.Clear();

            var normalBrush = (Brush?)TryFindResource("CE_Lens")       ?? Brushes.Gray;
            var hoverBrush  = (Brush?)TryFindResource("CE_Lens_Hover") ?? Brushes.Silver;
            var bgBrush     = (Brush?)TryFindResource("CE_Lens_Bg")    ?? Brushes.Transparent;
            double fontSize  = LensLineHeight * 0.72;   // ~11.5 px for a 16-px slot
            double baseX     = ShowLineNumbers ? TextAreaLeftOffset : LeftMargin;
            double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            int visIdx = 0;
            for (int i = _firstVisibleLine; i <= _lastVisibleLine; i++)
            {
                if (_foldingEngine?.IsLineHidden(i) == true) { continue; }

                if (IsLensEntryVisible(i) && _lensData.TryGetValue(i, out var entry) && entry.Count > 0)
                {
                    // Indent hint to match the leading whitespace of the declaration line.
                    string lineText = _document.Lines[i].Text ?? string.Empty;
                    int indent = 0;
                    while (indent < lineText.Length && (lineText[indent] == ' ' || lineText[indent] == '\t'))
                        indent++;
                    double x = baseX + _glyphRenderer.ComputeVisualX(lineText, indent);

                    string label  = entry.Count == 1 ? "1 reference" : $"{entry.Count} references";
                    var    brush  = i == _hoveredLensLine ? hoverBrush : normalBrush;
                    var ft = new System.Windows.Media.FormattedText(
                        label,
                        System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        _typeface,
                        fontSize,
                        brush,
                        pixelsPerDip);

                    double y = GetLensZoneY(visIdx) + 1.0;  // 1 px top padding
                    // Subtle pill background — makes the hint visually distinct from code/comments.
                    dc.DrawRoundedRectangle(bgBrush, null, new Rect(x - 3, y, ft.Width + 6, ft.Height + 1), 3, 3);
                    dc.DrawText(ft, new Point(x, y));
                    _lensHitZones.Add((new Rect(x - 3, y, ft.Width + 6, LensLineHeight - 2), i, entry.Symbol));
                }

                visIdx++;
            }
        }

        /// <summary>
        /// Returns the 0-based column of the first whole-word occurrence of
        /// <paramref name="symbol"/> in line <paramref name="lineIdx"/>.
        /// Falls back to 0 if not found (cursor lands at line start).
        /// </summary>
        private int FindSymbolColumnInLine(int lineIdx, string symbol)
        {
            if (string.IsNullOrEmpty(symbol) || _document == null || lineIdx >= _document.Lines.Count)
                return 0;

            string text = _document.Lines[lineIdx].Text;
            if (string.IsNullOrEmpty(text)) return 0;

            int idx = text.IndexOf(symbol, StringComparison.Ordinal);
            if (idx < 0) return 0;

            bool leftOk  = idx == 0             || !IsWordChar(text[idx - 1]);
            bool rightOk = idx + symbol.Length >= text.Length || !IsWordChar(text[idx + symbol.Length]);
            return (leftOk && rightOk) ? idx : 0;
        }

        /// <summary>
        /// Draws a "[…]" badge after the text of a collapsed fold-opener line and
        /// registers the badge rect in <see cref="_foldLabelHitZones"/> for click-to-toggle.
        /// </summary>
        private void RenderFoldCollapseLabel(DrawingContext dc, int lineIndex, double textX, double y)
        {
            if (_foldingEngine == null) return;
            var region = _foldingEngine.GetRegionAt(lineIndex);
            if (region == null || !region.IsCollapsed) return;

            // Resolve theme-aware brushes at render time so theme switches are reflected immediately.
            // Hover state: use brighter tokens when mouse is over this label.
            bool isHovered = lineIndex == _foldPeekTargetLine;
            var borderBrush = (isHovered
                ? TryFindResource("CE_FoldLabelBorderHover") as Brush
                : null)
                ?? TryFindResource("CE_FoldLabelBorder") as Brush
                ?? s_foldLabelPenBrush;
            var bgBrush = (isHovered
                ? TryFindResource("CE_FoldLabelBgHover") as Brush
                : null)
                ?? TryFindResource("CE_FoldLabelBg") as Brush
                ?? s_foldLabelBgBrush;
            var textBrush   = TryFindResource("CE_FoldLabelFg")     as Brush ?? s_foldLabelTextBrush;
            var pen         = new Pen(borderBrush, isHovered ? 1.5 : 1.0);

            double labelX;
            string labelText;

            if (region.Kind == FoldingRegionKind.Directive)
            {
                // For directive regions: blank the opening "#region ..." text by drawing a
                // background-colored rect over it (drawn after text, so it renders on top),
                // then place the label box at the original indentation level of the #region line.
                var editorBg = TryFindResource("CE_Background") as Brush;
                if (editorBg != null)
                    dc.DrawRectangle(editorBg, null,
                        new Rect(textX, y, Math.Max(0, ActualWidth - textX), _lineHeight));

                string dirText  = _document.Lines[lineIndex].Text ?? string.Empty;
                int    indentLen = dirText.Length - dirText.TrimStart().Length;
                double indentX   = _glyphRenderer?.ComputeVisualX(dirText, indentLen)
                                   ?? indentLen * _charWidth;
                labelX    = textX + indentX;
                labelText = string.IsNullOrEmpty(region.Name) ? "#region" : region.Name;
            }
            else
            {
                // For brace regions: label appears after the opening line text (e.g. after '{').
                var codeLine = _document.Lines[lineIndex];
                double textLen = (codeLine.Text?.TrimEnd().Length ?? 0) * _charWidth;
                labelX    = textX + textLen + _charWidth * 0.5;
                labelText = "{ \u2026 }";
            }

            double FontSize = _fontSize;
            const double PaddingH = 8.0;

            var ft = new FormattedText(
                labelText,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                s_foldLabelTypeface, FontSize,
                textBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            // Box spans the full line height so it aligns flush with the visible line boundaries.
            double boxW   = ft.Width + PaddingH * 2;
            double boxH   = _lineHeight;
            double labelY = y;

            var rect = new Rect(labelX, labelY, boxW, boxH);
            dc.DrawRoundedRectangle(bgBrush, pen, rect, 2.0, 2.0);
            dc.DrawText(ft, new Point(labelX + PaddingH, labelY + (boxH - ft.Height) / 2.0));
            _foldLabelHitZones.Add((rect, lineIndex));
        }

        /// <summary>
        /// Main rendering method - draws all visual elements
        /// Called by WPF when visual update is needed
        /// </summary>
        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            if (_document == null || _document.Lines.Count == 0)
                return;

            // Cursor-change detection: covers every code path that moves the caret
            // (mouse click, keyboard, undo/redo, NavigateToLine, etc.) without requiring
            // a ScheduleWordHighlightUpdate() call at each individual site.
            if (_cursorLine != _wordHighlightTrackedLine || _cursorColumn != _wordHighlightTrackedCol)
            {
                _wordHighlightTrackedLine = _cursorLine;
                _wordHighlightTrackedCol  = _cursorColumn;
                ScheduleWordHighlightUpdate();
            }


            bool hasVBar = _vScrollBar?.Visibility == Visibility.Visible;
            bool hasHBar = _hScrollBar?.Visibility == Visibility.Visible;
            double contentW = ActualWidth  - (hasVBar ? ScrollBarThickness : 0);
            double contentH = ActualHeight - (hasHBar ? ScrollBarThickness : 0);
            double textLeft = ShowLineNumbers ? TextAreaLeftOffset : LeftMargin;

            // Calculate visible line range
            int prevFirstVisible = _firstVisibleLine, prevLastVisible = _lastVisibleLine;
            CalculateVisibleLines();
            // Pre-compute per-visible-line Y positions; only declaration lines get +LensLineHeight.
            ComputeVisibleLinePositions();

            // OPT-D: rebuild per-line Y positions only when the visible range, CodeLens, or
            // folding state changed — not on every caret-blink render frame.
            if (_firstVisibleLine != prevFirstVisible || _lastVisibleLine != prevLastVisible)
                _linePositionsDirty = true;

            if (_linePositionsDirty)
            {
                ComputeVisibleLinePositions();
                _linePositionsDirty = false;
            }

            // -- Clip to content area (prevent drawing over scrollbars) --
            dc.PushClip(new RectangleGeometry(new Rect(0, 0, contentW, contentH)));

            // 1. Editor background
            dc.DrawRectangle(EditorBackground, null, new Rect(0, 0, contentW, contentH));

            // 2. Line number gutter background (fixed — no H offset)
            if (ShowLineNumbers)
                dc.DrawRectangle(LineNumberBackground, null, new Rect(0, 0, LineNumberWidth, contentH));

            // 3. Current line highlight (spans visible text area, no H offset)
            RenderCurrentLineHighlight(dc, contentW, contentH);

            // -- Text area clip + horizontal translate -------------------
            dc.PushClip(new RectangleGeometry(new Rect(textLeft, 0, Math.Max(0, contentW - textLeft), contentH)));

            // 3b. CodeLens hints — drawn inside the text-area clip but WITHOUT the
            //     H-scroll transform so they stay anchored at the left edge.
            RenderCodeLensHints(dc);

            dc.PushTransform(new System.Windows.Media.TranslateTransform(-_horizontalScrollOffset, 0));

            // 4. Find result highlights
            RenderFindResults(dc);

            // 4b. Word-under-caret highlights (rendered below selection so selection stays visible)
            RenderWordHighlights(dc);

            // 4c. Ctrl+hover symbol underline (above word highlights, below selection)
            _symbolHitZones.Clear();
            RenderCtrlHoverUnderline(dc);

            // 5. Selection
            RenderSelection(dc);

            // 5a. Rectangular (block/column) selection overlay — Feature A
            RenderRectSelection(dc);

            // 5b. Drag-and-drop insertion caret — Feature B
            RenderDragDropCaret(dc);

            // 6a. Scope guides (drawn behind text so they don't obscure characters)
            RenderScopeGuides(dc);

            // 6. Text content
            RenderTextContent(dc);

            // 7. Validation errors (Phase 5)
            if (EnableValidation)
                RenderValidationErrors(dc);

            // 8. Bracket matching (Phase 6)
            RenderBracketMatching(dc);

            // 9. Cursor
            RenderCursor(dc);

            dc.Pop(); // H translate transform
            dc.Pop(); // text area clip

            // 10. Line numbers (no H offset — drawn on top of gutter background)
            if (ShowLineNumbers)
                RenderLineNumbers(dc);

            dc.Pop(); // content clip

            // 11. Corner background (intersection of V + H scrollbars)
            if (hasVBar && hasHBar)
                dc.DrawRectangle(LineNumberBackground ?? Brushes.Transparent, null,
                    new Rect(contentW, contentH, ScrollBarThickness, ScrollBarThickness));

            // Phase 11.4: Periodically cleanup token cache
            if (_frameCount++ % 60 == 0)
                _document.CleanupTokenCache(MaxCachedLines);

            // Schedule background highlighting only when the visible range changed or dirty lines exist.
            // Never re-schedule from a render triggered by the pipeline itself (breaks render loop).
            bool rangeChanged = _firstVisibleLine != _lastHighlightFirst || _lastVisibleLine != _lastHighlightLast;
            bool hasDirty     = false;
            if (!rangeChanged)
            {
                int lo = Math.Max(0, _firstVisibleLine);
                int hi = Math.Min(_document.Lines.Count - 1, _lastVisibleLine);
                for (int i = lo; i <= hi; i++)
                {
                    if (_document.Lines[i].IsCacheDirty) { hasDirty = true; break; }
                }
            }
            // Skip highlight scheduling while the smooth-scroll animation is running.
            // The visible range changes every ~16 ms during scroll; scheduling on every frame
            // triggers background work + HighlightsComputed → extra InvalidateVisual per frame.
            // A final pass is triggered by SmoothScrollTimer_Tick when the animation settles.
            if ((rangeChanged || hasDirty) && !_smoothScrollTimer.IsEnabled)
            {
                _lastHighlightFirst = _firstVisibleLine;
                _lastHighlightLast  = _lastVisibleLine;
                _highlightPipeline.ScheduleAsync(
                    _document.Lines,
                    _firstVisibleLine,
                    _lastVisibleLine,
                    _highlighter,
                    ExternalHighlighter);
            }
        }

        private int _frameCount = 0; // Frame counter for periodic cache cleanup

        /// <summary>
        /// Measure: update cached max content width; scrollbars manage their own layout.
        /// </summary>
        protected override Size MeasureOverride(Size availableSize)
        {
            // Update cached max content width using incremental tracker (P1-CE-02 — O(1))
            _maxContentWidth = _document != null && _document.Lines.Count > 0
                ? _cachedMaxLineLength * _charWidth + 20
                : 0;

            // Measure scrollbar children
            _vScrollBar?.Measure(new Size(ScrollBarThickness, double.IsInfinity(availableSize.Height) ? double.PositiveInfinity : Math.Max(0, availableSize.Height)));
            _hScrollBar?.Measure(new Size(double.IsInfinity(availableSize.Width) ? double.PositiveInfinity : Math.Max(0, availableSize.Width), ScrollBarThickness));

            // Fill all available space (scrolling is internal)
            double textLeft = ShowLineNumbers ? TextAreaLeftOffset : LeftMargin;
            double w = double.IsInfinity(availableSize.Width)
                ? Math.Max(400, textLeft + _maxContentWidth + ScrollBarThickness)
                : availableSize.Width;
            double h = double.IsInfinity(availableSize.Height)
                ? Math.Max(300, TopMargin + (_document?.Lines.Count ?? 0) * _lineHeight + ScrollBarThickness)
                : availableSize.Height;
            return new Size(w, h);
        }

        /// <summary>
        /// Arrange: position scrollbars and update their ranges.
        /// </summary>
        protected override Size ArrangeOverride(Size finalSize)
        {
            double textLeft    = ShowLineNumbers ? TextAreaLeftOffset : LeftMargin;
            int    hiddenLines = _foldingEngine?.TotalHiddenLineCount ?? 0;
            double totalH      = TopMargin + ((_document?.Lines.Count ?? 0) - hiddenLines) * _lineHeight
                             + (ShowCodeLens ? _visibleLensCount * LensLineHeight : 0);
            double totalTW     = textLeft + _maxContentWidth;

            // Determine which scrollbars are needed (check for mutual dependency)
            bool needsV = totalH  > finalSize.Height;
            bool needsH = totalTW > finalSize.Width;
            if (needsV) needsH = totalTW > (finalSize.Width  - ScrollBarThickness);
            if (needsH) needsV = totalH  > (finalSize.Height - ScrollBarThickness);

            double contentW = needsV ? finalSize.Width  - ScrollBarThickness : finalSize.Width;
            double contentH = needsH ? finalSize.Height - ScrollBarThickness : finalSize.Height;

            _vScrollBar.Visibility = needsV ? Visibility.Visible : Visibility.Hidden;
            _hScrollBar.Visibility = needsH ? Visibility.Visible : Visibility.Hidden;

            var vScrollRect = needsV ? new Rect(contentW, 0, ScrollBarThickness, contentH) : new Rect(0, 0, 0, 0);
            _vScrollBar.Arrange(vScrollRect);
            _hScrollBar.Arrange(needsH ? new Rect(0, contentH, contentW, ScrollBarThickness) : new Rect(0, 0, 0, 0));

            // Overlay scroll marker panel on top of the vertical scrollbar (click-through).
            _codeScrollMarkerPanel?.Arrange(vScrollRect);

            // Arrange the folding gutter flush against the left edge (inside the line-number strip).
            if (_gutterControl != null)
            {
                bool showGutter = IsFoldingEnabled && ShowLineNumbers;
                _gutterControl.Visibility = showGutter ? Visibility.Visible : Visibility.Collapsed;
                _gutterControl.Arrange(showGutter
                    ? new Rect(0, 0, _gutterControl.Width, contentH)
                    : new Rect(0, 0, 0, 0));
            }

            UpdateScrollBars(contentW, contentH);
            return finalSize;
        }

        #region ScrollBar Management

        /// <summary>
        /// Sync scrollbar ranges and values with current scroll/content state.
        /// Called from ArrangeOverride and after document/viewport changes.
        /// </summary>
        private void UpdateScrollBars(double contentW, double contentH)
        {
            if (_vScrollBar == null || _hScrollBar == null) return;

            _updatingScrollBar = true;
            try
            {
                double textLeft = ShowLineNumbers ? TextAreaLeftOffset : LeftMargin;

                // -- Vertical --------------------------------------------
                // Subtract collapsed fold lines so the scrollbar range matches actual rendered content.
                int    foldHidden = _foldingEngine?.TotalHiddenLineCount ?? 0;
                double totalH     = TopMargin + ((_document?.Lines.Count ?? 0) - foldHidden) * _lineHeight
                    + (ShowCodeLens ? _visibleLensCount * LensLineHeight : 0);
                double maxV       = Math.Max(0, totalH - contentH);

                // Clamp internal offset (e.g. file got shorter after edit)
                _verticalScrollOffset = Math.Min(_verticalScrollOffset, maxV);
                _currentScrollOffset  = _verticalScrollOffset;
                _targetScrollOffset   = _verticalScrollOffset;
                if (_virtualizationEngine != null)
                    _virtualizationEngine.ScrollOffset = _verticalScrollOffset;

                _vScrollBar.Minimum     = 0;
                _vScrollBar.Maximum     = maxV;
                _vScrollBar.ViewportSize = contentH;
                _vScrollBar.SmallChange = _lineHeight;
                _vScrollBar.LargeChange = contentH;
                _vScrollBar.Value       = _verticalScrollOffset;

                // -- Horizontal ------------------------------------------
                double totalTW = textLeft + _maxContentWidth;
                double maxH    = Math.Max(0, totalTW - contentW);

                _horizontalScrollOffset = Math.Min(_horizontalScrollOffset, maxH);

                _hScrollBar.Minimum      = 0;
                _hScrollBar.Maximum      = maxH;
                _hScrollBar.ViewportSize = Math.Max(0, contentW - textLeft);
                _hScrollBar.SmallChange  = _charWidth * 3;
                _hScrollBar.LargeChange  = Math.Max(contentW - textLeft, _charWidth);
                _hScrollBar.Value        = _horizontalScrollOffset;
            }
            finally
            {
                _updatingScrollBar = false;
            }
        }

        private void SyncVScrollBar()
        {
            if (_vScrollBar == null || _updatingScrollBar) return;
            _updatingScrollBar = true;
            _vScrollBar.Value  = _verticalScrollOffset;
            _updatingScrollBar = false;
        }

        private void SyncHScrollBar()
        {
            if (_hScrollBar == null || _updatingScrollBar) return;
            _updatingScrollBar = true;
            _hScrollBar.Value  = _horizontalScrollOffset;
            _updatingScrollBar = false;
        }

        private void VScrollBar_ValueChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
        {
            if (_updatingScrollBar) return;
            _verticalScrollOffset = e.NewValue;
            _currentScrollOffset  = e.NewValue;
            _targetScrollOffset   = e.NewValue;
            if (_virtualizationEngine != null)
            {
                _virtualizationEngine.ScrollOffset = e.NewValue;
                _virtualizationEngine.CalculateVisibleRange();
            }
            InvalidateVisual();
        }

        private void HScrollBar_ValueChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
        {
            if (_updatingScrollBar) return;
            _horizontalScrollOffset = e.NewValue;
            InvalidateVisual();
        }

        /// <summary>
        /// Ensure the cursor column is visible in the text area (horizontal auto-scroll).
        /// </summary>
        private void EnsureCursorColumnVisible()
        {
            if (_hScrollBar == null) return;
            double textLeft  = ShowLineNumbers ? TextAreaLeftOffset : LeftMargin;
            double contentW  = ActualWidth - (_vScrollBar?.Visibility == Visibility.Visible ? ScrollBarThickness : 0);
            double textAreaW = Math.Max(0, contentW - textLeft);
            if (textAreaW <= 0) return;

            double cursorX = _cursorColumn * _charWidth;        // position in the text area (no offset)
            double rightEdge = cursorX + _charWidth;

            if (cursorX < _horizontalScrollOffset)
            {
                _horizontalScrollOffset = Math.Max(0, cursorX);
                SyncHScrollBar();
                InvalidateVisual();
            }
            else if (rightEdge > _horizontalScrollOffset + textAreaW)
            {
                _horizontalScrollOffset = rightEdge - textAreaW;
                SyncHScrollBar();
                InvalidateVisual();
            }
        }

        #endregion

        /// <summary>
        /// Calculate which lines are visible in the viewport
        /// Phase 1: Simple calculation (no virtual scrolling yet)
        /// </summary>
        private void CalculateVisibleLines()
        {
            bool hasHBar = _hScrollBar?.Visibility == Visibility.Visible;
            double viewportH = ActualHeight - TopMargin - (hasHBar ? ScrollBarThickness : 0);

            // Phase 11: Use VirtualizationEngine if enabled
            if (EnableVirtualScrolling && _virtualizationEngine != null)
            {
                // Update virtualization state
                _virtualizationEngine.ViewportHeight = viewportH;
                _virtualizationEngine.LineHeight = _lineHeight;
                _virtualizationEngine.ScrollOffset = _verticalScrollOffset;

                // Calculate visible range with render buffer
                var (first, last) = _virtualizationEngine.CalculateVisibleRange();
                _firstVisibleLine = first;
                _lastVisibleLine = last;
            }
            else
            {
                // Phase 1 fallback: Show all lines that fit in viewport (no virtualization)
                _firstVisibleLine = 0;
                _lastVisibleLine = Math.Min(_document.Lines.Count - 1,
                    (int)(viewportH / _lineHeight));
            }

            // Forward-scan: count visible (non-hidden) lines from _firstVisibleLine until the
            // viewport + render buffer is filled.  Single-pass; correctly handles folds whose
            // hidden range spans the initial VirtualizationEngine window.
            if (_foldingEngine != null && _foldingEngine.TotalHiddenLineCount > 0)
            {
                int needed  = (int)(viewportH / _lineHeight) + RenderBuffer + 1;
                int visible = 0;
                int i       = _firstVisibleLine;
                while (i < _document.Lines.Count)
                {
                    if (!_foldingEngine.IsLineHidden(i))
                    {
                        visible++;
                        if (visible >= needed) break;
                    }
                    i++;
                }
                _lastVisibleLine = Math.Min(_document.Lines.Count - 1, i);
            }

            // Sync gutter layout with the newly computed visible range.
            // Pass scroll fraction so gutter markers follow smooth-scroll sub-pixel offset.
            double gutterScrollFraction = (EnableVirtualScrolling && _virtualizationEngine != null)
                ? _virtualizationEngine.GetLineYPosition(_firstVisibleLine)
                : 0.0;
            _gutterControl?.Update(_lineHeight, _firstVisibleLine, _lastVisibleLine,
                                   TopMargin, gutterScrollFraction, _lineYLookup);
        }

        /// <summary>
        /// Render line numbers in left gutter
        /// </summary>
        private void RenderLineNumbers(DrawingContext dc)
        {
            // Flush line-number FormattedText cache when font parameters change (P1-CE-03)
            if (_cachedLineNumberFontSize != LineNumberFontSize ||
                !Equals(_cachedLineNumberTypeface, _lineNumberTypeface))
            {
                _lineNumberCache.Clear();
                _cachedLineNumberFontSize = LineNumberFontSize;
                _cachedLineNumberTypeface = _lineNumberTypeface;
            }

            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            int visIdx = 0;
            for (int i = _firstVisibleLine; i <= _lastVisibleLine && i < _document.Lines.Count; i++)
            {
                // Skip lines hidden inside a collapsed fold region.
                if (_foldingEngine != null && _foldingEngine.IsLineHidden(i)) continue;

                double y = GetFoldAwareLineY(visIdx);
                visIdx++;

                // Cache FormattedText per line number — eliminates 2,400 allocations/s (P1-CE-03)
                if (!_lineNumberCache.TryGetValue(i + 1, out var formattedText))
                {
                    formattedText = new FormattedText(
                        (i + 1).ToString(),
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        _lineNumberTypeface,
                        LineNumberFontSize,
                        LineNumberForeground,
                        dpi);
                    _lineNumberCache[i + 1] = formattedText;
                }

                // Right-align line numbers.
                // Align the FormattedText baseline to the GlyphRun baseline so the line
                // number sits on the same optical baseline as the code text on the same row.
                // Fallback: vertical center when GlyphRunRenderer is not yet initialised.
                double x     = LineNumberWidth - formattedText.Width - LineNumberMargin;
                double lineY = _glyphRenderer != null
                    ? y + _glyphRenderer.Baseline - formattedText.Baseline
                    : y + (_lineHeight - formattedText.Height) / 2;

                dc.DrawText(formattedText, new Point(x, lineY));

                // Render validation glyphs (error/warning icons) in left margin
                RenderValidationGlyph(dc, i, y);
            }

            // Draw separator line between line numbers and text (cached frozen pen — OPT-PERF-02)
            dc.DrawLine(s_lineNumberSeparatorPen, new Point(LineNumberWidth, 0), new Point(LineNumberWidth, ActualHeight));
        }

        /// <summary>
        /// Render validation glyph (error/warning icon) for a line if it has validation errors
        /// </summary>
        private void RenderValidationGlyph(DrawingContext dc, int line, double y)
        {
            // OPT-PERF-01: O(1) dictionary lookup instead of O(n) LINQ scan per visible line.
            if (!EnableValidation || _validationByLine.Count == 0) return;
            if (!_validationByLine.TryGetValue(line, out var lineErrors)) return;

            // Worst severity drives the glyph color (Error > Warning > Info).
            ValidationSeverity worstSeverity = lineErrors[0].Severity;
            for (int i = 1; i < lineErrors.Count; i++)
                if (lineErrors[i].Severity > worstSeverity) worstSeverity = lineErrors[i].Severity;

            if (worstSeverity == ValidationSeverity.Info) return; // No glyph for Info

            Brush glyphBrush = worstSeverity == ValidationSeverity.Error
                ? s_squigglyError.Brush
                : s_squigglyWarning.Brush;

            double glyphSize = Math.Min(_lineHeight * 0.6, 12);
            double glyphX    = 5;
            double glyphY    = y + (_lineHeight - glyphSize) / 2;

            dc.DrawEllipse(glyphBrush, null, new Point(glyphX + glyphSize / 2, glyphY + glyphSize / 2), glyphSize / 2, glyphSize / 2);

            if (worstSeverity == ValidationSeverity.Error)
            {
                double offset = glyphSize * 0.25;
                dc.DrawLine(s_glyphInnerPen, new Point(glyphX + offset, glyphY + offset),             new Point(glyphX + glyphSize - offset, glyphY + glyphSize - offset));
                dc.DrawLine(s_glyphInnerPen, new Point(glyphX + glyphSize - offset, glyphY + offset), new Point(glyphX + offset, glyphY + glyphSize - offset));
            }
            else
            {
                double centerX = glyphX + glyphSize / 2;
                dc.DrawLine(s_glyphInnerPen, new Point(centerX, glyphY + glyphSize * 0.2), new Point(centerX, glyphY + glyphSize * 0.6));
                dc.DrawEllipse(Brushes.White, null, new Point(centerX, glyphY + glyphSize * 0.8), 1, 1);
            }
        }

        /// <summary>
        /// Render current line highlight
        /// </summary>
        private void RenderCurrentLineHighlight(DrawingContext dc, double contentW, double contentH)
        {
            if (_cursorLine < _firstVisibleLine || _cursorLine > _lastVisibleLine)
                return;

            // Do not highlight a line that is hidden inside a collapsed fold region.
            if (_foldingEngine != null && _foldingEngine.IsLineHidden(_cursorLine))
                return;

            // Count non-hidden lines before _cursorLine to compute the correct visual Y.
            int visIdx = 0;
            for (int i = _firstVisibleLine; i < _cursorLine; i++)
                if (_foldingEngine == null || !_foldingEngine.IsLineHidden(i)) visIdx++;

            double y = GetFoldAwareLineY(visIdx);

            double x = ShowLineNumbers ? TextAreaLeftOffset : LeftMargin;

            // Draw background highlight (spans visible text area width — no H offset needed)
            if (ShowCurrentLineHighlight)
            {
                dc.DrawRectangle(CurrentLineBackground, null,
                    new Rect(x, y, contentW - x, _lineHeight));
            }

            // Draw border if enabled
            if (ShowCurrentLineBorder)
            {
                var borderBrush = new SolidColorBrush(CurrentLineBorderColor);
                borderBrush.Freeze();
                var borderPen = new Pen(borderBrush, 1);
                borderPen.Freeze();
                dc.DrawRectangle(null, borderPen,
                    new Rect(x, y, contentW - x, _lineHeight));
            }
        }

        /// <summary>
        /// Render text selection overlay (Phase 3 - Enhanced with multi-line support)
        /// </summary>
        private void RenderSelection(DrawingContext dc)
        {
            if (_selection.IsEmpty)
                return;

            // Use InactiveSelectionBackground when the editor (or any child) has no keyboard focus.
            // IsKeyboardFocusWithin is used rather than IsFocused so that interacting with
            // the scrollbars (child visuals) does not incorrectly dim the selection.
            Brush selectionBrush = IsKeyboardFocusWithin ? SelectionBackground : InactiveSelectionBackground;

            var start = _selection.NormalizedStart;
            var end = _selection.NormalizedEnd;

            // Single-line selection
            if (start.Line == end.Line)
            {
                if (start.Line >= _firstVisibleLine && start.Line <= _lastVisibleLine)
                {
                    double y = _lineYLookup.TryGetValue(start.Line, out double sy) ? sy
                        : (EnableVirtualScrolling && _virtualizationEngine != null
                            ? TopMargin + _virtualizationEngine.GetLineYPosition(start.Line)
                            : TopMargin + (start.Line - _firstVisibleLine) * _lineHeight);

                    double x1 = (ShowLineNumbers ? TextAreaLeftOffset : LeftMargin) + (start.Column * _charWidth);
                    double x2 = (ShowLineNumbers ? TextAreaLeftOffset : LeftMargin) + (end.Column * _charWidth);

                    dc.DrawRoundedRectangle(selectionBrush, null, new Rect(x1, y, x2 - x1, _lineHeight), SelectionCornerRadius, SelectionCornerRadius);
                }
            }
            else // Multi-line selection (Phase 3)
            {
                double leftEdge = ShowLineNumbers ? TextAreaLeftOffset : LeftMargin;

                // Collect overlapping segments then union them so the brush is applied once,
                // preventing double-alpha darkening at junctions with semi-transparent selection brushes.
                var segments = new List<Geometry>();

                // First line — extend bottom by CornerRadius so the rounded tail merges with next segment
                if (start.Line >= _firstVisibleLine && start.Line <= _lastVisibleLine)
                {
                    double y  = _lineYLookup.TryGetValue(start.Line, out double fsy) ? fsy
                        : TopMargin + (EnableVirtualScrolling && _virtualizationEngine != null
                            ? _virtualizationEngine.GetLineYPosition(start.Line)
                            : (start.Line - _firstVisibleLine) * _lineHeight);
                    double x1 = leftEdge + (start.Column * _charWidth);
                    double x2 = leftEdge + (_document.Lines[start.Line].Length * _charWidth);
                    segments.Add(new RectangleGeometry(new Rect(x1, y, Math.Max(x2 - x1, _charWidth), _lineHeight + SelectionCornerRadius), SelectionCornerRadius, SelectionCornerRadius));
                }

                // Middle lines — extend top and bottom by CornerRadius to merge with neighbours.
                // Clamp to visible viewport so the loop is O(visible_lines) rather than O(selected_lines).
                int middleFirst = Math.Max(start.Line + 1, _firstVisibleLine);
                int middleLast  = Math.Min(end.Line   - 1, _lastVisibleLine);
                for (int line = middleFirst; line <= middleLast; line++)
                {
                    double lineBaseY = _lineYLookup.TryGetValue(line, out double mly) ? mly
                        : TopMargin + (EnableVirtualScrolling && _virtualizationEngine != null
                            ? _virtualizationEngine.GetLineYPosition(line)
                            : (line - _firstVisibleLine) * _lineHeight);
                    double y = lineBaseY - SelectionCornerRadius;
                    double width = _document.Lines[line].Length * _charWidth;
                    segments.Add(new RectangleGeometry(new Rect(leftEdge, y, Math.Max(width, _charWidth), _lineHeight + SelectionCornerRadius * 2), SelectionCornerRadius, SelectionCornerRadius));
                }

                // Last line — extend top by CornerRadius so the rounded head merges with previous segment
                if (end.Line >= _firstVisibleLine && end.Line <= _lastVisibleLine)
                {
                    double y  = (_lineYLookup.TryGetValue(end.Line, out double ely) ? ely
                        : TopMargin + (EnableVirtualScrolling && _virtualizationEngine != null
                            ? _virtualizationEngine.GetLineYPosition(end.Line)
                            : (end.Line - _firstVisibleLine) * _lineHeight)) - SelectionCornerRadius;
                    double x2 = leftEdge + (end.Column * _charWidth);
                    segments.Add(new RectangleGeometry(new Rect(leftEdge, y, x2 - leftEdge, _lineHeight + SelectionCornerRadius), SelectionCornerRadius, SelectionCornerRadius));
                }

                if (segments.Count > 0)
                {
                    Geometry combined = segments[0];
                    for (int i = 1; i < segments.Count; i++)
                        combined = Geometry.Combine(combined, segments[i], GeometryCombineMode.Union, null);
                    combined.Freeze();
                    dc.DrawGeometry(selectionBrush, null, combined);
                }
            }
        }

        /// <summary>
        /// Renders the rectangular (block/column) selection overlay as a single seamless rectangle
        /// spanning the full vertical extent of the selection. Drawing one rectangle eliminates
        /// the anti-aliasing seams that appear when drawing one rect per line.
        /// Uses _lineYLookup for CodeLens-aware Y offsets (mandatory).
        /// </summary>
        private void RenderRectSelection(DrawingContext dc)
        {
            if (_rectSelection.IsEmpty) return;

            // Clamp selection range to the visible viewport.
            int visTop    = Math.Max(_rectSelection.TopLine,    _firstVisibleLine);
            int visBottom = Math.Min(_rectSelection.BottomLine, _lastVisibleLine);
            if (visTop > visBottom) return; // selection entirely outside viewport

            Brush selBrush = IsKeyboardFocusWithin ? SelectionBackground : InactiveSelectionBackground;

            double leftEdge = ShowLineNumbers ? TextAreaLeftOffset : LeftMargin;
            var (leftCol, rightCol) = _rectSelection.GetColumnRange();
            double x1    = leftEdge + leftCol  * _charWidth;
            double x2    = leftEdge + rightCol * _charWidth;
            double width = Math.Max(x2 - x1, 1.0); // at least 1px when collapsed

            // Mandatory: use _lineYLookup for CodeLens-aware Y offset.
            double yTop = _lineYLookup.TryGetValue(visTop, out double lt) ? lt
                : TopMargin + (visTop - _firstVisibleLine) * _lineHeight;
            double yBottom = (_lineYLookup.TryGetValue(visBottom, out double lb) ? lb
                : TopMargin + (visBottom - _firstVisibleLine) * _lineHeight) + _lineHeight;

            dc.DrawRectangle(selBrush, null, new Rect(x1, yTop, width, yBottom - yTop));
        }

        /// <summary>
        /// Renders a 2px wide vertical insertion-caret bar at the drag-drop target position.
        /// Orange by default (VS convention for drag insertion points).
        /// </summary>
        private void RenderDragDropCaret(DrawingContext dc)
        {
            if (_dragDrop.Phase != DragPhase.Dragging) return;

            var drop = _dragDrop.DropPosition;
            if (drop.Line < _firstVisibleLine || drop.Line > _lastVisibleLine) return;

            double leftEdge = ShowLineNumbers ? TextAreaLeftOffset : LeftMargin;
            double y = _lineYLookup.TryGetValue(drop.Line, out double ly) ? ly
                : TopMargin + (drop.Line - _firstVisibleLine) * _lineHeight;
            double x = leftEdge + drop.Column * _charWidth;

            var caretBrush = TryFindResource("CE_DragCaret") as Brush ?? Brushes.Orange;
            dc.DrawRectangle(caretBrush, null, new Rect(x - 1, y, 2, _lineHeight));
        }

        /// <summary>
        /// Render find/replace results highlighting
        /// </summary>
        private void RenderFindResults(DrawingContext dc)
        {
            if (_findResults == null || _findResults.Count == 0)
                return;

            double leftEdge = ShowLineNumbers ? TextAreaLeftOffset : LeftMargin;

            // Render all find results with FindResultColor
            for (int i = 0; i < _findResults.Count; i++)
            {
                var result = _findResults[i];

                if (result.Line < _firstVisibleLine || result.Line > _lastVisibleLine)
                    continue;

                // Y: use _lineYLookup so CodeLens hint rows are accounted for
                double y = _lineYLookup.TryGetValue(result.Line, out double ry) ? ry
                    : TopMargin + (result.Line - _firstVisibleLine) * _lineHeight;

                // X: expand tabs correctly via ComputeVisualX instead of raw column * charWidth
                var lineText = result.Line < _document.Lines.Count ? _document.Lines[result.Line].Text : string.Empty;
                double x1 = leftEdge + _glyphRenderer.ComputeVisualX(lineText, result.Column);
                double x2 = leftEdge + _glyphRenderer.ComputeVisualX(lineText, result.Column + _findMatchLength);

                // Use HighlightMatchColor for current match, FindResultColor for others
                Brush highlightBrush = (i == _currentFindMatchIndex)
                    ? HighlightMatchColor
                    : FindResultColor;
                if (highlightBrush.IsFrozen == false)
                    highlightBrush.Freeze();

                dc.DrawRoundedRectangle(highlightBrush, null, new Rect(x1, y, x2 - x1, _lineHeight), SelectionCornerRadius, SelectionCornerRadius);
            }
        }

        /// <summary>
        /// Renders a subtle highlight box on every occurrence of the word currently under the caret.
        /// Called from OnRender after RenderFindResults and before RenderSelection.
        /// </summary>
        private void RenderWordHighlights(DrawingContext dc)
        {
            if (_wordHighlights.Count == 0 || _wordHighlightLen == 0)
                return;

            double leftEdge = ShowLineNumbers ? TextAreaLeftOffset : LeftMargin;

            foreach (var pos in _wordHighlights)
            {
                if (pos.Line < _firstVisibleLine || pos.Line > _lastVisibleLine)
                    continue;

                // Word highlights on CodeLens declaration lines are distracting:
                // the hint zone sits above the code text and the rectangle would
                // overlap it.  Skip these lines entirely.
                if (ShowCodeLens && IsLensEntryVisible(pos.Line))
                    continue;

                // Skip collapsed directive opener lines — text is blanked; a rect outline
                // would appear as stray top/bottom horizontal strokes over the fold label.
                if (_foldingEngine?.GetRegionAt(pos.Line) is { IsCollapsed: true, Kind: FoldingRegionKind.Directive })
                    continue;

                double y = _lineYLookup.TryGetValue(pos.Line, out double wy) ? wy
                    : (EnableVirtualScrolling && _virtualizationEngine != null
                        ? TopMargin + _virtualizationEngine.GetLineYPosition(pos.Line)
                        : TopMargin + (pos.Line - _firstVisibleLine) * _lineHeight);

                double x1 = leftEdge + pos.Column * _charWidth;
                double x2 = x1 + _wordHighlightLen * _charWidth;

                dc.DrawRectangle(s_wordHighlightBg, s_wordHighlightPen,
                    new Rect(x1, y, x2 - x1, _lineHeight));
            }
        }

        /// <summary>
        /// Extracts the identifier word at <paramref name="col"/> within <paramref name="text"/>.
        /// Returns an empty string if the character at col is not a word character or the word is shorter than 2 chars.
        /// </summary>
        private (string Word, int StartCol) GetWordAt(string text, int col)
        {
            if (string.IsNullOrEmpty(text) || col < 0 || col >= text.Length)
                return (string.Empty, col);

            if (!IsWordChar(text[col]))
                return (string.Empty, col);

            int start = col;
            while (start > 0 && IsWordChar(text[start - 1]))
                start--;

            int end = col;
            while (end < text.Length - 1 && IsWordChar(text[end + 1]))
                end++;

            string word = text.Substring(start, end - start + 1);
            return word.Length >= 2 ? (word, start) : (string.Empty, start);
        }

        /// <summary>
        /// Returns the <see cref="SyntaxTokenKind"/> of the cached syntax token that covers
        /// <paramref name="column"/> on <paramref name="lineIndex"/>.
        /// Returns <see cref="SyntaxTokenKind.Default"/> when the cache is not yet populated
        /// or no token covers that column (uncached lines are treated as potentially navigable).
        /// </summary>
        private SyntaxTokenKind GetTokenKindAtColumn(int lineIndex, int column)
        {
            if (lineIndex < 0 || lineIndex >= _document.Lines.Count) return SyntaxTokenKind.Default;

            var cache = _document.Lines[lineIndex].TokensCache;
            if (cache is null) return SyntaxTokenKind.Default;

            foreach (var token in cache)
            {
                if (column >= token.StartColumn && column < token.StartColumn + token.Length)
                    return token.Kind;
            }
            return SyntaxTokenKind.Default;
        }

        /// <summary>
        /// Rescans the document for all occurrences of the word under the caret and
        /// updates both the viewport highlight list and the scroll marker panel.
        /// Called by the debounce timer; runs on the UI thread.
        /// </summary>
        private void UpdateWordHighlights()
        {
            _wordHighlights.Clear();
            _wordHighlightWord = string.Empty;
            _wordHighlightLen  = 0;

            string word = ResolveHighlightWord();

            if (word.Length >= 2)
            {
                _wordHighlightWord = word;
                _wordHighlightLen  = word.Length;

                // Whole-word scan across all lines.
                for (int li = 0; li < _document.Lines.Count; li++)
                {
                    string lineText = _document.Lines[li].Text ?? string.Empty;
                    int idx = 0;
                    while ((idx = lineText.IndexOf(word, idx, StringComparison.Ordinal)) >= 0)
                    {
                        bool leftOk  = idx == 0                          || !IsWordChar(lineText[idx - 1]);
                        bool rightOk = idx + word.Length >= lineText.Length || !IsWordChar(lineText[idx + word.Length]);

                        if (leftOk && rightOk)
                            _wordHighlights.Add(new TextPosition(li, idx));

                        idx += word.Length;
                    }
                }
            }

            InvalidateVisual();

            // Update scroll bar tick marks.
            if (_codeScrollMarkerPanel != null)
            {
                if (_wordHighlights.Count == 0)
                    _codeScrollMarkerPanel.ClearWordMarkers();
                else
                {
                    var distinctLines = _wordHighlights
                        .Select(p => p.Line)
                        .Distinct()
                        .ToList();
                    _codeScrollMarkerPanel.UpdateWordMarkers(distinctLines,
                        Math.Max(1, _document?.TotalLines ?? 1));
                }

                // Sync caret + selection markers every render pass.
                if (_document != null)
                {
                    int visibleLines = Math.Max(1, _document.Lines.Count - (_foldingEngine?.TotalHiddenLineCount ?? 0));
                    bool hasSelection = !_selection.IsEmpty && _selection.NormalizedStart.Line != _selection.NormalizedEnd.Line;
                    _codeScrollMarkerPanel.UpdateCaretAndSelection(
                        _cursorLine,
                        hasSelection ? _selection.NormalizedStart.Line : -1,
                        hasSelection ? _selection.NormalizedEnd.Line   : -1,
                        visibleLines);
                }
            }
        }

        /// <summary>
        /// Returns the word to highlight: the selected text (single-line, ≥2 chars) if a
        /// selection is active, otherwise the identifier word at the current caret position.
        /// Returns <see cref="string.Empty"/> when no suitable word is found.
        /// </summary>
        private string ResolveHighlightWord()
        {
            if (!EnableWordHighlight || _document == null || _document.Lines.Count == 0)
                return string.Empty;

            // Single-line selection takes priority.
            if (!_selection.IsEmpty && !_selection.IsMultiLine)
            {
                int li = _selection.NormalizedStart.Line;
                int s  = _selection.NormalizedStart.Column;
                int e  = _selection.NormalizedEnd.Column;
                if (li < _document.Lines.Count && e > s)
                {
                    string lt = _document.Lines[li].Text ?? string.Empty;
                    if (e <= lt.Length)
                        return lt.Substring(s, e - s);
                }
            }

            // Fall back to word at caret.
            if (_cursorLine < _document.Lines.Count)
                return GetWordAt(_document.Lines[_cursorLine].Text ?? string.Empty, _cursorColumn).Word;

            return string.Empty;
        }

        /// <summary>
        /// Arms (or re-arms) the 250 ms debounce timer that fires <see cref="UpdateWordHighlights"/>.
        /// Safe to call on every keystroke / caret move.
        /// </summary>
        private void ScheduleWordHighlightUpdate()
        {
            _wordHighlightTimer?.Stop();
            _wordHighlightTimer?.Start();
        }

        /// <summary>
        /// Render text content with syntax highlighting (Phase 2)
        /// </summary>
        private void RenderTextContent(DrawingContext dc)
        {
            double x = ShowLineNumbers ? TextAreaLeftOffset : LeftMargin;

            var context = new JsonParserContext();

            // Rebuild URL hit-zones each render pass (document may have changed).
            _urlHitZones.Clear();
            _foldLabelHitZones.Clear();

            // Lazy-init the underline pen from the current SyntaxUrlColor DP.
            // Re-created each render so theme changes are reflected immediately.
            var urlPen = new Pen(SyntaxUrlColor, 1.0);
            urlPen.Freeze();

            // Reset external highlighter state before a full render pass.
            ExternalHighlighter?.Reset();

            int visIdx = 0;
            for (int i = _firstVisibleLine; i <= _lastVisibleLine && i < _document.Lines.Count; i++)
            {
                // Skip lines hidden inside a collapsed fold region.
                if (_foldingEngine != null && _foldingEngine.IsLineHidden(i)) continue;

                double y = GetFoldAwareLineY(visIdx);
                visIdx++;

                var line = _document.Lines[i];

                if (!string.IsNullOrEmpty(line.Text))
                {
                    // ── P1-CE-05: GlyphRun cache fast path ───────────────────────────
                    // When the line text has not changed since the last render and a
                    // GlyphRun cache exists, skip token generation entirely and draw
                    // the pre-built runs via a single PushTransform.  URL hit-zones are
                    // restored from the per-line cache so click detection stays intact.
                    if (_glyphRenderer != null
                        && !line.IsGlyphCacheDirty
                        && line.GlyphRunCache is { Count: > 0 } cachedRuns)
                    {
                        // Restore URL hit-zones (built when the cache was first populated).
                        if (line.CachedUrlZones is { } zones)
                            foreach (var z in zones)
                                _urlHitZones.Add(new UrlHitZone(i, z.StartCol, z.EndCol, z.Url));

                        // Translate once per line → draw all cached GlyphRuns.
                        dc.PushTransform(new System.Windows.Media.TranslateTransform(x, y));
                        foreach (var entry in cachedRuns)
                            dc.DrawGlyphRun(entry.Foreground, entry.Run);
                        dc.Pop();

                        // URL hover underline (changes per mouse-move without dirtying the cache).
                        if (_hoveredUrlZone.HasValue && _hoveredUrlZone.Value.Line == i)
                        {
                            foreach (var entry in cachedRuns)
                            {
                                if (entry.IsUrlToken
                                    && entry.StartColumn >= _hoveredUrlZone.Value.StartCol
                                    && entry.StartColumn <  _hoveredUrlZone.Value.EndCol)
                                {
                                    double underlineY = y + _glyphRenderer.Baseline + 2;
                                    double tokenX     = x + entry.Run.BaselineOrigin.X;
                                    dc.DrawLine(urlPen,
                                        new Point(tokenX, underlineY),
                                        new Point(tokenX + entry.TokenLength * _charWidth, underlineY));
                                }
                            }
                        }

                        // Draw fold-collapse label at end of line if this is a collapsed region opener.
                        RenderFoldCollapseLabel(dc, i, x, y);

                        // Advance stateful highlighter even when using the glyph cache,
                        // so block-comment tracking (_inBlockComment) stays correct for
                        // subsequent lines that may not be cached.
                        ExternalHighlighter?.Highlight(line.Text, i);

                        continue; // skip slow path
                    }
                    // ── end fast path ─────────────────────────────────────────────────

                    // ── OPT-A Fast path B: optimistic stale-cache rendering ───────────
                    // The background pipeline has not yet refreshed this line (IsCacheDirty=true)
                    // but a GlyphRun cache from the previous frame is still available.
                    // Render the stale frame immediately so the caret stays instant, and let
                    // the pipeline trigger a clean frame within ~100 ms when it finishes.
                    if (_glyphRenderer != null
                        && line.IsCacheDirty
                        && line.IsGlyphCacheDirty
                        && line.GlyphRunCache is { Count: > 0 } staleRuns)
                    {
                        if (line.CachedUrlZones is { } zones)
                            foreach (var z in zones)
                                _urlHitZones.Add(new UrlHitZone(i, z.StartCol, z.EndCol, z.Url));

                        dc.PushTransform(new System.Windows.Media.TranslateTransform(x, y));
                        foreach (var entry in staleRuns)
                            dc.DrawGlyphRun(entry.Foreground, entry.Run);
                        dc.Pop();

                        // Advance stateful block-comment tracking even when using stale cache
                        // so subsequent (non-cached) lines in the same frame have correct state.
                        ExternalHighlighter?.Highlight(line.Text, i);
                        RenderFoldCollapseLabel(dc, i, x, y);
                        continue;
                    }
                    // ── end OPT-A Fast path B ─────────────────────────────────────────

                    // Use external (language-pluggable) highlighter when available,
                    // otherwise fall back to the built-in JSON highlighter.
                    bool hasExternalHighlighter = ExternalHighlighter is not null;
                    IEnumerable<Helpers.SyntaxHighlightToken> rawTokens;

                    if (ExternalHighlighter is { } ext)
                    {
                        // OPT-A Fast path C: background pipeline has refreshed this line
                        // (IsCacheDirty=false) and fresh tokens are cached.  Use them directly
                        // instead of re-running the regex highlighter on the UI thread.
                        // ext.Highlight() is still called (result discarded) to keep the stateful
                        // block-comment tracker in sync for subsequent lines in this frame.
                        if (!line.IsCacheDirty && line.TokensCache is { Count: > 0 } freshTokens)
                        {
                            ext.Highlight(line.Text, i); // state tracking only — result discarded
                            rawTokens = freshTokens.Select(t => t with
                            {
                                Foreground = ResolveBrushForKind(t.Kind) ?? t.Foreground
                            });
                        }
                        else
                        {
                            // Resolve brushes at render time from live CodeEditor DPs (CE_* keys).
                            // This ensures correct colors even when the theme changes after file open,
                            // and avoids the timing issue of baking brushes at file-open time.
                            rawTokens = ext.Highlight(line.Text, i)
                                .Select(t => t with
                                {
                                    Foreground = ResolveBrushForKind(t.Kind) ?? t.Foreground
                                });
                        }
                    }
                    else
                    {
                        var jsonTokens = _highlighter.HighlightLine(line, context);
                        rawTokens = jsonTokens.Select(t => new Helpers.SyntaxHighlightToken(
                            t.StartColumn, t.Length, t.Text ?? string.Empty,
                            t.Foreground ?? EditorForeground, t.IsBold, t.IsItalic));
                    }

                    // URL post-pass: overlay URL tokens on top of the highlighter output.
                    // URLs are detected regardless of which highlighter is active and always
                    // rendered with SyntaxUrlColor + underline so they are visually distinct.
                    // Materialise to list so we can both render and cache in one pass.
                    var renderTokens = OverlayUrlTokens(line.Text, i, rawTokens).ToList();

                    // Pre-compute baseline Y once per line (GlyphRun requires it).
                    double baselineY = _glyphRenderer != null
                        ? y + _glyphRenderer.Baseline
                        : y + _charHeight * 0.8;

                    // Base pass (external highlighter only): draw the entire line in EditorForeground
                    // so unmatched spans (identifiers, punctuation not covered by any regex rule)
                    // remain visible in the default text color instead of being invisible.
                    if (hasExternalHighlighter)
                    {
                        var baseToken = new Helpers.SyntaxHighlightToken(
                            0, line.Text.Length, line.Text, EditorForeground);
                        if (_glyphRenderer != null)
                            _glyphRenderer.RenderToken(dc, baseToken, x, y, baselineY);
                        else
                        {
                            var ft = new FormattedText(
                                line.Text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                                _typeface, _fontSize, EditorForeground,
                                VisualTreeHelper.GetDpi(this).PixelsPerDip);
                            dc.DrawText(ft, new Point(x, y));
                        }
                    }

                    foreach (var token in renderTokens)
                    {
                        // Use tab-aware X so tokens on tab-indented lines are not shifted left.
                        double tokenX = x + (_glyphRenderer?.ComputeVisualX(line.Text, token.StartColumn)
                                             ?? token.StartColumn * _charWidth);

                        if (_glyphRenderer != null)
                        {
                            _glyphRenderer.RenderToken(dc, token, tokenX, y, baselineY);
                        }
                        else
                        {
                            // Safety fallback: FormattedText (e.g. before first measure pass).
                            var typeface = token.IsBold ? _boldTypeface : _typeface;
                            var ft = new FormattedText(
                                token.Text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                                typeface, _fontSize, token.Foreground,
                                VisualTreeHelper.GetDpi(this).PixelsPerDip);
                            if (token.IsItalic)
                                ft.SetFontStyle(FontStyles.Italic);
                            dc.DrawText(ft, new Point(tokenX, y));
                        }

                        // Draw underline only on the URL currently hovered by the mouse.
                        // This avoids permanent underlines on xmlns/href URIs in XML/XAML files.
                        if (ReferenceEquals(token.Foreground, SyntaxUrlColor)
                            && _hoveredUrlZone.HasValue
                            && _hoveredUrlZone.Value.Line     == i
                            && token.StartColumn              >= _hoveredUrlZone.Value.StartCol
                            && token.StartColumn              <  _hoveredUrlZone.Value.EndCol)
                        {
                            // Place the underline 2px below the text baseline (tight, VS-style).
                            double underlineY = baselineY + 2;
                            dc.DrawLine(urlPen,
                                new Point(tokenX, underlineY),
                                new Point(tokenX + token.Length * _charWidth, underlineY));
                        }
                    }

                    // ── P1-CE-05: Build GlyphRun cache after first render ─────────────
                    // Cache for the base-pass token too when using external highlighter.
                    if (_glyphRenderer != null)
                    {
                        var allCacheTokens = hasExternalHighlighter
                            ? Enumerable.Concat(
                                new[] { new Helpers.SyntaxHighlightToken(
                                    0, line.Text.Length, line.Text, EditorForeground) },
                                renderTokens)
                            : (IEnumerable<Helpers.SyntaxHighlightToken>)renderTokens;

                        line.GlyphRunCache     = _glyphRenderer.BuildLineGlyphRuns(allCacheTokens, SyntaxUrlColor, line.Text);
                        line.IsGlyphCacheDirty = false;

                        // Cache URL zones for GlyphRun-hit renders (no re-run of OverlayUrlTokens).
                        line.CachedUrlZones = _urlHitZones
                            .Where(z => z.Line == i)
                            .Select(z => (z.StartCol, z.EndCol, z.Url))
                            .ToList();
                    }
                    // ── end cache build ───────────────────────────────────────────────
                }

                // Draw fold-collapse label (handles both non-empty and empty opener lines).
                RenderFoldCollapseLabel(dc, i, x, y);
            }
        }

        /// <summary>
        /// Scans <paramref name="lineText"/> for HTTP/HTTPS URLs using <see cref="s_urlRegex"/>.
        /// For each match, replaces any existing tokens at the URL's columns with a new token
        /// colored with <see cref="SyntaxUrlColor"/>, and registers a <see cref="UrlHitZone"/>
        /// for cursor and Ctrl+Click handling.
        /// </summary>
        private IEnumerable<SyntaxHighlightToken> OverlayUrlTokens(
            string lineText, int lineIndex, IEnumerable<SyntaxHighlightToken> source)
        {
            // Fast-path: cheap string contains check avoids regex allocation on lines without URLs (OPT-PERF-04).
            if (!lineText.Contains("http", StringComparison.OrdinalIgnoreCase)) return source;
            var matches = s_urlRegex.Matches(lineText);
            if (matches.Count == 0) return source;

            // Materialise the source so we can splice URL tokens in.
            var result = source.ToList();
            var urlBrush = SyntaxUrlColor;

            foreach (Match m in matches)
            {
                // Register hit-zone for mouse interaction.
                _urlHitZones.Add(new UrlHitZone(lineIndex, m.Index, m.Index + m.Length, m.Value));

                // Remove any tokens that overlap the URL range (they'll be replaced).
                result.RemoveAll(t => t.StartColumn < m.Index + m.Length
                                   && t.StartColumn + t.Length > m.Index);

                // Add the URL token with SyntaxUrlColor (used as a sentinel in RenderTextContent).
                result.Add(new SyntaxHighlightToken(m.Index, m.Length, m.Value, urlBrush));
            }

            // Re-sort by start column so tokens render left-to-right.
            result.Sort(static (a, b) => a.StartColumn.CompareTo(b.StartColumn));
            return result;
        }

        /// <summary>
        /// Render cursor (simple rectangle for Phase 1)
        /// Phase 1: Static cursor, blinking will be added later
        /// </summary>
        private void RenderCursor(DrawingContext dc)
        {
            // Show cursor even without focus (but dimmed)
            bool hasFocus = IsFocused;

            // Check caret visibility for blinking effect (only blink when focused)
            if (hasFocus && !_caretVisible)
                return;

            if (_cursorLine < _firstVisibleLine || _cursorLine > _lastVisibleLine)
                return;

            // Use per-line Y lookup so the caret sits at the code-text Y on CodeLens lines.
            double y = _lineYLookup.TryGetValue(_cursorLine, out double cy) ? cy
                : (EnableVirtualScrolling && _virtualizationEngine != null
                    ? TopMargin + _virtualizationEngine.GetLineYPosition(_cursorLine)
                    : TopMargin + (_cursorLine - _firstVisibleLine) * _lineHeight);

            double x = (ShowLineNumbers ? TextAreaLeftOffset : LeftMargin) + (_cursorColumn * _charWidth);

            // Draw cursor as vertical line using DPs for color and width
            // When not focused, use 50% opacity to show inactive cursor
            Color caretColor = CaretColor;
            if (!hasFocus)
            {
                caretColor = Color.FromArgb(128, caretColor.R, caretColor.G, caretColor.B); // 50% opacity
            }

            var cursorPen = new Pen(new SolidColorBrush(caretColor), CaretWidth);
            cursorPen.Freeze();

            dc.DrawLine(cursorPen,
                new Point(x, y),
                new Point(x, y + _lineHeight - 2));
        }

        /// <summary>
        /// Render validation errors as squiggly lines (Phase 5)
        /// </summary>
        private void RenderValidationErrors(DrawingContext dc)
        {
            if (_validationErrors == null || _validationErrors.Count == 0)
                return;

            double leftEdge = ShowLineNumbers ? TextAreaLeftOffset : LeftMargin;

            foreach (var error in _validationErrors)
            {
                // Skip if not visible
                if (error.Line < _firstVisibleLine || error.Line > _lastVisibleLine)
                    continue;

                double y = TopMargin + (EnableVirtualScrolling && _virtualizationEngine != null
                    ? _virtualizationEngine.GetLineYPosition(error.Line)
                    : (error.Line - _firstVisibleLine) * _lineHeight) + _lineHeight - 3;
                double x1 = leftEdge + (error.Column * _charWidth);
                double x2 = x1 + (error.Length * _charWidth);

                // OPT-PERF-02: use pre-cached frozen pens — no allocation per error.
                Pen squigglyPen = error.Severity switch
                {
                    ValidationSeverity.Warning => s_squigglyWarning,
                    ValidationSeverity.Info    => s_squigglyInfo,
                    _                          => s_squigglyError,
                };

                DrawSquigglyLine(dc, x1, x2, y, squigglyPen);
            }
        }

        /// <summary>
        /// Draws a squiggly (wavy) underline using direct dc.DrawLine calls with a pre-cached pen.
        /// Avoids StreamGeometry + Pen allocations per error per frame (OPT-PERF-02).
        /// </summary>
        private static void DrawSquigglyLine(DrawingContext dc, double x1, double x2, double y, Pen pen)
        {
            double x  = x1;
            bool   up = true;
            while (x + 2 <= x2)
            {
                double xNext = x + 2;
                dc.DrawLine(pen, new Point(x, y), new Point(xNext, y + (up ? -2 : 2)));
                x  = xNext;
                up = !up;
            }
        }

        /// <summary>
        /// Render bracket matching highlights (Phase 6)
        /// </summary>
        private void RenderBracketMatching(DrawingContext dc)
        {
            if (_cursorColumn < 0 || _cursorLine < 0 || _cursorLine >= _document.Lines.Count)
                return;

            // No bracket matching on collapsed directive opener lines (text is blanked).
            if (_foldingEngine?.GetRegionAt(_cursorLine) is { IsCollapsed: true, Kind: FoldingRegionKind.Directive })
                return;

            var line = _document.Lines[_cursorLine];

            // Check character before cursor
            char? charBeforeCursor = null;
            int charBeforePos = _cursorColumn - 1;
            if (charBeforePos >= 0 && charBeforePos < line.Text.Length)
            {
                charBeforeCursor = line.Text[charBeforePos];
            }

            // Check character at cursor
            char? charAtCursor = null;
            if (_cursorColumn < line.Text.Length)
            {
                charAtCursor = line.Text[_cursorColumn];
            }

            // Try to find matching bracket
            TextPosition? matchPos = null;
            char? bracketChar = null;
            int bracketColumn = -1;

            // Check if cursor is ON a bracket
            if (charAtCursor.HasValue && IsBracket(charAtCursor.Value))
            {
                bracketChar = charAtCursor.Value;
                bracketColumn = _cursorColumn;
                matchPos = FindMatchingBracket(_cursorLine, _cursorColumn, charAtCursor.Value);
            }
            // Check if cursor is AFTER a bracket (more common)
            else if (charBeforeCursor.HasValue && IsBracket(charBeforeCursor.Value))
            {
                bracketChar = charBeforeCursor.Value;
                bracketColumn = charBeforePos;
                matchPos = FindMatchingBracket(_cursorLine, charBeforePos, charBeforeCursor.Value);
            }

            // Highlight both brackets if match found
            if (matchPos.HasValue && bracketColumn >= 0)
            {
                var highlightBrush = new SolidColorBrush(Color.FromArgb(80, 0, 120, 215)); // Semi-transparent blue
                highlightBrush.Freeze();

                var borderPen = new Pen(new SolidColorBrush(Color.FromRgb(0, 120, 215)), 1.5);
                borderPen.Freeze();

                // Highlight bracket at cursor
                HighlightBracket(dc, _cursorLine, bracketColumn, highlightBrush, borderPen);

                // Highlight matching bracket
                HighlightBracket(dc, matchPos.Value.Line, matchPos.Value.Column, highlightBrush, borderPen);
            }
        }

        /// <summary>
        /// Highlight a single bracket
        /// </summary>
        private void HighlightBracket(DrawingContext dc, int line, int column, Brush background, Pen border)
        {
            if (line < _firstVisibleLine || line > _lastVisibleLine)
                return;

            // Use _lineYLookup to account for CodeLens hint zone height offset (same pattern as RenderCursor/RenderSelection/RenderWordHighlights)
            double y = _lineYLookup.TryGetValue(line, out double by) ? by
                : (EnableVirtualScrolling && _virtualizationEngine != null
                    ? TopMargin + _virtualizationEngine.GetLineYPosition(line)
                    : TopMargin + (line - _firstVisibleLine) * _lineHeight);
            double x = (ShowLineNumbers ? TextAreaLeftOffset : LeftMargin) + (column * _charWidth);

            // Draw background highlight
            dc.DrawRectangle(background, null, new Rect(x, y, _charWidth, _lineHeight));

            // Draw border
            dc.DrawRectangle(null, border, new Rect(x, y, _charWidth, _lineHeight));
        }

        /// <summary>
        /// Check if character is a bracket
        /// </summary>
        private bool IsBracket(char ch)
        {
            return ch == '(' || ch == ')' || ch == '[' || ch == ']' || ch == '{' || ch == '}';
        }

        /// <summary>
        /// Find matching bracket for given position
        /// </summary>
        private TextPosition? FindMatchingBracket(int line, int column, char bracket)
        {
            if (line < 0 || line >= _document.Lines.Count)
                return null;

            // Determine direction and matching bracket
            bool searchForward;
            char matchingBracket;

            switch (bracket)
            {
                case '(':
                    searchForward = true;
                    matchingBracket = ')';
                    break;
                case ')':
                    searchForward = false;
                    matchingBracket = '(';
                    break;
                case '[':
                    searchForward = true;
                    matchingBracket = ']';
                    break;
                case ']':
                    searchForward = false;
                    matchingBracket = '[';
                    break;
                case '{':
                    searchForward = true;
                    matchingBracket = '}';
                    break;
                case '}':
                    searchForward = false;
                    matchingBracket = '{';
                    break;
                default:
                    return null;
            }

            if (searchForward)
            {
                return FindMatchingBracketForward(line, column + 1, bracket, matchingBracket);
            }
            else
            {
                return FindMatchingBracketBackward(line, column - 1, bracket, matchingBracket);
            }
        }

        /// <summary>
        /// Search forward for matching bracket
        /// </summary>
        private TextPosition? FindMatchingBracketForward(int startLine, int startColumn, char openBracket, char closeBracket)
        {
            int depth = 1;
            bool inString = false;
            bool escaped = false;

            for (int lineIdx = startLine; lineIdx < _document.Lines.Count; lineIdx++)
            {
                var line = _document.Lines[lineIdx];
                int start = (lineIdx == startLine) ? startColumn : 0;

                for (int col = start; col < line.Text.Length; col++)
                {
                    char ch = line.Text[col];

                    // Handle escape sequences
                    if (escaped)
                    {
                        escaped = false;
                        continue;
                    }

                    if (ch == '\\')
                    {
                        escaped = true;
                        continue;
                    }

                    // Handle strings (skip brackets inside strings)
                    if (ch == '"')
                    {
                        inString = !inString;
                        continue;
                    }

                    if (inString)
                        continue;

                    // Check brackets
                    if (ch == openBracket)
                    {
                        depth++;
                    }
                    else if (ch == closeBracket)
                    {
                        depth--;
                        if (depth == 0)
                        {
                            return new TextPosition(lineIdx, col);
                        }
                    }
                }
            }

            return null; // No match found
        }

        /// <summary>
        /// Search backward for matching bracket
        /// </summary>
        private TextPosition? FindMatchingBracketBackward(int startLine, int startColumn, char closeBracket, char openBracket)
        {
            int depth = 1;

            for (int lineIdx = startLine; lineIdx >= 0; lineIdx--)
            {
                var line = _document.Lines[lineIdx];
                int start = (lineIdx == startLine) ? startColumn : line.Text.Length - 1;

                for (int col = start; col >= 0; col--)
                {
                    char ch = line.Text[col];

                    // Simple check (doesn't handle strings perfectly in backward direction)
                    // This is acceptable for most cases
                    if (ch == closeBracket)
                    {
                        depth++;
                    }
                    else if (ch == openBracket)
                    {
                        depth--;
                        if (depth == 0)
                        {
                            return new TextPosition(lineIdx, col);
                        }
                    }
                }
            }

            return null; // No match found
        }

        #endregion

        #region Keyboard Input Handling (Phase 1)

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            // Track Ctrl key to enable symbol underline + Ctrl+Click navigation.
            if ((e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl) && !_ctrlDown)
            {
                _ctrlDown = true;
                InvalidateVisual();
            }

            // Reset caret blink on keypress
            ResetCaretBlink();

            // Block editing input when read-only (navigation keys still allowed below)
            if (IsReadOnly)
            {
                bool isNavigationOrCopy = e.Key is Key.Left or Key.Right or Key.Up or Key.Down
                    or Key.Home or Key.End or Key.PageUp or Key.PageDown or Key.Escape
                    || (e.Key == Key.C && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
                    || (e.Key == Key.A && (Keyboard.Modifiers & ModifierKeys.Control) != 0);
                if (!isNavigationOrCopy) { e.Handled = true; return; }
            }

            bool ctrlPressed = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
            bool shiftPressed = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;

            // Cancel any pending outline chord on any key press without Ctrl held.
            if (!ctrlPressed) _outlineChordPending = false;

            switch (e.Key)
            {
                case Key.Left:
                    if (ctrlPressed) MoveWordLeft(shiftPressed);
                    else             MoveCursor(-1, 0, shiftPressed);
                    e.Handled = true;
                    break;

                case Key.Right:
                    if (ctrlPressed) MoveWordRight(shiftPressed);
                    else             MoveCursor(1, 0, shiftPressed);
                    e.Handled = true;
                    break;

                case Key.Up:
                    MoveCursor(0, -1, shiftPressed);
                    e.Handled = true;
                    break;

                case Key.Down:
                    MoveCursor(0, 1, shiftPressed);
                    e.Handled = true;
                    break;

                case Key.Home:
                    if (ctrlPressed) MoveCursorToDocumentStart(shiftPressed);
                    else             MoveCursorToLineStart(shiftPressed);
                    e.Handled = true;
                    break;

                case Key.End:
                    if (ctrlPressed) MoveCursorToDocumentEnd(shiftPressed);
                    else             MoveCursorToLineEnd(shiftPressed);
                    e.Handled = true;
                    break;

                case Key.Enter:
                    InsertNewLine();
                    e.Handled = true;
                    break;

                case Key.Back:
                    if (!_selection.IsEmpty)
                        DeleteSelection();
                    else
                        DeleteCharBefore();
                    e.Handled = true;
                    break;

                case Key.Delete:
                    DeleteCharAfter();
                    e.Handled = true;
                    break;

                case Key.Tab:
                    // Try snippet expansion first; fall back to regular tab insertion.
                    if (!TryExpandSnippet())
                        InsertTab();
                    e.Handled = true;
                    break;

                // Clipboard operations (Phase 3)
                case Key.C:
                    if (ctrlPressed)
                    {
                        CopyToClipboard();
                        e.Handled = true;
                    }
                    break;

                case Key.V:
                    if (ctrlPressed)
                    {
                        PasteFromClipboard();
                        e.Handled = true;
                    }
                    break;

                case Key.X:
                    if (ctrlPressed)
                    {
                        CutToClipboard();
                        e.Handled = true;
                    }
                    break;

                // Undo/Redo
                case Key.Z:
                    if (ctrlPressed && shiftPressed)   // Ctrl+Shift+Z = alternate Redo
                    {
                        Redo();
                        e.Handled = true;
                    }
                    else if (ctrlPressed)
                    {
                        Undo();
                        e.Handled = true;
                    }
                    break;

                case Key.Y:
                    if (ctrlPressed)
                    {
                        Redo();
                        e.Handled = true;
                    }
                    break;

                // Select All (Phase 3)
                case Key.A:
                    if (ctrlPressed)
                    {
                        SelectAll();
                        e.Handled = true;
                    }
                    break;

                // IntelliSense trigger (Phase 4)
                case Key.Space:
                    if (ctrlPressed && _enableIntelliSense)
                    {
                        TriggerIntelliSense();
                        e.Handled = true;
                    }
                    break;

                // ── Folding keyboard shortcuts (P2-02) ─────────────────────
                // Ctrl+M → toggle fold at caret line
                // ── Outlining chord: Ctrl+M arms the chord; second key executes action ────
                case Key.M:
                    if (ctrlPressed && IsFoldingEnabled)
                    {
                        if (_outlineChordPending)
                        {
                            _outlineChordPending = false;
                            OutlineToggleCurrent(); // Ctrl+M, Ctrl+M
                        }
                        else
                        {
                            _outlineChordPending = true; // arm chord, wait for second key
                        }
                        e.Handled = true;
                    }
                    break;

                case Key.L:
                    if (ctrlPressed && _outlineChordPending)
                    {
                        _outlineChordPending = false;
                        OutlineToggleAll(); // Ctrl+M, Ctrl+L
                        e.Handled = true;
                    }
                    break;

                case Key.P:
                    if (ctrlPressed && _outlineChordPending)
                    {
                        _outlineChordPending = false;
                        OutlineStop(); // Ctrl+M, Ctrl+P
                        e.Handled = true;
                    }
                    break;

                case Key.U:
                    if (ctrlPressed && _outlineChordPending)
                    {
                        _outlineChordPending = false;
                        OutlineStopHidingCurrent(); // Ctrl+M, Ctrl+U
                        e.Handled = true;
                    }
                    break;

                case Key.O:
                    if (ctrlPressed && _outlineChordPending)
                    {
                        _outlineChordPending = false;
                        OutlineCollapseToDefinitions(); // Ctrl+M, Ctrl+O
                        e.Handled = true;
                    }
                    break;
                // ────────────────────────────────────────────────────────────────────────
                // Ctrl+Shift+[ → collapse all folds
                case Key.OemOpenBrackets:
                    if (ctrlPressed && shiftPressed && IsFoldingEnabled && _foldingEngine != null)
                    {
                        _foldingEngine.CollapseAll();
                        InvalidateVisual();
                        e.Handled = true;
                    }
                    break;
                // Ctrl+Shift+] → expand all folds
                case Key.OemCloseBrackets:
                    if (ctrlPressed && shiftPressed && IsFoldingEnabled && _foldingEngine != null)
                    {
                        _foldingEngine.ExpandAll();
                        InvalidateVisual();
                        e.Handled = true;
                    }
                    break;
                // ───────────────────────────────────────────────────────────

                case Key.Escape:
                    // Feature A: clear rectangular selection first.
                    if (!_rectSelection.IsEmpty)
                    {
                        _rectSelection.Clear();
                        _isRectSelecting = false;
                        InvalidateVisual();
                        e.Handled = true;
                        break;
                    }

                    // Feature B: cancel active drag-to-move.
                    if (_dragDrop.Phase != DragPhase.None)
                    {
                        if (_dragDrop.Phase == DragPhase.Dragging)
                            ReleaseMouseCapture();
                        Cursor = Cursors.IBeam;
                        // Restore original selection.
                        _selection.Start = _dragDrop.SelectionStart;
                        _selection.End   = _dragDrop.SelectionEnd;
                        _cursorLine   = _dragDrop.SelectionEnd.Line;
                        _cursorColumn = _dragDrop.SelectionEnd.Column;
                        _dragDrop.Reset();
                        InvalidateVisual();
                        e.Handled = true;
                        break;
                    }

                    // Dismiss Quick Info popup on Escape.
                    _quickInfoPopup?.Hide();
                    _hoverQuickInfoService?.Cancel();
                    e.Handled = _quickInfoPopup?.IsShowing == true;
                    break;
            }

            InvalidateVisual();
        }

        protected override void OnTextInput(TextCompositionEventArgs e)
        {
            base.OnTextInput(e);

            // Reset caret blink on text input
            ResetCaretBlink();

            if (!string.IsNullOrEmpty(e.Text))
            {
                foreach (char ch in e.Text)
                {
                    // Skip control characters
                    if (char.IsControl(ch))
                        continue;

                    InsertChar(ch);

                    // Auto-close brackets and quotes
                    if (ShouldAutoClose(ch))
                    {
                        char closingChar = GetClosingChar(ch);
                        InsertChar(closingChar);
                        // Move cursor back one position to be inside the pair
                        _cursorColumn--;
                    }

                    // Auto-trigger IntelliSense on specific characters (Phase 4)
                    if (EnableIntelliSense && ShouldAutoTriggerIntelliSense(ch))
                    {
                        TriggerIntelliSenseWithDelay();
                    }
                }
                // OPT-B: InvalidateVisual() removed — Document_TextChanged fires in the same
                // call stack and already calls InvalidateVisual() or InvalidateMeasure() as
                // appropriate via smart-invalidation routing.  Calling it again here produced
                // a guaranteed double render on every single keystroke.
            }
        }

        #endregion

        #region Cursor Movement

        private void MoveCursor(int deltaColumn, int deltaLine, bool extendSelection)
        {
            // Save old position for selection
            var oldPosition = new TextPosition(_cursorLine, _cursorColumn);

            // Move line
            if (deltaLine != 0)
            {
                _cursorLine = Math.Max(0, Math.Min(_document.Lines.Count - 1, _cursorLine + deltaLine));
                // Clamp column to line length
                _cursorColumn = Math.Min(_cursorColumn, _document.Lines[_cursorLine].Length);
            }

            // Move column
            if (deltaColumn != 0)
            {
                _cursorColumn += deltaColumn;

                // Handle line boundaries
                if (_cursorColumn < 0 && _cursorLine > 0)
                {
                    // Move to end of previous line
                    _cursorLine--;
                    _cursorColumn = _document.Lines[_cursorLine].Length;
                }
                else if (_cursorColumn > _document.Lines[_cursorLine].Length && _cursorLine < _document.Lines.Count - 1)
                {
                    // Move to start of next line
                    _cursorLine++;
                    _cursorColumn = 0;
                }
                else
                {
                    // Clamp to line bounds
                    _cursorColumn = Math.Max(0, Math.Min(_document.Lines[_cursorLine].Length, _cursorColumn));
                }
            }

            // Handle selection
            if (extendSelection)
            {
                if (_selection.IsEmpty)
                    _selection.Start = oldPosition;
                _selection.End = new TextPosition(_cursorLine, _cursorColumn);
            }
            else
            {
                _selection.Clear();
            }

            // Phase 11.3: Ensure cursor stays visible when using virtual scrolling
            EnsureCursorVisible();
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);

            // Clear Ctrl+hover state when Ctrl is released.
            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
            {
                _ctrlDown = false;
                _hoveredSymbolZone = null;
                _ctrlClickService?.Cancel();
                Cursor = Cursors.IBeam;
                InvalidateVisual();
            }
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);

            if (_hoveredUrlZone.HasValue)
            {
                _hoveredUrlZone = null;
                Cursor = Cursors.IBeam;
                HideUrlTooltip();
                InvalidateVisual();
            }

            if (_hoveredLensLine >= 0)
            {
                _hoveredLensLine = -1;
                ToolTip = null;
                InvalidateVisual();
            }

            // Cancel fold peek on editor exit.
            _foldPeekTargetLine = -1;
            _foldPeekTimer?.Stop();
            _foldPeekPopup?.Hide();

            // Start Quick Info grace timer — popup stays open for 200 ms so mouse can enter it.
            _quickInfoPopup?.OnEditorMouseLeft();
            _hoverQuickInfoService?.Cancel();

            // Clear Ctrl+hover state on mouse leave.
            if (_ctrlDown)
            {
                _ctrlDown = false;
                _hoveredSymbolZone = null;
                _ctrlClickService?.Cancel();
                InvalidateVisual();
            }
        }

        private void ShowUrlTooltip()
        {
            _urlTooltip ??= new ToolTip { Content = "Ctrl+Click to open" };
            _urlTooltip.PlacementTarget = this;
            _urlTooltip.Placement       = System.Windows.Controls.Primitives.PlacementMode.Mouse;
            _urlTooltip.IsOpen          = true;
        }

        private void HideUrlTooltip()
        {
            if (_urlTooltip is not null)
                _urlTooltip.IsOpen = false;
        }

        private void MoveCursorToLineStart(bool extendSelection)
        {
            var oldPosition = new TextPosition(_cursorLine, _cursorColumn);
            _cursorColumn = 0;

            if (extendSelection)
            {
                if (_selection.IsEmpty)
                    _selection.Start = oldPosition;
                _selection.End = new TextPosition(_cursorLine, _cursorColumn);
            }
            else
            {
                _selection.Clear();
            }
        }

        private void MoveCursorToLineEnd(bool extendSelection)
        {
            var oldPosition = new TextPosition(_cursorLine, _cursorColumn);
            _cursorColumn = _document.Lines[_cursorLine].Length;

            if (extendSelection)
            {
                if (_selection.IsEmpty)
                    _selection.Start = oldPosition;
                _selection.End = new TextPosition(_cursorLine, _cursorColumn);
            }
            else
            {
                _selection.Clear();
            }
        }

        private void MoveCursorToDocumentStart(bool extendSelection)
        {
            var oldPosition = new TextPosition(_cursorLine, _cursorColumn);
            _cursorLine   = 0;
            _cursorColumn = 0;

            if (extendSelection)
            {
                if (_selection.IsEmpty)
                    _selection.Start = oldPosition;
                _selection.End = new TextPosition(_cursorLine, _cursorColumn);
            }
            else
            {
                _selection.Clear();
            }

            EnsureCursorVisible();
        }

        private void MoveCursorToDocumentEnd(bool extendSelection)
        {
            var oldPosition = new TextPosition(_cursorLine, _cursorColumn);
            _cursorLine   = Math.Max(0, _document.Lines.Count - 1);
            _cursorColumn = _document.Lines[_cursorLine].Length;

            if (extendSelection)
            {
                if (_selection.IsEmpty)
                    _selection.Start = oldPosition;
                _selection.End = new TextPosition(_cursorLine, _cursorColumn);
            }
            else
            {
                _selection.Clear();
            }

            EnsureCursorVisible();
        }

        private void MoveWordLeft(bool extendSelection)
        {
            var oldPosition = new TextPosition(_cursorLine, _cursorColumn);
            string line = _document.Lines[_cursorLine].Text;
            int col = _cursorColumn;

            // Skip non-word chars to the left (punctuation, whitespace)
            while (col > 0 && !IsWordChar(line[col - 1])) col--;
            // Skip word chars to the left
            while (col > 0 && IsWordChar(line[col - 1])) col--;

            if (col == _cursorColumn && _cursorLine > 0)
            {
                // Step to end of previous line
                _cursorLine--;
                _cursorColumn = _document.Lines[_cursorLine].Text.Length;
            }
            else
            {
                _cursorColumn = col;
            }

            if (extendSelection)
            {
                if (_selection.IsEmpty)
                    _selection.Start = oldPosition;
                _selection.End = new TextPosition(_cursorLine, _cursorColumn);
            }
            else
            {
                _selection.Clear();
            }

            EnsureCursorVisible();
        }

        private void MoveWordRight(bool extendSelection)
        {
            var oldPosition = new TextPosition(_cursorLine, _cursorColumn);
            string line = _document.Lines[_cursorLine].Text;
            int col = _cursorColumn;

            // Skip word chars to the right
            while (col < line.Length && IsWordChar(line[col])) col++;
            // Skip non-word chars to the right (punctuation, whitespace)
            while (col < line.Length && !IsWordChar(line[col])) col++;

            if (col == _cursorColumn && _cursorLine < _document.Lines.Count - 1)
            {
                // Step to start of next line
                _cursorLine++;
                _cursorColumn = 0;
            }
            else
            {
                _cursorColumn = col;
            }

            if (extendSelection)
            {
                if (_selection.IsEmpty)
                    _selection.Start = oldPosition;
                _selection.End = new TextPosition(_cursorLine, _cursorColumn);
            }
            else
            {
                _selection.Clear();
            }

            EnsureCursorVisible();
        }

        #endregion

        #region Text Editing Operations

        private void InsertChar(char ch)
        {
            _document.InsertChar(_cursorLine, _cursorColumn, ch);
            _cursorColumn++;
        }

        private void InsertNewLine()
        {
            _document.InsertNewLine(_cursorLine, _cursorColumn);
            _cursorLine++;
            _cursorColumn = CalculateAutoIndentColumn();
        }

        private void InsertTab()
        {
            // Insert spaces for tab (respects IndentSize)
            int spacesToInsert = _document.IndentSize - (_cursorColumn % _document.IndentSize);
            for (int i = 0; i < spacesToInsert; i++)
            {
                InsertChar(' ');
            }
        }

        /// <summary>
        /// Reads the word immediately left of the cursor and tries to expand it as a snippet.
        /// </summary>
        /// <returns><c>true</c> if a snippet was found and applied.</returns>
        private bool TryExpandSnippet()
        {
            var mgr = SnippetManager;
            if (mgr == null || _cursorColumn == 0)
                return false;

            string lineText = _document.Lines[_cursorLine].Text ?? string.Empty;

            // Extract the non-whitespace word immediately to the left of the caret.
            int end   = _cursorColumn;
            int start = end - 1;
            while (start > 0 && !char.IsWhiteSpace(lineText[start - 1]))
                start--;

            if (start >= end)
                return false;

            string word = lineText.Substring(start, end - start);

            if (!mgr.TryExpand(word, out var snippet))
                return false;

            var expansion = SnippetManager.BuildExpansion(snippet, _cursorLine, start, word.Length);
            ApplySnippetExpansion(expansion);
            return true;
        }

        /// <summary>
        /// Deletes the trigger text and inserts the expanded snippet body,
        /// then positions the caret at the <c>$cursor</c> marker location.
        /// </summary>
        private void ApplySnippetExpansion(SnippetExpansion expansion)
        {
            // Delete trigger: range [InsertColumn .. InsertColumn + TriggerLength).
            _document.DeleteRange(
                new TextPosition(expansion.InsertLine, expansion.InsertColumn),
                new TextPosition(expansion.InsertLine, expansion.InsertColumn + expansion.TriggerLength));

            // Insert expanded body at the now-empty position.
            _document.InsertText(
                new TextPosition(expansion.InsertLine, expansion.InsertColumn),
                expansion.ExpandedText);

            // Move caret to the $cursor position.
            _cursorLine   = expansion.CaretLine;
            _cursorColumn = expansion.CaretColumn;

            EnsureCursorVisible();
            InvalidateVisual();
        }

        /// <summary>
        /// Check if a character should trigger auto-closing
        /// </summary>
        private bool ShouldAutoClose(char ch)
        {
            switch (ch)
            {
                case '{':
                case '[':
                case '(':
                    return EnableAutoClosingBrackets;
                case '"':
                case '\'':
                    return EnableAutoClosingQuotes;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Get the closing character for an opening character
        /// </summary>
        private char GetClosingChar(char ch)
        {
            switch (ch)
            {
                case '{': return '}';
                case '[': return ']';
                case '(': return ')';
                case '"': return '"';
                case '\'': return '\'';
                default: return ch;
            }
        }

        private void DeleteCharBefore()
        {
            if (_cursorColumn > 0)
            {
                // SmartBackspace: Delete by indent level if on leading whitespace
                if (SmartBackspace && IsOnLeadingWhitespace())
                {
                    var line = _document.Lines[_cursorLine];
                    int spaces = _cursorColumn;
                    int indentSize = IndentSize;

                    // Calculate how many spaces to delete to reach previous indent level
                    int spacesToDelete = spaces % indentSize;
                    if (spacesToDelete == 0)
                        spacesToDelete = indentSize;

                    // Delete multiple spaces
                    for (int i = 0; i < spacesToDelete && _cursorColumn > 0; i++)
                    {
                        _cursorColumn--;
                        _document.DeleteChar(_cursorLine, _cursorColumn);
                    }
                }
                else
                {
                    // Regular backspace - delete single character
                    _cursorColumn--;
                    _document.DeleteChar(_cursorLine, _cursorColumn);
                }
            }
            else if (_cursorLine > 0)
            {
                // Delete newline - merge with previous line
                int prevLineLength = _document.Lines[_cursorLine - 1].Length;
                _document.Lines[_cursorLine - 1].Text += _document.Lines[_cursorLine].Text;
                _document.DeleteLine(_cursorLine);
                _cursorLine--;
                _cursorColumn = prevLineLength;
            }
        }

        /// <summary>
        /// Check if cursor is on leading whitespace
        /// </summary>
        private bool IsOnLeadingWhitespace()
        {
            if (_cursorLine >= _document.Lines.Count)
                return false;

            var line = _document.Lines[_cursorLine];

            // Check if all characters before cursor are spaces
            for (int i = 0; i < _cursorColumn && i < line.Text.Length; i++)
            {
                if (line.Text[i] != ' ')
                    return false;
            }

            return true;
        }

        private void DeleteCharAfter()
        {
            var currentLine = _document.Lines[_cursorLine];

            if (_cursorColumn < currentLine.Length)
            {
                _document.DeleteChar(_cursorLine, _cursorColumn);
            }
            else if (_cursorLine < _document.Lines.Count - 1)
            {
                // Delete newline - merge with next line
                currentLine.Text += _document.Lines[_cursorLine + 1].Text;
                _document.DeleteLine(_cursorLine + 1);
            }
        }

        private int CalculateAutoIndentColumn()
        {
            if (_cursorLine >= _document.Lines.Count)
                return 0;

            var line = _document.Lines[_cursorLine];
            int spaces = 0;

            foreach (char ch in line.Text)
            {
                if (ch == ' ')
                    spaces++;
                else
                    break;
            }

            return spaces;
        }

        #endregion

        #region Mouse Input Handling (Phase 3)

        /// <summary>
        /// Handle mouse wheel for vertical scrolling (Phase 11.3)
        /// </summary>
        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);

            // Swallow wheel events while the references popup is open so the editor
            // does not scroll under it. The user must dismiss the popup first.
            if (_referencesPopup?.IsOpen == true)
            {
                e.Handled = true;
                return;
            }

            _quickInfoPopup?.Hide();

            // Ctrl + wheel → zoom in / out (B6).
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                double step  = e.Delta > 0 ? 0.1 : -0.1;
                ZoomLevel = Math.Clamp(ZoomLevel + step, 0.5, 4.0);
                e.Handled = true;
                return;
            }

            if (Keyboard.Modifiers == ModifierKeys.Shift)
            {
                // Horizontal scroll: one notch = resolved speed chars (matches HexEditor model).
                int hSpeed    = MouseWheelSpeed == MouseWheelSpeed.System
                    ? SystemParameters.WheelScrollLines
                    : (int)MouseWheelSpeed;
                double hDelta = -Math.Sign(e.Delta) * hSpeed * _charWidth * HorizontalScrollSensitivity;
                double maxH   = _hScrollBar?.Maximum ?? 0;
                _horizontalScrollOffset = Math.Max(0, Math.Min(maxH, _horizontalScrollOffset + hDelta));
                SyncHScrollBar();
                InvalidateVisual();
                e.Handled = true;
            }
            else if (EnableVirtualScrolling && _virtualizationEngine != null)
            {
                // Vertical scroll: same model as HexEditor.
                // MouseWheelSpeed.System → WheelScrollLines, else cast enum value directly.
                int    speed      = MouseWheelSpeed == MouseWheelSpeed.System
                    ? SystemParameters.WheelScrollLines
                    : (int)MouseWheelSpeed;
                double pixelDelta = -Math.Sign(e.Delta) * speed * _lineHeight;
                ScrollVertical(pixelDelta);

                // If a drag-selection is in progress, keep the selection end anchored to
                // the text position under the mouse after the viewport has moved.
                if (_isSelecting)
                {
                    var mousePos = e.GetPosition(this);
                    _lastMousePosition = mousePos;
                    var textPos = PixelToTextPosition(mousePos);
                    _selection.End = textPos;
                    _cursorLine    = textPos.Line;
                    _cursorColumn  = textPos.Column;
                }

                e.Handled = true;
            }
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);

            // Dismiss any open references popup on any click in the editor,
            // but NOT when the click originated inside the popup itself.
            // Two complementary guards cover both WPF-routing and Win32 click-through paths:
            //   1. IsEventFromInsidePopup — detects via PresentationSource (HWND-aware)
            //   2. IsClickInsidePopupBounds — screen-coordinate fallback
            if (!IsEventFromInsidePopup(e.OriginalSource) && !IsClickInsidePopupBounds(e.GetPosition(this)))
                _referencesPopup?.Close();

            // Dismiss Quick Info popup and cancel pending hover on any click.
            _quickInfoPopup?.Hide();
            _hoverQuickInfoService?.Cancel();

            Focus(); // Ensure editor gets keyboard focus

            var pos = e.GetPosition(this);
            var textPos = PixelToTextPosition(pos);

            // Right-click behavior: don't clear selection if clicking inside it
            if (e.RightButton == MouseButtonState.Pressed)
            {
                // Check if click is inside existing selection
                if (!_selection.IsEmpty && IsPositionInSelection(textPos))
                {
                    // Don't clear selection, just let context menu open
                    e.Handled = true;
                    return;
                }
                else
                {
                    // Click outside selection - move cursor and clear selection
                    _cursorLine = textPos.Line;
                    _cursorColumn = textPos.Column;
                    _selection.Start = textPos;
                    _selection.End = textPos;
                    InvalidateVisual();
                    NotifyCaretMovedIfChanged();
                    return;
                }
            }

            // Left-click (or double-click when FoldToggleOnDoubleClick is set) on an inline
            // fold-collapse label → toggle the fold.
            bool foldClickOk = e.LeftButton == MouseButtonState.Pressed
                && (!FoldToggleOnDoubleClick || e.ClickCount == 2);
            if (foldClickOk && _foldLabelHitZones.Count > 0)
            {
                var clickPos = e.GetPosition(this);
                foreach (var (rect, line) in _foldLabelHitZones)
                {
                    if (rect.Contains(clickPos))
                    {
                        _foldingEngine?.ToggleRegion(line);
                        e.Handled = true;
                        return;
                    }
                }
            }

            // Left-click on a CodeLens hint → navigate cursor onto the symbol and open references popup.
            if (ShowCodeLens && e.LeftButton == MouseButtonState.Pressed && _lensHitZones.Count > 0)
            {
                var clickPos = e.GetPosition(this);
                foreach (var (zone, lineIdx, symbol) in _lensHitZones)
                {
                    if (zone.Contains(clickPos))
                    {
                        // Do NOT move the caret — pass line/symbol directly so the
                        // user's cursor position is preserved.
                        _ = FindAllReferencesAsync(lineOverride: lineIdx, symbolOverride: symbol);
                        e.Handled = true;
                        return;
                    }
                }
            }

            // Ctrl+Left-click on a URL → open in browser.
            if (e.LeftButton == MouseButtonState.Pressed
                && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                var urlZone = FindUrlZone(textPos.Line, textPos.Column);
                if (urlZone.HasValue)
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo(urlZone.Value.Url) { UseShellExecute = true });
                    }
                    catch { /* Ignore failures to launch browser (e.g. malformed URL) */ }
                    e.Handled = true;
                    return;
                }

                // Ctrl+Left-click on a symbol → Go to Definition.
                if (_hoveredSymbolZone.HasValue)
                {
                    _ = NavigateToDefinitionAsync(_hoveredSymbolZone.Value);
                    e.Handled = true;
                    return;
                }
            }

            // Block caret placement when clicking in the CodeLens hint zone
            // (the LensLineHeight strip above the code text of a declaration line).
            if (ShowCodeLens
                && _lineYLookup.TryGetValue(textPos.Line, out double codeTextY)
                && pos.Y < codeTextY)
            {
                e.Handled = true;
                return;
            }

            // Feature A: Alt+LeftClick → start rectangular selection.
            // e.Handled = true prevents menu-bar Alt activation.
            bool altDown = (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt;
            if (altDown && e.LeftButton == MouseButtonState.Pressed && e.ClickCount == 1)
            {
                _isSelecting    = false;
                _isRectSelecting = true;
                _selection.Clear();
                _rectSelection.Begin(textPos);
                _cursorLine   = textPos.Line;
                _cursorColumn = textPos.Column;
                CaptureMouse();
                InvalidateVisual();
                NotifyCaretMovedIfChanged();
                e.Handled = true;
                return;
            }

            // Any non-Alt, single click: check for rect-block drag BEFORE clearing the rect.
            if (!_rectSelection.IsEmpty && e.ClickCount == 1
                && IsInsideRectBlock(textPos.Line, textPos.Column))
            {
                // Click inside the active rect block → start potential rect drag-to-move.
                _dragDrop.Phase           = DragPhase.Pending;
                _dragDrop.ClickPixel      = pos;
                _dragDrop.ClickedPosition = textPos;
                _dragDrop.SelectionStart  = new TextPosition(_rectSelection.TopLine,    _rectSelection.LeftColumn);
                _dragDrop.SelectionEnd    = new TextPosition(_rectSelection.BottomLine, _rectSelection.RightColumn);
                _isRectDrag = true;
                e.Handled   = true;
                return;
            }

            // Non-Alt click with no rect-drag → clear rectangular selection.
            if (!_rectSelection.IsEmpty)
            {
                _rectSelection.Clear();
                _isRectSelecting = false;
            }
            _isRectDrag = false;

            // Left-click behavior (unchanged)
            _cursorLine = textPos.Line;
            _cursorColumn = textPos.Column;

            if (e.ClickCount == 2) // Double-click = select word
            {
                SelectWordAtPosition(textPos);
                e.Handled = true;
            }
            else if (e.ClickCount == 3) // Triple-click = select line
            {
                SelectLineAtPosition(textPos);
                e.Handled = true;
            }
            else
            {
                // Feature B: click inside existing text selection → potential drag-to-move.
                if (!_selection.IsEmpty && IsPositionInSelection(textPos))
                {
                    _dragDrop.Phase           = DragPhase.Pending;
                    _dragDrop.ClickPixel      = pos;
                    _dragDrop.ClickedPosition = textPos;
                    _dragDrop.SelectionStart  = _selection.NormalizedStart;
                    _dragDrop.SelectionEnd    = _selection.NormalizedEnd;
                    // Do NOT set _isSelecting — wait to see if threshold is crossed.
                    e.Handled = true;
                    return;
                }

                // Start normal selection
                _isSelecting = true;
                _mouseDownPosition = textPos;
                _selection.Start = textPos;
                _selection.End = textPos;
                CaptureMouse();
            }

            InvalidateVisual();
            NotifyCaretMovedIfChanged();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            // URL hover: show Hand cursor + underline when the pointer is over a URL zone.
            if (!_isSelecting)
            {
                var hoverPos = PixelToTextPosition(e.GetPosition(this));
                var urlZone  = FindUrlZone(hoverPos.Line, hoverPos.Column);

                // Only repaint when the hovered zone actually changes (avoids per-mousemove redraws).
                if (urlZone != _hoveredUrlZone)
                {
                    _hoveredUrlZone = urlZone;
                    InvalidateVisual();
                }

                // CodeLens hint zones: Hand cursor, hover highlight, and tooltip.
                var mousePixel = e.GetPosition(this);
                int prevHover  = _hoveredLensLine;
                _hoveredLensLine = -1;
                string? lensTooltip = null;
                foreach (var (zone, lineIdx, sym) in _lensHitZones)
                {
                    if (zone.Contains(mousePixel))
                    {
                        _hoveredLensLine = lineIdx;
                        if (_lensData.TryGetValue(lineIdx, out var entry))
                        {
                            lensTooltip = entry.Count == 1
                                ? $"1 reference to '{sym}'  (Alt+3)"
                                : $"{entry.Count} references to '{sym}'  (Alt+3)";
                        }
                        break;
                    }
                }
                if (_hoveredLensLine != prevHover)
                    InvalidateVisual();

                bool overLens = _hoveredLensLine >= 0;
                ToolTip = overLens ? lensTooltip : null;

                // Fold label zones — Hand cursor + 1.5s peek-on-hover.
                int newHoveredFoldLine = -1;
                foreach (var (rect, line) in _foldLabelHitZones)
                    if (rect.Contains(mousePixel)) { newHoveredFoldLine = line; break; }

                bool overFoldLabel = newHoveredFoldLine >= 0;

                if (overFoldLabel)
                {
                    if (newHoveredFoldLine != _foldPeekTargetLine)
                    {
                        // Mouse moved to a different label — restart peek timer + repaint hover.
                        _foldPeekTargetLine = newHoveredFoldLine;
                        _foldPeekTimer?.Stop();
                        _foldPeekPopup?.Hide();
                        _foldPeekTimer?.Start();
                        InvalidateVisual();
                    }
                    // else: still on same label — timer already running.
                }
                else
                {
                    if (_foldPeekTargetLine >= 0)
                    {
                        // Mouse left all fold labels — cancel, close, repaint to remove hover style.
                        _foldPeekTargetLine = -1;
                        _foldPeekTimer?.Stop();
                        _foldPeekPopup?.Hide();
                        InvalidateVisual();
                    }
                }

                if (overLens)
                {
                    Cursor = Cursors.Hand;
                    HideUrlTooltip();
                }
                else if (urlZone.HasValue)
                {
                    Cursor = Cursors.Hand;
                    ShowUrlTooltip();
                }
                else if (overFoldLabel)
                {
                    Cursor = Cursors.Hand;
                    HideUrlTooltip();
                }
                else
                {
                    Cursor = Cursors.IBeam;
                    HideUrlTooltip();
                }

                // Quick Info hover — dispatch after cursor state is settled
                if (ShowQuickInfo && _hoverQuickInfoService is not null && !_isSelecting)
                    HandleQuickInfoHover(hoverPos, e.GetPosition(this));

                // Ctrl+hover symbol underline.
                // Enabled when: (a) no LanguageDefinition is registered for this file type
                // (Language == null → backward-compatible default ON), or (b) the language
                // explicitly declares EnableCtrlClickNavigation = true.
                // Languages that set EnableCtrlClickNavigation = false (e.g. JSON, YAML, HTML)
                // suppress the hand-cursor and block navigation.
                if (_ctrlDown && (Language is null || Language.EnableCtrlClickNavigation))
                {
                    HandleCtrlHover(hoverPos);
                    if (!overLens && !urlZone.HasValue)
                        Cursor = _hoveredSymbolZone.HasValue ? Cursors.Hand : Cursors.IBeam;
                }
            }

            // Feature A: extend rectangular selection during Alt+drag.
            if (_isRectSelecting && e.LeftButton == MouseButtonState.Pressed)
            {
                var pos     = e.GetPosition(this);
                var textPos = PixelToTextPosition(pos);
                _rectSelection.Extend(textPos);
                _cursorLine   = textPos.Line;
                _cursorColumn = textPos.Column;
                if (!_selectionRenderPending)
                {
                    _selectionRenderPending = true;
                    Dispatcher.InvokeAsync(() =>
                    {
                        _selectionRenderPending = false;
                        InvalidateVisual();
                    }, System.Windows.Threading.DispatcherPriority.Render);
                }
                return;
            }

            // Feature B: handle drag-pending or drag-in-progress state.
            if (_dragDrop.Phase != DragPhase.None && e.LeftButton == MouseButtonState.Pressed)
            {
                var pos     = e.GetPosition(this);
                var textPos = PixelToTextPosition(pos);

                if (_dragDrop.Phase == DragPhase.Pending && _dragDrop.HasMovedBeyondThreshold(pos))
                {
                    _dragDrop.Phase = DragPhase.Dragging;
                    CaptureMouse();
                    Cursor = Cursors.SizeAll;
                }

                if (_dragDrop.Phase == DragPhase.Dragging)
                {
                    _dragDrop.DropPosition = textPos;
                    if (!_selectionRenderPending)
                    {
                        _selectionRenderPending = true;
                        Dispatcher.InvokeAsync(() =>
                        {
                            _selectionRenderPending = false;
                            InvalidateVisual();
                        }, System.Windows.Threading.DispatcherPriority.Render);
                    }
                }
                return;
            }

            if (_isSelecting && e.LeftButton == MouseButtonState.Pressed)
            {
                var pos = e.GetPosition(this);
                _lastMousePosition = pos;

                // Start or stop the auto-scroll timer based on whether the mouse
                // is outside the visible viewport bounds.
                bool outsideBounds = pos.Y < 0 || pos.Y > ActualHeight;
                if (outsideBounds && !_autoScrollTimer.IsEnabled)
                    _autoScrollTimer.Start();
                else if (!outsideBounds && _autoScrollTimer.IsEnabled)
                    _autoScrollTimer.Stop();

                var textPos = PixelToTextPosition(pos);

                // Guard: skip re-render if the selection endpoint hasn't moved to a new cell.
                // Mouse-move events can fire at 200–1000 Hz; many will resolve to the same
                // text position and would trigger a full OnRender() for nothing.
                if (textPos == _selection.End) return;

                _selection.End = textPos;
                _cursorLine    = textPos.Line;
                _cursorColumn  = textPos.Column;

                // Coalesce: queue at most one render per WPF frame (~60 Hz) instead of
                // one per OS mouse event.  _selectionRenderPending is cleared inside the
                // dispatched lambda so subsequent events re-arm correctly.
                if (_selectionRenderPending) return;
                _selectionRenderPending = true;
                Dispatcher.InvokeAsync(() =>
                {
                    _selectionRenderPending = false;
                    InvalidateVisual();
                }, System.Windows.Threading.DispatcherPriority.Render);
            }
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);

            // Feature A: terminate rectangular selection drag.
            if (_isRectSelecting)
            {
                _isRectSelecting = false;
                _autoScrollTimer.Stop();
                ReleaseMouseCapture();
                return;
            }

            // Feature B: commit or cancel text drag-to-move.
            if (_dragDrop.Phase != DragPhase.None)
            {
                ReleaseMouseCapture();
                Cursor = Cursors.IBeam;

                if (_dragDrop.Phase == DragPhase.Dragging)
                {
                    if (_isRectDrag) CommitRectDrop();
                    else             CommitTextDrop();
                }
                else
                {
                    // Pending phase (no threshold crossed): clear selection, place caret.
                    _cursorLine   = _dragDrop.ClickedPosition.Line;
                    _cursorColumn = _dragDrop.ClickedPosition.Column;
                    _selection.Clear();
                    _rectSelection.Clear();
                    InvalidateVisual();
                    NotifyCaretMovedIfChanged();
                }

                _isRectDrag = false;
                _dragDrop.Reset();
                return;
            }

            if (_isSelecting)
            {
                _isSelecting = false;
                _autoScrollTimer.Stop();
                ReleaseMouseCapture();
            }
        }

        protected override void OnGotFocus(RoutedEventArgs e)
        {
            base.OnGotFocus(e);

            // Start caret blinking when focused
            if (_caretTimer != null && CaretBlinkRate > 0)
            {
                _caretVisible = true;
                _caretTimer.Stop();
                _caretTimer.Start();
            }
            else if (_caretTimer != null)
            {
                // If blink rate is 0 (always visible), ensure caret is shown
                _caretVisible = true;
            }

            // Force immediate repaint to show caret and active selection
            InvalidateVisual();

            // Force update layout to ensure cursor is visible immediately
            UpdateLayout();
        }

        protected override void OnLostFocus(RoutedEventArgs e)
        {
            base.OnLostFocus(e);

            // Stop caret blinking when not focused
            if (_caretTimer != null)
            {
                _caretTimer.Stop();
                _caretVisible = false;
            }

            // Clear Ctrl+hover and Quick Info state on focus loss.
            _quickInfoPopup?.Hide();
            _hoverQuickInfoService?.Cancel();
            if (_ctrlDown)
            {
                _ctrlDown = false;
                _hoveredSymbolZone = null;
                _ctrlClickService?.Cancel();
            }

            // Repaint to show inactive selection
            InvalidateVisual();
        }

        /// <summary>
        /// Returns the <see cref="UrlHitZone"/> that contains <paramref name="column"/> on
        /// <paramref name="line"/>, or <see langword="null"/> if no URL occupies that position.
        /// Hit-zones are rebuilt on every render pass by <see cref="OverlayUrlTokens"/>.
        /// </summary>
        private UrlHitZone? FindUrlZone(int line, int column)
        {
            foreach (var zone in _urlHitZones)
            {
                if (zone.Line == line && column >= zone.StartCol && column < zone.EndCol)
                    return zone;
            }
            return null;
        }

        /// <summary>
        /// Convert pixel position to text position (line, column)
        /// </summary>
        private TextPosition PixelToTextPosition(Point pixel)
        {
            double leftEdge = ShowLineNumbers ? TextAreaLeftOffset : LeftMargin;
            int line;

            if (ShowCodeLens && _visLinePositions.Count > 0)
            {
                // Variable-height scan: CodeLens declaration lines have a taller slot.
                // Each lens-line slot spans (codeY - LensLineHeight → codeY + _lineHeight).
                // Each normal-line slot spans (codeY → codeY + _lineHeight).
                line = _visLinePositions[^1].LineIndex; // default: last visible line
                for (int k = 0; k < _visLinePositions.Count; k++)
                {
                    var (lineIdx, codeY) = _visLinePositions[k];
                    double slotTop = IsLensEntryVisible(lineIdx) ? codeY - LensLineHeight : codeY;
                    if (pixel.Y >= slotTop && pixel.Y < codeY + _lineHeight)
                    {
                        line = lineIdx;
                        break;
                    }
                }
            }
            else
            {
                // Uniform-height path: use VirtualizationEngine for sub-line scroll accuracy.
                line = EnableVirtualScrolling && _virtualizationEngine != null
                    ? _virtualizationEngine.GetLineAtYPosition(pixel.Y - TopMargin)
                    : _firstVisibleLine + (int)((pixel.Y - TopMargin) / _lineHeight);
            }

            line = Math.Max(0, Math.Min(_document.Lines.Count - 1, line));

            // Calculate column (account for horizontal scroll offset)
            int column = (int)((pixel.X - leftEdge + _horizontalScrollOffset) / _charWidth);
            column = Math.Max(0, Math.Min(_document.Lines[line].Length, column));

            return new TextPosition(line, column);
        }

        /// <summary>
        /// Select word at position (double-click handler)
        /// </summary>
        private void SelectWordAtPosition(TextPosition pos)
        {
            if (pos.Line < 0 || pos.Line >= _document.Lines.Count)
                return;

            var line = _document.Lines[pos.Line];
            if (string.IsNullOrEmpty(line.Text) || pos.Column >= line.Text.Length)
            {
                _selection.Clear();
                return;
            }

            // Find word boundaries
            int start = pos.Column;
            int end = pos.Column;

            // Expand left
            while (start > 0 && IsWordChar(line.Text[start - 1]))
                start--;

            // Expand right
            while (end < line.Text.Length && IsWordChar(line.Text[end]))
                end++;

            _selection.Start = new TextPosition(pos.Line, start);
            _selection.End = new TextPosition(pos.Line, end);
        }

        /// <summary>
        /// Select entire line at position (triple-click handler)
        /// </summary>
        private void SelectLineAtPosition(TextPosition pos)
        {
            if (pos.Line < 0 || pos.Line >= _document.Lines.Count)
                return;

            _selection.Start = new TextPosition(pos.Line, 0);
            _selection.End = new TextPosition(pos.Line, _document.Lines[pos.Line].Length);
        }

        /// <summary>
        /// Check if a position is inside the current selection
        /// </summary>
        private bool IsPositionInSelection(TextPosition pos)
        {
            if (_selection.IsEmpty)
                return false;

            var start = _selection.NormalizedStart;
            var end = _selection.NormalizedEnd;

            // Single line selection
            if (start.Line == end.Line)
            {
                return pos.Line == start.Line && pos.Column >= start.Column && pos.Column <= end.Column;
            }

            // Multi-line selection
            if (pos.Line < start.Line || pos.Line > end.Line)
                return false;

            if (pos.Line == start.Line)
                return pos.Column >= start.Column;

            if (pos.Line == end.Line)
                return pos.Column <= end.Column;

            // Middle lines are always inside
            return true;
        }

        /// <summary>
        /// Check if character is part of a word (alphanumeric or underscore)
        /// </summary>
        private bool IsWordChar(char ch)
        {
            return char.IsLetterOrDigit(ch) || ch == '_';
        }

        #endregion

        #region Clipboard Operations (Phase 3)

        private void CopyToClipboard()
        {
            // Feature A: rectangular selection takes priority.
            if (!_rectSelection.IsEmpty) { CopyRectSelection(); return; }

            if (_selection.IsEmpty)
                return;

            try
            {
                string selectedText = _document.GetText(_selection.NormalizedStart, _selection.NormalizedEnd);
                Clipboard.SetText(selectedText);
            }
            catch (Exception)
            {
                // Silently ignore clipboard errors
            }
        }

        private void CopyRectSelection()
        {
            if (_rectSelection.IsEmpty || _document is null) return;
            var lines = _document.Lines.Select(l => l.Text).ToList();
            string text = _rectSelection.ExtractText(lines);
            if (!string.IsNullOrEmpty(text))
            {
                try { Clipboard.SetText(text); }
                catch { /* Silently ignore clipboard errors */ }
            }
        }

        private void CutRectSelection()
        {
            if (_rectSelection.IsEmpty || _document is null || IsReadOnly) return;
            CopyRectSelection();
            DeleteRectSelection();
        }

        private void DeleteRectSelection()
        {
            if (_rectSelection.IsEmpty || _document is null || IsReadOnly) return;

            var (left, right) = _rectSelection.GetColumnRange();

            // Wrap all per-line deletes in a single atomic undo step.
            using (_undoEngine.BeginTransaction("Delete Rectangular Selection"))
            {
                // Iterate bottom-to-top so that per-line deletions don't shift line indices.
                for (int li = _rectSelection.BottomLine; li >= _rectSelection.TopLine; li--)
                {
                    if (li >= _document.Lines.Count) continue;
                    var line  = _document.Lines[li].Text;
                    int safeL = Math.Min(left,  line.Length);
                    int safeR = Math.Min(right, line.Length);
                    if (safeR <= safeL) continue;

                    var delStart = new TextPosition(li, safeL);
                    var delEnd   = new TextPosition(li, safeR);
                    _document.DeleteRange(delStart, delEnd);
                }
            }

            _cursorLine   = _rectSelection.TopLine;
            _cursorColumn = _rectSelection.LeftColumn;
            _rectSelection.Clear();
            InvalidateVisual();
            NotifyCaretMovedIfChanged();
        }

        private void PasteFromClipboard()
        {
            try
            {
                if (!Clipboard.ContainsText()) return;

                string text = Clipboard.GetText();

                // Wrap the entire paste (selection delete + multi-line insert) atomically.
                using (_undoEngine.BeginTransaction("Paste"))
                {
                    if (!_selection.IsEmpty)
                        DeleteSelection();

                    _document.InsertText(new TextPosition(_cursorLine, _cursorColumn), text);

                    var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                    if (lines.Length == 1)
                        _cursorColumn += text.Length;
                    else
                    {
                        _cursorLine  += lines.Length - 1;
                        _cursorColumn = lines[lines.Length - 1].Length;
                    }
                }

                _selection.Clear();
                InvalidateVisual();
            }
            catch (Exception)
            {
                // Silently ignore clipboard errors
            }
        }

        private void CutToClipboard()
        {
            // Feature A: rectangular selection takes priority.
            if (!_rectSelection.IsEmpty) { CutRectSelection(); return; }

            if (_selection.IsEmpty)
                return;

            CopyToClipboard();
            using (_undoEngine.BeginTransaction("Cut"))
                DeleteSelection();
        }

        private void DeleteSelection()
        {
            if (_selection.IsEmpty)
                return;

            var start = _selection.NormalizedStart;
            var end = _selection.NormalizedEnd;

            _document.DeleteRange(start, end);

            _cursorLine = start.Line;
            _cursorColumn = start.Column;
            _selection.Clear();
        }

        private void SelectAll()
        {
            if (_document.Lines.Count == 0)
                return;

            _selection.Start = new TextPosition(0, 0);
            _selection.End = new TextPosition(_document.Lines.Count - 1, _document.Lines[_document.Lines.Count - 1].Length);
            InvalidateVisual();
        }

        // -----------------------------------------------------------------------
        // Feature B — Text drag-and-drop commit logic
        // -----------------------------------------------------------------------

        /// <summary>
        /// Executes the text move: deletes source selection, adjusts drop position for the
        /// deletion offset, then inserts at the adjusted target.
        /// </summary>
        private void CommitTextDrop()
        {
            if (_document is null || IsReadOnly) return;

            var drop     = _dragDrop.DropPosition;
            var srcStart = _dragDrop.SelectionStart;
            var srcEnd   = _dragDrop.SelectionEnd;

            // Drop inside the original selection → cancel (no-op move).
            if (DragDropState.IsDropInsideSelection(drop, srcStart, srcEnd))
            {
                _selection.Start = srcStart;
                _selection.End   = srcEnd;
                _cursorLine   = srcEnd.Line;
                _cursorColumn = srcEnd.Column;
                InvalidateVisual();
                return;
            }

            string movedText = _document.GetText(srcStart, srcEnd);
            bool dropBefore  = drop < srcStart;

            _document.DeleteRange(srcStart, srcEnd);
            _selection.Clear();

            // If the drop target came after the deleted range, shift it to account for
            // the removed content.
            TextPosition insertAt = dropBefore
                ? drop
                : AdjustPositionAfterDelete(drop, srcStart, srcEnd);

            _document.InsertText(insertAt, movedText);

            _cursorLine   = insertAt.Line;
            _cursorColumn = insertAt.Column;
            InvalidateVisual();
            NotifyCaretMovedIfChanged();
        }

        /// <summary>
        /// Returns true when (line, col) falls inside the active rectangular selection block.
        /// </summary>
        private bool IsInsideRectBlock(int line, int col)
            => !_rectSelection.IsEmpty
               && line >= _rectSelection.TopLine    && line <= _rectSelection.BottomLine
               && col  >= _rectSelection.LeftColumn && col  <= _rectSelection.RightColumn;

        /// <summary>
        /// Executes a rect-block move: extracts the selected column block, deletes it, then
        /// re-inserts each row at the drop column, preserving the block's row count.
        /// </summary>
        private void CommitRectDrop()
        {
            if (_document is null || IsReadOnly) { _isRectDrag = false; _dragDrop.Reset(); return; }

            int topLine    = _rectSelection.TopLine;
            int bottomLine = _rectSelection.BottomLine;
            int leftCol    = _rectSelection.LeftColumn;
            int rightCol   = _rectSelection.RightColumn;
            int blockWidth = rightCol - leftCol;
            int blockHeight= bottomLine - topLine + 1;

            int dropLine   = _dragDrop.DropPosition.Line;
            int dropCol    = _dragDrop.DropPosition.Column;

            // Drop inside the original block → no-op.
            if (dropLine >= topLine && dropLine <= bottomLine
                && dropCol >= leftCol && dropCol <= rightCol)
            {
                _isRectDrag = false;
                _dragDrop.Reset();
                return;
            }

            // Snapshot block text before deletion.
            var lineTexts = _document.Lines.Select(l => l.Text).ToList();
            string blockText = _rectSelection.ExtractText(lineTexts);
            string[] blockLines = blockText.Split('\n');

            // Delete the source block (bottom-to-top to preserve indices).
            using (_undoEngine.BeginTransaction("Move Rectangular Block"))
            {
                for (int li = bottomLine; li >= topLine; li--)
                {
                    if (li >= _document.Lines.Count) continue;
                    var lineText = _document.Lines[li].Text;
                    int safeL = Math.Min(leftCol,  lineText.Length);
                    int safeR = Math.Min(rightCol, lineText.Length);
                    if (safeR > safeL)
                        _document.DeleteRange(new TextPosition(li, safeL), new TextPosition(li, safeR));
                }

                // Adjust drop column when drop is on an affected line and after the deleted block.
                if (dropLine >= topLine && dropLine <= bottomLine && dropCol > rightCol)
                    dropCol = Math.Max(leftCol, dropCol - blockWidth);

                // Insert each block row at the drop column.
                for (int i = 0; i < blockHeight; i++)
                {
                    int targetLine = dropLine + i;
                    if (targetLine >= _document.Lines.Count) break;
                    string lineContent = i < blockLines.Length ? blockLines[i] : string.Empty;
                    if (!string.IsNullOrEmpty(lineContent))
                        _document.InsertText(new TextPosition(targetLine, Math.Min(dropCol, _document.Lines[targetLine].Length)), lineContent);
                }
            }

            // Reposition rect selection at the new block location.
            _rectSelection.Begin(new TextPosition(dropLine, dropCol));
            _rectSelection.Extend(new TextPosition(Math.Min(dropLine + blockHeight - 1, _document.Lines.Count - 1), dropCol + blockWidth));

            _cursorLine   = dropLine;
            _cursorColumn = dropCol;
            _isRectDrag   = false;
            _dragDrop.Reset();
            InvalidateVisual();
            NotifyCaretMovedIfChanged();
        }

        /// <summary>
        /// Shifts <paramref name="pos"/> to account for the text that was deleted between
        /// <paramref name="delStart"/> and <paramref name="delEnd"/>.
        /// Used when the drop target lies after the deleted source range.
        /// </summary>
        private static TextPosition AdjustPositionAfterDelete(
            TextPosition pos,
            TextPosition delStart,
            TextPosition delEnd)
        {
            int newLine = pos.Line - (delEnd.Line - delStart.Line);
            int newCol  = pos.Column;

            // If the drop was on the same line where the deletion ended, adjust column.
            if (pos.Line == delEnd.Line)
                newCol = delStart.Column + (pos.Column - delEnd.Column);

            return new TextPosition(Math.Max(0, newLine), Math.Max(0, newCol));
        }

        #endregion

        #region Undo/Redo Operations

        public void Undo()
        {
            if (!_undoEngine.CanUndo) return;

            try
            {
                _isInternalEdit = true;
                var entry = _undoEngine.TryUndo();
                if (entry != null)
                {
                    ApplyInverseEntry(entry);
                    InvalidateVisual();
                }
            }
            finally
            {
                _isInternalEdit = false;
            }
        }

        public void Redo()
        {
            if (!_undoEngine.CanRedo) return;

            try
            {
                _isInternalEdit = true;
                var entry = _undoEngine.TryRedo();
                if (entry != null)
                {
                    ApplyForwardEntry(entry);
                    InvalidateVisual();
                }
            }
            finally
            {
                _isInternalEdit = false;
            }
        }

        // Apply the entry in the forward (redo) direction.
        private void ApplyForwardEntry(WpfHexEditor.Editor.Core.Undo.IUndoEntry entry)
        {
            if (entry is WpfHexEditor.Editor.Core.Undo.CompositeUndoEntry composite)
            {
                foreach (var child in composite.Children)
                    ApplyForwardEntry(child);
                return;
            }

            if (entry is not Models.CodeEditorUndoEntry e) return;

            switch (e.Kind)
            {
                case Models.CodeEditKind.Insert:
                {
                    _document.InsertText(e.Position, e.Text);
                    // Compute cursor end — accounts for embedded newlines in multi-line pastes.
                    var fwdLines = e.Text.Split('\n');
                    if (fwdLines.Length == 1)
                    {
                        _cursorLine   = e.Position.Line;
                        _cursorColumn = e.Position.Column + e.Text.Length;
                    }
                    else
                    {
                        _cursorLine   = e.Position.Line + fwdLines.Length - 1;
                        _cursorColumn = fwdLines[^1].Length;
                    }
                    break;
                }

                case Models.CodeEditKind.Delete:
                {
                    // Compute the actual end of the deleted range — accounts for multi-line text.
                    var fwdDelLines = e.Text.Split('\n');
                    var delEnd = fwdDelLines.Length == 1
                        ? new TextPosition(e.Position.Line, e.Position.Column + e.Text.Length)
                        : new TextPosition(e.Position.Line + fwdDelLines.Length - 1, fwdDelLines[^1].Length);
                    _document.DeleteRange(e.Position, delEnd);
                    _cursorLine   = e.Position.Line;
                    _cursorColumn = e.Position.Column;
                    break;
                }

                case Models.CodeEditKind.NewLine:
                    // Redo: re-split the line at the original position.
                    _document.InsertNewLine(e.Position.Line, e.Position.Column);
                    _cursorLine   = e.Position.Line + 1;
                    _cursorColumn = 0;
                    break;

                case Models.CodeEditKind.DeleteLine:
                    // Redo: re-merge. At redo time Lines[line-1]=part1, Lines[line]=e.Text.
                    int prevLen = _document.Lines[e.Position.Line - 1].Text.Length;
                    _document.Lines[e.Position.Line - 1].Text += e.Text;  // direct merge (no event)
                    _document.DeleteLine(e.Position.Line);                 // _isInternalEdit=true → not re-recorded
                    _cursorLine   = e.Position.Line - 1;
                    _cursorColumn = prevLen;
                    break;
            }
        }

        // Apply the inverse (undo) direction.
        private void ApplyInverseEntry(WpfHexEditor.Editor.Core.Undo.IUndoEntry entry)
        {
            if (entry is WpfHexEditor.Editor.Core.Undo.CompositeUndoEntry composite)
            {
                // Apply children in reverse order for undo.
                for (int i = composite.Children.Count - 1; i >= 0; i--)
                    ApplyInverseEntry(composite.Children[i]);
                return;
            }

            if (entry is not Models.CodeEditorUndoEntry e) return;

            switch (e.Kind)
            {
                case Models.CodeEditKind.Insert:
                {
                    // Inverse of insert = delete the inserted text.
                    // For multi-line text the end position is on a different line.
                    var invLines = e.Text.Split('\n');
                    var insEnd = invLines.Length == 1
                        ? new TextPosition(e.Position.Line, e.Position.Column + e.Text.Length)
                        : new TextPosition(e.Position.Line + invLines.Length - 1, invLines[^1].Length);
                    _document.DeleteRange(e.Position, insEnd);
                    _cursorLine   = e.Position.Line;
                    _cursorColumn = e.Position.Column;
                    break;
                }

                case Models.CodeEditKind.Delete:
                {
                    // Inverse of delete = re-insert the deleted text.
                    _document.InsertText(e.Position, e.Text);
                    _cursorLine   = e.Position.Line;
                    _cursorColumn = e.Position.Column;
                    break;
                }

                case Models.CodeEditKind.NewLine:
                    // e.Text = right part (content of line+1) stored at recording time.
                    // At undo time (LIFO): Lines[line].Text = left, Lines[line+1].Text = e.Text.
                    var leftText = _document.Lines[e.Position.Line].Text;
                    _document.Lines[e.Position.Line].Text = leftText + e.Text;
                    if (e.Position.Line + 1 < _document.Lines.Count)
                        _document.DeleteLine(e.Position.Line + 1);  // _isInternalEdit=true → not re-recorded
                    _cursorLine   = e.Position.Line;
                    _cursorColumn = e.Position.Column;
                    break;

                case Models.CodeEditKind.DeleteLine:
                    // e.Position=(deletedLine,0), e.Text=content of the deleted line.
                    // At undo time (LIFO): Lines[line-1].Text = part1 + e.Text (merged).
                    var mergedText = _document.Lines[e.Position.Line - 1].Text;
                    int splitAt    = mergedText.Length - e.Text.Length;
                    _document.Lines[e.Position.Line - 1].Text = splitAt > 0 ? mergedText[..splitAt] : string.Empty;
                    _document.InsertNewLine(e.Position.Line - 1,
                        _document.Lines[e.Position.Line - 1].Text.Length);  // splits at end → creates empty line+1
                    _document.Lines[e.Position.Line].Text = e.Text;          // restore deleted line content
                    _cursorLine   = e.Position.Line;
                    _cursorColumn = 0;
                    break;
            }
        }

        // Handles UndoEngine state changes: updates _isDirty and fires events.
        private void OnUndoEngineStateChanged(object? sender, EventArgs e)
        {
            bool dirty = !_undoEngine.IsAtSavePoint;
            if (dirty != _isDirty)
            {
                _isDirty = dirty;
                ModifiedChanged?.Invoke(this, EventArgs.Empty);
                TitleChanged?.Invoke(this, BuildTitle());
            }
            CanUndoChanged?.Invoke(this, EventArgs.Empty);
            CanRedoChanged?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region Document Event Handlers

        private void Document_TextChanged(object sender, Models.TextChangedEventArgs e)
        {
            // Dismiss Quick Info when the document changes — content is stale.
            _quickInfoPopup?.Hide();
            _hoverQuickInfoService?.Cancel();

            // Record in undo engine (unless replaying an undo/redo operation).
            if (!_isInternalEdit)
            {
                var kind = (Models.CodeEditKind)(int)e.ChangeType;

                // For NewLine, store the RIGHT part (content that went to line+1).
                // e.Text = Environment.NewLine is useless for reconstruction; we need
                // the actual text so ApplyInverseEntry can merge the lines back.
                string recordedText = e.ChangeType == Models.TextChangeType.NewLine
                    ? (e.Position.Line + 1 < _document.Lines.Count
                        ? _document.Lines[e.Position.Line + 1].Text
                        : string.Empty)
                    : e.Text;

                var entry = new Models.CodeEditorUndoEntry(kind, e.Position, recordedText);
                _undoEngine.Push(entry);
                // _isDirty + events are handled by OnUndoEngineStateChanged via StateChanged.
            }

            // Phase 5: Trigger validation with debounce
            if (EnableValidation)
            {
                _validationTimer.Stop();
                _validationTimer.Start();
            }

            // Refresh folding regions — debounced 500ms to avoid O(n) scan on every keystroke (P1-CE-01).
            if (IsFoldingEnabled && _foldingEngine != null)
            {
                _foldingDebounceTimer?.Stop();
                _foldingDebounceTimer?.Start();
            }

            // Debounce LSP didChange — 300 ms (Phase 4: LSP Integration).
            if (_lspClient is not null && _currentFilePath is not null)
            {
                _lspChangeTimer?.Stop();
                _lspChangeTimer?.Start();
            }

            // Incremental max-width update (P1-CE-02) — O(1) on growth, O(n) only on shrink
            int changedLine    = e.Position.Line;
            int prevMaxLength  = _cachedMaxLineLength;
            if (changedLine >= 0 && changedLine < _document.Lines.Count)
            {
                int newLen = _document.Lines[changedLine].Text.Length;
                if (newLen > _cachedMaxLineLength)
                    _cachedMaxLineLength = newLen;
                else if (newLen < _cachedMaxLineLength)
                    _cachedMaxLineLength = _document.Lines.Count > 0
                        ? _document.Lines.Max(l => l.Text.Length) : 0;
            }
            bool maxWidthChanged = _cachedMaxLineLength != prevMaxLength;

            // Invalidate line-number cache for the changed line (P1-CE-03)
            _lineNumberCache.Remove(changedLine);

            // OPT-B: Smart invalidation routing — only trigger a full layout pass when
            // the document *structure* changes (line count or max-line-width).  For a plain
            // char insert/delete on an existing line the scrollbar ranges are unchanged, so
            // InvalidateVisual() is sufficient and avoids the heavier Measure→Arrange chain.
            bool lineCountChanged = e.ChangeType is TextChangeType.NewLine
                                                 or TextChangeType.DeleteLine;
            if (lineCountChanged)
                _linePositionsDirty = true; // OPT-D: new/deleted lines shift subsequent Y positions

            if (lineCountChanged || maxWidthChanged)
                InvalidateMeasure(); // scrollbar ranges may have changed
            else
                InvalidateVisual();  // layout unaffected — redraw only
        }

        #endregion

        #region IntelliSense Methods (Phase 4)

        /// <summary>
        /// Trigger IntelliSense immediately (Ctrl+Space)
        /// </summary>
        private void TriggerIntelliSense()
        {
            if (!_enableIntelliSense || _intelliSensePopup == null)
                return;

            _intelliSensePopup.TriggerImmediate();
        }

        /// <summary>
        /// Trigger IntelliSense with delay (auto-trigger)
        /// </summary>
        private void TriggerIntelliSenseWithDelay()
        {
            if (!_enableIntelliSense || _intelliSensePopup == null)
                return;

            _intelliSensePopup.TriggerWithDelay(IntelliSenseDelay);
        }

        /// <summary>
        /// Check if character should auto-trigger IntelliSense
        /// </summary>
        private bool ShouldAutoTriggerIntelliSense(char ch)
        {
            // Trigger on: quote (start of key/value), colon (after key), comma (new item), opening brace/bracket
            return ch == '"' || ch == ':' || ch == ',' || ch == '{' || ch == '[';
        }

        #endregion

        #region Validation Methods (Phase 5)

        /// <summary>
        /// Trigger validation timer
        /// </summary>
        private void ValidationTimer_Tick(object sender, EventArgs e)
        {
            _validationTimer.Stop();
            PerformValidation();
        }

        /// <summary>
        /// Perform validation immediately
        /// </summary>
        private void PerformValidation()
        {
            if (!EnableValidation || _validator == null || _document == null)
                return;

            try
            {
                string textToValidate;
                var dirty = _document.DirtyLines;

                // Incremental path (P1-CE-07): validate only the dirty region + context when
                // fewer than 10% of lines changed.  Full pass otherwise (initial load, paste, etc.).
                if (dirty.Count > 0 && dirty.Count < _document.TotalLines / 10)
                {
                    int minDirty = dirty.Min();
                    int maxDirty = dirty.Max();
                    // Include 5-line context above and below for accurate state-dependent validators
                    int rangeStart = Math.Max(0, minDirty - 5);
                    int rangeEnd   = Math.Min(_document.Lines.Count - 1, maxDirty + 5);
                    textToValidate = string.Join(Environment.NewLine,
                        _document.Lines.Skip(rangeStart).Take(rangeEnd - rangeStart + 1).Select(l => l.Text));
                }
                else
                {
                    textToValidate = _document.SaveToString();
                }

                _document.ClearDirtyLines();
                _validationErrors = _validator.Validate(textToValidate);
                RebuildValidationIndex();
                InvalidateVisual();
                DiagnosticsChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception)
            {
                // Silently ignore validation errors
                _document.ClearDirtyLines();
                _validationErrors.Clear();
                _validationByLine.Clear();
                DiagnosticsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Rebuilds the line→errors dictionary used by RenderValidationGlyph for O(1) lookup.
        /// Must be called whenever _validationErrors is replaced or bulk-modified (OPT-PERF-01).
        /// </summary>
        private void RebuildValidationIndex()
        {
            _validationByLine.Clear();
            foreach (var error in _validationErrors)
            {
                if (!_validationByLine.TryGetValue(error.Line, out var list))
                {
                    list = new List<Models.ValidationError>(2);
                    _validationByLine[error.Line] = list;
                }
                list.Add(error);
            }
        }

        /// <summary>
        /// Trigger validation manually (public API)
        /// </summary>
        public void TriggerValidation()
        {
            if (EnableValidation)
            {
                PerformValidation();
            }
        }

        /// <summary>
        /// Get current validation errors
        /// </summary>
        public List<Models.ValidationError> ValidationErrors => _validationErrors;

        #endregion

        #region Public API

        /// <summary>
        /// Get the document model
        /// </summary>
        public CodeDocument Document => _document;

        /// <summary>
        /// Replaces the document model with an externally supplied instance.
        /// Used by <see cref="CodeEditorSplitHost"/> to share one <see cref="CodeDocument"/>
        /// between the primary and secondary editor panes.
        /// </summary>
        public void SetDocument(CodeDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));

            // Unsubscribe from the old document.
            _document.TextChanged -= Document_TextChanged;

            _document = document;

            // Subscribe to the new document.
            _document.TextChanged += Document_TextChanged;

            // Re-attach CodeLens service to the new document.
            _codeLensService.Attach(_document, _currentFilePath);

            // Reset view state.
            _cursorLine = 0;
            _cursorColumn = 0;
            _selection.Clear();
            _verticalScrollOffset   = 0;
            _currentScrollOffset    = 0;
            _targetScrollOffset     = 0;
            _horizontalScrollOffset = 0;

            UpdateVirtualization();
            RebuildMaxLineLength(); // O(n) scan at doc swap — acceptable

            if (IsFoldingEnabled && _foldingEngine != null)
                _foldingEngine.Analyze(_document.Lines);

            _lineNumberCache.Clear();
            InvalidateMeasure();
        }

        /// <summary>
        /// Load text content
        /// </summary>
        public void LoadText(string text)
        {
            _document.LoadFromString(text);
            _cursorLine = 0;
            _cursorColumn = 0;
            _selection.Clear();
            _verticalScrollOffset   = 0;
            _currentScrollOffset    = 0;
            _targetScrollOffset     = 0;
            _horizontalScrollOffset = 0;

            // Clear undo history — loaded content is the new baseline.
            _undoEngine.Reset();
            _undoEngine.MarkSaved();
            _isDirty = false;

            // Sync TotalLines so the virtualization engine reflects the newly loaded document.
            UpdateVirtualization();
            RebuildMaxLineLength();

            if (IsFoldingEnabled && _foldingEngine != null)
                _foldingEngine.Analyze(_document.Lines);

            _lineNumberCache.Clear();
            InvalidateMeasure();
        }

        /// <summary>
        /// Get current text content
        /// </summary>
        public string GetText()
        {
            return _document.SaveToString();
        }

        /// <summary>
        /// Get current cursor position
        /// </summary>
        public TextPosition CursorPosition => new TextPosition(_cursorLine, _cursorColumn);

        /// <summary>
        /// Get current selection
        /// </summary>
        public TextSelection Selection => _selection;

        /// <summary>
        /// Check if can undo
        /// </summary>
        public bool CanUndo => _undoEngine.CanUndo;

        /// <summary>
        /// Check if can redo
        /// </summary>
        public bool CanRedo => _undoEngine.CanRedo;

        /// <summary>Number of available undo steps.</summary>
        public int UndoCount => _undoEngine.UndoCount;

        /// <summary>Number of available redo steps.</summary>
        public int RedoCount => _undoEngine.RedoCount;

        /// <summary>
        /// Get validation error count
        /// </summary>
        public int ValidationErrorCount => _validationErrors?.Count(e => e.Severity == ValidationSeverity.Error) ?? 0;

        /// <summary>
        /// Get validation warning count
        /// </summary>
        public int ValidationWarningCount => _validationErrors?.Count(e => e.Severity == ValidationSeverity.Warning) ?? 0;

        #endregion

        #region IDocumentEditor

        // -- State ----------------------------------------------------------

        /// <summary>
        /// True when the document has unsaved changes.
        /// </summary>
        public bool IsDirty => _isDirty;

        // -- IsReadOnly DP -------------------------------------------------

        public static readonly DependencyProperty IsReadOnlyProperty =
            DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(CodeEditor),
                new System.Windows.PropertyMetadata(false, (_, _) => { }));

        public bool IsReadOnly
        {
            get => (bool)GetValue(IsReadOnlyProperty);
            set => SetValue(IsReadOnlyProperty, value);
        }

        // -- Title ---------------------------------------------------------

        public string Title => BuildTitle();

        // -- Commands ------------------------------------------------------

        public System.Windows.Input.ICommand UndoCommand => new JsonRelayCommand(_ => Undo(), _ => CanUndo);
        public System.Windows.Input.ICommand RedoCommand => new JsonRelayCommand(_ => Redo(), _ => CanRedo);
        public System.Windows.Input.ICommand SaveCommand => new JsonRelayCommand(_ => Save());
        public System.Windows.Input.ICommand CopyCommand => new JsonRelayCommand(_ => CopyToClipboard(), _ => !_selection.IsEmpty);
        public System.Windows.Input.ICommand CutCommand => new JsonRelayCommand(_ => CutToClipboard(), _ => !_selection.IsEmpty && !IsReadOnly);
        public System.Windows.Input.ICommand PasteCommand => new JsonRelayCommand(_ => PasteFromClipboard(), _ => !IsReadOnly && Clipboard.ContainsText());
        public System.Windows.Input.ICommand DeleteCommand => new JsonRelayCommand(_ => DeleteSelection(), _ => !_selection.IsEmpty && !IsReadOnly);
        public System.Windows.Input.ICommand SelectAllCommand => new JsonRelayCommand(_ => SelectAll());

        // -- Methods -------------------------------------------------------

        public void Save()
        {
            // Delegate to SaveAsync so file I/O runs off the UI thread.
            // Fire-and-forget: the async path handles status/dirty updates.
            if (!string.IsNullOrEmpty(_currentFilePath))
                _ = SaveAsync();
        }

        public async System.Threading.Tasks.Task SaveAsync(System.Threading.CancellationToken ct = default)
        {
            if (!string.IsNullOrEmpty(_currentFilePath))
                await SaveAsAsync(_currentFilePath, ct);
        }

        public async System.Threading.Tasks.Task SaveAsAsync(string filePath, System.Threading.CancellationToken ct = default)
        {
            // Snapshot text on the UI thread before switching threads.
            var text = GetText();
            try
            {
                await Task.Run(() => File.WriteAllText(filePath, text, System.Text.Encoding.UTF8), ct);
            }
            catch (Exception ex)
            {
                StatusMessage?.Invoke(this, $"Save failed: {ex.Message}");
                return;
            }

            _currentFilePath = filePath;
            _undoEngine.MarkSaved();  // Stamp save-point so Undo can detect "back to clean".
            _isDirty = false;
            ModifiedChanged?.Invoke(this, EventArgs.Empty);
            TitleChanged?.Invoke(this, BuildTitle());
            StatusMessage?.Invoke(this, $"Saved: {Path.GetFileName(filePath)}");
        }

        // -- Open file -----------------------------------------------------

        public void LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                StatusMessage?.Invoke(this, $"File not found: {Path.GetFileName(filePath)}");
                return;
            }

            var text = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
            LoadText(text);
            _currentFilePath = filePath;
            TitleChanged?.Invoke(this, BuildTitle());
            StatusMessage?.Invoke(this, $"Opened: {Path.GetFileName(filePath)}");
            RefreshJsonStatusBarItems();
        }

        async Task IOpenableDocument.OpenAsync(string filePath, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            if (!File.Exists(filePath))
            {
                StatusMessage?.Invoke(this, $"File not found: {Path.GetFileName(filePath)}");
                return;
            }

            // Read + split on a background thread to keep the UI responsive (P1-TE-05 / OPT-PERF-05).
            // content.Split + new CodeLine[] are pure computation with no WPF dependency.
            string text;
            CodeLine[] lines;
            try
            {
                (text, lines) = await Task.Run(() =>
                {
                    var raw   = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
                    var parts = raw.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                    var arr   = new CodeLine[parts.Length == 0 ? 1 : parts.Length];
                    for (int i = 0; i < parts.Length; i++)
                        arr[i] = new CodeLine(parts[i], i);
                    if (arr.Length == 0)
                        arr[0] = new CodeLine(string.Empty, 0);
                    return (raw, arr);
                }, ct);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                StatusMessage?.Invoke(this, $"Open failed: {ex.Message}");
                return;
            }

            // UI-thread work: swap the pre-built line array into the document, then run the same
            // post-load steps as LoadText() so VirtualizationEngine.TotalLines is updated.
            _document.LoadLines(lines, text);
            _undoEngine.Reset();    // Loaded content is the new baseline.
            _undoEngine.MarkSaved();
            _isDirty = false;
            _cursorLine             = 0;
            _cursorColumn           = 0;
            _selection.Clear();
            _verticalScrollOffset   = 0;
            _currentScrollOffset    = 0;
            _targetScrollOffset     = 0;
            _horizontalScrollOffset = 0;
            UpdateVirtualization();
            RebuildMaxLineLength();
            if (IsFoldingEnabled && _foldingEngine != null)
                _foldingEngine.Analyze(_document.Lines);
            _lineNumberCache.Clear();
            InvalidateMeasure();
            _currentFilePath = filePath;

            // Re-attach CodeLens with the resolved file path so workspace-wide
            // counting uses the correct extension filter.
            _codeLensService.Attach(_document, _currentFilePath);

            // Apply .editorconfig settings (P2-03): indent style, size, EOL, etc.
            ApplyEditorConfig(Services.EditorConfigService.Resolve(filePath));

            // Notify the LSP server about the newly opened document (Phase 4).
            if (_lspClient?.IsInitialized == true)
            {
                _lspClient.OpenDocument(filePath, DetectLanguageId(filePath), text);
                _lspDocVersion = 1;
            }

            TitleChanged?.Invoke(this, BuildTitle());
            StatusMessage?.Invoke(this, $"Opened: {Path.GetFileName(filePath)}");
            RefreshJsonStatusBarItems();
        }

        /// <summary>
        /// Applies <see cref="Services.EditorConfigSettings"/> properties to the matching
        /// editor DependencyProperties.  Null settings properties are left unchanged.
        /// </summary>
        private void ApplyEditorConfig(Services.EditorConfigSettings cfg)
        {
            if (cfg.IndentSize is int indent) IndentSize = indent;
        }

        // -- LSP Client Wiring (Phase 4) ──────────────────────────────────

        /// <summary>
        /// Injects or replaces the active LSP client.
        /// Call with <c>null</c> to detach (e.g., when closing a document).
        /// The method subscribes to <see cref="ILspClient.DiagnosticsReceived"/> and
        /// sends textDocument/didOpen when the editor already has a file loaded.
        /// </summary>
        public void SetLspClient(WpfHexEditor.Editor.Core.LSP.ILspClient? client)
        {
            if (_lspClient is not null)
            {
                _lspClient.DiagnosticsReceived -= OnLspDiagnosticsReceived;
                if (_currentFilePath is not null)
                    _lspClient.CloseDocument(_currentFilePath);
            }

            _lspClient = client;
            _hoverQuickInfoService?.SetLspClient(client);
            _ctrlClickService?.SetLspClient(client);
            _lspDocVersion = 0;

            if (_lspClient is null) return;

            _lspClient.DiagnosticsReceived += OnLspDiagnosticsReceived;

            // If a file is already open, send didOpen immediately.
            if (_currentFilePath is not null && _document is not null)
            {
                var langId = DetectLanguageId(_currentFilePath);
                _lspClient.OpenDocument(_currentFilePath, langId, _document.SaveToString());
            }

            // Create the change-debounce timer (300 ms) on first attach.
            if (_lspChangeTimer is null)
            {
                _lspChangeTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(300),
                };
                _lspChangeTimer.Tick += (_, _) =>
                {
                    _lspChangeTimer.Stop();
                    if (_lspClient is null || _currentFilePath is null) return;
                    _lspClient.DidChange(_currentFilePath, ++_lspDocVersion, _document.SaveToString());
                };
            }
        }

        /// <summary>Maps a file extension to an LSP language identifier.</summary>
        private static string DetectLanguageId(string filePath) =>
            Path.GetExtension(filePath).ToLowerInvariant() switch
            {
                ".json" or ".jsonc" => "json",
                ".xml"              => "xml",
                ".xaml"             => "xaml",
                ".cs"               => "csharp",
                ".ts"               => "typescript",
                ".js"               => "javascript",
                ".py"               => "python",
                ".lua"              => "lua",
                _                   => "plaintext",
            };

        /// <summary>
        /// Feeds LSP push diagnostics into the editor's validation error list.
        /// Always called on the UI thread (guaranteed by LspClientImpl).
        /// </summary>
        private void OnLspDiagnosticsReceived(
            object? sender,
            WpfHexEditor.Editor.Core.LSP.LspDiagnosticsReceivedEventArgs e)
        {
            if (_currentFilePath is null) return;
            if (!new Uri(_currentFilePath).AbsoluteUri.Equals(e.DocumentUri, StringComparison.OrdinalIgnoreCase))
                return;

            // Remove any previous LSP-sourced errors and replace with the new set.
            _validationErrors.RemoveAll(v => v.Layer == Models.ValidationLayer.Lsp);
            foreach (var d in e.Diagnostics)
            {
                _validationErrors.Add(new Models.ValidationError
                {
                    Line     = d.StartLine,
                    Column   = d.StartColumn,
                    Message  = d.Message,
                    Severity = d.Severity == "error"   ? Models.ValidationSeverity.Error
                             : d.Severity == "warning" ? Models.ValidationSeverity.Warning
                                                       : Models.ValidationSeverity.Info,
                    Layer    = Models.ValidationLayer.Lsp,
                });
            }
            RebuildValidationIndex();
            DiagnosticsChanged?.Invoke(this, EventArgs.Empty);
            InvalidateVisual();
        }

        // -- Find All References (LSP) ------------------------------------

        /// <summary>
        /// Returns the identifier word (letters, digits, underscore) at the current caret
        /// position, or <see cref="string.Empty"/> when the caret is not on a word character.
        /// Reuses the same boundary logic as <see cref="SelectWordAtPosition"/>.
        /// </summary>
        private string GetWordAtCursor()
        {
            if (_document is null
                || _cursorLine  < 0
                || _cursorLine  >= _document.Lines.Count)
                return string.Empty;

            var lineText = _document.Lines[_cursorLine].Text;
            if (string.IsNullOrEmpty(lineText) || _cursorColumn > lineText.Length)
                return string.Empty;

            // Snap column to valid range
            int col   = Math.Min(_cursorColumn, lineText.Length - 1);
            if (!IsWordChar(lineText[col]))
                return string.Empty;

            int start = col;
            int end   = col;

            while (start > 0 && IsWordChar(lineText[start - 1]))
                start--;

            while (end < lineText.Length && IsWordChar(lineText[end]))
                end++;

            return lineText[start..end];
        }

        /// <summary>
        /// Invokes <c>textDocument/references</c> for the symbol at the caret, groups
        /// the results by file, reads snippets, then shows <see cref="ReferencesPopup"/>.
        /// </summary>
        private async Task FindAllReferencesAsync(int? lineOverride = null, string? symbolOverride = null)
        {
            if (_document is null) return;

            // When called from a CodeLens click, line/symbol are supplied directly
            // so the caret is never mutated. The Shift+F12 path supplies no overrides
            // and reads _cursorLine / _cursorColumn as before.
            int    line   = lineOverride ?? _cursorLine;
            int    column = lineOverride.HasValue
                                ? FindSymbolColumnInLine(line, symbolOverride ?? string.Empty)
                                : _cursorColumn;
            string symbol = symbolOverride ?? GetWordAtCursor();

            if (string.IsNullOrEmpty(symbol))
            {
                StatusMessage?.Invoke(this, "Place the caret on a symbol to find references.");
                return;
            }

            List<ReferenceGroup> groups;

            // ── LSP path (preferred when a language server is running) ─────────
            if (_lspClient?.IsInitialized == true && _currentFilePath is not null)
            {
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10));
                IReadOnlyList<WpfHexEditor.Editor.Core.LSP.LspLocation> locations;
                try
                {
                    locations = await _lspClient.ReferencesAsync(
                        _currentFilePath, line, column, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    StatusMessage?.Invoke(this, "Find References timed out — falling back to local scan.");
                    locations = Array.Empty<WpfHexEditor.Editor.Core.LSP.LspLocation>();
                }

                if (locations.Count > 0)
                {
                    groups = BuildGroupsFromLspLocations(locations, symbol);
                    ShowReferencesPopup(groups, symbol, locations.Count, source: "LSP", line: line, column: column);
                    return;
                }
                // Fall through to local scan when LSP returns no results.
            }

            // ── Local/workspace scan fallback ────────────────────────────────
            // When a solution is loaded, search across all files of the same
            // extension; otherwise fall back to the current document only.
            groups = BuildGroupsFromWorkspaceScan(symbol);
            int total = groups.Sum(g => g.Items.Count);

            if (total == 0)
            {
                StatusMessage?.Invoke(this, $"No occurrences of '{symbol}' found.");
                return;
            }

            ShowReferencesPopup(groups, symbol, total, source: "workspace", line: line, column: column);
        }

        /// <summary>
        /// Scans the current in-memory document AND all solution files of the same
        /// file extension for whole-word occurrences of <paramref name="symbol"/>.
        /// Falls back to current-document-only scan when no solution is loaded.
        /// </summary>
        private List<ReferenceGroup> BuildGroupsFromWorkspaceScan(string symbol)
        {
            // Step 1 — always scan the current in-memory document.
            var groups = BuildGroupsFromLocalScan(symbol);

            // Step 2 — scan workspace files of matching extension.
            var ext = Path.GetExtension(_currentFilePath ?? string.Empty);
            if (string.IsNullOrEmpty(ext)) return groups;

            var workspacePaths = WorkspaceFileCache.GetPathsForExtensions([ext]);
            foreach (var path in workspacePaths)
            {
                if (path.Equals(_currentFilePath, StringComparison.OrdinalIgnoreCase))
                    continue; // already covered by the in-memory scan above

                var lines = WorkspaceFileCache.GetLines(path);
                if (lines is null) continue;

                var items = new List<ReferenceItem>();
                for (int lineIdx = 0; lineIdx < lines.Length; lineIdx++)
                {
                    var lineText = lines[lineIdx];
                    if (string.IsNullOrEmpty(lineText)) continue;

                    int col = 0;
                    while (true)
                    {
                        int pos = lineText.IndexOf(symbol, col, StringComparison.Ordinal);
                        if (pos < 0) break;

                        bool leftOk  = pos == 0                       || !IsWordChar(lineText[pos - 1]);
                        bool rightOk = pos + symbol.Length >= lineText.Length
                                       || !IsWordChar(lineText[pos + symbol.Length]);

                        if (leftOk && rightOk)
                        {
                            var snippet = lineText.Length > 200 ? lineText[..200] : lineText;
                            items.Add(new ReferenceItem
                            {
                                Line    = lineIdx,
                                Column  = pos,
                                Snippet = snippet.TrimStart()
                            });
                        }

                        col = pos + symbol.Length;
                    }
                }

                if (items.Count > 0)
                    groups.Add(new ReferenceGroup
                    {
                        FilePath     = path,
                        DisplayLabel = Path.GetFileName(path),
                        Items        = items
                    });
            }

            return groups;
        }

        /// <summary>
        /// Converts LSP location results into <see cref="ReferenceGroup"/> list,
        /// reading snippets from the in-memory document or disk.
        /// </summary>
        private List<ReferenceGroup> BuildGroupsFromLspLocations(
            IReadOnlyList<WpfHexEditor.Editor.Core.LSP.LspLocation> locations,
            string symbol)
        {
            var byFile = new Dictionary<string, List<WpfHexEditor.Editor.Core.LSP.LspLocation>>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var loc in locations)
            {
                var path = UriToFilePath(loc.Uri);
                if (!byFile.TryGetValue(path, out var list))
                    byFile[path] = list = new List<WpfHexEditor.Editor.Core.LSP.LspLocation>();
                list.Add(loc);
            }

            var groups = new List<ReferenceGroup>(byFile.Count);
            foreach (var (filePath, locs) in byFile)
            {
                string[]? lines = null;
                if (filePath.Equals(_currentFilePath, StringComparison.OrdinalIgnoreCase))
                    lines = _document.Lines.Select(l => l.Text).ToArray();
                else if (File.Exists(filePath))
                    lines = File.ReadAllLines(filePath);

                var items = new List<ReferenceItem>(locs.Count);
                foreach (var loc in locs)
                {
                    var raw = (lines != null && loc.StartLine < lines.Length)
                        ? lines[loc.StartLine]
                        : string.Empty;
                    if (raw.Length > 200) raw = raw[..200];

                    items.Add(new ReferenceItem
                    {
                        Line    = loc.StartLine,
                        Column  = loc.StartColumn,
                        Snippet = raw.TrimStart()
                    });
                }

                groups.Add(new ReferenceGroup
                {
                    FilePath     = filePath,
                    DisplayLabel = Path.GetFileName(filePath),
                    Items        = items
                });
            }

            return groups;
        }

        /// <summary>
        /// Scans the current in-memory document for all whole-word occurrences of
        /// <paramref name="symbol"/> and returns them as a single-file
        /// <see cref="ReferenceGroup"/>. Used when no LSP client is available.
        /// </summary>
        private List<ReferenceGroup> BuildGroupsFromLocalScan(string symbol)
        {
            var items = new List<ReferenceItem>();

            for (int lineIdx = 0; lineIdx < _document.Lines.Count; lineIdx++)
            {
                var lineText = _document.Lines[lineIdx].Text;
                if (string.IsNullOrEmpty(lineText)) continue;

                int col = 0;
                while (true)
                {
                    int pos = lineText.IndexOf(symbol, col, StringComparison.Ordinal);
                    if (pos < 0) break;

                    // Whole-word boundary check — skip if adjacent chars are word chars.
                    bool leftOk  = pos == 0                       || !IsWordChar(lineText[pos - 1]);
                    bool rightOk = pos + symbol.Length >= lineText.Length
                                   || !IsWordChar(lineText[pos + symbol.Length]);

                    if (leftOk && rightOk)
                    {
                        var snippet = lineText.Length > 200 ? lineText[..200] : lineText;
                        items.Add(new ReferenceItem
                        {
                            Line    = lineIdx,
                            Column  = pos,
                            Snippet = snippet.TrimStart()
                        });
                    }

                    col = pos + symbol.Length;
                }
            }

            if (items.Count == 0) return new List<ReferenceGroup>();

            return new List<ReferenceGroup>
            {
                new ReferenceGroup
                {
                    FilePath     = _currentFilePath ?? string.Empty,
                    DisplayLabel = _currentFilePath is not null
                                   ? Path.GetFileName(_currentFilePath)
                                   : "(unsaved document)",
                    Items        = items
                }
            };
        }

        /// <summary>
        /// Computes the anchor Point, shows the popup and updates the status bar.
        /// </summary>
        private void ShowReferencesPopup(
            List<ReferenceGroup> groups, string symbol, int total, string source,
            int line = -1, int column = -1)
        {
            // Defaults: Shift+F12 path supplies no overrides, reads cursor fields.
            if (line   < 0) line   = _cursorLine;
            if (column < 0) column = _cursorColumn;

            int visLineOffset = line - _firstVisibleLine;
            double lh = _lineHeight > 0 ? _lineHeight : 16.0;
            double cw = _charWidth  > 0 ? _charWidth  : 8.0;
            double x  = (ShowLineNumbers ? TextAreaLeftOffset : LeftMargin) + column * cw;

            // Prefer the actual rendered hit-zone Top as anchor Y.
            // _lineYLookup can accumulate rounding errors when many CodeLens lines appear
            // above the declaration, causing the popup to float too high.
            double anchorY = -1;
            foreach (var (hz, hzLine, _) in _lensHitZones)
            {
                if (hzLine == line) { anchorY = hz.Top; break; }
            }
            if (anchorY < 0)
            {
                // Shift+F12 / hint not currently rendered — fall back to _lineYLookup.
                double codeY = _lineYLookup.TryGetValue(line, out double ly)
                    ? ly : TopMargin + visLineOffset * lh;
                anchorY = codeY - LensLineHeight;
            }

            var anchor = new Point(x, anchorY);

            // Resolve kind icon from lens data (null-safe: Shift+F12 invocations have no lens entry).
            string iconGlyph = "\uE8A5";
            System.Windows.Media.Brush iconBrush = System.Windows.Media.Brushes.Gray;
            if (_lensData.TryGetValue(line, out var lensEntry))
            {
                iconGlyph = lensEntry.IconGlyph;
                iconBrush = lensEntry.IconBrush;
            }

            // Store latest results so pin handler can forward them to the dock host.
            _lastReferenceGroups = groups;
            _lastReferenceSymbol = symbol;

            _referencesPopup ??= new ReferencesPopup();
            _referencesPopup.NavigationRequested -= OnReferencesNavigationRequested;
            _referencesPopup.NavigationRequested += OnReferencesNavigationRequested;
            _referencesPopup.RefreshRequested    -= OnPopupRefreshRequested;
            _referencesPopup.RefreshRequested    += OnPopupRefreshRequested;
            _referencesPopup.PinRequested        -= OnPopupPinRequested;
            _referencesPopup.PinRequested        += OnPopupPinRequested;

            _referencesPopup.Show(this, groups, symbol, anchor, lh, iconGlyph, iconBrush);
            StatusMessage?.Invoke(this,
                $"{total} occurrence{(total != 1 ? "s" : "")} of '{symbol}' ({source}).");
        }

        /// <summary>Converts a <c>file:///</c> URI to a local file-system path.</summary>
        private static string UriToFilePath(string uri)
        {
            if (uri.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
                return Uri.UnescapeDataString(uri[8..]).Replace('/', Path.DirectorySeparatorChar);
            if (uri.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                return Uri.UnescapeDataString(uri[7..]).Replace('/', Path.DirectorySeparatorChar);
            return uri;
        }

        /// <summary>
        /// Routes a reference-popup navigation event: same-file → local scroll;
        /// different file → propagates to the IDE host.
        /// </summary>
        private void OnReferencesNavigationRequested(
            object? sender, ReferencesNavigationEventArgs e)
        {
            _referencesPopup?.Close();

            if (e.FilePath.Equals(_currentFilePath, StringComparison.OrdinalIgnoreCase))
                NavigateToLine(e.Line);
            else
                ReferenceNavigationRequested?.Invoke(this, e);
        }

        private async void OnPopupRefreshRequested(object? sender, EventArgs e)
            => await FindAllReferencesAsync();

        private void OnPopupPinRequested(object? sender, EventArgs e)
        {
            _referencesPopup?.Close();
            FindAllReferencesDockRequested?.Invoke(this, new FindAllReferencesDockEventArgs
            {
                Groups     = _lastReferenceGroups,
                SymbolName = _lastReferenceSymbol
            });
        }

        /// <summary>
        /// Returns true when <paramref name="originalSource"/> lives in the same Win32 HWND
        /// (PresentationSource) as the references popup — i.e., the click originated inside
        /// the popup's own layered window, not in the CodeEditor window.
        /// <para>
        /// VisualTreeHelper.GetParent cannot cross HWND boundaries, so an HWND-aware check
        /// via <see cref="PresentationSource.FromVisual"/> is required for AllowsTransparency
        /// popups that live in their own HwndSource.
        /// </para>
        /// </summary>
        private bool IsEventFromInsidePopup(object? originalSource)
        {
            if (_referencesPopup?.IsOpen != true || _referencesPopup.Child is null)
                return false;
            if (originalSource is not Visual visual)
                return false;

            // Compare HwndSource instances: equal ⟹ same HWND ⟹ click is from the popup.
            var clickSource = PresentationSource.FromVisual(visual);
            var popupSource = PresentationSource.FromVisual(_referencesPopup.Child);
            return clickSource is not null && ReferenceEquals(clickSource, popupSource);
        }

        /// <summary>
        /// Returns true when the click position (relative to this editor) maps to a screen
        /// point that falls within the references popup's bounding rectangle.
        /// Belt-and-suspenders fallback for the Win32 click-through scenario where
        /// <see cref="IsEventFromInsidePopup"/> cannot detect the popup source.
        /// </summary>
        private bool IsClickInsidePopupBounds(Point posRelativeToThis)
        {
            if (_referencesPopup?.IsOpen != true || _referencesPopup.Child is not UIElement child)
                return false;
            try
            {
                var screenPt     = PointToScreen(posRelativeToThis);
                var popupTopLeft = child.PointToScreen(new Point(0, 0));
                return new Rect(popupTopLeft, child.RenderSize).Contains(screenPt);
            }
            catch { return false; }
        }

        // -- Public methods (IDocumentEditor) -----------------------------

        void IDocumentEditor.Copy() => CopyToClipboard();
        void IDocumentEditor.Cut() => CutToClipboard();
        void IDocumentEditor.Paste() => PasteFromClipboard();
        void IDocumentEditor.Delete() => DeleteSelection();
        void IDocumentEditor.SelectAll() => SelectAll();

        public void Close()
        {
            // Notify the LSP server before clearing the path (Phase 4).
            if (_lspClient?.IsInitialized == true && _currentFilePath is not null)
                _lspClient.CloseDocument(_currentFilePath);

            _quickInfoPopup?.Hide();
            _hoverQuickInfoService?.Dispose();
            _hoverQuickInfoService = null;
            _ctrlClickService?.Dispose();
            _ctrlClickService = null;

            _document = new Models.CodeDocument();
            _currentFilePath = null;
            _isDirty = false;
            _cursorLine = 0;
            _cursorColumn = 0;
            _selection.Clear();
            _undoEngine.Reset();
            _validationErrors.Clear();
            InvalidateVisual();
            ModifiedChanged?.Invoke(this, EventArgs.Empty);
            TitleChanged?.Invoke(this, BuildTitle());
            DiagnosticsChanged?.Invoke(this, EventArgs.Empty);
        }

        // -- Events --------------------------------------------------------

        /// <summary>Current caret line (0-based). Updated after every cursor movement.</summary>
        public int CursorLine => _cursorLine;

        /// <summary>Fired when the caret moves to a different line (debounced to line-level changes).</summary>
        public event EventHandler? CaretMoved;

        /// <summary>
        /// Raised when the user navigates to a reference in a different file via the
        /// Find All References popup. The host should open the target file and move
        /// the caret to the specified position.
        /// </summary>
        public event EventHandler<ReferencesNavigationEventArgs>? ReferenceNavigationRequested;

        /// <summary>
        /// Raised when the user pins the References popup into a docked panel.
        /// The host should open (or activate) a <see cref="FindReferencesPanel"/>
        /// and call <c>Refresh</c> with the supplied groups.
        /// </summary>
        public event EventHandler<FindAllReferencesDockEventArgs>? FindAllReferencesDockRequested;

        public event EventHandler? ModifiedChanged;
        public event EventHandler? CanUndoChanged;
        public event EventHandler? CanRedoChanged;
        public event EventHandler<string>? TitleChanged;
        public event EventHandler<string>? StatusMessage;
        public event EventHandler<string>? OutputMessage;
        public event EventHandler? SelectionChanged;

        /// <summary>
        /// Raised when Ctrl+Click targets an external symbol (e.g. BCL / NuGet assembly).
        /// The IDE host should route this to AssemblyExplorer or open a decompiled-source tab.
        /// </summary>
        public event EventHandler<GoToExternalDefinitionEventArgs>? GoToExternalDefinitionRequested;

        // -- Long-running operations (no-op: CodeEditor has no async operations) --
        public bool IsBusy => false;
        public void CancelOperation() { }
        public event EventHandler<DocumentOperationEventArgs>?          OperationStarted;
        public event EventHandler<DocumentOperationEventArgs>?          OperationProgress;
        public event EventHandler<DocumentOperationCompletedEventArgs>? OperationCompleted;

        // -- Helpers -------------------------------------------------------

        private string BuildTitle()
        {
            var name = !string.IsNullOrEmpty(_currentFilePath)
                ? Path.GetFileName(_currentFilePath)
                : "untitled.json";
            return _isDirty ? name + " *" : name;
        }

        #endregion

        // -- IPropertyProviderSource -------------------------------------------
        private WpfHexEditor.Editor.CodeEditor.CodeEditorPropertyProvider? _propertyProvider;
        public IPropertyProvider? GetPropertyProvider()
            => _propertyProvider ??= new WpfHexEditor.Editor.CodeEditor.CodeEditorPropertyProvider(this);

        // ═══════════════════════════════════════════════════════════════════
        // IStatusBarContributor
        // ═══════════════════════════════════════════════════════════════════

        private ObservableCollection<StatusBarItem>? _jsonStatusBarItems;
        private StatusBarItem _sbLanguage  = null!;
        private StatusBarItem _sbPosition  = null!;
        private StatusBarItem _sbZoom      = null!;
        private StatusBarItem _sbSelection = null!;

        /// <summary>Current caret column (0-based). Companion to <see cref="CursorLine"/>.</summary>
        public int CursorColumn => _cursorColumn;

        public ObservableCollection<StatusBarItem> StatusBarItems
            => _jsonStatusBarItems ??= BuildJsonStatusBarItems();

        private ObservableCollection<StatusBarItem> BuildJsonStatusBarItems()
        {
            _sbLanguage  = new StatusBarItem { Label = "Language", Tooltip = "Detected syntax language" };
            _sbPosition  = new StatusBarItem { Label = "Position", Tooltip = "Caret line and column" };
            _sbZoom      = new StatusBarItem { Label = "Zoom",     Tooltip = "Editor zoom level" };
            _sbSelection = new StatusBarItem { Label = "Sel",      Tooltip = "Number of selected characters", IsVisible = false };

            // Zoom preset choices — selecting one applies the zoom level immediately.
            foreach (var (pct, factor) in new (string, double)[] { ("50%", 0.5), ("75%", 0.75), ("100%", 1.0), ("125%", 1.25), ("150%", 1.5), ("200%", 2.0) })
            {
                var capture = factor;
                _sbZoom.Choices.Add(new StatusBarChoice
                {
                    DisplayName = pct,
                    Command     = new JsonRelayCommand(_ => ZoomLevel = capture),
                });
            }

            // Wire live-update events once (lazy-init guard ensures single subscription).
            CaretMoved       += (_, _) => RefreshJsonStatusBarItems();
            ZoomLevelChanged += (_, _) => RefreshJsonStatusBarItems();
            SelectionChanged += (_, _) => RefreshJsonStatusBarItems();

            RefreshJsonStatusBarItems();
            return new ObservableCollection<StatusBarItem> { _sbLanguage, _sbPosition, _sbZoom, _sbSelection };
        }

        void IStatusBarContributor.RefreshStatusBarItems() => RefreshJsonStatusBarItems();

        internal void RefreshJsonStatusBarItems()
        {
            if (_jsonStatusBarItems is null) return;

            _sbLanguage.Value = ExternalHighlighter?.LanguageName ?? _highlighter.LanguageName ?? "JSON";
            _sbPosition.Value = $"Ln {_cursorLine + 1}, Col {_cursorColumn + 1}";
            _sbZoom.Value     = $"{(int)(ZoomLevel * 100)}%";

            bool hasSelection = !_selection.IsEmpty;
            _sbSelection.IsVisible = hasSelection;
            if (hasSelection)
            {
                int charCount = _selection.IsMultiLine
                    ? (_document?.GetText(_selection.NormalizedStart, _selection.NormalizedEnd).Length ?? 0)
                    : Math.Abs(_selection.NormalizedEnd.Column - _selection.NormalizedStart.Column);
                _sbSelection.Value = charCount.ToString();
            }

            // Keep zoom choice checkmarks in sync.
            string zoomLabel = $"{(int)(ZoomLevel * 100)}%";
            foreach (var choice in _sbZoom.Choices)
                choice.IsActive = choice.DisplayName == zoomLabel;
        }

        // ═══════════════════════════════════════════════════════════════════
        // IEditorPersistable
        // Persists caret position, scroll offset, and syntax language id.
        // Binary changeset (byte-level) is not applicable to a text editor —
        // GetChangesetSnapshot returns Empty and ApplyChangeset is a no-op.
        // ═══════════════════════════════════════════════════════════════════

        EditorConfigDto IEditorPersistable.GetEditorConfig()
        {
            return new EditorConfigDto
            {
                CaretLine        = _cursorLine + 1,   // store 1-based
                CaretColumn      = _cursorColumn + 1,
                FirstVisibleLine = (int)(_verticalScrollOffset / Math.Max(1, _lineHeight)) + 1,
                SyntaxLanguageId = ExternalHighlighter is not null ? "external" : null,
            };
        }

        void IEditorPersistable.ApplyEditorConfig(EditorConfigDto config)
        {
            if (config.CaretLine > 0 && _document != null)
            {
                _cursorLine   = Math.Clamp(config.CaretLine - 1, 0, _document.Lines.Count - 1);
                _cursorColumn = Math.Max(0, config.CaretColumn - 1);
            }
            if (config.FirstVisibleLine > 0 && _lineHeight > 0)
            {
                _verticalScrollOffset = (config.FirstVisibleLine - 1) * _lineHeight;
            }
            InvalidateVisual();
        }

        // CodeEditor has no binary modifications — return null / no-op
        byte[]? IEditorPersistable.GetUnsavedModifications() => null;
        void IEditorPersistable.ApplyUnsavedModifications(byte[] data) { }

        // Binary changeset model is not applicable for a text editor
        ChangesetSnapshot IEditorPersistable.GetChangesetSnapshot() => ChangesetSnapshot.Empty;
        void IEditorPersistable.ApplyChangeset(ChangesetDto changeset) { }

        void IEditorPersistable.MarkChangesetSaved()
        {
            _undoEngine.MarkSaved();
            _isDirty = false;
            ModifiedChanged?.Invoke(this, EventArgs.Empty);
        }

        // No bookmark concept in CodeEditor yet
        IReadOnlyList<BookmarkDto>? IEditorPersistable.GetBookmarks() => null;
        void IEditorPersistable.ApplyBookmarks(IReadOnlyList<BookmarkDto> bookmarks) { }

        // ═══════════════════════════════════════════════════════════════════
        // IDiagnosticSource
        // ═══════════════════════════════════════════════════════════════════

        public event EventHandler? DiagnosticsChanged;

        string IDiagnosticSource.SourceLabel
            => !string.IsNullOrEmpty(_currentFilePath) ? Path.GetFileName(_currentFilePath)! : "JSON Editor";

        IReadOnlyList<DiagnosticEntry> IDiagnosticSource.GetDiagnostics()
        {
            if (_validationErrors == null || _validationErrors.Count == 0)
                return [];

            var fileName = !string.IsNullOrEmpty(_currentFilePath) ? Path.GetFileName(_currentFilePath) : null;
            var filePath = _currentFilePath;

            return _validationErrors.Select(ve => new DiagnosticEntry(
                Severity:    ve.Severity switch
                {
                    ValidationSeverity.Warning => DiagnosticSeverity.Warning,
                    ValidationSeverity.Info    => DiagnosticSeverity.Message,
                    _                          => DiagnosticSeverity.Error,
                },
                Code:        !string.IsNullOrEmpty(ve.ErrorCode) ? ve.ErrorCode : ve.Layer.ToString(),
                Description: ve.Message ?? string.Empty,
                FileName:    fileName,
                FilePath:    filePath,
                Line:        ve.Line + 1,
                Column:      ve.Column + 1
            )).ToList();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Quick Info — hover dispatch and popup management
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Evaluates whether a new Quick Info request should be dispatched for the
        /// given hover position, then calls <see cref="Services.HoverQuickInfoService.RequestAsync"/>.
        /// </summary>
        private void HandleQuickInfoHover(TextPosition hoverPos, Point pixelPos)
        {
            if (_hoverQuickInfoService is null) return;

            // Suppress if the mouse is already inside the popup itself.
            if (_quickInfoPopup?.IsMouseOverPopup == true) return;

            // Jitter guard: don't re-dispatch unless the text position actually changed.
            if (hoverPos == _lastHoverTextPos) return;
            _lastHoverTextPos = hoverPos;
            _lastHoverPixel   = pixelPos;

            if (_currentFilePath is null || _document is null) return;

            var lineText = hoverPos.Line >= 0 && hoverPos.Line < _document.Lines.Count
                ? _document.Lines[hoverPos.Line].Text ?? string.Empty
                : string.Empty;
            var (word, _) = GetWordAt(lineText, hoverPos.Column);

            if (string.IsNullOrEmpty(word))
            {
                _hoverQuickInfoService.Cancel();
                return;
            }

            // Snapshot diagnostics for the service (cross-thread safe copy).
            _hoverQuickInfoService.SetDiagnostics(_validationErrors.ToArray());

            var lineSnapshot = _document.Lines.ToArray();
            _hoverQuickInfoService.RequestAsync(
                _currentFilePath, hoverPos.Line, hoverPos.Column, word, lineSnapshot);
        }

        /// <summary>
        /// Called when <see cref="Services.HoverQuickInfoService"/> fires
        /// <see cref="Services.HoverQuickInfoService.QuickInfoResolved"/>.
        /// </summary>
        private void OnFoldPeekTimerTick(object? sender, EventArgs e)
        {
            _foldPeekTimer!.Stop();
            if (_foldPeekTargetLine < 0 || _foldingEngine == null || _document == null) return;

            var region = _foldingEngine.GetRegionAt(_foldPeekTargetLine);
            if (region == null || !region.IsCollapsed) return;

            // Find the label rect so we can anchor the popup beneath it.
            Rect labelRect = default;
            foreach (var (rect, ln) in _foldLabelHitZones)
                if (ln == _foldPeekTargetLine) { labelRect = rect; break; }

            _foldPeekPopup ??= new FoldPeekPopup();
            _foldPeekPopup.Show(this, region, _document.Lines, _typeface, _fontSize, labelRect,
                                ExternalHighlighter);
        }

        private void OnQuickInfoResolved(object? sender, WpfHexEditor.SDK.ExtensionPoints.QuickInfoResult? result)
        {
            if (result is null)
            {
                _quickInfoPopup?.Hide();
                return;
            }

            // Lazy-create popup on first use.
            if (_quickInfoPopup is null)
            {
                _quickInfoPopup = new QuickInfoPopup();
                _quickInfoPopup.ActionRequested += OnQuickInfoActionRequested;
            }

            // Compute anchor below the hovered line (CodeLens-aware Y via _lineYLookup).
            double anchorX = TextAreaLeftOffset + _lastHoverTextPos.Column * _charWidth
                             - _horizontalScrollOffset;
            double anchorY = _lineYLookup.TryGetValue(_lastHoverTextPos.Line, out double ly)
                ? ly + _lineHeight + 2
                : _lastHoverTextPos.Line * _lineHeight + _lineHeight + 2;
            var anchor = new Point(anchorX, anchorY);

            _quickInfoPopup.Show(this, result, anchor);
        }

        /// <summary>Routes action link clicks from the Quick Info popup.</summary>
        private void OnQuickInfoActionRequested(object? sender, QuickInfoActionEventArgs e)
        {
            switch (e.Command)
            {
                case "GoToDefinition":
                    if (_hoveredSymbolZone.HasValue)
                        _ = NavigateToDefinitionAsync(_hoveredSymbolZone.Value);
                    break;

                case "FindAllReferences":
                    _ = FindAllReferencesAsync();
                    break;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Ctrl+Click — symbol underline and definition navigation
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Updates <see cref="_hoveredSymbolZone"/> and dispatches an async resolution
        /// via <see cref="Services.CtrlClickNavigationService"/> when Ctrl is held.
        /// </summary>
        private void HandleCtrlHover(TextPosition hoverPos)
        {
            if (_document is null || _ctrlClickService is null) return;

            var lineText = hoverPos.Line >= 0 && hoverPos.Line < _document.Lines.Count
                ? _document.Lines[hoverPos.Line].Text ?? string.Empty
                : string.Empty;
            var (word, startCol) = GetWordAt(lineText, hoverPos.Column);

            if (string.IsNullOrEmpty(word))
            {
                // Keep the zone when the cursor is still within the word's column span.
                // WPF fires MouseMove just before MouseDown; a sub-pixel shift can land on
                // a column boundary where GetWordAt returns empty even though the pointer is
                // visually inside the underlined token. Clearing here would make OnMouseDown
                // see HasValue = false and silently skip navigation.
                if (_hoveredSymbolZone.HasValue
                    && hoverPos.Line   == _hoveredSymbolZone.Value.Line
                    && hoverPos.Column >= _hoveredSymbolZone.Value.StartCol
                    && hoverPos.Column <= _hoveredSymbolZone.Value.EndCol)
                    return;

                if (_hoveredSymbolZone.HasValue)
                {
                    _hoveredSymbolZone = null;
                    _ctrlClickService.Cancel();
                    InvalidateVisual();
                }
                return;
            }

            int endCol = startCol + word.Length;

            // Skip non-navigable token kinds (keywords, literals, comments, operators).
            // Only Identifier, Type, Attribute, and Default (unclassified) tokens are navigable.
            var tokenKind = GetTokenKindAtColumn(hoverPos.Line, startCol);
            if (tokenKind is SyntaxTokenKind.Keyword
                          or SyntaxTokenKind.Comment
                          or SyntaxTokenKind.String
                          or SyntaxTokenKind.Number
                          or SyntaxTokenKind.Operator
                          or SyntaxTokenKind.Bracket)
            {
                if (_hoveredSymbolZone.HasValue)
                {
                    _hoveredSymbolZone = null;
                    _ctrlClickService.Cancel();
                    // Cursor stays Hand — Ctrl is still held.
                    InvalidateVisual();
                }
                return;
            }

            // Skip if zone is identical to the current one (avoid redundant invalidate + async call).
            if (_hoveredSymbolZone.HasValue
                && _hoveredSymbolZone.Value.Line     == hoverPos.Line
                && _hoveredSymbolZone.Value.StartCol == startCol
                && _hoveredSymbolZone.Value.EndCol   == endCol)
                return;

            // Create provisional zone; TargetFilePath will be filled in by OnCtrlClickTargetResolved.
            _hoveredSymbolZone = new SymbolHitZone(
                hoverPos.Line, startCol, endCol, word,
                string.Empty, 0, 0, false);

            InvalidateVisual();

            if (_currentFilePath is null) return;
            var lineSnapshot = _document.Lines.ToArray();
            _ctrlClickService.RequestAsync(
                _currentFilePath, hoverPos.Line, hoverPos.Column,
                startCol, endCol, word, lineSnapshot);
        }

        /// <summary>
        /// Called when <see cref="Services.CtrlClickNavigationService"/> fires
        /// <see cref="Services.CtrlClickNavigationService.TargetResolved"/>.
        /// Updates the hovered zone with the resolved target location.
        /// </summary>
        private void OnCtrlClickTargetResolved(
            object? sender, Services.CtrlClickTarget? target)
        {
            if (target is null || !_hoveredSymbolZone.HasValue) return;

            // Only apply if the resolution still matches the zone currently under the cursor.
            var current = _hoveredSymbolZone.Value;
            if (current.Line     != target.Line
                || current.StartCol != target.StartCol
                || current.EndCol   != target.EndCol)
                return;

            _hoveredSymbolZone = new SymbolHitZone(
                target.Line, target.StartCol, target.EndCol, target.SymbolName,
                target.TargetFilePath, target.TargetLine, target.TargetColumn,
                target.IsExternal);
        }

        /// <summary>Navigates to the definition of the given symbol zone.</summary>
        private async Task NavigateToDefinitionAsync(SymbolHitZone zone)
        {
            _quickInfoPopup?.Hide();

            // 1. Already resolved to an in-project file — navigate directly.
            if (!string.IsNullOrEmpty(zone.TargetFilePath) && !zone.IsExternal)
            {
                if (zone.TargetFilePath.Equals(_currentFilePath, StringComparison.OrdinalIgnoreCase))
                    NavigateToLine(zone.TargetLine);
                else
                    ReferenceNavigationRequested?.Invoke(this, new ReferencesNavigationEventArgs
                    {
                        FilePath = zone.TargetFilePath,
                        Line     = zone.TargetLine + 1,
                        Column   = zone.TargetColumn + 1
                    });
                return;
            }

            // 2. Ask LSP for definition.
            if (_lspClient?.IsInitialized == true && _currentFilePath is not null)
            {
                try
                {
                    using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
                    var locations = await _lspClient.DefinitionAsync(
                        _currentFilePath, zone.Line, zone.StartCol, cts.Token)
                        .ConfigureAwait(true);

                    if (locations.Count > 0)
                    {
                        await HandleDefinitionLocationsAsync(locations, zone.SymbolName)
                            .ConfigureAwait(true);
                        return;
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception) { }
            }

            // 3. Local CodeStructureParser scan — finds declarations within the current document.
            //    Runs synchronously so a Ctrl+Click always produces feedback even without LSP.
            if (_document.Lines.Count > 0)
            {
                var snapshot = CodeStructureParser.Parse(_document.Lines);
                var all      = snapshot.Types.Concat(snapshot.Members);
                var decl     = all.FirstOrDefault(item =>
                    string.Equals(item.Name, zone.SymbolName, StringComparison.Ordinal)
                    && item.Line != zone.Line);

                if (decl is not null)
                {
                    NavigateToLine(decl.Line);
                    return;
                }
            }

            // 3b. Workspace-wide declaration scan — searches all solution files of the same
            //     extension using CodeStructureParser.  Handles cross-file navigation when
            //     no LSP is running (e.g. Ctrl+Click on a type defined in another project file).
            {
                var ext = Path.GetExtension(_currentFilePath ?? string.Empty);
                if (!string.IsNullOrEmpty(ext))
                {
                    var workspacePaths = WorkspaceFileCache.GetPathsForExtensions([ext]);
                    foreach (var path in workspacePaths)
                    {
                        if (path.Equals(_currentFilePath, StringComparison.OrdinalIgnoreCase))
                            continue;

                        var fileLines = WorkspaceFileCache.GetLines(path);
                        if (fileLines is null) continue;

                        // Wrap string[] as CodeLine list so CodeStructureParser can consume it.
                        var codeLines = fileLines
                            .Select((t, i) => new CodeLine(t, i))
                            .ToList();

                        var snap  = CodeStructureParser.Parse(codeLines);
                        var found = snap.Types.Concat(snap.Members).FirstOrDefault(item =>
                            string.Equals(item.Name, zone.SymbolName, StringComparison.Ordinal));

                        if (found is not null)
                        {
                            ReferenceNavigationRequested?.Invoke(this, new ReferencesNavigationEventArgs
                            {
                                FilePath = path,
                                Line     = found.Line + 1,
                                Column   = 1
                            });
                            return;
                        }
                    }
                }
            }

            // 4. External / no LSP fallback — symbol not found in any solution file.
            HandleExternalDefinitionAsync(zone.SymbolName);
        }

        /// <summary>
        /// Processes a list of LSP definition locations: navigates in-project targets
        /// directly; routes external/metadata targets via <see cref="GoToExternalDefinitionRequested"/>.
        /// </summary>
        private async Task HandleDefinitionLocationsAsync(
            IReadOnlyList<WpfHexEditor.Editor.Core.LSP.LspLocation> locations,
            string symbolName)
        {
            // Multiple definition locations (e.g. interface + implementation) — show popup
            // so the user can pick the target rather than silently navigating to the first.
            if (locations.Count > 1)
            {
                var groups = BuildGroupsFromLspLocations(locations, symbolName);
                ShowReferencesPopup(groups, symbolName, locations.Count,
                    source: "definition", line: _cursorLine, column: _cursorColumn);
                return;
            }

            var loc = locations[0];
            bool isMetadata = loc.Uri.StartsWith("metadata:", StringComparison.OrdinalIgnoreCase)
                           || loc.Uri.StartsWith("omnisharp-metadata:", StringComparison.OrdinalIgnoreCase);

            if (isMetadata)
            {
                // Pass the raw URI so the host can parse assembly/type from the query string.
                HandleExternalDefinitionAsync(symbolName, loc.Uri);
                return;
            }

            string? localPath = null;
            try { localPath = new Uri(loc.Uri).LocalPath; }
            catch { /* malformed URI — treat as external */ }

            if (localPath is null || !System.IO.File.Exists(localPath))
            {
                HandleExternalDefinitionAsync(symbolName, loc.Uri);
                return;
            }

            // In-project navigation.
            if (localPath.Equals(_currentFilePath, StringComparison.OrdinalIgnoreCase))
                NavigateToLine(loc.StartLine);
            else
                ReferenceNavigationRequested?.Invoke(this, new ReferencesNavigationEventArgs
                {
                    FilePath = localPath,
                    Line     = loc.StartLine + 1,
                    Column   = loc.StartColumn + 1
                });

            await Task.CompletedTask.ConfigureAwait(true);
        }

        /// <summary>
        /// Fires <see cref="GoToExternalDefinitionRequested"/> so the IDE host can route
        /// to AssemblyExplorer or open a decompiled-source tab.
        /// </summary>
        private void HandleExternalDefinitionAsync(string symbolName, string? metadataUri = null)
        {
            GoToExternalDefinitionRequested?.Invoke(this,
                new GoToExternalDefinitionEventArgs(symbolName, _currentFilePath, metadataUri));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Ctrl+Hover underline rendering
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Draws a single-pixel underline beneath the symbol token currently hovered
        /// while Ctrl is held.  Called from <see cref="OnRender"/> inside the
        /// H-scroll transform so coordinates are in document space.
        /// </summary>
        private void RenderCtrlHoverUnderline(DrawingContext dc)
        {
            if (!_ctrlDown || !_hoveredSymbolZone.HasValue) return;

            var zone = _hoveredSymbolZone.Value;

            // Lazy-create pen from theme resource; recreated each render pass so
            // theme switches are reflected immediately (same pattern as urlPen).
            var brush = TryFindResource("CE_CtrlHover_Underline") as Brush ?? Brushes.Cyan;
            var pen   = new Pen(brush, 1.0);
            pen.Freeze();

            double y        = _lineYLookup.TryGetValue(zone.Line, out double ly)
                ? ly + _lineHeight - 2
                : zone.Line * _lineHeight + _lineHeight - 2;
            double textLeft = ShowLineNumbers ? TextAreaLeftOffset : LeftMargin;
            double x1       = textLeft + zone.StartCol * _charWidth;
            double x2       = textLeft + zone.EndCol   * _charWidth;

            // Add the symbol to the hit-zone list so OnMouseMove can detect it even
            // when the async resolution hasn't completed yet.
            _symbolHitZones.Add(zone);

            dc.DrawLine(pen, new Point(x1, y), new Point(x2, y));
        }

        // -- URL hit-zone (per-render, rebuilt in RenderTextContent) ---------------

        /// <summary>
        /// Represents a single URL token position for hit-testing (cursor + Ctrl+Click).
        /// The list of active zones is rebuilt in <see cref="RenderTextContent"/> on each render.
        /// </summary>
        private readonly record struct UrlHitZone(int Line, int StartCol, int EndCol, string Url);

        // -- Symbol hit-zone (per-render when Ctrl is held) ────────────────────

        /// <summary>
        /// Identifier token position for Ctrl+hover underline and Ctrl+Click navigation.
        /// TargetFilePath/Line/Column are set by <see cref="Services.CtrlClickNavigationService"/>
        /// after async resolution. IsExternal = true requires decompilation fallback.
        /// </summary>
        private readonly record struct SymbolHitZone(
            int    Line,
            int    StartCol,
            int    EndCol,
            string SymbolName,
            string TargetFilePath,
            int    TargetLine,
            int    TargetColumn,
            bool   IsExternal);
    }

    // -- GoToExternalDefinition event args ─────────────────────────────────────

    /// <summary>
    /// Carries the symbol name and originating file path for a
    /// <see cref="CodeEditor.GoToExternalDefinitionRequested"/> event.
    /// The IDE host should route this to AssemblyExplorer or open a decompiled tab.
    /// </summary>
    public sealed class GoToExternalDefinitionEventArgs : EventArgs
    {
        /// <summary>Symbol name (type, method, property, etc.) to navigate to.</summary>
        public string SymbolName { get; }

        /// <summary>
        /// Full path of the source file that triggered the navigation request.
        /// May be null when the editor has no current file (untitled buffer).
        /// </summary>
        public string? SourceFilePath { get; }

        /// <summary>
        /// Raw LSP URI that identified this symbol as external (e.g.
        /// "omnisharp-metadata:?assembly=System.Console&amp;type=System.Console&amp;...").
        /// The IDE host can parse <c>assembly=</c> and <c>type=</c> query parameters to
        /// locate and decompile the assembly. Null when the symbol was not resolved via LSP.
        /// </summary>
        public string? MetadataUri { get; }

        internal GoToExternalDefinitionEventArgs(
            string  symbolName,
            string? sourceFilePath,
            string? metadataUri = null)
        {
            SymbolName     = symbolName;
            SourceFilePath = sourceFilePath;
            MetadataUri    = metadataUri;
        }
    }

    // -- File-scoped RelayCommand ----------------------------------------------
    file sealed class JsonRelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
        : System.Windows.Input.ICommand
    {
        public bool CanExecute(object? p)  => canExecute?.Invoke(p) ?? true;
        public void Execute(object? p)     => execute(p);
        public event EventHandler? CanExecuteChanged;
    }
}
