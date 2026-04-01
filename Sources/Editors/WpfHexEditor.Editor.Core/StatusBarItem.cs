//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// A single choice shown in the popup of an interactive status bar item.
/// </summary>
public sealed class StatusBarChoice : INotifyPropertyChanged
{
    private bool _isActive;

    public string  DisplayName { get; set; } = "";

    /// <summary>
    /// True when this choice matches the editor's current value (shows a checkmark).
    /// </summary>
    public bool IsActive
    {
        get => _isActive;
        set { _isActive = value; OnPropertyChanged(); }
    }

    public ICommand Command { get; set; } = null!;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}

/// <summary>
/// An observable, clickable item in the editor's status bar contribution.
/// Label and Value are displayed as "Label: Value".  Clicking opens Choices.
/// </summary>
public sealed class StatusBarItem : INotifyPropertyChanged
{
    private string _value    = "";
    private bool   _isVisible = true;

    public string Label   { get; set; } = "";
    public string Tooltip { get; set; } = "";

    public string Value
    {
        get => _value;
        set { _value = value; OnPropertyChanged(); }
    }

    public bool IsVisible
    {
        get => _isVisible;
        set { _isVisible = value; OnPropertyChanged(); }
    }

    public ObservableCollection<StatusBarChoice> Choices { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}
