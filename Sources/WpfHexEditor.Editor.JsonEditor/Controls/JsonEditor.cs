//////////////////////////////////////////////
// Apache 2.0  - 2026
// Custom JsonEditor - Main Editor Control (Phase 1 - Foundation)
// Author : Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com), Claude Sonnet 4.6
// Inspired by HexViewport.cs custom rendering pattern
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Editor.JsonEditor.Models;
using WpfHexEditor.Editor.JsonEditor.Helpers;
using WpfHexEditor.Editor.JsonEditor.Services;
using WpfHexEditor.Core.Settings;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.Editor.JsonEditor.Controls
{
    /// <summary>
    /// High-performance JSON text editor using custom rendering (FrameworkElement).
    /// Phase 1: Basic text display + keyboard input + line numbers
    /// Phase 2: Syntax highlighting with JsonSyntaxHighlighter
    /// Future phases will add: IntelliSense, validation
    /// </summary>
    public class JsonEditor : FrameworkElement, IDocumentEditor, IDiagnosticSource, IPropertyProviderSource, IOpenableDocument, IStatusBarContributor
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
        private bool _isDirty = false;        // IDocumentEditor: unsaved changes flag
        private string? _currentFilePath;     // IDocumentEditor: last saved file path

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

        private List<Models.ValidationError> _validationErrors = new List<Models.ValidationError>();
        private FormatSchemaValidator _validator;
        private System.Windows.Threading.DispatcherTimer _validationTimer;

        #endregion

        #region Fields - Virtual Scrolling (Phase 11)

        private VirtualizationEngine _virtualizationEngine;
        private double _verticalScrollOffset = 0;

        #endregion

        #region Fields - Smooth Scrolling

        private System.Windows.Threading.DispatcherTimer _smoothScrollTimer;
        private double _targetScrollOffset = 0;
        private double _currentScrollOffset = 0;
        private const double SmoothScrollSpeed = 0.2; // Interpolation factor (0-1)

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
        private double _fontSize = 12.0;
        private double _charWidth;          // Cached character width
        private double _charHeight;         // Cached character height
        private double _lineHeight;         // Line height with padding
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

        #endregion

        #region Fields - Colors (Brushes)

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
                if (value)
                    TriggerValidation();
                else
                {
                    _validationErrors.Clear();
                    DiagnosticsChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        // ===== APPEARANCE - FONTS =====

        public static readonly DependencyProperty EditorFontFamilyProperty =
            DependencyProperty.Register(nameof(EditorFontFamily), typeof(FontFamily), typeof(JsonEditor),
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
            DependencyProperty.Register(nameof(EditorFontSize), typeof(double), typeof(JsonEditor),
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

        public static readonly DependencyProperty LineNumberFontSizeProperty =
            DependencyProperty.Register(nameof(LineNumberFontSize), typeof(double), typeof(JsonEditor),
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
            DependencyProperty.Register(nameof(EditorFontWeight), typeof(FontWeight), typeof(JsonEditor),
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
            DependencyProperty.Register(nameof(LineNumberFontFamily), typeof(FontFamily), typeof(JsonEditor),
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
            DependencyProperty.Register(nameof(LineHeightMultiplier), typeof(double), typeof(JsonEditor),
                new FrameworkPropertyMetadata(1.3, FrameworkPropertyMetadataOptions.AffectsRender,
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
            DependencyProperty.Register(nameof(BoldKeywords), typeof(bool), typeof(JsonEditor),
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
            DependencyProperty.Register(nameof(ItalicComments), typeof(bool), typeof(JsonEditor),
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
            DependencyProperty.Register(nameof(EditorBackground), typeof(Brush), typeof(JsonEditor),
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
            DependencyProperty.Register(nameof(EditorForeground), typeof(Brush), typeof(JsonEditor),
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
            DependencyProperty.Register(nameof(LineNumberBackground), typeof(Brush), typeof(JsonEditor),
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
            DependencyProperty.Register(nameof(LineNumberForeground), typeof(Brush), typeof(JsonEditor),
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
            DependencyProperty.Register(nameof(CurrentLineBackground), typeof(Brush), typeof(JsonEditor),
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
            DependencyProperty.Register(nameof(SelectionBackground), typeof(Brush), typeof(JsonEditor),
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
            DependencyProperty.Register(nameof(CaretColor), typeof(Color), typeof(JsonEditor),
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
            DependencyProperty.Register(nameof(CurrentLineBorderColor), typeof(Color), typeof(JsonEditor),
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
            DependencyProperty.Register(nameof(InactiveSelectionBackground), typeof(Color), typeof(JsonEditor),
                new FrameworkPropertyMetadata(Color.FromArgb(50, 128, 128, 128), FrameworkPropertyMetadataOptions.AffectsRender));

        [Category("Appearance.Colors")]
        [DisplayName("Inactive Selection Background")]
        [Description("Selection background color when editor loses focus")]
        public Color InactiveSelectionBackground
        {
            get => (Color)GetValue(InactiveSelectionBackgroundProperty);
            set => SetValue(InactiveSelectionBackgroundProperty, value);
        }

        public static readonly DependencyProperty ValidationErrorGlyphColorProperty =
            DependencyProperty.Register(nameof(ValidationErrorGlyphColor), typeof(Color), typeof(JsonEditor),
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
            DependencyProperty.Register(nameof(ValidationWarningGlyphColor), typeof(Color), typeof(JsonEditor),
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

        // ===== BEHAVIOR - SELECTION & CURSOR =====

        public static readonly DependencyProperty ShowCurrentLineHighlightProperty =
            DependencyProperty.Register(nameof(ShowCurrentLineHighlight), typeof(bool), typeof(JsonEditor),
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
            DependencyProperty.Register(nameof(ShowCurrentLineBorder), typeof(bool), typeof(JsonEditor),
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
            DependencyProperty.Register(nameof(CaretBlinkRate), typeof(int), typeof(JsonEditor),
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
            if (d is JsonEditor editor)
            {
                editor.UpdateCaretBlinkTimer();
            }
        }

        public static readonly DependencyProperty CaretWidthProperty =
            DependencyProperty.Register(nameof(CaretWidth), typeof(double), typeof(JsonEditor),
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
            DependencyProperty.Register(nameof(SmartBackspace), typeof(bool), typeof(JsonEditor),
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
            DependencyProperty.Register(nameof(EnableBracketMatching), typeof(bool), typeof(JsonEditor),
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
            DependencyProperty.Register(nameof(EnableAutoClosingBrackets), typeof(bool), typeof(JsonEditor),
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
            DependencyProperty.Register(nameof(EnableAutoClosingQuotes), typeof(bool), typeof(JsonEditor),
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
            DependencyProperty.Register(nameof(EnableVirtualScrolling), typeof(bool), typeof(JsonEditor),
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
            DependencyProperty.Register(nameof(SmoothScrolling), typeof(bool), typeof(JsonEditor),
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
            DependencyProperty.Register(nameof(ScrollSpeedMultiplier), typeof(double), typeof(JsonEditor),
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

        public static readonly DependencyProperty HorizontalScrollSensitivityProperty =
            DependencyProperty.Register(nameof(HorizontalScrollSensitivity), typeof(double), typeof(JsonEditor),
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
            DependencyProperty.Register(nameof(ScrollBarVisibilityMode), typeof(ScrollBarVisibility), typeof(JsonEditor),
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
            DependencyProperty.Register(nameof(RenderBuffer), typeof(int), typeof(JsonEditor),
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
            DependencyProperty.Register(nameof(MaxCachedLines), typeof(int), typeof(JsonEditor),
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
            DependencyProperty.Register(nameof(UseHardwareAcceleration), typeof(bool), typeof(JsonEditor),
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

        public static readonly DependencyProperty SyntaxCommaColorProperty =
            DependencyProperty.Register(nameof(SyntaxCommaColor), typeof(Brush), typeof(JsonEditor),
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
            DependencyProperty.Register(nameof(SyntaxColonColor), typeof(Brush), typeof(JsonEditor),
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
            DependencyProperty.Register(nameof(SyntaxEscapeSequenceColor), typeof(Brush), typeof(JsonEditor),
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
            DependencyProperty.Register(nameof(SyntaxUrlColor), typeof(Brush), typeof(JsonEditor),
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
            DependencyProperty.Register(nameof(SyntaxDeprecatedColor), typeof(Brush), typeof(JsonEditor),
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
            DependencyProperty.Register(nameof(HighlightMatchColor), typeof(Brush), typeof(JsonEditor),
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
            DependencyProperty.Register(nameof(FindResultColor), typeof(Brush), typeof(JsonEditor),
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

        private static void OnUseHardwareAccelerationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is JsonEditor editor)
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

            // Apply theme resource bindings when connected to the visual tree
            Loaded += (_, _) => ApplyThemeResourceBindings();
        }

        /// <summary>
        /// Binds all color DPs to the active theme's TE_* resource keys.
        /// Called on Loaded so the element is connected to the application resource tree.
        /// Safe to call multiple times — subsequent calls just re-register the same keys.
        /// </summary>
        private void ApplyThemeResourceBindings()
        {
            // Viewport
            SetResourceReference(EditorBackgroundProperty,      "TE_Background");
            SetResourceReference(EditorForegroundProperty,      "TE_Foreground");
            SetResourceReference(LineNumberBackgroundProperty,  "TE_LineNumberBackground");
            SetResourceReference(LineNumberForegroundProperty,  "TE_LineNumberForeground");
            SetResourceReference(CurrentLineBackgroundProperty, "TE_CurrentLineBrush");
            SetResourceReference(SelectionBackgroundProperty,   "TE_SelectionBackground");
            SetResourceReference(CaretColorProperty,            "TE_CaretColor");

            // Syntax highlighting
            SetResourceReference(SyntaxKeyColorProperty,              "TE_Keyword");
            SetResourceReference(SyntaxKeywordColorProperty,          "TE_Keyword");
            SetResourceReference(SyntaxBooleanColorProperty,          "TE_Keyword");
            SetResourceReference(SyntaxStringValueColorProperty,      "TE_String");
            SetResourceReference(SyntaxNumberColorProperty,           "TE_Number");
            SetResourceReference(SyntaxCommentColorProperty,          "TE_Comment");
            SetResourceReference(SyntaxValueTypeColorProperty,        "TE_Type");
            SetResourceReference(SyntaxCalcExpressionColorProperty,   "TE_Directive");
            SetResourceReference(SyntaxVariableReferenceColorProperty,"TE_Register");
            SetResourceReference(SyntaxEscapeSequenceColorProperty,   "TE_Label");
            SetResourceReference(SyntaxUrlColorProperty,              "TE_Register");
            SetResourceReference(SyntaxBraceColorProperty,            "TE_Foreground");
            SetResourceReference(SyntaxBracketColorProperty,          "TE_Foreground");
            SetResourceReference(SyntaxCommaColorProperty,            "TE_Foreground");
            SetResourceReference(SyntaxColonColorProperty,            "TE_Foreground");
            SetResourceReference(SyntaxNullColorProperty,             "TE_Operator");
            SetResourceReference(SyntaxDeprecatedColorProperty,       "TE_Operator");
        }

        private void UpdateTypefacesFromDPs()
        {
            _typeface = new Typeface(EditorFontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            _boldTypeface = new Typeface(EditorFontFamily, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
            _lineNumberTypeface = new Typeface(LineNumberFontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            _fontSize = EditorFontSize;
        }

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
                // Close enough - snap to target and stop
                _currentScrollOffset = _targetScrollOffset;
                _verticalScrollOffset = _targetScrollOffset;
                _smoothScrollTimer.Stop();
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

            InvalidateVisual();
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
            _lineHeight = (_charHeight + 4) * LineHeightMultiplier; // Apply line height multiplier DP
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

        // ── Find bar ─────────────────────────────────────────────────────

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

        // ── Format JSON ──────────────────────────────────────────────────

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

        // ── Validate JSON ────────────────────────────────────────────────

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
            SizeChanged += JsonEditor_SizeChanged;
        }

        private void JsonEditor_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_virtualizationEngine != null)
            {
                _virtualizationEngine.ViewportHeight = ActualHeight;
                _virtualizationEngine.CalculateVisibleRange();
                InvalidateVisual();
            }
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

            double newOffset = _virtualizationEngine.ScrollByPixels(delta * ScrollSpeedMultiplier);

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
                InvalidateVisual();
            }
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

            // 4. Draw find results (background highlights)
            RenderFindResults(dc);

            // 5. Draw selection (before text)
            RenderSelection(dc);

            // 6. Draw line numbers
            if (ShowLineNumbers)
            {
                RenderLineNumbers(dc);
            }

            // 7. Draw text content
            RenderTextContent(dc);

            // 8. Draw validation errors (Phase 5 - squiggly lines)
            if (EnableValidation)
            {
                RenderValidationErrors(dc);
            }

            // 9. Draw bracket matching (Phase 6)
            RenderBracketMatching(dc);

            // 10. Draw cursor (with blinking animation)
            RenderCursor(dc);

            // Phase 11.4: Periodically cleanup token cache to respect MaxCachedLines
            // Only run every ~60 frames to avoid performance impact
            if (_frameCount++ % 60 == 0)
            {
                _document.CleanupTokenCache(MaxCachedLines);
            }
        }

        private int _frameCount = 0; // Frame counter for periodic cache cleanup

        /// <summary>
        /// Measure desired size based on content (all lines)
        /// Required for proper ScrollViewer support
        /// </summary>
        protected override Size MeasureOverride(Size availableSize)
        {
            if (_document == null || _document.Lines.Count == 0)
                return new Size(400, 300); // Default size

            // Calculate total height needed for all lines
            double totalHeight = TopMargin + (_document.Lines.Count * _lineHeight) + 10; // +10 bottom padding

            // Calculate width based on longest line (with a reasonable max)
            double maxLineWidth = 0;
            foreach (var line in _document.Lines)
            {
                double lineWidth = (ShowLineNumbers ? TextAreaLeftOffset : LeftMargin) +
                                  (line.Text.Length * _charWidth) + 20; // +20 right padding
                maxLineWidth = Math.Max(maxLineWidth, lineWidth);
            }

            // Use available width if it's larger, otherwise use calculated width
            double desiredWidth = double.IsInfinity(availableSize.Width) ?
                Math.Max(800, maxLineWidth) : availableSize.Width;

            return new Size(desiredWidth, totalHeight);
        }

        /// <summary>
        /// Arrange the element at the desired size
        /// Required for proper ScrollViewer support
        /// </summary>
        protected override Size ArrangeOverride(Size finalSize)
        {
            return finalSize;
        }

        /// <summary>
        /// Calculate which lines are visible in the viewport
        /// Phase 1: Simple calculation (no virtual scrolling yet)
        /// </summary>
        private void CalculateVisibleLines()
        {
            // Phase 11: Use VirtualizationEngine if enabled
            if (EnableVirtualScrolling && _virtualizationEngine != null)
            {
                // Update virtualization state
                _virtualizationEngine.ViewportHeight = ActualHeight - TopMargin;
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
                    (int)((ActualHeight - TopMargin) / _lineHeight));
            }
        }

        /// <summary>
        /// Render line numbers in left gutter
        /// </summary>
        private void RenderLineNumbers(DrawingContext dc)
        {
            for (int i = _firstVisibleLine; i <= _lastVisibleLine && i < _document.Lines.Count; i++)
            {
                // Phase 11: Calculate Y position with virtual scrolling support
                double y = EnableVirtualScrolling && _virtualizationEngine != null
                    ? TopMargin + _virtualizationEngine.GetLineYPosition(i)
                    : TopMargin + ((i - _firstVisibleLine) * _lineHeight);

                var lineNumber = (i + 1).ToString(); // Display as 1-based
                var formattedText = new FormattedText(
                    lineNumber,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    _lineNumberTypeface, // Use separate line number typeface
                    LineNumberFontSize,
                    LineNumberForeground,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);

                // Right-align line numbers
                double x = LineNumberWidth - formattedText.Width - LineNumberMargin;

                dc.DrawText(formattedText, new Point(x, y));

                // Render validation glyphs (error/warning icons) in left margin
                RenderValidationGlyph(dc, i, y);
            }

            // Draw separator line between line numbers and text
            var pen = new Pen(new SolidColorBrush(Color.FromRgb(200, 200, 200)), 1);
            pen.Freeze();
            dc.DrawLine(pen, new Point(LineNumberWidth, 0), new Point(LineNumberWidth, ActualHeight));
        }

        /// <summary>
        /// Render validation glyph (error/warning icon) for a line if it has validation errors
        /// </summary>
        private void RenderValidationGlyph(DrawingContext dc, int line, double y)
        {
            if (!EnableValidation || _validationErrors == null || _validationErrors.Count == 0)
                return;

            // Find errors for this line
            var lineErrors = _validationErrors.Where(e => e.Line == line).ToList();
            if (lineErrors.Count == 0)
                return;

            // Determine severity - show worst one (Error > Warning > Info)
            ValidationSeverity worstSeverity = lineErrors.Max(e => e.Severity);

            // Determine glyph color based on severity
            Brush glyphBrush;
            if (worstSeverity == ValidationSeverity.Error)
                glyphBrush = new SolidColorBrush(ValidationErrorGlyphColor);
            else if (worstSeverity == ValidationSeverity.Warning)
                glyphBrush = new SolidColorBrush(ValidationWarningGlyphColor);
            else
                return; // No glyph for Info severity

            glyphBrush.Freeze();

            // Draw circle glyph in left margin area (before line numbers)
            double glyphSize = Math.Min(_lineHeight * 0.6, 12);
            double glyphX = 5; // Left margin
            double glyphY = y + (_lineHeight - glyphSize) / 2;

            // Draw filled circle
            dc.DrawEllipse(glyphBrush, null, new Point(glyphX + glyphSize / 2, glyphY + glyphSize / 2), glyphSize / 2, glyphSize / 2);

            // Draw exclamation mark or X symbol inside
            var pen = new Pen(Brushes.White, 1.5);
            pen.Freeze();

            if (worstSeverity == ValidationSeverity.Error)
            {
                // Draw X for errors
                double offset = glyphSize * 0.25;
                dc.DrawLine(pen,
                    new Point(glyphX + offset, glyphY + offset),
                    new Point(glyphX + glyphSize - offset, glyphY + glyphSize - offset));
                dc.DrawLine(pen,
                    new Point(glyphX + glyphSize - offset, glyphY + offset),
                    new Point(glyphX + offset, glyphY + glyphSize - offset));
            }
            else
            {
                // Draw ! for warnings
                double centerX = glyphX + glyphSize / 2;
                dc.DrawLine(pen,
                    new Point(centerX, glyphY + glyphSize * 0.2),
                    new Point(centerX, glyphY + glyphSize * 0.6));
                dc.DrawEllipse(Brushes.White, null, new Point(centerX, glyphY + glyphSize * 0.8), 1, 1);
            }
        }

        /// <summary>
        /// Render current line highlight
        /// </summary>
        private void RenderCurrentLineHighlight(DrawingContext dc)
        {
            if (_cursorLine < _firstVisibleLine || _cursorLine > _lastVisibleLine)
                return;

            // Phase 11: Calculate Y position with virtual scrolling support
            double y = EnableVirtualScrolling && _virtualizationEngine != null
                ? TopMargin + _virtualizationEngine.GetLineYPosition(_cursorLine)
                : TopMargin + (_cursorLine - _firstVisibleLine) * _lineHeight;

            double x = ShowLineNumbers ? TextAreaLeftOffset : LeftMargin;

            // Draw background highlight
            if (ShowCurrentLineHighlight)
            {
                dc.DrawRectangle(CurrentLineBackground, null,
                    new Rect(x, y, ActualWidth - x, _lineHeight));
            }

            // Draw border if enabled
            if (ShowCurrentLineBorder)
            {
                var borderBrush = new SolidColorBrush(CurrentLineBorderColor);
                borderBrush.Freeze();
                var borderPen = new Pen(borderBrush, 1);
                borderPen.Freeze();
                dc.DrawRectangle(null, borderPen,
                    new Rect(x, y, ActualWidth - x, _lineHeight));
            }
        }

        /// <summary>
        /// Render text selection overlay (Phase 3 - Enhanced with multi-line support)
        /// </summary>
        private void RenderSelection(DrawingContext dc)
        {
            if (_selection.IsEmpty)
                return;

            // Use InactiveSelectionBackground when not focused
            Brush selectionBrush = IsFocused ? SelectionBackground : new SolidColorBrush(InactiveSelectionBackground);

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

                    dc.DrawRectangle(selectionBrush, null, new Rect(x1, y, x2 - x1, _lineHeight));
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

                    dc.DrawRectangle(selectionBrush, null, new Rect(x1, y, Math.Max(x2 - x1, _charWidth), _lineHeight));
                }

                // Render middle lines (entire line width)
                for (int line = start.Line + 1; line < end.Line; line++)
                {
                    if (line >= _firstVisibleLine && line <= _lastVisibleLine)
                    {
                        double y = TopMargin + (line - _firstVisibleLine) * _lineHeight;
                        double width = _document.Lines[line].Length * _charWidth;

                        dc.DrawRectangle(selectionBrush, null, new Rect(leftEdge, y, Math.Max(width, _charWidth), _lineHeight));
                    }
                }

                // Render last line (from start of line to end.Column)
                if (end.Line >= _firstVisibleLine && end.Line <= _lastVisibleLine)
                {
                    double y = TopMargin + (end.Line - _firstVisibleLine) * _lineHeight;
                    double x2 = leftEdge + (end.Column * _charWidth);

                    dc.DrawRectangle(selectionBrush, null, new Rect(leftEdge, y, x2 - leftEdge, _lineHeight));
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

                dc.DrawRectangle(highlightBrush, null, new Rect(x1, y, x2 - x1, _lineHeight));
            }
        }

        /// <summary>
        /// Render text content with syntax highlighting (Phase 2)
        /// </summary>
        private void RenderTextContent(DrawingContext dc)
        {
            double x = ShowLineNumbers ? TextAreaLeftOffset : LeftMargin;

            var context = new JsonParserContext();

            for (int i = _firstVisibleLine; i <= _lastVisibleLine && i < _document.Lines.Count; i++)
            {
                // Phase 11: Calculate Y position with virtual scrolling support
                double y = EnableVirtualScrolling && _virtualizationEngine != null
                    ? TopMargin + _virtualizationEngine.GetLineYPosition(i)
                    : TopMargin + ((i - _firstVisibleLine) * _lineHeight);

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
            }
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

            if (EnableVirtualScrolling && _virtualizationEngine != null)
            {
                // Scroll by delta (negative delta = scroll down)
                double lineScrollAmount = 3; // Scroll 3 lines per wheel notch
                double pixelDelta = -e.Delta / 120.0 * lineScrollAmount * _lineHeight;

                ScrollVertical(pixelDelta);
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
            if (!EnableValidation || _validator == null || _document == null)
                return;

            try
            {
                var jsonText = _document.SaveToString();
                _validationErrors = _validator.Validate(jsonText);
                InvalidateVisual();
                DiagnosticsChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception)
            {
                // Silently ignore validation errors
                _validationErrors.Clear();
                DiagnosticsChanged?.Invoke(this, EventArgs.Empty);
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

        #region IDocumentEditor

        // ── State ──────────────────────────────────────────────────────────

        /// <summary>
        /// True when the document has unsaved changes.
        /// </summary>
        public bool IsDirty => _isDirty;

        // ── IsReadOnly DP ─────────────────────────────────────────────────

        public static readonly DependencyProperty IsReadOnlyProperty =
            DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(JsonEditor),
                new System.Windows.PropertyMetadata(false, (_, _) => { }));

        public bool IsReadOnly
        {
            get => (bool)GetValue(IsReadOnlyProperty);
            set => SetValue(IsReadOnlyProperty, value);
        }

        // ── Title ─────────────────────────────────────────────────────────

        public string Title => BuildTitle();

        // ── Commands ──────────────────────────────────────────────────────

        public System.Windows.Input.ICommand UndoCommand => new JsonRelayCommand(_ => Undo(), _ => CanUndo);
        public System.Windows.Input.ICommand RedoCommand => new JsonRelayCommand(_ => Redo(), _ => CanRedo);
        public System.Windows.Input.ICommand SaveCommand => new JsonRelayCommand(_ => Save());
        public System.Windows.Input.ICommand CopyCommand => new JsonRelayCommand(_ => CopyToClipboard(), _ => !_selection.IsEmpty);
        public System.Windows.Input.ICommand CutCommand => new JsonRelayCommand(_ => CutToClipboard(), _ => !_selection.IsEmpty && !IsReadOnly);
        public System.Windows.Input.ICommand PasteCommand => new JsonRelayCommand(_ => PasteFromClipboard(), _ => !IsReadOnly && Clipboard.ContainsText());
        public System.Windows.Input.ICommand DeleteCommand => new JsonRelayCommand(_ => DeleteSelection(), _ => !_selection.IsEmpty && !IsReadOnly);
        public System.Windows.Input.ICommand SelectAllCommand => new JsonRelayCommand(_ => SelectAll());

        // ── Methods ───────────────────────────────────────────────────────

        public void Save()
        {
            if (!string.IsNullOrEmpty(_currentFilePath))
            {
                File.WriteAllText(_currentFilePath, GetText(), System.Text.Encoding.UTF8);
                _isDirty = false;
                ModifiedChanged?.Invoke(this, EventArgs.Empty);
                TitleChanged?.Invoke(this, BuildTitle());
                StatusMessage?.Invoke(this, "Saved");
            }
        }

        public async System.Threading.Tasks.Task SaveAsync(System.Threading.CancellationToken ct = default)
        {
            if (!string.IsNullOrEmpty(_currentFilePath))
                await SaveAsAsync(_currentFilePath, ct);
        }

        public async System.Threading.Tasks.Task SaveAsAsync(string filePath, System.Threading.CancellationToken ct = default)
        {
            var text = GetText();
            await System.Threading.Tasks.Task.Run(() => File.WriteAllText(filePath, text, System.Text.Encoding.UTF8), ct);
            _currentFilePath = filePath;
            _isDirty = false;
            ModifiedChanged?.Invoke(this, EventArgs.Empty);
            TitleChanged?.Invoke(this, BuildTitle());
            StatusMessage?.Invoke(this, $"Saved: {Path.GetFileName(filePath)}");
        }

        // ── Open file ─────────────────────────────────────────────────────

        public void LoadFromFile(string filePath)
        {
            var text = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
            LoadText(text);
            _currentFilePath = filePath;
            TitleChanged?.Invoke(this, BuildTitle());
            StatusMessage?.Invoke(this, $"Opened: {Path.GetFileName(filePath)}");
            RefreshJsonStatusBarItems();
        }

        System.Threading.Tasks.Task IOpenableDocument.OpenAsync(string filePath, System.Threading.CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            LoadFromFile(filePath);
            return System.Threading.Tasks.Task.CompletedTask;
        }

        // ── Public methods (IDocumentEditor) ─────────────────────────────

        void IDocumentEditor.Copy() => CopyToClipboard();
        void IDocumentEditor.Cut() => CutToClipboard();
        void IDocumentEditor.Paste() => PasteFromClipboard();
        void IDocumentEditor.Delete() => DeleteSelection();
        void IDocumentEditor.SelectAll() => SelectAll();

        public void Close()
        {
            _document = new Models.JsonDocument();
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

        // ── Events ────────────────────────────────────────────────────────

        public event EventHandler? ModifiedChanged;
        public event EventHandler? CanUndoChanged;
        public event EventHandler? CanRedoChanged;
        public event EventHandler<string>? TitleChanged;
        public event EventHandler<string>? StatusMessage;
        public event EventHandler<string>? OutputMessage;
        public event EventHandler? SelectionChanged;

        // ── Long-running operations (no-op: JsonEditor has no async operations) ──
        public bool IsBusy => false;
        public void CancelOperation() { }
        public event EventHandler<DocumentOperationEventArgs>?          OperationStarted;
        public event EventHandler<DocumentOperationEventArgs>?          OperationProgress;
        public event EventHandler<DocumentOperationCompletedEventArgs>? OperationCompleted;

        // ── Helpers ───────────────────────────────────────────────────────

        private string BuildTitle()
        {
            var name = !string.IsNullOrEmpty(_currentFilePath)
                ? Path.GetFileName(_currentFilePath)
                : "untitled.json";
            return _isDirty ? name + " *" : name;
        }

        #endregion

        // ── IPropertyProviderSource ───────────────────────────────────────────
        private WpfHexEditor.Editor.JsonEditor.JsonEditorPropertyProvider? _propertyProvider;
        public IPropertyProvider? GetPropertyProvider()
            => _propertyProvider ??= new WpfHexEditor.Editor.JsonEditor.JsonEditorPropertyProvider(this);

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

        internal void RefreshJsonStatusBarItems()
        {
            if (_jsonStatusBarItems == null) return;
            _sbJsonFile.Value = !string.IsNullOrEmpty(_currentFilePath)
                ? Path.GetFileName(_currentFilePath)
                : "(unsaved)";
        }

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
    }

    // ── File-scoped RelayCommand ──────────────────────────────────────────────
    file sealed class JsonRelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
        : System.Windows.Input.ICommand
    {
        public bool CanExecute(object? p)  => canExecute?.Invoke(p) ?? true;
        public void Execute(object? p)     => execute(p);
        public event EventHandler? CanExecuteChanged;
    }
}
