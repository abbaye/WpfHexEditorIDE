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
// ==========================================================

using System.ComponentModel;

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
    private bool    _enableFolding       = true;
    private bool    _enableMultiCaret    = true;
    private bool    _enableIntelliSense  = true;
    private bool    _enableSnippets      = true;
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

    public bool EnableFolding
    {
        get => _enableFolding;
        set { _enableFolding = value; Notify(); }
    }

    public bool EnableMultiCaret
    {
        get => _enableMultiCaret;
        set { _enableMultiCaret = value; Notify(); }
    }

    public bool EnableIntelliSense
    {
        get => _enableIntelliSense;
        set { _enableIntelliSense = value; Notify(); }
    }

    public bool EnableSnippets
    {
        get => _enableSnippets;
        set { _enableSnippets = value; Notify(); }
    }

    /// <summary>
    /// Optional per-session theme override (null = follow IDE global theme).
    /// </summary>
    public string? ThemeOverride
    {
        get => _themeOverride;
        set { _themeOverride = value; Notify(); }
    }

    // -----------------------------------------------------------------------

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Notify([System.Runtime.CompilerServices.CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}
