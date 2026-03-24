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
        get => _showWhitespace;
        set { _showWhitespace = value; Notify(); }
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
