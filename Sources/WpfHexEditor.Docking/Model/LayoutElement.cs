// Apache 2.0 - 2026
// WpfHexEditor.Docking - VS-Like Docking Framework

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WpfHexEditor.Docking.Model;

/// <summary>
/// Abstract base for every node in the layout tree.
/// Provides parent tracking, property change notification, and unique identity.
/// </summary>
public abstract class LayoutElement : INotifyPropertyChanged
{
    private LayoutElement? _parent;

    /// <summary>Stable unique identifier for this element (used in serialization).</summary>
    public string Id { get; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Parent node in the layout tree. Set automatically when added to/removed from a parent's children.
    /// </summary>
    public LayoutElement? Parent
    {
        get => _parent;
        internal set
        {
            if (_parent == value) return;
            _parent = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Root));
        }
    }

    /// <summary>
    /// Walks up the parent chain to find the LayoutRoot.
    /// Returns null if this element is detached from any root.
    /// </summary>
    public LayoutRoot? Root
    {
        get
        {
            var current = this;
            while (current != null)
            {
                if (current is LayoutRoot root)
                    return root;
                current = current.Parent;
            }
            return null;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
