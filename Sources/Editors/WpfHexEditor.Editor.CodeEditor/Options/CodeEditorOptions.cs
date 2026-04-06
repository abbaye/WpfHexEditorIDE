// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Options/CodeEditorOptions.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Strongly-typed options model for the CodeEditor.
//     Persisted via AppSettings and bound by CodeEditorOptionsPage.
//
// Architecture Notes:
//     Pattern: Options / Settings Model
//     All properties are mutable so AppSettings serialization works.
//     CodeEditor reads these on Init and subscribes to OptionsChanged.
//     SyntaxColorOverrides: null value = use theme default CE_* resource;
//     non-null = user-specified color that overrides the theme.
// ==========================================================

using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Media;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Core.ProjectSystem.Languages;

namespace WpfHexEditor.Editor.CodeEditor.Options;

/// <summary>
/// Serializable options model for the CodeEditor.
/// </summary>
public sealed class CodeEditorOptions : INotifyPropertyChanged
{
    private string  _fontFamily          = "Consolas";
    private double  _fontSize            = 13.0;
    private int     _tabSize             = 4;
    private bool    _convertTabsToSpaces = true;
    private bool    _showWhitespace      = false;
    private WhitespaceDisplayMode _whitespaceMode = WhitespaceDisplayMode.Selection;
    private bool    _showLineNumbers     = true;
    private bool    _wordWrap            = false;
    private bool    _enableFolding            = true;
    private bool    _foldToggleOnDoubleClick  = true;
    private bool    _showScopeGuides     = true;
    private bool    _enableMultiCaret    = true;
    private bool    _enableSmartComplete       = true;
    private bool    _enableSnippets           = true;
    private bool    _enableFindAllReferences  = true;
    private bool    _showInlineHints          = true;
    private InlineHintsSymbolKinds _inlineHintsVisibleKinds = InlineHintsSymbolKinds.All;
    private string? _themeOverride       = null;

    // -----------------------------------------------------------------------

    public string FontFamily
    {
        get => _fontFamily;
        set { _fontFamily = value; Notify(); }
    }

    public double FontSize
    {
        get => _fontSize;
        set { _fontSize = value; Notify(); }
    }

    public int TabSize
    {
        get => _tabSize;
        set { _tabSize = Math.Clamp(value, 1, 16); Notify(); }
    }

    public bool ConvertTabsToSpaces
    {
        get => _convertTabsToSpaces;
        set { _convertTabsToSpaces = value; Notify(); }
    }

    public bool ShowWhitespace
    {
        get => _whitespaceMode != WhitespaceDisplayMode.None;
        set { WhitespaceMode = value ? WhitespaceDisplayMode.Always : WhitespaceDisplayMode.None; }
    }

    /// <summary>
    /// Controls when whitespace markers (dots/arrows) are rendered:
    /// None = never, Selection = only in selected text, Always = everywhere.
    /// </summary>
    public WhitespaceDisplayMode WhitespaceMode
    {
        get => _whitespaceMode;
        set { _whitespaceMode = value; Notify(); Notify(nameof(ShowWhitespace)); }
    }

    public bool ShowLineNumbers
    {
        get => _showLineNumbers;
        set { _showLineNumbers = value; Notify(); }
    }

    /// <summary>Wrap long lines visually at the viewport edge (hides horizontal scrollbar).</summary>
    public bool WordWrap
    {
        get => _wordWrap;
        set { _wordWrap = value; Notify(); }
    }

    public bool EnableFolding
    {
        get => _enableFolding;
        set { _enableFolding = value; Notify(); }
    }

    /// <summary>
    /// When true, fold regions (gutter triangle and inline label) require a double-click
    /// to toggle instead of a single click.
    /// </summary>
    public bool FoldToggleOnDoubleClick
    {
        get => _foldToggleOnDoubleClick;
        set { _foldToggleOnDoubleClick = value; Notify(); }
    }

    public bool ShowScopeGuides
    {
        get => _showScopeGuides;
        set { _showScopeGuides = value; Notify(); }
    }

    public bool EnableMultiCaret
    {
        get => _enableMultiCaret;
        set { _enableMultiCaret = value; Notify(); }
    }

    public bool EnableSmartComplete
    {
        get => _enableSmartComplete;
        set { _enableSmartComplete = value; Notify(); }
    }

    public bool EnableSnippets
    {
        get => _enableSnippets;
        set { _enableSnippets = value; Notify(); }
    }

    /// <summary>
    /// When false, the "Find All References" command (Shift+F12) and its
    /// context-menu item are disabled regardless of LSP availability.
    /// </summary>
    public bool EnableFindAllReferences
    {
        get => _enableFindAllReferences;
        set { _enableFindAllReferences = value; Notify(); }
    }

    private bool _clickableLinksEnabled = true;

    /// <summary>
    /// When <see langword="true"/>, HTTP/HTTPS URLs in code are detected and Ctrl+Click opens them in the browser.
    /// </summary>
    public bool ClickableLinksEnabled
    {
        get => _clickableLinksEnabled;
        set { _clickableLinksEnabled = value; Notify(); }
    }

    private bool _clickableEmailsEnabled = true;

    /// <summary>
    /// When <see langword="true"/>, email addresses in code are detected and Ctrl+Click opens the mail client.
    /// </summary>
    public bool ClickableEmailsEnabled
    {
        get => _clickableEmailsEnabled;
        set { _clickableEmailsEnabled = value; Notify(); }
    }

    /// <summary>
    /// When true, shows "N références" hints above each declaration line.
    /// Clicking a hint opens the Find All References popup.
    /// </summary>
    public bool ShowInlineHints
    {
        get => _showInlineHints;
        set { _showInlineHints = value; Notify(); }
    }

    /// <summary>
    /// Bitmask controlling which symbol kinds display inline hints.
    /// Default: <see cref="InlineHintsSymbolKinds.All"/> (all 12 kinds visible).
    /// </summary>
    public InlineHintsSymbolKinds InlineHintsVisibleKinds
    {
        get => _inlineHintsVisibleKinds;
        set { _inlineHintsVisibleKinds = value; Notify(); }
    }

    private int _inlineHintsSource = 0;

    /// <summary>
    /// Reference-count source: 0=Auto (Roslyn when available, regex fallback),
    /// 1=RoslynOnly (no hint for non-C#/VB files), 2=RegexAlways.
    /// </summary>
    public int InlineHintsSource
    {
        get => _inlineHintsSource;
        set { _inlineHintsSource = value; Notify(); }
    }

    private int  _maxUndoHistory      = 500;

    /// <summary>
    /// Maximum number of undo steps retained in memory. Oldest entries are silently
    /// trimmed when the limit is exceeded. Clamped to [10, 5000].
    /// </summary>
    public int MaxUndoHistory
    {
        get => _maxUndoHistory;
        set { _maxUndoHistory = Math.Clamp(value, 10, 5000); Notify(); }
    }

    private bool _showEndOfBlockHint    = true;
    private int  _endOfBlockHintDelayMs = 600;

    /// <summary>
    /// When true, hovering over }, #endregion, &lt;/Tag&gt; shows the matching opening line(s).
    /// </summary>
    public bool ShowEndOfBlockHint
    {
        get => _showEndOfBlockHint;
        set { _showEndOfBlockHint = value; Notify(); }
    }

    /// <summary>
    /// Hover dwell time in milliseconds before the end-of-block hint popup appears. Clamped to [100, 2000].
    /// </summary>
    public int EndOfBlockHintDelayMs
    {
        get => _endOfBlockHintDelayMs;
        set { _endOfBlockHintDelayMs = Math.Clamp(value, 100, 2000); Notify(); }
    }

    private bool _autoClosingBrackets   = true;
    private bool _autoClosingQuotes     = true;
    private bool _skipOverClosingChar   = true;
    private bool _wrapSelectionInPairs  = true;

    /// <summary>Automatically insert matching closing bracket/brace/paren when typing the opening one.</summary>
    public bool AutoClosingBrackets
    {
        get => _autoClosingBrackets;
        set { _autoClosingBrackets = value; Notify(); }
    }

    /// <summary>Automatically insert matching closing quote when typing an opening quote.</summary>
    public bool AutoClosingQuotes
    {
        get => _autoClosingQuotes;
        set { _autoClosingQuotes = value; Notify(); }
    }

    /// <summary>Skip over an existing closing bracket/quote instead of inserting a duplicate.</summary>
    public bool SkipOverClosingChar
    {
        get => _skipOverClosingChar;
        set { _skipOverClosingChar = value; Notify(); }
    }

    /// <summary>When text is selected and an opening char is typed, wrap the selection in the pair.</summary>
    public bool WrapSelectionInPairs
    {
        get => _wrapSelectionInPairs;
        set { _wrapSelectionInPairs = value; Notify(); }
    }

    private bool _enableWordHighlight = true;

    /// <summary>
    /// When true, all occurrences of the word under the caret are highlighted
    /// with a subtle box and scroll-bar tick marks (VS Code style).
    /// </summary>
    public bool EnableWordHighlight
    {
        get => _enableWordHighlight;
        set { _enableWordHighlight = value; Notify(); }
    }

    // -- Bracket Pair Colorization (#162) --------------------------------------

    private bool _bracketPairColorization = true;

    /// <summary>
    /// When true, bracket pairs are colored with CE_Bracket_1/2/3/4 based on
    /// nesting depth (VS-style multi-level bracket colorization).
    /// Requires <c>bracketPairs</c> in the active language's whfmt definition.
    /// </summary>
    public bool BracketPairColorization
    {
        get => _bracketPairColorization;
        set { _bracketPairColorization = value; Notify(); }
    }

    private bool _rainbowScopeGuides = true;

    /// <summary>
    /// When true and bracket pair colorization is enabled, scope guide lines
    /// are colored with CE_Bracket_1/2/3/4 based on nesting depth.
    /// </summary>
    public bool RainbowScopeGuides
    {
        get => _rainbowScopeGuides;
        set { _rainbowScopeGuides = value; Notify(); }
    }

    // -- Color Swatch Preview (#168) ------------------------------------------

    private bool _colorSwatchPreview = true;

    /// <summary>
    /// When true, a 12×12 colour preview swatch is rendered to the left of each
    /// colour literal in languages that have <c>colorLiteralPatterns</c> in their whfmt.
    /// Clicking a swatch raises <c>CodeEditor.ColorSwatchClicked</c>.
    /// </summary>
    public bool ColorSwatchPreview
    {
        get => _colorSwatchPreview;
        set { _colorSwatchPreview = value; Notify(); }
    }

    // -- Code Formatting (#159) -----------------------------------------------

    private bool _formatOnSave = false;

    /// <summary>
    /// When true, the document is automatically formatted (Ctrl+K, Ctrl+D)
    /// each time it is saved.  Requires either an LSP server with a formatting
    /// provider or a whfmt <c>formattingRules</c> block.
    /// </summary>
    public bool FormatOnSave
    {
        get => _formatOnSave;
        set { _formatOnSave = value; Notify(); }
    }

    // ── Formatting overrides (null = use whfmt language default) ─────────────

    private bool? _trimTrailingWhitespace;
    private bool? _insertFinalNewline;
    private BraceStyle? _braceStyleOverride;
    private bool? _spaceAfterKeywords;
    private bool? _spaceAroundBinaryOperators;
    private bool? _spaceAfterComma;
    private bool? _indentCaseLabels;
    private bool? _organizeImports;

    /// <summary>Override whfmt trimTrailingWhitespace. Null = inherit language default.</summary>
    public bool? TrimTrailingWhitespace
    {
        get => _trimTrailingWhitespace;
        set { _trimTrailingWhitespace = value; Notify(); }
    }

    /// <summary>Override whfmt insertFinalNewline. Null = inherit language default.</summary>
    public bool? InsertFinalNewline
    {
        get => _insertFinalNewline;
        set { _insertFinalNewline = value; Notify(); }
    }

    /// <summary>Override whfmt braceStyle. Null = inherit language default.</summary>
    public BraceStyle? BraceStyleOverride
    {
        get => _braceStyleOverride;
        set { _braceStyleOverride = value; Notify(); }
    }

    /// <summary>Override whfmt spaceAfterKeywords. Null = inherit language default.</summary>
    public bool? SpaceAfterKeywords
    {
        get => _spaceAfterKeywords;
        set { _spaceAfterKeywords = value; Notify(); }
    }

    /// <summary>Override whfmt spaceAroundBinaryOperators. Null = inherit.</summary>
    public bool? SpaceAroundBinaryOperators
    {
        get => _spaceAroundBinaryOperators;
        set { _spaceAroundBinaryOperators = value; Notify(); }
    }

    /// <summary>Override whfmt spaceAfterComma. Null = inherit.</summary>
    public bool? SpaceAfterComma
    {
        get => _spaceAfterComma;
        set { _spaceAfterComma = value; Notify(); }
    }

    /// <summary>Override whfmt indentCaseLabels. Null = inherit.</summary>
    public bool? IndentCaseLabels
    {
        get => _indentCaseLabels;
        set { _indentCaseLabels = value; Notify(); }
    }

    /// <summary>Override whfmt organizeImports. Null = inherit.</summary>
    public bool? OrganizeImports
    {
        get => _organizeImports;
        set { _organizeImports = value; Notify(); }
    }

    /// <summary>
    /// Builds a <see cref="FormattingOverrides"/> from the user's current settings.
    /// Properties left at <see langword="null"/> inherit the whfmt language default.
    /// TabSize and ConvertTabsToSpaces are always applied (non-nullable).
    /// </summary>
    public FormattingOverrides BuildOverrides() => new()
    {
        IndentSize                 = TabSize,
        UseTabs                    = !ConvertTabsToSpaces,
        TrimTrailingWhitespace     = _trimTrailingWhitespace,
        InsertFinalNewline         = _insertFinalNewline,
        BraceStyle                 = _braceStyleOverride,
        SpaceAfterKeywords         = _spaceAfterKeywords,
        SpaceAroundBinaryOperators = _spaceAroundBinaryOperators,
        SpaceAfterComma            = _spaceAfterComma,
        IndentCaseLabels           = _indentCaseLabels,
        OrganizeImports            = _organizeImports,
    };

    // -- Sticky Scroll (#160) -------------------------------------------------

    private bool   _stickyScrollEnabled          = true;
    private int    _stickyScrollMaxLines         = 4;
    private bool   _stickyScrollSyntaxHighlight  = true;
    private bool   _stickyScrollClickToNavigate  = true;
    private double _stickyScrollOpacity          = 0.95;
    private int    _stickyScrollMinScopeLines    = 5;

    /// <summary>Show sticky scope headers pinned at the top of the editor while scrolling.</summary>
    public bool StickyScrollEnabled
    {
        get => _stickyScrollEnabled;
        set { _stickyScrollEnabled = value; Notify(); }
    }

    /// <summary>Maximum number of scope signature lines to display (1–10). Default: 4.</summary>
    public int StickyScrollMaxLines
    {
        get => _stickyScrollMaxLines;
        set { _stickyScrollMaxLines = Math.Clamp(value, 1, 10); Notify(); }
    }

    /// <summary>Apply syntax highlighting to sticky header rows. Default: true.</summary>
    public bool StickyScrollSyntaxHighlight
    {
        get => _stickyScrollSyntaxHighlight;
        set { _stickyScrollSyntaxHighlight = value; Notify(); }
    }

    /// <summary>Clicking a sticky header row scrolls to that scope's start. Default: true.</summary>
    public bool StickyScrollClickToNavigate
    {
        get => _stickyScrollClickToNavigate;
        set { _stickyScrollClickToNavigate = value; Notify(); }
    }

    /// <summary>Opacity of the sticky header panel (0.5–1.0). Default: 0.95.</summary>
    public double StickyScrollOpacity
    {
        get => _stickyScrollOpacity;
        set { _stickyScrollOpacity = Math.Clamp(value, 0.5, 1.0); Notify(); }
    }

    /// <summary>Scopes spanning fewer than this many lines are excluded from the header. Default: 5.</summary>
    public int StickyScrollMinScopeLines
    {
        get => _stickyScrollMinScopeLines;
        set { _stickyScrollMinScopeLines = Math.Clamp(value, 2, 20); Notify(); }
    }

    /// <summary>
    /// Optional per-session theme override (null = follow IDE global theme).
    /// </summary>
    public string? ThemeOverride
    {
        get => _themeOverride;
        set { _themeOverride = value; Notify(); }
    }

    // -- Syntax Color Overrides -----------------------------------------------

    /// <summary>
    /// Per-token user color overrides. A null value means "use the active theme's CE_* resource".
    /// A non-null Color overrides that resource so the CodeEditor uses the exact color chosen
    /// by the user regardless of which theme is active.
    /// </summary>
    public Dictionary<SyntaxTokenKind, Color?> SyntaxColorOverrides { get; set; } = new();

    /// <summary>
    /// Returns the user override for <paramref name="kind"/>, or null if no override is set.
    /// </summary>
    public Color? GetOverride(SyntaxTokenKind kind)
        => SyntaxColorOverrides.TryGetValue(kind, out var c) ? c : null;

    /// <summary>
    /// Sets or clears the user override for <paramref name="kind"/>.
    /// Pass null to revert to the theme default.
    /// </summary>
    public void SetOverride(SyntaxTokenKind kind, Color? color)
    {
        SyntaxColorOverrides[kind] = color;
        Notify(nameof(SyntaxColorOverrides));
    }

    /// <summary>
    /// Removes all per-token color overrides, restoring theme defaults for all tokens.
    /// </summary>
    public void ResetAllOverrides()
    {
        SyntaxColorOverrides.Clear();
        Notify(nameof(SyntaxColorOverrides));
    }

    // -----------------------------------------------------------------------

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Notify([System.Runtime.CompilerServices.CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}
