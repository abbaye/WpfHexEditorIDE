// Apache 2.0 - 2026
// Contributors: Claude Sonnet 4.6

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// Optional interface for document editors that contribute contextual action buttons
/// to the App's host toolbar pod.
///
/// When the active editor implements this interface the host (MainWindow) shows a
/// dynamic pod (styled with DockToolBarPodStyle) whose buttons are driven by
/// <see cref="ToolbarItems"/>.  The pod collapses automatically when the active editor
/// does not implement this interface.
///
/// This is the toolbar analogue of <see cref="IStatusBarContributor"/> — same pattern,
/// same switch mechanism via <c>ActiveToolbarContributor</c> in OnActiveDocumentChanged.
/// </summary>
public interface IEditorToolbarContributor
{
    ObservableCollection<EditorToolbarItem> ToolbarItems { get; }
}

/// <summary>
/// A single item in an editor's contextual toolbar contribution.
/// Can represent an icon button, a labelled button, a dropdown button, or a separator.
/// </summary>
public sealed class EditorToolbarItem : INotifyPropertyChanged
{
    private bool _isEnabled = true;

    /// <summary>
    /// Segoe MDL2 Assets character code, e.g. <c>"\uE74E"</c> (Save).
    /// </summary>
    public string? Icon { get; init; }

    /// <summary>Optional text label displayed beside the icon.</summary>
    public string? Label { get; init; }

    /// <summary>Tooltip shown on hover.</summary>
    public string? Tooltip { get; init; }

    /// <summary>Command executed when the button is clicked (non-dropdown buttons).</summary>
    public ICommand? Command { get; init; }

    /// <summary>
    /// When <see langword="true"/>, the item renders as a 1-px vertical separator line
    /// (all other properties are ignored).
    /// </summary>
    public bool IsSeparator { get; init; }

    /// <summary>
    /// When non-null, the button renders as a dropdown (▾).
    /// Clicking it opens a ContextMenu populated from this collection.
    /// </summary>
    public ObservableCollection<EditorToolbarItem>? DropdownItems { get; init; }

    /// <summary>
    /// Bindable enabled state — change this to enable/disable the button at runtime.
    /// </summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set { _isEnabled = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}
