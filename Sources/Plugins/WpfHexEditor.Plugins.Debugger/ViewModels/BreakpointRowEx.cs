// ==========================================================
// Project: WpfHexEditor.Plugins.Debugger
// File: ViewModels/BreakpointRowEx.cs
// Description:
//     Extended breakpoint row model for the Breakpoint Explorer panel.
//     INPC for IsEnabled/HitCount live updates.
// ==========================================================

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WpfHexEditor.Plugins.Debugger.ViewModels;

public sealed class BreakpointRowEx : INotifyPropertyChanged
{
    private bool _isEnabled;
    private int  _hitCount;

    public string  FilePath     { get; init; } = string.Empty;
    public string  FileName     { get; init; } = string.Empty;
    public string  ProjectName  { get; init; } = string.Empty;
    public int     Line       { get; init; }
    public string? Condition  { get; init; }
    public bool    IsVerified { get; init; }

    public bool IsEnabled
    {
        get => _isEnabled;
        set { if (_isEnabled == value) return; _isEnabled = value; OnPropertyChanged(); OnPropertyChanged(nameof(TypeLabel)); OnPropertyChanged(nameof(TypeGlyph)); }
    }

    public int HitCount
    {
        get => _hitCount;
        set { if (_hitCount == value) return; _hitCount = value; OnPropertyChanged(); }
    }

    /// <summary>"Conditional", "Standard", or "Disabled".</summary>
    public string TypeLabel => !IsEnabled ? "Disabled"
                             : !string.IsNullOrEmpty(Condition) ? "Conditional"
                             : "Standard";

    /// <summary>Segoe MDL2 Assets glyph for the breakpoint type.</summary>
    public string TypeGlyph => !IsEnabled ? "\uECC9"          // Circle outline
                             : !string.IsNullOrEmpty(Condition) ? "\uEA3F"  // Warning
                             : "\uEA3B";                                      // Filled circle

    public string DisplayLocation => $"{FileName}:{Line}";

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? p = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}
