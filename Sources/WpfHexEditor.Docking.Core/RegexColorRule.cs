//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WpfHexEditor.Docking.Core;

/// <summary>
/// A single regex rule that maps document tab titles matching <see cref="Pattern"/>
/// to a specific accent color given as a hex string (e.g. <c>#FF6495ED</c>).
/// </summary>
public sealed class RegexColorRule : INotifyPropertyChanged
{
    private string _pattern = "";
    private string _colorHex = "#FF6495ED"; // default: cornflower blue

    /// <summary>
    /// Regular-expression pattern tested against the document tab title.
    /// First matching rule wins.
    /// </summary>
    public string Pattern
    {
        get => _pattern;
        set => Set(ref _pattern, value);
    }

    /// <summary>
    /// Accent color as <c>#AARRGGBB</c> or <c>#RRGGBB</c> hex string.
    /// </summary>
    public string ColorHex
    {
        get => _colorHex;
        set => Set(ref _colorHex, value);
    }

    // ─── INotifyPropertyChanged ──────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
