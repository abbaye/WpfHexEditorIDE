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
using WpfHexEditor.Core;
using WpfHexEditor.Core.Settings;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.CodeEditor.Options;
using WpfHexEditor.ProjectSystem.Languages;

namespace WpfHexEditor.Editor.CodeEditor.Controls
{
    /// <summary>
    /// High-performance JSON text editor using custom rendering (FrameworkElement).
    /// Phase 1: Basic text display + keyboard input + line numbers
    /// Phase 2: Syntax highlighting with CodeSyntaxHighlighter
    /// Future phases will add: IntelliSense, validation
    /// </summary>
    public class CodeEditor : FrameworkElement, IDocumentEditor, IDiagnosticSource, IPropertyProviderSource, IOpenableDocument, IStatusBarContributor, ISearchTarget, IEditorPersistable
    {
        #region Fields - Document Model

        private CodeDocument _document;
        private int _cursorLine = 0;        // Current cursor line (0-based)
        private int _cursorColumn = 0;      // Current cursor column (0-based)
        private TextSelection _selection;   // Current text selection

        #endregion

        #region Fields - Syntax Highlighting (Phase 2)

        private CodeSyntaxHighlighter _highlighter;

        // URL hit-zones: rebuilt on every render pass; used for cursor + Ctrl+Click.
        private readonly List<UrlHitZone> _urlHitZones = new();

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
            if (d is CodeEditor editor && e.NewValue is ISyntaxHighlighter h)
                h.Reset();
        }

        #endregion

        #region Fields - Undo/Redo (Phase 3)

        private UndoRedoStack _undoRedoStack;
        private bool _isInternalEdit = false; // Prevent undo during undo/redo operations
        private bool _isDirty = false;        // IDocumentEditor: unsaved changes flag
        private string? _currentFilePath;     // IDocumentEditor: last saved file path

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

        private static Pen MakeSquigglyPen(Color color) => MakeFrozenPen(color, 1.5);

        private static Pen MakeFrozenPen(Color color, double thickness)
        {
            var pen = new Pen(new SolidColorBrush(color), thickness);
            pen.Brush.Freeze();
            pen.Freeze();
            return pen;
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
                new FrameworkPropertyMetadata(true));

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

            // Initialize undo/redo stack (Phase 3)
            _undoRedoStack = new UndoRedoStack();

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
                Value        = 0
            };
            _hScrollBar = new System.Windows.Controls.Primitives.ScrollBar
            {
                Orientation = Orientation.Horizontal,
                SmallChange  = _charWidth * 3,
                LargeChange  = 100,
                Minimum      = 0,
                Maximum      = 0,
                Value        = 0
            };
            _vScrollBar.ValueChanged += VScrollBar_ValueChanged;
            _hScrollBar.ValueChanged += HScrollBar_ValueChanged;
            _scrollBarChildren.Add(_vScrollBar);
            _scrollBarChildren.Add(_hScrollBar);

            // Initialize folding subsystem (B3).
            _foldingEngine = new FoldingEngine(new BraceFoldingStrategy());
            _gutterControl = new GutterControl();
            _gutterControl.SetEngine(_foldingEngine);
            _scrollBarChildren.Add(_gutterControl);

            // Apply theme resource bindings when connected to the visual tree
            Loaded += (_, _) => ApplyThemeResourceBindings();
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

            ShowLineNumbers = options.ShowLineNumbers;

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

            // Scroll speed scales with how far outside the edge the mouse is.
            double delta = 0;
            if (mouseY < 0)
                delta = Math.Max(-6 * _lineHeight, mouseY / ActualHeight * 4 * _lineHeight);
            else if (mouseY > ActualHeight)
                delta = Math.Min(6 * _lineHeight, (mouseY - ActualHeight) / ActualHeight * 4 * _lineHeight);

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
                Header = "Cu_t",
                InputGestureText = "Ctrl+X",
                Command = ApplicationCommands.Cut,
                CommandTarget = this
            };
            contextMenu.Items.Add(cutMenuItem);

            // Copy
            var copyMenuItem = new MenuItem
            {
                Header = "_Copy",
                InputGestureText = "Ctrl+C",
                Command = ApplicationCommands.Copy,
                CommandTarget = this
            };
            contextMenu.Items.Add(copyMenuItem);

            // Paste
            var pasteMenuItem = new MenuItem
            {
                Header = "_Paste",
                InputGestureText = "Ctrl+V",
                Command = ApplicationCommands.Paste,
                CommandTarget = this
            };
            contextMenu.Items.Add(pasteMenuItem);

            // Separator
            contextMenu.Items.Add(new Separator());

            // Undo
            var undoMenuItem = new MenuItem
            {
                Header = "_Undo",
                InputGestureText = "Ctrl+Z",
                Command = ApplicationCommands.Undo,
                CommandTarget = this
            };
            contextMenu.Items.Add(undoMenuItem);

            // Redo
            var redoMenuItem = new MenuItem
            {
                Header = "_Redo",
                InputGestureText = "Ctrl+Y",
                Command = ApplicationCommands.Redo,
                CommandTarget = this
            };
            contextMenu.Items.Add(redoMenuItem);

            // Separator
            contextMenu.Items.Add(new Separator());

            // Select All
            var selectAllMenuItem = new MenuItem
            {
                Header = "Select _All",
                InputGestureText = "Ctrl+A",
                Command = ApplicationCommands.SelectAll,
                CommandTarget = this
            };
            contextMenu.Items.Add(selectAllMenuItem);

            // Delete
            var deleteMenuItem = new MenuItem
            {
                Header = "_Delete",
                InputGestureText = "Del",
                Command = ApplicationCommands.Delete,
                CommandTarget = this
            };
            contextMenu.Items.Add(deleteMenuItem);

            // Separator
            contextMenu.Items.Add(new Separator());

            // Find
            var findMenuItem = new MenuItem
            {
                Header = "_Find...",
                InputGestureText = "Ctrl+F",
                Command = ApplicationCommands.Find,
                CommandTarget = this
            };
            contextMenu.Items.Add(findMenuItem);

            // Replace
            var replaceMenuItem = new MenuItem
            {
                Header = "_Replace...",
                InputGestureText = "Ctrl+H",
                Command = ApplicationCommands.Replace,
                CommandTarget = this
            };
            contextMenu.Items.Add(replaceMenuItem);

            // Separator
            contextMenu.Items.Add(new Separator());

            // Format JSON
            var formatJsonMenuItem = new MenuItem
            {
                Header = "F_ormat JSON",
                InputGestureText = "Ctrl+Shift+F"
            };
            formatJsonMenuItem.Click += FormatJsonMenuItem_Click;
            contextMenu.Items.Add(formatJsonMenuItem);

            // Validate
            var validateMenuItem = new MenuItem
            {
                Header = "_Validate JSON",
                InputGestureText = "F5"
            };
            validateMenuItem.Click += ValidateMenuItem_Click;
            contextMenu.Items.Add(validateMenuItem);

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
            // Cut
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Cut,
                (sender, e) => CutToClipboard(),
                (sender, e) => e.CanExecute = !_selection.IsEmpty));

            // Copy
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy,
                (sender, e) => CopyToClipboard(),
                (sender, e) => e.CanExecute = !_selection.IsEmpty));

            // Paste
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Paste,
                (sender, e) => PasteFromClipboard(),
                (sender, e) => e.CanExecute = Clipboard.ContainsText()));

            // Undo
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Undo,
                (sender, e) => Undo(),
                (sender, e) => e.CanExecute = _undoRedoStack.CanUndo));

            // Redo
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Redo,
                (sender, e) => Redo(),
                (sender, e) => e.CanExecute = _undoRedoStack.CanRedo));

            // Select All
            CommandBindings.Add(new CommandBinding(ApplicationCommands.SelectAll,
                (sender, e) => SelectAll()));

            // Delete
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Delete,
                (sender, e) => DeleteSelection(),
                (sender, e) => e.CanExecute = !_selection.IsEmpty));

            // Find
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Find,
                (sender, e) => ShowFindDialog()));

            // Replace
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Replace,
                (sender, e) => ShowReplaceDialog()));
        }

        private void FormatJsonMenuItem_Click(object sender, RoutedEventArgs e)
        {
            FormatJson();
        }

        private void ValidateMenuItem_Click(object sender, RoutedEventArgs e)
        {
            RunValidation();
        }

        // -- Find bar -----------------------------------------------------

        private Window? _findWindow;
        private System.Windows.Controls.TextBox? _findTextBox;
        private string? _lastFindQuery;

        /// <summary>
        /// Shows the modeless find bar (public entry point for host).
        /// </summary>
        public void ShowFindBar()
        {
            if (_findWindow?.IsVisible == true)
            {
                _findWindow.Activate();
                _findTextBox?.SelectAll();
                _findTextBox?.Focus();
                return;
            }
            _findWindow = BuildFindWindow(replace: false);
            _findWindow.Show();
            _findTextBox?.Focus();
        }

        private void ShowFindDialog() => ShowFindBar();

        private void ShowReplaceDialog()
        {
            if (_findWindow?.IsVisible == true)
            {
                _findWindow.Close();
            }
            _findWindow = BuildFindWindow(replace: true);
            _findWindow.Show();
            _findTextBox?.Focus();
        }

        private Window BuildFindWindow(bool replace)
        {
            var tb = new System.Windows.Controls.TextBox
            {
                Width = 200,
                Margin = new Thickness(4),
                VerticalAlignment = VerticalAlignment.Center,
                Text = _lastFindQuery ?? string.Empty
            };
            _findTextBox = tb;

            var btnNext  = new System.Windows.Controls.Button { Content = "▼", Width = 28, Height = 28, Margin = new Thickness(2, 4, 0, 4), ToolTip = "Find Next (Enter)" };
            var btnPrev  = new System.Windows.Controls.Button { Content = "▲", Width = 28, Height = 28, Margin = new Thickness(2, 4, 0, 4), ToolTip = "Find Previous (Shift+Enter)" };
            var btnClose = new System.Windows.Controls.Button { Content = "✕", Width = 28, Height = 28, Margin = new Thickness(2, 4, 4, 4), ToolTip = "Close (Esc)" };

            System.Windows.Controls.TextBox? tbReplace = null;
            System.Windows.Controls.Button? btnReplace = null;

            var panel = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(4) };
            var row1  = new StackPanel { Orientation = Orientation.Horizontal };
            row1.Children.Add(new System.Windows.Controls.TextBlock { Text = "Find:", Width = 52, VerticalAlignment = VerticalAlignment.Center });
            row1.Children.Add(tb);
            row1.Children.Add(btnNext);
            row1.Children.Add(btnPrev);
            row1.Children.Add(btnClose);
            panel.Children.Add(row1);

            if (replace)
            {
                tbReplace   = new System.Windows.Controls.TextBox { Width = 200, Margin = new Thickness(4), VerticalAlignment = VerticalAlignment.Center };
                btnReplace  = new System.Windows.Controls.Button { Content = "Replace", Margin = new Thickness(2, 4, 0, 4), Padding = new Thickness(6, 2, 6, 2) };
                var row2 = new StackPanel { Orientation = Orientation.Horizontal };
                row2.Children.Add(new System.Windows.Controls.TextBlock { Text = "Replace:", Width = 52, VerticalAlignment = VerticalAlignment.Center });
                row2.Children.Add(tbReplace);
                row2.Children.Add(btnReplace);
                panel.Children.Add(row2);
            }

            var parentWindow = Window.GetWindow(this);
            var win = new Window
            {
                Title       = replace ? "Find & Replace" : "Find",
                Content     = panel,
                SizeToContent = SizeToContent.WidthAndHeight,
                ResizeMode  = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow,
                ShowInTaskbar = false,
                Owner       = parentWindow,
            };

            if (parentWindow != null)
            {
                win.Left = parentWindow.Left + (parentWindow.Width - 380) / 2;
                win.Top  = parentWindow.Top  + 80;
            }

            // Live search as user types
            tb.TextChanged += (s, e) =>
            {
                _lastFindQuery = tb.Text;
                ExecuteFind(_lastFindQuery);
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
            };

            tb.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) != 0)
                    { FindPrevious(); e.Handled = true; }
                else if (e.Key == Key.Enter)
                    { FindNext(); e.Handled = true; }
                else if (e.Key == Key.Escape)
                    { ClearFind(); win.Close(); Focus(); e.Handled = true; }
            };

            btnNext.Click  += (s, e) => FindNext();
            btnPrev.Click  += (s, e) => FindPrevious();
            btnClose.Click += (s, e) => { ClearFind(); win.Close(); Focus(); };

            if (btnReplace != null && tbReplace != null)
            {
                btnReplace.Click += (s, e) =>
                {
                    if (_currentFindMatchIndex < 0 || _currentFindMatchIndex >= _findResults.Count)
                        return;
                    var match = _findResults[_currentFindMatchIndex];
                    _selection.Start = match;
                    _selection.End   = new TextPosition(match.Line, match.Column + _findMatchLength);
                    DeleteSelection();
                    foreach (var ch in tbReplace.Text) InsertChar(ch);
                    ExecuteFind(_lastFindQuery ?? string.Empty);
                    FindNext();
                };
            }

            win.Closed += (s, e) => { _findWindow = null; _findTextBox = null; };
            return win;
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

        SearchBarCapabilities ISearchTarget.Capabilities => SearchBarCapabilities.CaseSensitive;

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
        }

        #endregion

        #region Rendering - OnRender Override

        /// <summary>
        /// Main rendering method - draws all visual elements
        /// Called by WPF when visual update is needed
        /// </summary>
        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            if (_document == null || _document.Lines.Count == 0)
                return;

            bool hasVBar = _vScrollBar?.Visibility == Visibility.Visible;
            bool hasHBar = _hScrollBar?.Visibility == Visibility.Visible;
            double contentW = ActualWidth  - (hasVBar ? ScrollBarThickness : 0);
            double contentH = ActualHeight - (hasHBar ? ScrollBarThickness : 0);
            double textLeft = ShowLineNumbers ? TextAreaLeftOffset : LeftMargin;

            // Calculate visible line range
            CalculateVisibleLines();

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
            dc.PushTransform(new System.Windows.Media.TranslateTransform(-_horizontalScrollOffset, 0));

            // 4. Find result highlights
            RenderFindResults(dc);

            // 5. Selection
            RenderSelection(dc);

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
            double textLeft = ShowLineNumbers ? TextAreaLeftOffset : LeftMargin;
            double totalH  = TopMargin + (_document?.Lines.Count ?? 0) * _lineHeight;
            double totalTW = textLeft + _maxContentWidth;

            // Determine which scrollbars are needed (check for mutual dependency)
            bool needsV = totalH  > finalSize.Height;
            bool needsH = totalTW > finalSize.Width;
            if (needsV) needsH = totalTW > (finalSize.Width  - ScrollBarThickness);
            if (needsH) needsV = totalH  > (finalSize.Height - ScrollBarThickness);

            double contentW = needsV ? finalSize.Width  - ScrollBarThickness : finalSize.Width;
            double contentH = needsH ? finalSize.Height - ScrollBarThickness : finalSize.Height;

            _vScrollBar.Visibility = needsV ? Visibility.Visible : Visibility.Hidden;
            _hScrollBar.Visibility = needsH ? Visibility.Visible : Visibility.Hidden;

            _vScrollBar.Arrange(needsV ? new Rect(contentW, 0, ScrollBarThickness, contentH) : new Rect(0, 0, 0, 0));
            _hScrollBar.Arrange(needsH ? new Rect(0, contentH, contentW, ScrollBarThickness) : new Rect(0, 0, 0, 0));

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
                double totalH = TopMargin + (_document?.Lines.Count ?? 0) * _lineHeight;
                double maxV   = Math.Max(0, totalH - contentH);

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

            // Sync gutter layout with the newly computed visible range.
            _gutterControl?.Update(_lineHeight, _firstVisibleLine, _lastVisibleLine, TopMargin);
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

            for (int i = _firstVisibleLine; i <= _lastVisibleLine && i < _document.Lines.Count; i++)
            {
                // Phase 11: Calculate Y position with virtual scrolling support
                double y = EnableVirtualScrolling && _virtualizationEngine != null
                    ? TopMargin + _virtualizationEngine.GetLineYPosition(i)
                    : TopMargin + ((i - _firstVisibleLine) * _lineHeight);

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

            // Phase 11: Calculate Y position with virtual scrolling support
            double y = EnableVirtualScrolling && _virtualizationEngine != null
                ? TopMargin + _virtualizationEngine.GetLineYPosition(_cursorLine)
                : TopMargin + (_cursorLine - _firstVisibleLine) * _lineHeight;

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
                    // Phase 11: Calculate Y position with virtual scrolling support
                    double y = EnableVirtualScrolling && _virtualizationEngine != null
                        ? TopMargin + _virtualizationEngine.GetLineYPosition(start.Line)
                        : TopMargin + (start.Line - _firstVisibleLine) * _lineHeight;

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
                    double y  = TopMargin + (EnableVirtualScrolling && _virtualizationEngine != null
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
                    double y     = TopMargin + (EnableVirtualScrolling && _virtualizationEngine != null
                        ? _virtualizationEngine.GetLineYPosition(line)
                        : (line - _firstVisibleLine) * _lineHeight) - SelectionCornerRadius;
                    double width = _document.Lines[line].Length * _charWidth;
                    segments.Add(new RectangleGeometry(new Rect(leftEdge, y, Math.Max(width, _charWidth), _lineHeight + SelectionCornerRadius * 2), SelectionCornerRadius, SelectionCornerRadius));
                }

                // Last line — extend top by CornerRadius so the rounded head merges with previous segment
                if (end.Line >= _firstVisibleLine && end.Line <= _lastVisibleLine)
                {
                    double y  = TopMargin + (EnableVirtualScrolling && _virtualizationEngine != null
                        ? _virtualizationEngine.GetLineYPosition(end.Line)
                        : (end.Line - _firstVisibleLine) * _lineHeight) - SelectionCornerRadius;
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

                // Calculate Y position with virtual scrolling support
                double y = EnableVirtualScrolling && _virtualizationEngine != null
                    ? TopMargin + _virtualizationEngine.GetLineYPosition(result.Line)
                    : TopMargin + (result.Line - _firstVisibleLine) * _lineHeight;

                double x1 = leftEdge + (result.Column * _charWidth);
                double x2 = leftEdge + ((result.Column + _findMatchLength) * _charWidth);

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
        /// Render text content with syntax highlighting (Phase 2)
        /// </summary>
        private void RenderTextContent(DrawingContext dc)
        {
            double x = ShowLineNumbers ? TextAreaLeftOffset : LeftMargin;

            var context = new JsonParserContext();

            // Rebuild URL hit-zones each render pass (document may have changed).
            _urlHitZones.Clear();

            // Lazy-init the underline pen from the current SyntaxUrlColor DP.
            // Re-created each render so theme changes are reflected immediately.
            var urlPen = new Pen(SyntaxUrlColor, 1.0);
            urlPen.Freeze();

            // Reset external highlighter state before a full render pass.
            ExternalHighlighter?.Reset();

            for (int i = _firstVisibleLine; i <= _lastVisibleLine && i < _document.Lines.Count; i++)
            {
                // Phase 11: Calculate Y position with virtual scrolling support
                double y = EnableVirtualScrolling && _virtualizationEngine != null
                    ? TopMargin + _virtualizationEngine.GetLineYPosition(i)
                    : TopMargin + ((i - _firstVisibleLine) * _lineHeight);

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

                        continue; // skip slow path
                    }
                    // ── end fast path ─────────────────────────────────────────────────

                    // Use external (language-pluggable) highlighter when available,
                    // otherwise fall back to the built-in JSON highlighter.
                    bool hasExternalHighlighter = ExternalHighlighter is not null;
                    IEnumerable<Helpers.SyntaxHighlightToken> rawTokens;

                    if (ExternalHighlighter is { } ext)
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
                        double tokenX = x + (token.StartColumn * _charWidth);

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

                        line.GlyphRunCache     = _glyphRenderer.BuildLineGlyphRuns(allCacheTokens, SyntaxUrlColor);
                        line.IsGlyphCacheDirty = false;

                        // Cache URL zones for GlyphRun-hit renders (no re-run of OverlayUrlTokens).
                        line.CachedUrlZones = _urlHitZones
                            .Where(z => z.Line == i)
                            .Select(z => (z.StartCol, z.EndCol, z.Url))
                            .ToList();
                    }
                    // ── end cache build ───────────────────────────────────────────────
                }
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

            // Phase 11: Calculate Y position with virtual scrolling support
            double y = EnableVirtualScrolling && _virtualizationEngine != null
                ? TopMargin + _virtualizationEngine.GetLineYPosition(_cursorLine)
                : TopMargin + (_cursorLine - _firstVisibleLine) * _lineHeight;

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

            double y = TopMargin + (EnableVirtualScrolling && _virtualizationEngine != null
                ? _virtualizationEngine.GetLineYPosition(line)
                : (line - _firstVisibleLine) * _lineHeight);
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

            switch (e.Key)
            {
                case Key.Left:
                    MoveCursor(-1, 0, shiftPressed);
                    e.Handled = true;
                    break;

                case Key.Right:
                    MoveCursor(1, 0, shiftPressed);
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
                    MoveCursorToLineStart(shiftPressed);
                    e.Handled = true;
                    break;

                case Key.End:
                    MoveCursorToLineEnd(shiftPressed);
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

                // Undo/Redo (Phase 3)
                case Key.Z:
                    if (ctrlPressed)
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
                case Key.M:
                    if (ctrlPressed && IsFoldingEnabled && _foldingEngine != null)
                    {
                        _foldingEngine.ToggleRegion(_cursorLine);
                        InvalidateVisual();
                        e.Handled = true;
                    }
                    break;
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
                InvalidateVisual();
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
                    return;
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
            }

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
                // Start selection
                _isSelecting = true;
                _mouseDownPosition = textPos;
                _selection.Start = textPos;
                _selection.End = textPos;
                CaptureMouse();
            }

            InvalidateVisual();
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

                if (urlZone.HasValue)
                {
                    Cursor = Cursors.Hand;
                    ShowUrlTooltip();
                }
                else
                {
                    Cursor = Cursors.IBeam;
                    HideUrlTooltip();
                }
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

            // Calculate line
            int line = _firstVisibleLine + (int)((pixel.Y - TopMargin) / _lineHeight);
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

        private void PasteFromClipboard()
        {
            try
            {
                if (!Clipboard.ContainsText())
                    return;

                string text = Clipboard.GetText();

                // Delete selection first if any
                if (!_selection.IsEmpty)
                {
                    DeleteSelection();
                }

                // Insert text at cursor
                _document.InsertText(new TextPosition(_cursorLine, _cursorColumn), text);

                // Move cursor to end of inserted text
                var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                if (lines.Length == 1)
                {
                    _cursorColumn += text.Length;
                }
                else
                {
                    _cursorLine += lines.Length - 1;
                    _cursorColumn = lines[lines.Length - 1].Length;
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
            if (_selection.IsEmpty)
                return;

            CopyToClipboard();
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

        #endregion

        #region Undo/Redo Operations (Phase 3)

        public void Undo()
        {
            if (!_undoRedoStack.CanUndo)
                return;

            _isInternalEdit = true;

            try
            {
                var edit = _undoRedoStack.Undo();
                if (edit != null)
                {
                    ApplyInverseEdit(edit);
                    InvalidateVisual();
                }
            }
            finally
            {
                _isInternalEdit = false;
            }

            CanUndoChanged?.Invoke(this, EventArgs.Empty);
            CanRedoChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Redo()
        {
            if (!_undoRedoStack.CanRedo)
                return;

            _isInternalEdit = true;

            try
            {
                var edit = _undoRedoStack.Redo();
                if (edit != null)
                {
                    ApplyEdit(edit);
                    InvalidateVisual();
                }
            }
            finally
            {
                _isInternalEdit = false;
            }

            CanUndoChanged?.Invoke(this, EventArgs.Empty);
            CanRedoChanged?.Invoke(this, EventArgs.Empty);
        }

        private void ApplyEdit(TextEdit edit)
        {
            switch (edit.Type)
            {
                case TextEditType.Insert:
                    _document.InsertText(edit.Position, edit.Text);
                    _cursorLine = edit.Position.Line;
                    _cursorColumn = edit.Position.Column + edit.Text.Length;
                    break;

                case TextEditType.Delete:
                    var endPos = new TextPosition(edit.Position.Line, edit.Position.Column + edit.Text.Length);
                    _document.DeleteRange(edit.Position, endPos);
                    _cursorLine = edit.Position.Line;
                    _cursorColumn = edit.Position.Column;
                    break;

                case TextEditType.Replace:
                    // Replace is handled as delete + insert in document model
                    break;
            }
        }

        private void ApplyInverseEdit(TextEdit edit)
        {
            var inverse = edit.CreateInverse();
            if (inverse != null)
            {
                ApplyEdit(inverse);
            }
        }

        #endregion

        #region Document Event Handlers

        private void Document_TextChanged(object sender, Models.TextChangedEventArgs e)
        {
            // Phase 3: Add to undo/redo stack (unless this is an undo/redo operation itself)
            if (!_isInternalEdit)
            {
                // Convert TextChangeType to TextEditType (same enum values, different types)
                var editType = (TextEditType)((int)e.ChangeType);
                var textEdit = new TextEdit(editType, e.Position, e.Text);
                _undoRedoStack.Push(textEdit);

                if (!_isDirty)
                {
                    _isDirty = true;
                    ModifiedChanged?.Invoke(this, EventArgs.Empty);
                    TitleChanged?.Invoke(this, BuildTitle());
                }
                CanUndoChanged?.Invoke(this, EventArgs.Empty);
                CanRedoChanged?.Invoke(this, EventArgs.Empty);
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
            int changedLine = e.Position.Line;
            if (changedLine >= 0 && changedLine < _document.Lines.Count)
            {
                int newLen = _document.Lines[changedLine].Text.Length;
                if (newLen > _cachedMaxLineLength)
                    _cachedMaxLineLength = newLen;
                else if (newLen < _cachedMaxLineLength)
                    _cachedMaxLineLength = _document.Lines.Count > 0
                        ? _document.Lines.Max(l => l.Text.Length) : 0;
            }

            // Invalidate line-number cache for the changed line (P1-CE-03)
            _lineNumberCache.Remove(changedLine);

            // Reset highlight tracking so the next render re-schedules the pipeline (content changed).
            _lastHighlightFirst = -1;
            _lastHighlightLast  = -1;

            // InvalidateMeasure triggers full layout pass → UpdateScrollBars (ranges may have changed)
            InvalidateMeasure();
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
            // Sync TotalLines so the virtualization engine reflects the newly loaded document.
            // Without this call, the engine keeps the initial count (27 lines from the default
            // template) and clamps LastVisibleLine prematurely for any larger file.
            UpdateVirtualization();
            RebuildMaxLineLength(); // single O(n) scan at load time

            // Initial folding analysis on freshly loaded content.
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
        public bool CanUndo => _undoRedoStack.CanUndo;

        /// <summary>
        /// Check if can redo
        /// </summary>
        public bool CanRedo => _undoRedoStack.CanRedo;

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

            _document = new Models.CodeDocument();
            _currentFilePath = null;
            _isDirty = false;
            _cursorLine = 0;
            _cursorColumn = 0;
            _selection.Clear();
            _undoRedoStack.Clear();
            _validationErrors.Clear();
            InvalidateVisual();
            ModifiedChanged?.Invoke(this, EventArgs.Empty);
            TitleChanged?.Invoke(this, BuildTitle());
            DiagnosticsChanged?.Invoke(this, EventArgs.Empty);
        }

        // -- Events --------------------------------------------------------

        public event EventHandler? ModifiedChanged;
        public event EventHandler? CanUndoChanged;
        public event EventHandler? CanRedoChanged;
        public event EventHandler<string>? TitleChanged;
        public event EventHandler<string>? StatusMessage;
        public event EventHandler<string>? OutputMessage;
        public event EventHandler? SelectionChanged;

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
        private StatusBarItem _sbJsonFile = null!;

        public ObservableCollection<StatusBarItem> StatusBarItems
            => _jsonStatusBarItems ??= BuildJsonStatusBarItems();

        private ObservableCollection<StatusBarItem> BuildJsonStatusBarItems()
        {
            _sbJsonFile = new StatusBarItem { Label = "File", Tooltip = "Current JSON file" };
            RefreshJsonStatusBarItems();
            return new ObservableCollection<StatusBarItem> { _sbJsonFile };
        }

        void IStatusBarContributor.RefreshStatusBarItems() => RefreshJsonStatusBarItems();

        internal void RefreshJsonStatusBarItems()
        {
            if (_jsonStatusBarItems == null) return;
            _sbJsonFile.Value = !string.IsNullOrEmpty(_currentFilePath)
                ? Path.GetFileName(_currentFilePath)
                : "(unsaved)";
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

        // -- URL hit-zone (per-render, rebuilt in RenderTextContent) ---------------

        /// <summary>
        /// Represents a single URL token position for hit-testing (cursor + Ctrl+Click).
        /// The list of active zones is rebuilt in <see cref="RenderTextContent"/> on each render.
        /// </summary>
        private readonly record struct UrlHitZone(int Line, int StartCol, int EndCol, string Url);
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
