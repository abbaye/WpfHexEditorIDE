// ==========================================================
// Project: WpfHexEditor.Plugins.Debugger
// File: ViewModels/BreakpointGroupNode.cs
// Description:
//     Tree group header for Breakpoint Explorer (groups by file/type/enabled).
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WpfHexEditor.Plugins.Debugger.ViewModels;

public sealed class BreakpointGroupNode : INotifyPropertyChanged
{
    private bool _isExpanded = true;

    public string GroupKey  { get; init; } = string.Empty;
    public string GroupIcon { get; init; } = string.Empty;
    public ObservableCollection<BreakpointRowEx> Children { get; } = [];
    public int Count => Children.Count;

    public bool IsExpanded
    {
        get => _isExpanded;
        set { if (_isExpanded == value) return; _isExpanded = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? p = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}
