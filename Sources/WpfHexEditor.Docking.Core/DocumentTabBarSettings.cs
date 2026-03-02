//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WpfHexEditor.Docking.Core;

/// <summary>
/// Where the document tab strip is rendered relative to the document area.
/// </summary>
public enum DocumentTabPlacement
{
    Top,
    Left,
    Right
}

/// <summary>
/// How document tabs are colorized.
/// </summary>
public enum DocumentTabColorMode
{
    None,
    Project,
    FileExtension,
    Regex
}

/// <summary>
/// Runtime settings for the document tab bar. Persisted in the layout JSON.
/// The same instance is shared by <c>DockLayoutRoot</c>, <c>DockControl</c>,
/// <c>DocumentTabHost</c>, and <c>TabConfigButton</c> so that in-place mutation
/// keeps everything in sync without extra events.
/// </summary>
public class DocumentTabBarSettings : INotifyPropertyChanged
{
    private DocumentTabPlacement _tabPlacement = DocumentTabPlacement.Top;
    private DocumentTabColorMode _colorMode = DocumentTabColorMode.None;
    private bool _multiRowTabs;
    private bool _multiRowWithMouseWheel = true;

    /// <summary>
    /// Position of the tab strip (only <see cref="DocumentTabPlacement.Top"/> is visually
    /// implemented; Left/Right are stored for future use).
    /// </summary>
    public DocumentTabPlacement TabPlacement
    {
        get => _tabPlacement;
        set => Set(ref _tabPlacement, value);
    }

    /// <summary>
    /// Controls whether (and how) document tabs are tinted by their associated project,
    /// file extension, or a regex rule.
    /// </summary>
    public DocumentTabColorMode ColorMode
    {
        get => _colorMode;
        set => Set(ref _colorMode, value);
    }

    /// <summary>
    /// When <see langword="true"/>, tabs wrap to additional rows instead of overflowing
    /// into the <c>⋯</c> dropdown.
    /// </summary>
    public bool MultiRowTabs
    {
        get => _multiRowTabs;
        set => Set(ref _multiRowTabs, value);
    }

    /// <summary>
    /// When <see langword="true"/> and <see cref="MultiRowTabs"/> is also true, the mouse
    /// wheel scrolls through tab rows inside the tab bar area.
    /// </summary>
    public bool MultiRowWithMouseWheel
    {
        get => _multiRowWithMouseWheel;
        set => Set(ref _multiRowWithMouseWheel, value);
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
