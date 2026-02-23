//////////////////////////////////////////////
// Apache 2.0  - 2026
// Custom JsonEditor - Main Editor Control (Phase 1 - Foundation)
// Author : Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com)
// Inspired by HexViewport.cs custom rendering pattern
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexaEditor.Models.JsonEditor;
using WpfHexaEditor.Helpers.JsonEditor;
using WpfHexaEditor.Services;

namespace WpfHexaEditor.Controls.JsonEditor
{
    /// <summary>
    /// High-performance JSON text editor using custom rendering (FrameworkElement).
    /// Phase 1: Basic text display + keyboard input + line numbers
    /// Phase 2: Syntax highlighting with JsonSyntaxHighlighter
    /// Future phases will add: IntelliSense, validation
    /// </summary>
    public class JsonEditor : FrameworkElement
    {
        #region Fields - Document Model

        private JsonDocument _document;
        private int _cursorLine = 0;        // Current cursor line (0-based)
        private int _cursorColumn = 0;      // Current cursor column (0-based)
        private TextSelection _selection;   // Current text selection

        #endregion

        #region Fields - Syntax Highlighting (Phase 2)

        private JsonSyntaxHighlighter _highlighter;

        #endregion

        #region Fields - Undo/Redo (Phase 3)

        private UndoRedoStack _undoRedoStack;
        private bool _isInternalEdit = false; // Prevent undo during undo/redo operations

        #endregion

        #region Fields - Mouse Selection (Phase 3)

        private bool _isSelecting = false;
        private TextPosition _mouseDownPosition;

        #endregion

        #region Fields - IntelliSense (Phase 4)

        private IntelliSensePopup _intelliSensePopup;
        private bool _enableIntelliSense = true;

        #endregion

        #region Fields - Validation (Phase 5)

        private List<ValidationError> _validationErrors = new List<ValidationError>();
        private FormatSchemaValidator _validator;
        private System.Windows.Threading.DispatcherTimer _validationTimer;
        private bool _enableValidation = true;

        #endregion

        #region Fields - Rendering State

        private Typeface _typeface;
        private Typeface _boldTypeface;
        private double _fontSize = 12.0;
        private double _charWidth;          // Cached character width
        private double _charHeight;         // Cached character height
        private double _lineHeight;         // Line height with padding
        private int _firstVisibleLine = 0;  // Scrolling support (Phase 1: always 0)
        private int _lastVisibleLine = 0;   // Will be calculated in Phase 1

        #endregion

        #region Fields - Layout Constants

        private const double TopMargin = 2;
        private const double LeftMargin = 5;
        private const double LineNumberWidth = 60;
        private const double LineNumberMargin = 5;
        private const double TextAreaLeftOffset = 70; // LineNumberWidth + margin

        #endregion

        #region Fields - Colors (Brushes)

        private Brush _editorBackground = Brushes.White;
        private Brush _editorForeground = Brushes.Black;
        private Brush _lineNumberBackground;
        private Brush _lineNumberForeground;
        private Brush _currentLineBackground;
        private Brush _selectionBackground;

        #endregion

        #region Dependency Properties with [Category] Attributes

        // Properties are organized by category for auto-generated settings panel
        // Uses same pattern as HexEditor with DynamicSettingsGenerator

        public static readonly DependencyProperty ShowLineNumbersProperty =
            DependencyProperty.Register(nameof(ShowLineNumbers), typeof(bool), typeof(JsonEditor),
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

        public static readonly DependencyProperty EnableIntelliSenseProperty =
            DependencyProperty.Register(nameof(EnableIntelliSense), typeof(bool), typeof(JsonEditor),
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
            DependencyProperty.Register(nameof(EnableValidation), typeof(bool), typeof(JsonEditor),
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
                _enableValidation = value;
                if (value)
                    TriggerValidation();
                else
                    _validationErrors.Clear();
            }
        }

        // ===== APPEARANCE - FONTS =====

        public static readonly DependencyProperty EditorFontFamilyProperty =
            DependencyProperty.Register(nameof(EditorFontFamily), typeof(FontFamily), typeof(JsonEditor),
                new FrameworkPropertyMetadata(new FontFamily("Consolas"), FrameworkPropertyMetadataOptions.AffectsRender,
                    OnFontChanged));

        [Category("Appearance - Fonts")]
        [DisplayName("Editor Font Family")]
        [Description("Font family for editor text (monospace recommended)")]
        public FontFamily EditorFontFamily
        {
            get => (FontFamily)GetValue(EditorFontFamilyProperty);
            set => SetValue(EditorFontFamilyProperty, value);
        }

        public static readonly DependencyProperty EditorFontSizeProperty =
            DependencyProperty.Register(nameof(EditorFontSize), typeof(double), typeof(JsonEditor),
                new FrameworkPropertyMetadata(12.0, FrameworkPropertyMetadataOptions.AffectsRender,
                    OnFontChanged));

        [Category("Appearance - Fonts")]
        [DisplayName("Editor Font Size")]
        [Description("Font size for editor text (points)")]
        public double EditorFontSize
        {
            get => (double)GetValue(EditorFontSizeProperty);
            set => SetValue(EditorFontSizeProperty, value);
        }

        public static readonly DependencyProperty LineNumberFontSizeProperty =
            DependencyProperty.Register(nameof(LineNumberFontSize), typeof(double), typeof(JsonEditor),
                new FrameworkPropertyMetadata(10.0, FrameworkPropertyMetadataOptions.AffectsRender));

        [Category("Appearance - Fonts")]
        [DisplayName("Line Number Font Size")]
        [Description("Font size for line numbers (points)")]
        public double LineNumberFontSize
        {
            get => (double)GetValue(LineNumberFontSizeProperty);
            set => SetValue(LineNumberFontSizeProperty, value);
        }

        // ===== APPEARANCE - COLORS =====

        public static readonly DependencyProperty EditorBackgroundProperty =
            DependencyProperty.Register(nameof(EditorBackground), typeof(Brush), typeof(JsonEditor),
                new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

        [Category("Appearance - Colors")]
        [DisplayName("Editor Background")]
        [Description("Background color of the editor")]
        public Brush EditorBackground
        {
            get => (Brush)GetValue(EditorBackgroundProperty);
            set => SetValue(EditorBackgroundProperty, value);
        }

        public static readonly DependencyProperty EditorForegroundProperty =
            DependencyProperty.Register(nameof(EditorForeground), typeof(Brush), typeof(JsonEditor),
                new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender));

        [Category("Appearance - Colors")]
        [DisplayName("Editor Foreground")]
        [Description("Default text color")]
        public Brush EditorForeground
        {
            get => (Brush)GetValue(EditorForegroundProperty);
            set => SetValue(EditorForegroundProperty, value);
        }

        public static readonly DependencyProperty LineNumberBackgroundProperty =
            DependencyProperty.Register(nameof(LineNumberBackground), typeof(Brush), typeof(JsonEditor),
                new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                    FrameworkPropertyMetadataOptions.AffectsRender));

        [Category("Appearance - Colors")]
        [DisplayName("Line Number Background")]
        [Description("Background color of line number gutter")]
        public Brush LineNumberBackground
        {
            get => (Brush)GetValue(LineNumberBackgroundProperty);
            set => SetValue(LineNumberBackgroundProperty, value);
        }

        public static readonly DependencyProperty LineNumberForegroundProperty =
            DependencyProperty.Register(nameof(LineNumberForeground), typeof(Brush), typeof(JsonEditor),
                new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(128, 128, 128)),
                    FrameworkPropertyMetadataOptions.AffectsRender));

        [Category("Appearance - Colors")]
        [DisplayName("Line Number Foreground")]
        [Description("Text color of line numbers")]
        public Brush LineNumberForeground
        {
            get => (Brush)GetValue(LineNumberForegroundProperty);
            set => SetValue(LineNumberForegroundProperty, value);
        }

        public static readonly DependencyProperty CurrentLineBackgroundProperty =
            DependencyProperty.Register(nameof(CurrentLineBackground), typeof(Brush), typeof(JsonEditor),
                new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromArgb(30, 0, 120, 215)),
                    FrameworkPropertyMetadataOptions.AffectsRender));

        [Category("Appearance - Colors")]
        [DisplayName("Current Line Background")]
        [Description("Highlight color for current line")]
        public Brush CurrentLineBackground
        {
            get => (Brush)GetValue(CurrentLineBackgroundProperty);
            set => SetValue(CurrentLineBackgroundProperty, value);
        }

        public static readonly DependencyProperty SelectionBackgroundProperty =
            DependencyProperty.Register(nameof(SelectionBackground), typeof(Brush), typeof(JsonEditor),
                new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(173, 214, 255)),
                    FrameworkPropertyMetadataOptions.AffectsRender));

        [Category("Appearance - Colors")]
        [DisplayName("Selection Background")]
        [Description("Background color for selected text")]
        public Brush SelectionBackground
        {
            get => (Brush)GetValue(SelectionBackgroundProperty);
            set => SetValue(SelectionBackgroundProperty, value);
        }

        // ===== BEHAVIOR =====

        public static readonly DependencyProperty IndentSizeProperty =
            DependencyProperty.Register(nameof(IndentSize), typeof(int), typeof(JsonEditor),
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
            DependencyProperty.Register(nameof(IntelliSenseDelay), typeof(int), typeof(JsonEditor),
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
            DependencyProperty.Register(nameof(ValidationDelay), typeof(int), typeof(JsonEditor),
                new FrameworkPropertyMetadata(1000));

        [Category("Behavior")]
        [DisplayName("Validation Delay (ms)")]
        [Description("Delay before running validation after text change (milliseconds)")]
        public int ValidationDelay
        {
            get => (int)GetValue(ValidationDelayProperty);
            set => SetValue(ValidationDelayProperty, value);
        }

        // ===== SYNTAX HIGHLIGHTING COLORS =====

        public static readonly DependencyProperty SyntaxBraceColorProperty =
            DependencyProperty.Register(nameof(SyntaxBraceColor), typeof(Brush), typeof(JsonEditor),
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
            DependencyProperty.Register(nameof(SyntaxBracketColor), typeof(Brush), typeof(JsonEditor),
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
            DependencyProperty.Register(nameof(SyntaxKeyColor), typeof(Brush), typeof(JsonEditor),
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
            DependencyProperty.Register(nameof(SyntaxStringValueColor), typeof(Brush), typeof(JsonEditor),
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
            DependencyProperty.Register(nameof(SyntaxNumberColor), typeof(Brush), typeof(JsonEditor),
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
            DependencyProperty.Register(nameof(SyntaxBooleanColor), typeof(Brush), typeof(JsonEditor),
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
            DependencyProperty.Register(nameof(SyntaxNullColor), typeof(Brush), typeof(JsonEditor),
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
            DependencyProperty.Register(nameof(SyntaxCommentColor), typeof(Brush), typeof(JsonEditor),
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
            DependencyProperty.Register(nameof(SyntaxKeywordColor), typeof(Brush), typeof(JsonEditor),
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
            DependencyProperty.Register(nameof(SyntaxValueTypeColor), typeof(Brush), typeof(JsonEditor),
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
            DependencyProperty.Register(nameof(SyntaxCalcExpressionColor), typeof(Brush), typeof(JsonEditor),
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
            DependencyProperty.Register(nameof(SyntaxVariableReferenceColor), typeof(Brush), typeof(JsonEditor),
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
            DependencyProperty.Register(nameof(SyntaxErrorColor), typeof(Brush), typeof(JsonEditor),
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

        #endregion

        #region Property Changed Callbacks

        private static void OnFontChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is JsonEditor editor)
            {
                // Update typefaces
                var fontFamily = editor.EditorFontFamily;
                editor._typeface = new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
                editor._boldTypeface = new Typeface(fontFamily, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
                editor._fontSize = editor.EditorFontSize;

                // Recalculate character dimensions
                editor.CalculateCharacterDimensions();
                editor.InvalidateVisual();
            }
        }

        private static void OnIndentSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is JsonEditor editor && editor._document != null)
            {
                editor._document.IndentSize = (int)e.NewValue;
            }
        }

        private static void OnSyntaxColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is JsonEditor editor && editor._highlighter != null)
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

        #endregion

        #region Constructor

        public JsonEditor()
        {
            // Initialize document
            _document = new JsonDocument();
            _selection = new TextSelection();

            // Subscribe to document changes
            _document.TextChanged += Document_TextChanged;

            // Initialize typefaces (will use DP values)
            UpdateTypefacesFromDPs();

            // Calculate character dimensions
            CalculateCharacterDimensions();

            // Initialize syntax highlighter (Phase 2)
            _highlighter = new JsonSyntaxHighlighter();
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

            // Make focusable for keyboard input
            Focusable = true;
            FocusVisualStyle = null; // No focus rectangle

            // Set minimum size
            MinWidth = 200;
            MinHeight = 100;
        }

        private void UpdateTypefacesFromDPs()
        {
            _typeface = new Typeface(EditorFontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            _boldTypeface = new Typeface(EditorFontFamily, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
            _fontSize = EditorFontSize;
        }

        private void UpdateSyntaxHighlighterColors()
        {
            if (_highlighter == null) return;

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
        }

        #endregion

        #region Character Dimension Calculation

        /// <summary>
        /// Calculate character dimensions using FormattedText
        /// Same pattern as HexViewport
        /// </summary>
        private void CalculateCharacterDimensions()
        {
            var formattedText = new FormattedText(
                "M", // Use 'M' as reference (monospace width)
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                _typeface,
                _fontSize,
                Brushes.Black,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            _charWidth = formattedText.Width;
            _charHeight = formattedText.Height;
            _lineHeight = _charHeight + 4; // Add 4px padding
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

            // Calculate visible line range (Phase 1: simple - show all lines up to viewport height)
            CalculateVisibleLines();

            // 1. Draw editor background
            dc.DrawRectangle(EditorBackground, null, new Rect(0, 0, ActualWidth, ActualHeight));

            // 2. Draw line number gutter background
            if (ShowLineNumbers)
            {
                dc.DrawRectangle(LineNumberBackground, null, new Rect(0, 0, LineNumberWidth, ActualHeight));
            }

            // 3. Draw current line highlight (before text)
            RenderCurrentLineHighlight(dc);

            // 4. Draw selection (before text)
            RenderSelection(dc);

            // 5. Draw line numbers
            if (ShowLineNumbers)
            {
                RenderLineNumbers(dc);
            }

            // 6. Draw text content
            RenderTextContent(dc);

            // 7. Draw validation errors (Phase 5 - squiggly lines)
            if (_enableValidation)
            {
                RenderValidationErrors(dc);
            }

            // 8. Draw bracket matching (Phase 6)
            RenderBracketMatching(dc);

            // 9. Draw cursor (blinking will be added later)
            RenderCursor(dc);
        }

        /// <summary>
        /// Calculate which lines are visible in the viewport
        /// Phase 1: Simple calculation (no virtual scrolling yet)
        /// </summary>
        private void CalculateVisibleLines()
        {
            _firstVisibleLine = 0; // Phase 1: always start at 0
            _lastVisibleLine = Math.Min(_document.Lines.Count - 1,
                (int)((ActualHeight - TopMargin) / _lineHeight));
        }

        /// <summary>
        /// Render line numbers in left gutter
        /// </summary>
        private void RenderLineNumbers(DrawingContext dc)
        {
            double y = TopMargin;

            for (int i = _firstVisibleLine; i <= _lastVisibleLine && i < _document.Lines.Count; i++)
            {
                var lineNumber = (i + 1).ToString(); // Display as 1-based
                var formattedText = new FormattedText(
                    lineNumber,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    _typeface,
                    LineNumberFontSize,
                    LineNumberForeground,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);

                // Right-align line numbers
                double x = LineNumberWidth - formattedText.Width - LineNumberMargin;

                dc.DrawText(formattedText, new Point(x, y));
                y += _lineHeight;
            }

            // Draw separator line between line numbers and text
            var pen = new Pen(new SolidColorBrush(Color.FromRgb(200, 200, 200)), 1);
            pen.Freeze();
            dc.DrawLine(pen, new Point(LineNumberWidth, 0), new Point(LineNumberWidth, ActualHeight));
        }

        /// <summary>
        /// Render current line highlight
        /// </summary>
        private void RenderCurrentLineHighlight(DrawingContext dc)
        {
            if (_cursorLine < _firstVisibleLine || _cursorLine > _lastVisibleLine)
                return;

            double y = TopMargin + (_cursorLine - _firstVisibleLine) * _lineHeight;
            double x = ShowLineNumbers ? TextAreaLeftOffset : LeftMargin;

            dc.DrawRectangle(CurrentLineBackground, null,
                new Rect(x, y, ActualWidth - x, _lineHeight));
        }

        /// <summary>
        /// Render text selection overlay (Phase 3 - Enhanced with multi-line support)
        /// </summary>
        private void RenderSelection(DrawingContext dc)
        {
            if (_selection.IsEmpty)
                return;

            var start = _selection.NormalizedStart;
            var end = _selection.NormalizedEnd;

            // Single-line selection
            if (start.Line == end.Line)
            {
                if (start.Line >= _firstVisibleLine && start.Line <= _lastVisibleLine)
                {
                    double y = TopMargin + (start.Line - _firstVisibleLine) * _lineHeight;
                    double x1 = (ShowLineNumbers ? TextAreaLeftOffset : LeftMargin) + (start.Column * _charWidth);
                    double x2 = (ShowLineNumbers ? TextAreaLeftOffset : LeftMargin) + (end.Column * _charWidth);

                    dc.DrawRectangle(SelectionBackground, null, new Rect(x1, y, x2 - x1, _lineHeight));
                }
            }
            else // Multi-line selection (Phase 3)
            {
                double leftEdge = ShowLineNumbers ? TextAreaLeftOffset : LeftMargin;

                // Render first line (from start.Column to end of line)
                if (start.Line >= _firstVisibleLine && start.Line <= _lastVisibleLine)
                {
                    double y = TopMargin + (start.Line - _firstVisibleLine) * _lineHeight;
                    double x1 = leftEdge + (start.Column * _charWidth);
                    double x2 = leftEdge + (_document.Lines[start.Line].Length * _charWidth);

                    dc.DrawRectangle(SelectionBackground, null, new Rect(x1, y, Math.Max(x2 - x1, _charWidth), _lineHeight));
                }

                // Render middle lines (entire line width)
                for (int line = start.Line + 1; line < end.Line; line++)
                {
                    if (line >= _firstVisibleLine && line <= _lastVisibleLine)
                    {
                        double y = TopMargin + (line - _firstVisibleLine) * _lineHeight;
                        double width = _document.Lines[line].Length * _charWidth;

                        dc.DrawRectangle(SelectionBackground, null, new Rect(leftEdge, y, Math.Max(width, _charWidth), _lineHeight));
                    }
                }

                // Render last line (from start of line to end.Column)
                if (end.Line >= _firstVisibleLine && end.Line <= _lastVisibleLine)
                {
                    double y = TopMargin + (end.Line - _firstVisibleLine) * _lineHeight;
                    double x2 = leftEdge + (end.Column * _charWidth);

                    dc.DrawRectangle(SelectionBackground, null, new Rect(leftEdge, y, x2 - leftEdge, _lineHeight));
                }
            }
        }

        /// <summary>
        /// Render text content with syntax highlighting (Phase 2)
        /// </summary>
        private void RenderTextContent(DrawingContext dc)
        {
            double x = ShowLineNumbers ? TextAreaLeftOffset : LeftMargin;
            double y = TopMargin;

            var context = new JsonParserContext();

            for (int i = _firstVisibleLine; i <= _lastVisibleLine && i < _document.Lines.Count; i++)
            {
                var line = _document.Lines[i];

                if (!string.IsNullOrEmpty(line.Text))
                {
                    // Phase 2: Use syntax highlighter to get colored tokens
                    var tokens = _highlighter.HighlightLine(line, context);

                    foreach (var token in tokens)
                    {
                        var typeface = token.IsBold ? _boldTypeface : _typeface;

                        var formattedText = new FormattedText(
                            token.Text,
                            CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight,
                            typeface,
                            _fontSize,
                            token.Foreground,
                            VisualTreeHelper.GetDpi(this).PixelsPerDip);

                        if (token.IsItalic)
                            formattedText.SetFontStyle(FontStyles.Italic);

                        double tokenX = x + (token.StartColumn * _charWidth);
                        dc.DrawText(formattedText, new Point(tokenX, y));
                    }
                }

                y += _lineHeight;
            }
        }

        /// <summary>
        /// Render cursor (simple rectangle for Phase 1)
        /// Phase 1: Static cursor, blinking will be added later
        /// </summary>
        private void RenderCursor(DrawingContext dc)
        {
            if (!IsFocused)
                return;

            if (_cursorLine < _firstVisibleLine || _cursorLine > _lastVisibleLine)
                return;

            double y = TopMargin + (_cursorLine - _firstVisibleLine) * _lineHeight;
            double x = (ShowLineNumbers ? TextAreaLeftOffset : LeftMargin) + (_cursorColumn * _charWidth);

            // Draw cursor as vertical line (Insert mode style)
            var cursorPen = new Pen(new SolidColorBrush(Color.FromRgb(0, 0, 0)), 1.5);
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

                double y = TopMargin + (error.Line - _firstVisibleLine) * _lineHeight + _lineHeight - 3;
                double x1 = leftEdge + (error.Column * _charWidth);
                double x2 = x1 + (error.Length * _charWidth);

                // Choose color based on severity
                Brush errorBrush;
                switch (error.Severity)
                {
                    case ValidationSeverity.Error:
                        errorBrush = Brushes.Red;
                        break;
                    case ValidationSeverity.Warning:
                        errorBrush = new SolidColorBrush(Color.FromRgb(255, 165, 0)); // Orange
                        break;
                    case ValidationSeverity.Info:
                        errorBrush = Brushes.Blue;
                        break;
                    default:
                        errorBrush = Brushes.Red;
                        break;
                }

                // Draw squiggly line
                DrawSquigglyLine(dc, x1, x2, y, errorBrush);
            }
        }

        /// <summary>
        /// Draw a squiggly (wavy) underline
        /// </summary>
        private void DrawSquigglyLine(DrawingContext dc, double x1, double x2, double y, Brush brush)
        {
            var pen = new Pen(brush, 1.5);
            pen.Freeze();

            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(new Point(x1, y), false, false);

                double x = x1;
                bool up = true;

                while (x < x2)
                {
                    x += 2;
                    ctx.LineTo(new Point(x, y + (up ? -2 : 2)), true, false);
                    up = !up;
                }
            }

            geometry.Freeze();
            dc.DrawGeometry(null, pen, geometry);
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

            double y = TopMargin + (line - _firstVisibleLine) * _lineHeight;
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
                    DeleteCharBefore();
                    e.Handled = true;
                    break;

                case Key.Delete:
                    DeleteCharAfter();
                    e.Handled = true;
                    break;

                case Key.Tab:
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
            }

            InvalidateVisual();
        }

        protected override void OnTextInput(TextCompositionEventArgs e)
        {
            base.OnTextInput(e);

            if (!string.IsNullOrEmpty(e.Text))
            {
                foreach (char ch in e.Text)
                {
                    // Skip control characters
                    if (char.IsControl(ch))
                        continue;

                    InsertChar(ch);

                    // Auto-trigger IntelliSense on specific characters (Phase 4)
                    if (_enableIntelliSense && ShouldAutoTriggerIntelliSense(ch))
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

        private void DeleteCharBefore()
        {
            if (_cursorColumn > 0)
            {
                _cursorColumn--;
                _document.DeleteChar(_cursorLine, _cursorColumn);
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

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);

            Focus(); // Ensure editor gets keyboard focus

            var pos = e.GetPosition(this);
            var textPos = PixelToTextPosition(pos);

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

            if (_isSelecting && e.LeftButton == MouseButtonState.Pressed)
            {
                var pos = e.GetPosition(this);
                var textPos = PixelToTextPosition(pos);

                _selection.End = textPos;
                _cursorLine = textPos.Line;
                _cursorColumn = textPos.Column;

                InvalidateVisual();
            }
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);

            if (_isSelecting)
            {
                _isSelecting = false;
                ReleaseMouseCapture();
            }
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

            // Calculate column
            int column = (int)((pixel.X - leftEdge) / _charWidth);
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
        }

        #endregion

        #region Undo/Redo Operations (Phase 3)

        private void Undo()
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
        }

        private void Redo()
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

        private void Document_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Phase 3: Add to undo/redo stack (unless this is an undo/redo operation itself)
            if (!_isInternalEdit)
            {
                // Convert TextChangeType to TextEditType (same enum values, different types)
                var editType = (TextEditType)((int)e.ChangeType);
                var textEdit = new TextEdit(editType, e.Position, e.Text);
                _undoRedoStack.Push(textEdit);
            }

            // Phase 5: Trigger validation with debounce
            if (_enableValidation)
            {
                _validationTimer.Stop();
                _validationTimer.Start();
            }

            InvalidateVisual();
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
            if (!_enableValidation || _validator == null || _document == null)
                return;

            try
            {
                var jsonText = _document.SaveToString();
                _validationErrors = _validator.Validate(jsonText);
                InvalidateVisual();
            }
            catch (Exception)
            {
                // Silently ignore validation errors
                _validationErrors.Clear();
            }
        }

        /// <summary>
        /// Trigger validation manually (public API)
        /// </summary>
        public void TriggerValidation()
        {
            if (_enableValidation)
            {
                PerformValidation();
            }
        }

        /// <summary>
        /// Get current validation errors
        /// </summary>
        public List<ValidationError> ValidationErrors => _validationErrors;

        #endregion

        #region Public API

        /// <summary>
        /// Get the document model
        /// </summary>
        public JsonDocument Document => _document;

        /// <summary>
        /// Load text content
        /// </summary>
        public void LoadText(string text)
        {
            _document.LoadFromString(text);
            _cursorLine = 0;
            _cursorColumn = 0;
            _selection.Clear();
            InvalidateVisual();
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
    }
}
