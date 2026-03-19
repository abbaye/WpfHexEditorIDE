// ==========================================================
// Project: WpfHexEditor.Sample.CodeEditor
// File: ViewModels/MainViewModel.cs
// Author: Auto
// Created: 2026-03-18
// Description:
//     Bindable view-model for the main window. Tracks editor state
//     (caret position, dirty flag, language, zoom, theme) and
//     exposes them to the XAML via INotifyPropertyChanged.
//
// Architecture Notes:
//     Pattern: MVVM ViewModel — thin adapter between CodeEditorSplitHost
//     events and WPF data-binding. No business logic here.
//     Theme: CurrentThemeName drives the menu radio-checkmarks and
//     toolbar toggle; IsDarkTheme is a convenience bool for the toolbar.
// ==========================================================

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WpfHexEditor.Sample.CodeEditor.ViewModels;

/// <summary>
/// Bindable state for the code editor sample main window.
/// </summary>
public sealed class MainViewModel : INotifyPropertyChanged
{
    // -- INotifyPropertyChanged ---------------------------------------------------

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }

    // -- Window title -------------------------------------------------------------

    private string _title = "CodeEditor Sample";
    /// <summary>Window title including the filename and dirty marker.</summary>
    public string Title
    {
        get => _title;
        set => Set(ref _title, value);
    }

    // -- File state ---------------------------------------------------------------

    private string? _currentFilePath;
    /// <summary>Full path of the currently open file, or null for a new unsaved buffer.</summary>
    public string? CurrentFilePath
    {
        get => _currentFilePath;
        set => Set(ref _currentFilePath, value);
    }

    private bool _isDirty;
    /// <summary>True when the document has unsaved changes.</summary>
    public bool IsDirty
    {
        get => _isDirty;
        set
        {
            if (Set(ref _isDirty, value))
                RefreshTitle();
        }
    }

    private string _encoding = "UTF-8";
    /// <summary>Encoding label shown in the status bar.</summary>
    public string Encoding
    {
        get => _encoding;
        set => Set(ref _encoding, value);
    }

    // -- Caret / Selection --------------------------------------------------------

    private int _caretLine = 1;
    /// <summary>1-based current caret line number.</summary>
    public int CaretLine
    {
        get => _caretLine;
        set => Set(ref _caretLine, value);
    }

    private int _caretColumn = 1;
    /// <summary>1-based current caret column number.</summary>
    public int CaretColumn
    {
        get => _caretColumn;
        set => Set(ref _caretColumn, value);
    }

    // -- Zoom ---------------------------------------------------------------------

    private double _zoomLevel = 1.0;
    /// <summary>Current zoom level (1.0 = 100%).</summary>
    public double ZoomLevel
    {
        get => _zoomLevel;
        set
        {
            Set(ref _zoomLevel, value);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ZoomPercent)));
        }
    }

    /// <summary>Zoom formatted as "100%" for the status bar.</summary>
    public string ZoomPercent => $"{(int)(_zoomLevel * 100)}%";

    // -- Language -----------------------------------------------------------------

    private string _languageName = "Plain Text";
    /// <summary>Display name of the currently active language.</summary>
    public string LanguageName
    {
        get => _languageName;
        set => Set(ref _languageName, value);
    }

    // -- Theme --------------------------------------------------------------------

    private string _currentThemeName = "DarkTheme";
    /// <summary>Canonical theme key matching a key in <see cref="App.Themes"/>.</summary>
    public string CurrentThemeName
    {
        get => _currentThemeName;
        set
        {
            Set(ref _currentThemeName, value);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDarkTheme)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLightTheme)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ThemeLabel)));
        }
    }

    /// <summary>True when the current theme is <c>DarkTheme</c>.</summary>
    public bool IsDarkTheme => _currentThemeName == "DarkTheme";

    /// <summary>True when the current theme is <c>OfficeTheme</c> (Light).</summary>
    public bool IsLightTheme => _currentThemeName == "OfficeTheme";

    /// <summary>Short label shown in the status bar.</summary>
    public string ThemeLabel => _currentThemeName switch
    {
        "DarkTheme"       => "Dark",
        "OfficeTheme"     => "Light",
        "VS2022DarkTheme" => "VS2022 Dark",
        "MinimalTheme"    => "Minimal",
        "CyberpunkTheme"  => "Cyberpunk",
        _                 => _currentThemeName
    };

    // -- Helpers ------------------------------------------------------------------

    private void RefreshTitle()
    {
        var fileName = CurrentFilePath is not null
            ? System.IO.Path.GetFileName(CurrentFilePath)
            : "Untitled";

        Title = IsDirty
            ? $"{fileName}* — CodeEditor Sample"
            : $"{fileName} — CodeEditor Sample";
    }

    /// <summary>Updates title, dirty flag, and file path after a file is opened.</summary>
    public void NotifyFileOpened(string filePath)
    {
        CurrentFilePath = filePath;
        IsDirty         = false;
        RefreshTitle();
    }

    /// <summary>Called when a new empty buffer is created.</summary>
    public void NotifyNewFile()
    {
        CurrentFilePath = null;
        IsDirty         = false;
        RefreshTitle();
    }
}
