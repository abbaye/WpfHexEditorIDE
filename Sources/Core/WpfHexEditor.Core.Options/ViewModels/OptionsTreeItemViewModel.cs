//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WpfHexEditor.Core.Options.ViewModels;

/// <summary>
/// ViewModel for a single item in the Options TreeView.
/// Can represent either a category (with children) or a leaf page.
/// </summary>
public sealed class OptionsTreeItemViewModel : INotifyPropertyChanged
{
    private bool _isExpanded = true;
    private bool _isSelected;

    /// <summary>Display name for this tree item.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional icon/emoji for visual identification.</summary>
    public string Icon { get; set; } = string.Empty;

    /// <summary>
    /// True if this is a category header (has children).
    /// False if this is a leaf page.
    /// </summary>
    public bool IsCategory { get; set; }

    /// <summary>
    /// The descriptor for this page (null for category headers).
    /// </summary>
    public OptionsPageDescriptor? Descriptor { get; set; }

    /// <summary>Child items (only populated for categories).</summary>
    public ObservableCollection<OptionsTreeItemViewModel> Children { get; } = new();

    /// <summary>Gets or sets whether this tree item is expanded.</summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetField(ref _isExpanded, value);
    }

    /// <summary>Gets or sets whether this tree item is selected.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    /// <summary>Computed property: display text with optional icon.</summary>
    public string DisplayText => string.IsNullOrEmpty(Icon) ? Name : $"{Icon} {Name}";

    /// <summary>Computed property: count of child pages (for categories).</summary>
    public int ChildCount => Children.Count;

    /// <summary>Computed property: display count for categories (e.g., "Hex Editor (4)").</summary>
    public string DisplayTextWithCount =>
        IsCategory && Children.Count > 0
            ? $"{DisplayText} ({Children.Count})"
            : DisplayText;

    // -- INotifyPropertyChanged implementation -----------------------------

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return;
        field = value;
        OnPropertyChanged(propertyName);
    }
}
